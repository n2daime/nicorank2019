using Newtonsoft.Json;
using nicorankLib.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

namespace nicorankLib.Util
{
    /// <summary>
    /// nicorank2019 の自動更新の確認を担う（Issue #47）。
    /// NAS 上の version.json を最新ポインタとし、exe の Assembly 版数と比較して更新要否を判定する。
    /// なぜ NAS の version.json か：GitHub API を叩かないためレート制限の心配がなく、
    /// 公開の一時停止や段階展開も version.json の更新有無で制御できるためである（Issue 合意）。
    /// 取得に失敗しても例外を投げず Unknown を返し、呼び出し側は旧版のまま動かす（fail-safe）。
    /// 表示の判断は呼び出し側（UI のダイアログ）に委ね、本クラスは判定値だけを返す。
    /// SQLite を使わないため、lib/ 欠け事故時でも起動直後に動く。
    /// </summary>
    public class AppUpdateChecker
    {
        /// <summary>更新確認の状態ファイル名。%TEMP% 直下に置く（カレントを汚さない。汎用名との衝突回避に接頭辞付き）。</summary>
        public const string StateFileName = "nicorank2019_update_check.json";

        /// <summary>updater への指示ファイル名。%TEMP% 直下に置く（本体が書いて updater が読む）。</summary>
        public const string TaskFileName = "nicorank2019_update_task.json";

        /// <summary>
        /// 既定の最新ポインタ URL。Config 不在でも動くよう Config.DefaultAppUpdateManifestUrl と同一値を重複定義する。
        /// 値を変えたら Config 側も同時更新すること（BaselineDownloader.DefaultManifestUrl と同一の運用）。
        /// </summary>
        public const string DefaultManifestUrl = "https://2daime.myds.me/nicorank/update/version.json";

        /// <summary>対応できる version.json の形式版数の上限。未知の形式なら更新せず旧版のまま動かす。</summary>
        public const int SupportedSchemaMax = 1;

        /// <summary>version.json の apps 内で自アプリを示すキー。将来 SnapShot・oldlog を追加しても形式変更が要らない。</summary>
        public const string TargetAppKey = "nicorank2019";

        /// <summary>更新確認の間隔（時間）。最終確認からこの時間が経つまで再取得しない。起動のたびに待たせないため。</summary>
        public const int ThrottleHours = 24;

        /// <summary>更新確認の取得リトライ回数。InternetUtil.TxtDownLoad（最大20回）より少なくする。起動時・ボタン操作で数分待たせないため。
        /// 体感の最大待ちは FetchTimeoutMs×FetchRetryMax＋待機1秒×(FetchRetryMax-1) の約17秒である。</summary>
        public const int FetchRetryMax = 3;

        /// <summary>更新確認の1回あたりの取得タイムアウト（ミリ秒）。</summary>
        public const int FetchTimeoutMs = 5 * 1000;

        /// <summary>判定基準のタイムゾーン（日本標準時）。SnapShotVersionChecker.JstOffset と同一値。Util 層から SnapShot 層を参照しないよう重複定義する。</summary>
        public static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

        /// <summary>version.json 内の1アプリ分の記述。配布スクリプトの出力キーと一致させる。</summary>
        public class UpdateFileEntry
        {
            [JsonProperty("tag")]
            public string Tag { get; set; }
            [JsonProperty("version")]
            public string Version { get; set; }
            [JsonProperty("url")]
            public string Url { get; set; }
            [JsonProperty("sha256")]
            public string Sha256 { get; set; }
            [JsonProperty("size")]
            public long Size { get; set; }
            [JsonProperty("notes")]
            public string Notes { get; set; }
        }

        /// <summary>version.json 全体。schema は形式版数であり、更新要否の判定には使わない。</summary>
        public class UpdateManifest
        {
            [JsonProperty("schema")]
            public int Schema { get; set; }
            [JsonProperty("apps")]
            public Dictionary<string, UpdateFileEntry> Apps { get; set; }
        }

        /// <summary>更新確認の状態（%TEMP% の状態ファイルの中身）。削除・破損時は空扱いとし、次回確認し直す。</summary>
        public class UpdateCheckState
        {
            /// <summary>最終確認のJST日（yyyy-MM-dd）。24時間間引きの判定に使う。</summary>
            [JsonProperty("lastCheck")]
            public string LastCheck { get; set; }
            /// <summary>最後に通知した version.json の version。同一版の再通知抑制に使う。</summary>
            [JsonProperty("lastNotified")]
            public string LastNotified { get; set; }
        }

        /// <summary>更新確認の結果。判定（Status）に加え、ダイアログ表示用に配布情報をそのまま保持する。</summary>
        public class UpdateCheckResult
        {
            public UpdateCheckStatus Status { get; set; } = UpdateCheckStatus.Unknown;
            public UpdateFileEntry Entry { get; set; }
        }

        /// <summary>
        /// 更新確認の判定値。
        /// UpToDate＝最新、Available＝新版あり（ダイアログ対象）、Throttled＝間引き・抑制で確認せず、
        /// Unknown＝取得・解析失敗で確認不能（いずれも旧版のまま動かす）。
        /// 未更新と確認不能を分けるのは、確認不能を理由に集計や起動を止めないためである（#45 の確認不能時開始と同一の考え方）。
        /// </summary>
        public enum UpdateCheckStatus
        {
            UpToDate,
            Available,
            Throttled,
            Unknown
        }

        /// <summary>テキスト取得処理の差し替え口。既定は InternetUtil 経由、単体テストではフェイクを注入する。</summary>
        public delegate bool TextFetch(string url, out string text);

        private readonly string stateDir;
        private readonly TextFetch textFetch;
        private readonly string manifestUrlOverride;
        private readonly Func<DateTimeOffset> nowProvider;
        private readonly Version currentVersionOverride;

        /// <summary>
        /// コンストラクタ。
        /// 呼び出し側は都度 new してよい。状態の真実は %TEMP% の状態ファイルに一本化し、
        /// ReadState で都度読み直すため、インスタンスを使い回さなくてもチグハグは起きない。
        /// 将来 LastNotified を判定に使う（必須化）場合は保持方式を再検討すること。
        /// </summary>
        /// <param name="stateDir">状態・タスクファイルの置き場所。省略時は %TEMP%（カレントを汚さない。削除されても確認し直すだけ）。テストでは一時フォルダを指定する</param>
        /// <param name="textFetch">テキスト取得処理（省略時は InternetUtil.TxtDownLoad）</param>
        /// <param name="manifestUrlOverride">最新ポインタ URL の上書き（テスト用。省略時は Config→既定の順に解決する）</param>
        /// <param name="nowProvider">現在時刻（省略時は実行時計。テストでは固定時刻を注入する）</param>
        /// <param name="currentVersion">自版数（省略時はエントリアセンブリ。テストでは固定版数を注入する）</param>
        public AppUpdateChecker(string stateDir = null, TextFetch textFetch = null, string manifestUrlOverride = null, Func<DateTimeOffset> nowProvider = null, Version currentVersion = null)
        {
            this.stateDir = stateDir ?? Path.GetTempPath();
            this.textFetch = textFetch ?? DefaultTextFetch;
            this.manifestUrlOverride = manifestUrlOverride;
            this.nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
            this.currentVersionOverride = currentVersion;
        }

        private static bool DefaultTextFetch(string url, out string text)
        {
            return FetchText(url, out text);
        }

        /// <summary>タイムアウト付きの WebClient。InternetUtil.NicoranWebClient と同型（5秒）。</summary>
        private class TimeoutWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                // 未知スキームでは null が返り得る。現状の到達スキーム（http／https／file）では起きないが、
                // 将来スキームを足した時のヌル参照を防ぐ。
                if (w != null)
                {
                    w.Timeout = FetchTimeoutMs;
                }
                return w;
            }
        }

        /// <summary>
        /// 更新確認専用のテキスト取得。InternetUtil.TxtDownLoad（最大20回・指数バックオフ）ではなく、
        /// 短い回数・短いタイムアウトで切り上げる。
        /// なぜ分けるか：起動時・ボタン操作の確認で数分待たせると、UIロックが長引いて「確認中のまま」に見えるためである。
        /// 一過性の失敗は翌日・次回起動の確認で拾う（fail-safe側のため取りこぼしは問題にならない）。
        /// file スキーム（NAS・GitHub配置前のローカル検証用）にも対応する。
        /// </summary>
        public static bool FetchText(string url, out string text)
        {
            text = null;
            // file 取得の不存在は再試行しても結果が変わらない恒久エラーのため待機しない。http系の一過性のみ待つ。
            bool isFile = !string.IsNullOrEmpty(url) && url.TrimStart().StartsWith("file:", StringComparison.OrdinalIgnoreCase);
            for (int attempt = 0; attempt < FetchRetryMax; attempt++)
            {
                try
                {
                    using (var web = new TimeoutWebClient())
                    {
                        // User-Agent 付与は file スキームでも例外にならず無視される（テストで担保）。
                        web.Headers.Add("User-Agent", "WeeklyNicoranProgram");
                        using (var stream = web.OpenRead(url))
                        {
                            using (var reader = new StreamReader(stream, Encoding.UTF8))
                            {
                                text = reader.ReadToEnd();
                            }
                        }
                    }
                    return true;
                }
                catch
                {
                    if (!isFile)
                    {
                        // nicorankLib.Common.Thread と区別するため完全修飾する。
                        System.Threading.Thread.Sleep(1000);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 最新ポインタ URL。nicorank.xml の SYSTEM/URL_APPUPDATE があればそれを使い、なければ既定を使う。
        /// なぜ try/catch か：設定取得に失敗しても既定で動かし、更新確認の機会を残すためである（BaselineDownloader.ManifestUrl と同一の考え方）。
        /// </summary>
        public string ManifestUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(manifestUrlOverride))
                {
                    return manifestUrlOverride;
                }
                try
                {
                    return Config.GetInstance().AppUpdateManifestUrl;
                }
                catch
                {
                    return DefaultManifestUrl;
                }
            }
        }

        /// <summary>状態ファイルの配置先。</summary>
        public string StatePath
        {
            get { return Path.Combine(stateDir, StateFileName); }
        }

        /// <summary>updater への指示ファイルの配置先。</summary>
        public string TaskPath
        {
            get { return Path.Combine(stateDir, TaskFileName); }
        }

        /// <summary>指定値を日本標準時に変換する（SnapShotVersionChecker.ToJst と同一。層依存を作らないため重複定義）。</summary>
        public static DateTimeOffset ToJst(DateTimeOffset value)
        {
            return value.ToOffset(JstOffset);
        }

        /// <summary>現在JST日（yyyy-MM-dd）。間引きと状態記録の基準にする。</summary>
        public string TodayString
        {
            get { return ToJst(nowProvider()).ToString("yyyy-MM-dd"); }
        }

        /// <summary>
        /// 自版数。エントリアセンブリ（nicorank2019.exe）の Assembly 版数を使う。
        /// なぜエントリか：本クラスは nicorankLib 内にあり、自 Assembly（1.0.0.0 固定の可能性）では本体の版数を表せないためである。
        /// 取得不能時は null とし、比較不能（Unknown）に倒す。不明な版で更新を促さないためである。
        /// </summary>
        public Version CurrentVersion
        {
            get
            {
                if (currentVersionOverride != null)
                {
                    return currentVersionOverride;
                }
                try
                {
                    Assembly entry = Assembly.GetEntryAssembly();
                    if (entry != null)
                    {
                        return entry.GetName().Version;
                    }
                }
                catch
                {
                }
                return null;
            }
        }

        /// <summary>
        /// version.json の読み取り。純粋処理のため単体テストで直接検証する。
        /// schema の上限・URL 組み立てはここでは見ず、呼び出し側（Check）で判定する。
        /// なぜ分けるか：updater 側に組み立てロジックを持たせない方針（Issue 合意）のため、完全 URL の正当性は必須とする。
        /// </summary>
        public static bool TryParseManifest(string text, out UpdateManifest manifest)
        {
            manifest = null;
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                var parsed = JsonConvert.DeserializeObject<UpdateManifest>(text);
                if (parsed == null || parsed.Apps == null)
                {
                    return false;
                }
                UpdateFileEntry entry;
                if (!parsed.Apps.TryGetValue(TargetAppKey, out entry) || !IsValidEntry(entry))
                {
                    return false;
                }
                manifest = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidEntry(UpdateFileEntry entry)
        {
            if (entry == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.Version) || string.IsNullOrWhiteSpace(entry.Url))
            {
                return false;
            }
            // url は DL 先の完全 URL でなければならない。タグ＋asset 名からの組み立てはしない（Issue 合意）。
            // 相対 URL を許すと参照元の解決が必要になり、命名変更で更新機構自体が壊れるため拒否する。
            // file スキームも許す。NAS・GitHub 配置前のローカル検証（version.json と zip を手元に置く通しテスト）のため。
            // 配布元が書く値であり信頼モデルは変わらず、size・sha256 照合も適用される。
            Uri uri;
            if (!Uri.TryCreate(entry.Url.Trim(), UriKind.Absolute, out uri))
            {
                return false;
            }
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile)
            {
                return false;
            }
            if (entry.Size <= 0)
            {
                return false;
            }
            // sha256 は必須とする（スクリプト自動作成のため人間負荷にならない）。
            // 64文字の16進数でなければ配布物の取り違えとみなす（BaselineDownloader.IsValidEntry と同一の基準）。
            if (string.IsNullOrWhiteSpace(entry.Sha256) || entry.Sha256.Trim().Length != 64)
            {
                return false;
            }
            foreach (char c in entry.Sha256.Trim())
            {
                bool isHex = ('0' <= c && c <= '9') || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }
            // tag・notes は表示・記録用であり空でもよい。欠落時は空文字に寄せる。
            if (entry.Tag == null)
            {
                entry.Tag = string.Empty;
            }
            if (entry.Notes == null)
            {
                entry.Notes = string.Empty;
            }
            return true;
        }

        /// <summary>
        /// 版数文字列を比較可能な Version に正規化する。欠落成分は 0 とみなす。
        /// なぜ自前か：System.Version は未指定成分を -1（未定義）として扱い、
        /// 「2026.10.03」（Revision 未定義）が Assembly「2026.10.3.0」（Revision 0）より小さい誤判定になるためである。
        /// 先頭ゼロ（03）は数値として読む。空・非数値・5成分以上・マイナスは比較不能として false を返す。
        /// </summary>
        public static bool TryNormalizeVersion(string text, out Version version)
        {
            version = null;
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                string[] parts = text.Trim().Split('.');
                if (parts.Length < 1 || parts.Length > 4)
                {
                    return false;
                }
                int[] numbers = new int[] { 0, 0, 0, 0 };
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    if (part.Length == 0)
                    {
                        return false;
                    }
                    int value;
                    if (!int.TryParse(part, out value) || value < 0)
                    {
                        return false;
                    }
                    // int.TryParse は先頭ゼロ・前後空白を許すが、符号付き・小数・16進はここでは来ない（Trim 済み・Split 済みのため）。
                    numbers[i] = value;
                }
                version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 配布版が自版より新しいかを判定する。純粋処理のため単体テストで直接検証する。
        /// 配布版が読めない場合は false（更新を促さない fail-safe側）に倒す。
        /// </summary>
        public static bool IsNewerVersion(string manifestVersion, Version current)
        {
            if (current == null)
            {
                return false;
            }
            Version manifest;
            if (!TryNormalizeVersion(manifestVersion, out manifest))
            {
                return false;
            }
            // current 側も欠落成分の扱いを揃える（Assembly は4成分だが、注入テスト値が3成分の場合に備える）。
            Version normalizedCurrent;
            if (!TryNormalizeVersion(current.ToString(), out normalizedCurrent))
            {
                return false;
            }
            return manifest.CompareTo(normalizedCurrent) > 0;
        }

        /// <summary>
        /// 状態ファイルを読む。存在しない・破損している場合は空状態を返す。
        /// なぜ空扱いか：削除・クリアされても次回確認し直すだけで安全であり、確認不能で止める理由にならないためである。
        /// </summary>
        public UpdateCheckState ReadState()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return new UpdateCheckState();
                }
                var state = JsonConvert.DeserializeObject<UpdateCheckState>(File.ReadAllText(StatePath));
                return state ?? new UpdateCheckState();
            }
            catch
            {
                return new UpdateCheckState();
            }
        }

        /// <summary>
        /// 状態ファイルを書く。書けなくても例外を投げない（次回確認し直すだけのため）。
        /// </summary>
        public void WriteState(UpdateCheckState state)
        {
            try
            {
                if (state == null)
                {
                    return;
                }
                Directory.CreateDirectory(stateDir);
                File.WriteAllText(StatePath, JsonConvert.SerializeObject(state));
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
        }

        /// <summary>
        /// ダイアログ表示後に呼ぶ。通知済み版を記録し、同一版の再通知を抑える。
        /// なぜ表示後に分けるか：Check 時点で記録すると、ダイアログ前に異常終了した場合に通知を見逃すためである。
        /// </summary>
        public void MarkNotified(string version)
        {
            try
            {
                UpdateCheckState state = ReadState();
                state.LastNotified = version;
                if (string.IsNullOrEmpty(state.LastCheck))
                {
                    state.LastCheck = TodayString;
                }
                WriteState(state);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
        }

        /// <summary>
        /// 更新確認の本体。例外を投げず、確認不能時は Unknown を返す（呼び出し側は旧版のまま動かす）。
        /// </summary>
        /// <param name="force">真なら24時間間引きと同一版抑制を無視する（手動「更新を確認」ボタン用）。押したのに何も起きない操作にしないため。</param>
        public UpdateCheckResult Check(bool force = false)
        {
            var result = new UpdateCheckResult();
            try
            {
                string today = TodayString;
                UpdateCheckState state = ReadState();
                // 間引き：最終確認から24時間未満なら取得しない。起動のたびに待たせないため。
                // 日付文字列の比較で24時間を近似する（厳密な時刻差ではなくJST日単位。仕様を単純にするため）。
                if (!force && string.Equals(state.LastCheck, today, StringComparison.Ordinal))
                {
                    result.Status = UpdateCheckStatus.Throttled;
                    return result;
                }
                string manifestUrl = ManifestUrl;
                string manifestText;
                bool fetched;
                try
                {
                    fetched = textFetch(manifestUrl, out manifestText);
                }
                catch
                {
                    return result;
                }
                if (!fetched)
                {
                    return result;
                }
                UpdateManifest manifest;
                if (!TryParseManifest(manifestText, out manifest))
                {
                    return result;
                }
                // 未知の形式なら更新せず旧版のまま動かす。更新要否の判定には使わない（Issue 合意）。
                if (manifest.Schema > SupportedSchemaMax)
                {
                    return result;
                }
                UpdateFileEntry entry = manifest.Apps[TargetAppKey];
                Version current = CurrentVersion;
                if (!IsNewerVersion(entry.Version, current))
                {
                    state.LastCheck = today;
                    WriteState(state);
                    result.Status = UpdateCheckStatus.UpToDate;
                    return result;
                }
                // 同一版の抑制は先頭の間引きで実現する：通知後に LastCheck が当日になるため、
                // 同日の再起動では取得自体を行わず再通知しない。翌日は再通知する（1日1回まで）。
                // 通知済み版の記録（LastNotified）は MarkNotified が行い、監査と将来の必須化に使う。
                state.LastCheck = today;
                WriteState(state);
                result.Status = UpdateCheckStatus.Available;
                result.Entry = entry;
                return result;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                result.Status = UpdateCheckStatus.Unknown;
                result.Entry = null;
                return result;
            }
        }

        /// <summary>
        /// updater への指示ファイルを書く。URL・ハッシュ・サイズは version.json の値をそのまま書く。
        /// なぜ全部書くか：updater 側に組み立てロジックを持たせると将来の命名変更で更新機構自体が壊れるためである（Issue 合意）。
        /// </summary>
        /// <param name="entry">配布情報（Check の Available で得たもの）</param>
        /// <param name="targetDir">置換対象の配置先（本体の実行フォルダ）</param>
        /// <param name="exePath">再起動する exe の完全パス</param>
        /// <param name="processId">終了待ちする本体プロセスID</param>
        public bool WriteTaskFile(UpdateFileEntry entry, string targetDir, string exePath, int processId)
        {
            try
            {
                if (entry == null || !IsValidEntry(entry))
                {
                    return false;
                }
                if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
                {
                    return false;
                }
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                {
                    return false;
                }
                Directory.CreateDirectory(stateDir);
                var task = new Dictionary<string, object>();
                task["schema"] = 1;
                task["tag"] = entry.Tag ?? string.Empty;
                task["version"] = entry.Version;
                task["url"] = entry.Url;
                task["sha256"] = entry.Sha256;
                task["size"] = entry.Size;
                task["targetDir"] = targetDir;
                task["exePath"] = exePath;
                task["processId"] = processId;
                File.WriteAllText(TaskPath, JsonConvert.SerializeObject(task));
                return File.Exists(TaskPath);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
        }
    }
}
