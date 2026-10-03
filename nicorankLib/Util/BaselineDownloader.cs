using nicorankLib.Analyze.model;
using nicorankLib.api;
using nicorankLib.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace nicorankLib.Util
{
    /// <summary>
    /// ベースラインDB（LogOfficial／NicoranHistory）の不在時自動取得を担う（Issue #36）。
    /// NAS の Web 公開配下（既定 https://2daime.myds.me/nicorank/baseline/）の baseline.json を最新ポインタとし、
    /// 本地ファイル不在時に限り zip を取得→サイズ・sha256 照合→展開→配置する。
    /// なぜ不在時に限るか：既存環境の DB を勝手に置き換えると蓄積データの上書き事故になるためである。
    /// 取得に失敗しても例外を投げず false を返し、呼び出し側の既存メッセージで中断する（fail-fast 維持）。
    /// 表示は StatusLog、詳細は ErrLog に寄せる（直接 Console に書かない。Issue #43 の作法）。
    /// </summary>
    public class BaselineDownloader
    {
        /// <summary>最新ポインタのファイル名。配布スクリプト（tools/make-baseline.ps1）が自動作成する。</summary>
        public const string ManifestFileName = "baseline.json";

        /// <summary>
        /// 既定の最新ポインタ URL。Config 不在でも動くよう Config.DefaultBaselineManifestUrl と同一値を重複定義する。
        /// 値を変えたら Config 側と配布スクリプトの OutDir 既定も同時更新すること。
        /// </summary>
        public const string DefaultManifestUrl = "https://2daime.myds.me/nicorank/baseline/baseline.json";

        /// <summary>baseline.json 内の1DB分の記述。配布スクリプトの出力キーと一致させる。</summary>
        public class BaselineFileEntry
        {
            [JsonProperty("file")]
            public string File { get; set; }
            [JsonProperty("date")]
            public string Date { get; set; }
            [JsonProperty("size")]
            public long Size { get; set; }
            [JsonProperty("sha256")]
            public string Sha256 { get; set; }
        }

        /// <summary>baseline.json 全体。対象はベースライン2種のみ（ApiXML／Dailylog は含めない）。</summary>
        public class BaselineManifest
        {
            [JsonProperty("logOfficial")]
            public BaselineFileEntry LogOfficial { get; set; }
            [JsonProperty("nicoranHistory")]
            public BaselineFileEntry NicoranHistory { get; set; }
        }

        /// <summary>
        /// ベースラインDBによる復旧（Issue #45）の実行結果1件分。
        /// 既存DBの上書きは保護対象のため、退避先と陳腐化ブロックの理由を分けて持つ。
        /// </summary>
        public class BaselineRestoreResult
        {
            /// <summary>復旧全体の成否（配置できれば true）。</summary>
            public bool Success;
            /// <summary>配布が本地より古い等の理由で中断した場合に true。</summary>
            public bool StaleBlocked;
            /// <summary>中断理由の区分（配布が古い／本地を確認不能／配布内容を確認不能／配布内容が古い）。表示の分岐に使う。</summary>
            public string StaleReason;
            /// <summary>人間向けの理由（成功時は配置報告、失敗・中断時は原因）。</summary>
            public string Message;
            /// <summary>上書き前に自動退避した既存DBの配置先一覧。</summary>
            public List<string> BackedUpPaths = new List<string>();
        }

        /// <summary>テキスト取得処理の差し替え口。既定は InternetUtil 経由、単体テストではフェイクを注入する。</summary>
        public delegate bool TextFetch(string url, out string text);

        /// <summary>ファイル取得処理の差し替え口。既定は InternetUtil 経由、単体テストではフェイクを注入する。</summary>
        public delegate bool FileFetch(string url, string localPath);

        private readonly string localBaseDir;
        private readonly TextFetch textFetch;
        private readonly FileFetch fileFetch;
        private readonly string manifestUrlOverride;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="localBaseDir">本地の基準フォルダ。省略時はカレントディレクトリ（DB/ からの相対解決のため）。テストでは一時フォルダを指定する</param>
        /// <param name="textFetch">テキスト取得処理（省略時は InternetUtil.TxtDownLoad）</param>
        /// <param name="fileFetch">ファイル取得処理（省略時は InternetUtil.FileDownLoad）</param>
        /// <param name="manifestUrlOverride">最新ポインタ URL の上書き（テスト用。省略時は Config→既定の順に解決する）</param>
        public BaselineDownloader(string localBaseDir = null, TextFetch textFetch = null, FileFetch fileFetch = null, string manifestUrlOverride = null)
        {
            this.localBaseDir = localBaseDir ?? Directory.GetCurrentDirectory();
            this.textFetch = textFetch ?? DefaultTextFetch;
            this.fileFetch = fileFetch ?? InternetUtil.FileDownLoad;
            this.manifestUrlOverride = manifestUrlOverride;
        }

        private static bool DefaultTextFetch(string url, out string text)
        {
            return InternetUtil.TxtDownLoad(url, out text);
        }

        /// <summary>
        /// 最新ポインタ URL。nicorank.xml の SYSTEM/URL_BASELINE があればそれを使い、なければ既定を使う。
        /// なぜ try/catch か：設定取得に失敗しても既定で動かし、不在時取得の機会を残すためである（NicoApi.ResolveThreadMax と同一の考え方）。
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
                    return Config.GetInstance().BaselineManifestUrl;
                }
                catch
                {
                    return DefaultManifestUrl;
                }
            }
        }

        /// <summary>
        /// ベースライン2種の不足を検出したら自動取得する。両方そろっていれば何もせず true を返す。
        /// </summary>
        /// <returns>両 DB が利用可能な状態になれば true、取得・照合・配置のいずれかに失敗したら false</returns>
        public bool EnsureBaseline()
        {
            string logPath = Path.Combine(localBaseDir, DB.LOG_OFFICEIAL);
            string histPath = Path.Combine(localBaseDir, DB.NiCORAN_HISTORY);
            bool logMissing = !File.Exists(logPath);
            bool histMissing = !File.Exists(histPath);
            if (!logMissing && !histMissing)
            {
                return true;
            }
            try
            {
                StatusLog.WriteLine("ベースラインDBの不足を検出しました。配布場所から自動取得します...");
                string manifestUrl = ManifestUrl;
                if (!textFetch(manifestUrl, out string manifestText))
                {
                    StatusLog.WriteLine("ベースラインの一覧を取得できませんでした。ネットワークと配布場所を確認してください");
                    return false;
                }
                BaselineManifest manifest;
                if (!TryParseManifest(manifestText, out manifest))
                {
                    StatusLog.WriteLine("ベースラインの一覧を読み取れませんでした。配布場所の baseline.json を確認してください");
                    return false;
                }
                string baseUrl = manifestUrl.Substring(0, manifestUrl.LastIndexOf('/') + 1);
                bool ok = true;
                if (logMissing)
                {
                    ok &= DownloadAndPlace(baseUrl, manifest.LogOfficial, logPath);
                }
                if (histMissing)
                {
                    ok &= DownloadAndPlace(baseUrl, manifest.NicoranHistory, histPath);
                }
                return ok && File.Exists(logPath) && File.Exists(histPath);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("ベースラインDBの自動取得に失敗しました。エラーログを確認してください");
                return false;
            }
        }

        /// <summary>
        /// 最新ポインタの取得と読み取りだけを行う。復旧前の確認表示用。
        /// なぜ分けるか：上書き前に人間が配布日を確認する必要があり、取得と配置を一体化すると
        /// 確認の余地なく上書きが進むためである。
        /// </summary>
        public bool TryFetchManifest(out BaselineManifest manifest, out string baseUrl, out string error)
        {
            manifest = null;
            baseUrl = null;
            error = null;
            try
            {
                string manifestUrl = ManifestUrl;
                if (!textFetch(manifestUrl, out string manifestText))
                {
                    error = "ベースラインの一覧を取得できませんでした。ネットワークと配布場所を確認してください";
                    return false;
                }
                if (!TryParseManifest(manifestText, out manifest))
                {
                    error = "ベースラインの一覧を読み取れませんでした。配布場所の baseline.json を確認してください";
                    return false;
                }
                baseUrl = manifestUrl.Substring(0, manifestUrl.LastIndexOf('/') + 1);
                return true;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                error = "ベースラインの一覧取得に失敗しました。エラーログを確認してください";
                return false;
            }
        }

        /// <summary>
        /// yyyyMMdd形式の日付文字列の桁数。固定長でなければ比較不能とみなす。
        /// </summary>
        private const int DateLength = 8;

        /// <summary>
        /// yyyyMMdd形式の日付文字列を数値化する。比較不能な値は null とする。
        /// なぜ数値化か：固定8桁のため文字列比較でも順序は一致するが、数値化すると
        /// 前後関係の判定意図が明確になり、桁崩れの混入も検出できるためである。
        /// </summary>
        public static long? TryParseDateToLong(string yyyyMMdd)
        {
            if (string.IsNullOrWhiteSpace(yyyyMMdd) || yyyyMMdd.Trim().Length != DateLength)
            {
                return null;
            }
            if (long.TryParse(yyyyMMdd.Trim(), out long value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// ベースラインが本地より古いかどうかを判定する。純粋処理のため単体テストで直接検証する。
        /// 復旧対象は NicoranHistory のみであり、LogOfficial は見ない。
        /// なぜ見ないか：LogOfficial は毎回の日次更新で自己回復するため配布復旧が不要であり、
        /// 配布日が作成日のため内容日との比較では常に古い判定になるからである。
        /// 本地最新が不明（null）の場合は比較不能として古い扱いにしない（何もない所への配置を妨げないため）。
        /// 配布日が読めない場合は安全側に倒してブロックする（不明な物を被せないため）。
        /// </summary>
        public static bool IsBaselineStale(BaselineManifest manifest, long? localHistMax)
        {
            if (manifest == null || manifest.NicoranHistory == null)
            {
                return true;
            }
            long? manifestHist = TryParseDateToLong(manifest.NicoranHistory.Date);
            if (manifestHist == null)
            {
                return true;
            }
            if (localHistMax != null && manifestHist < localHistMax)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 本地の NicoranHistory.db の Weekly 最新集計日を求める。不明時は null。
        /// 集計日は yyyyMMdd の8桁（文字列束縛と数値束縛が混在するが固定長のため MAX の順序は一致する）。
        /// </summary>
        public static long? GetMaxWeeklyDate(ISQLiteCtrl historyCtrl)
        {
            TryGetMaxWeeklyDate(historyCtrl, out long? max);
            return max;
        }

        /// <summary>
        /// 本地の NicoranHistory.db の Weekly 最新集計日を求める。
        /// 戻り値が false の場合は読取失敗（表なし・破損等）であり、null の最新日と区別する。
        /// なぜ区別するか：読取失敗を「データ無し」と同じ扱いにすると、壊れたDBを古い配布で
        /// 上書きし得るため、安全側（中断）に倒す必要があるからである。
        /// </summary>
        public static bool TryGetMaxWeeklyDate(ISQLiteCtrl historyCtrl, out long? max)
        {
            max = null;
            try
            {
                if (historyCtrl == null || !historyCtrl.IsOpen)
                {
                    return false;
                }
                using (var cmd = historyCtrl.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT MAX(集計日) FROM LastResult WHERE 種別 = @種別;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@種別", EAnalyzeMode.Weekly.ToString());
                    object value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                    {
                        // 行なしは正常系であり、失敗ではない。
                        return true;
                    }
                    string text = value.ToString().Trim();
                    if (long.TryParse(text, out long parsed))
                    {
                        max = parsed;
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
        }

        /// <summary>
        /// 上書き前に既存DBを退避する。存在しなければ何もせず null を返す。
        /// 退避先は DB/backup/&lt;yyyyMMdd_HHmmss&gt;/元名 とし、-wal/-shm があれば一緒に運ぶ。
        /// なぜ一緒に運ぶか：WAL モードでは本体だけ戻しても付随ファイルとの不整合で開けなくなる場合があるためである。
        /// コピー失敗時は例外を投げ、呼び出し側で配置を中止する（中途半端な上書きを作らないため）。
        /// </summary>
        public static string BackupExistingFile(string destPath)
        {
            if (string.IsNullOrEmpty(destPath) || !File.Exists(destPath))
            {
                return null;
            }
            string dir = Path.GetDirectoryName(destPath);
            if (string.IsNullOrEmpty(dir))
            {
                dir = ".";
            }
            string backupDir = Path.Combine(dir, "backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir, Path.GetFileName(destPath));
            File.Copy(destPath, backupPath, true);
            string wal = destPath + "-wal";
            if (File.Exists(wal))
            {
                // 付随ファイルのコピー失敗は握りつぶさず記録して続行を明示する。
                // なぜ続行か：本体の退避が成功していれば手動復旧の材料は残り、
                // ここで止めると復旧自体が進まなくなるためである。
                try { File.Copy(wal, backupPath + "-wal", true); }
                catch (Exception ex) { ErrLog.GetInstance().Write(ex); }
            }
            string shm = destPath + "-shm";
            if (File.Exists(shm))
            {
                try { File.Copy(shm, backupPath + "-shm", true); }
                catch (Exception ex) { ErrLog.GetInstance().Write(ex); }
            }
            return backupPath;
        }

        /// <summary>
        /// ベースラインDBによる復旧（Issue #45 の本命手段）。
        /// 復旧対象は NicoranHistory.db のみである。
        /// なぜ LogOfficial を対象外にするか：毎回の日次更新で自己回復するため配布復旧が不要であり、
        /// 配布日が作成日のため内容日との比較では常に古い判定になるからである。
        /// 配布が本地より古い場合は警告して中断する（必須仕様）。
        /// 既存DBは上書き前に自動退避し、コピー失敗時は配置を中止する。
        /// 表示は StatusLog、詳細は ErrLog に寄せる（Issue #43 の作法）。
        /// </summary>
        public BaselineRestoreResult RestoreBaseline()
        {
            var result = new BaselineRestoreResult();
            string histPath = Path.Combine(localBaseDir, DB.NiCORAN_HISTORY);
            try
            {
                if (!TryFetchManifest(out BaselineManifest manifest, out string baseUrl, out string fetchError))
                {
                    result.Success = false;
                    result.Message = fetchError;
                    StatusLog.WriteLine(fetchError);
                    return result;
                }
                // 本地最新の読み取りは開閉を短時間で済ませ、上書き時のロック競合を起こさない。
                // 読取失敗（表なし・破損等）はデータ無しと区別し、安全側（中断）に倒す。
                long? localHistMax = null;
                if (File.Exists(histPath))
                {
                    using (var histCtrl = new SQLiteCtrl())
                    {
                        if (histCtrl.Open(histPath))
                        {
                            if (!TryGetMaxWeeklyDate(histCtrl, out localHistMax))
                            {
                                result.Success = false;
                                result.StaleBlocked = true;
                                result.StaleReason = "本地を確認不能";
                                result.Message = "本地の最新日を確認できなかったため中断しました。DBの破損の可能性があるため、エラーログを確認してください";
                                StatusLog.WriteLine(result.Message);
                                return result;
                            }
                        }
                        else
                        {
                            // 開けない本地DBを比較不能として通すと、壊れたDBを配布で上書きし得るため中断する。
                            // 例外オブジェクトはないため、理由を文字列で ErrLog に残す。
                            ErrLog.GetInstance().Write("ベースライン復旧：本地のDBを開けなかったため中断しました: " + histPath);
                            result.Success = false;
                            result.StaleBlocked = true;
                            result.StaleReason = "本地を確認不能";
                            result.Message = "本地のDBを開けなかったため中断しました。DBの破損の可能性があるため、エラーログを確認してください";
                            StatusLog.WriteLine(result.Message);
                            return result;
                        }
                    }
                }
                if (IsBaselineStale(manifest, localHistMax))
                {
                    result.Success = false;
                    result.StaleBlocked = true;
                    result.StaleReason = "配布が古い";
                    result.Message = string.Format(
                        "配布中のベースライン（NicoranHistory:{0}）が本地より古いため中断しました。回収集計している人に相談してください",
                        manifest.NicoranHistory.Date);
                    StatusLog.WriteLine(result.Message);
                    return result;
                }
                StatusLog.WriteLine(string.Format(
                    "ベースラインDBで復旧します（配布日 NicoranHistory:{0}）",
                    manifest.NicoranHistory.Date));
                // 配布内容の検証は一時展開DBの内容日で行う。
                // なぜ内容日か：manifest の date は梱包日であり、配布元が休止後に梱包すると
                // 内容は古いのに梱包日だけ新しくなり、本地の新しい週を消してしまうためである。
                string tempDir = Path.Combine(Path.GetTempPath(), "nicorank_baseline_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                try
                {
                    if (!FetchVerifiedZip(baseUrl, manifest.NicoranHistory, tempDir, out string zipPath))
                    {
                        result.Success = false;
                        result.Message = "ベースラインDBの取得に失敗しました。ネットワークと配布場所を確認してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    if (!TryExtractDbTemp(zipPath, tempDir, out string tempDb))
                    {
                        result.Success = false;
                        result.Message = "ベースラインDBの展開に失敗しました。配布物を確認してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    long? contentMax = null;
                    bool contentReadOk = false;
                    using (var contentCtrl = new SQLiteCtrl())
                    {
                        if (contentCtrl.Open(tempDb))
                        {
                            contentReadOk = TryGetMaxWeeklyDate(contentCtrl, out contentMax);
                        }
                    }
                    if (!contentReadOk)
                    {
                        result.Success = false;
                        result.StaleBlocked = true;
                        result.StaleReason = "配布内容を確認不能";
                        result.Message = "配布内容の最新日を確認できなかったため中断しました。配布物の破損の可能性があるため、回収集計している人に相談してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    if (contentMax != null && localHistMax != null && contentMax < localHistMax)
                    {
                        result.Success = false;
                        result.StaleBlocked = true;
                        result.StaleReason = "配布内容が古い";
                        result.Message = "配布内容が本地より古いため中断しました。回収集計している人に相談してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    if (contentMax == null && localHistMax != null)
                    {
                        // 内容に Weekly の行が1つもない場合は、梱包日が新しくても上書きしない。
                        // なぜ中断か：空DBで本地の実績を置き換えると「復旧完了」と誤表示され、誤りに気づけないためである。
                        result.Success = false;
                        result.StaleBlocked = true;
                        result.StaleReason = "配布内容にデータなし";
                        result.Message = "配布内容に集計データがないため中断しました。回収集計している人に相談してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    // 退避は上書きの直前に行い、失敗時は配置を中止する。
                    try
                    {
                        string backedHist = BackupExistingFile(histPath);
                        if (backedHist != null)
                        {
                            result.BackedUpPaths.Add(backedHist);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrLog.GetInstance().Write(ex);
                        result.Success = false;
                        result.Message = "既存DBの退避に失敗したため中断しました。DBフォルダの権限を確認してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    // 既存本体の削除失敗は握りつぶさず記録する（-wal／-shm 側と同一の基準）。
                    try { if (File.Exists(histPath)) { File.Delete(histPath); } }
                    catch (Exception ex)
                    {
                        ErrLog.GetInstance().Write(ex);
                        result.Success = false;
                        result.Message = "既存DBの置き換えに失敗したため中断しました。DBフォルダの権限を確認してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    try { if (File.Exists(histPath + "-wal")) { File.Delete(histPath + "-wal"); } } catch (Exception ex) { ErrLog.GetInstance().Write(ex); }
                    try { if (File.Exists(histPath + "-shm")) { File.Delete(histPath + "-shm"); } } catch (Exception ex) { ErrLog.GetInstance().Write(ex); }
                    try
                    {
                        string histDir = Path.GetDirectoryName(histPath);
                        if (!string.IsNullOrEmpty(histDir))
                        {
                            Directory.CreateDirectory(histDir);
                        }
                        File.Move(tempDb, histPath);
                    }
                    catch (Exception ex)
                    {
                        ErrLog.GetInstance().Write(ex);
                        result.Success = false;
                        result.Message = "ベースラインDBの配置に失敗しました。DB/backup 以下に退避済みのため手動で戻せます。エラーログを確認してください";
                        StatusLog.WriteLine(result.Message);
                        return result;
                    }
                    result.Success = File.Exists(histPath);
                    if (result.Success)
                    {
                        result.Message = "ベースラインDBの復旧が完了しました";
                    }
                    else
                    {
                        // 退避は済んでいるため、DB/backup 以下から手動で戻せる旨を添える。
                        result.Message = "ベースラインDBの復旧に失敗しました。DB/backup 以下に退避済みのため手動で戻せます。エラーログを確認してください";
                    }
                    StatusLog.WriteLine(result.Message);
                    return result;
                }
                finally
                {
                    try { if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); } } catch { }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                result.Success = false;
                result.Message = "ベースラインDBの復旧に失敗しました。DB/backup 以下の退避から手動で戻せます。エラーログを確認してください";
                StatusLog.WriteLine(result.Message);
                return result;
            }
        }

        /// <summary>
        /// キャッシュ扱いの DB（ApiXML／Dailylog）がなければ空から作る（Issue #36 方針）。
        /// なぜ失敗でも true か：キャッシュの確保失敗を理由に集計全体を止めると、1件の取得失敗が全件失敗に見えるためである。
        /// 除外の判断は入力側に残し、ここでは best-effort で確保だけ行う（Issue #40 と同一の考え方）。
        /// ファイルごとに try/catch を分ける。片方の失敗がもう片方の確保を巻き込まないためである（re-review指摘対応）。
        /// </summary>
        /// <returns>常に true（確保失敗時はログに残して続ける）</returns>
        public bool EnsureCacheFiles()
        {
            try
            {
                EnsureApiXmlFile();
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("動画情報キャッシュの確保に失敗しましたが、集計を続けます");
            }
            try
            {
                EnsureDailylogFile();
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("中間集計キャッシュの確保に失敗しましたが、集計を続けます");
            }
            return true;
        }

        /// <summary>
        /// baseline.json の読み取り。純粋処理のため単体テストで直接検証する。
        /// </summary>
        public static bool TryParseManifest(string text, out BaselineManifest manifest)
        {
            manifest = null;
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                var parsed = JsonConvert.DeserializeObject<BaselineManifest>(text);
                if (parsed == null || !IsValidEntry(parsed.LogOfficial) || !IsValidEntry(parsed.NicoranHistory))
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

        private static bool IsValidEntry(BaselineFileEntry entry)
        {
            if (entry == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.File) || string.IsNullOrWhiteSpace(entry.Date))
            {
                return false;
            }
            // file は zip 内ではなく配置先フォルダ直下に置く単純名でなければならない。
            // パス区切りを許すと一時フォルダ外への書き込みや URL の意図しない解決につながるため拒否する。
            // 配布元は自前 NAS で実害は小さいが、防御が安い箇所のためここで縛る（reviewer指摘対応）。
            if (entry.File.IndexOf('/') >= 0 || entry.File.IndexOf('\\') >= 0)
            {
                return false;
            }
            if (entry.Size <= 0)
            {
                return false;
            }
            // sha256 は必須とする（スクリプト自動作成のため人間負荷にならない）。
            // 64文字の16進数でなければ配布物の取り違えとみなす。
            if (string.IsNullOrWhiteSpace(entry.Sha256) || entry.Sha256.Length != 64)
            {
                return false;
            }
            foreach (char c in entry.Sha256)
            {
                bool isHex = ('0' <= c && c <= '9') || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// zip1件の取得→照合→展開→配置。公開情報の表示は StatusLog、例外の詳細は ErrLog に残す。
        /// 既存ファイルがある場合は上書きしない（不在時取得の不置換方針。復旧の上書き配置は
        /// RestoreBaseline 側の検証付き経路を使うため、ここに上書き分岐は持たせない）。
        /// </summary>
        private bool DownloadAndPlace(string baseUrl, BaselineFileEntry entry, string destPath)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "nicorank_baseline_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                if (!FetchVerifiedZip(baseUrl, entry, tempDir, out string zipPath))
                {
                    return false;
                }
                if (!ExtractSingleDb(zipPath, destPath))
                {
                    StatusLog.WriteLine(string.Format("{0} の展開に失敗しました。配布物を確認してください", entry.File));
                    return false;
                }
                StatusLog.WriteLine(string.Format("{0} を配置しました", entry.File));
                return true;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine(string.Format("{0} の配置に失敗しました。エラーログを確認してください", entry.File));
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) { Directory.Delete(tempDir, true); } } catch { }
            }
        }

        /// <summary>
        /// zip1件の取得→サイズ・sha256照合。一時フォルダへの配置までを行い、展開は呼び出し側が行う。
        /// なぜ分けるか：復旧時は配置前に一時展開DBの内容日を検証する必要があり、
        /// 取得と配置を一体化すると検証の余地なく上書きが進むためである。
        /// </summary>
        private bool FetchVerifiedZip(string baseUrl, BaselineFileEntry entry, string tempDir, out string zipPath)
        {
            zipPath = Path.Combine(tempDir, entry.File);
            StatusLog.WriteLine(string.Format("{0} を取得しています...", entry.File));
            if (!fileFetch(baseUrl + entry.File, zipPath))
            {
                StatusLog.WriteLine(string.Format("{0} を取得できませんでした。ネットワークと配布場所を確認してください", entry.File));
                return false;
            }
            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length != entry.Size)
            {
                StatusLog.WriteLine(string.Format("{0} のサイズが一致しません。再取得してください", entry.File));
                return false;
            }
            if (!VerifySha256(zipPath, entry.Sha256))
            {
                StatusLog.WriteLine(string.Format("{0} の検証に失敗しました。再取得してください", entry.File));
                return false;
            }
            return true;
        }

        /// <summary>
        /// zip 内の .db 1件を配置先へ出す。zip 直下・サブフォルダのいずれでも先頭の .db を使う。
        /// なぜ1件に絞るか：配布スクリプトが DB ごとに分離して zip 化するため、1つの zip に複数の DB が混ざらない前提だからである。
        /// 既存ファイルがある場合は上書きしない（蓄積データの保護を優先する）。
        /// 復旧の上書き配置は RestoreBaseline 側の検証付き経路を使うため、ここに上書き分岐は持たせない。
        /// </summary>
        private static bool ExtractSingleDb(string zipPath, string destPath)
        {
            string destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            if (File.Exists(destPath))
            {
                // 競合で既に置かれた場合は上書きしない（蓄積データの保護を優先する）。
                return true;
            }
            if (!TryExtractDbTemp(zipPath, Path.GetTempPath(), out string tempDb))
            {
                return false;
            }
            try
            {
                File.Move(tempDb, destPath);
                return true;
            }
            finally
            {
                try { if (File.Exists(tempDb)) { File.Delete(tempDb); } } catch { }
            }
        }

        /// <summary>
        /// zip 内の先頭 .db を一時ファイルへ出す。配置先への移動は呼び出し側が行う。
        /// なぜ分けるか：復旧時は移動前に内容日を検証する必要があり、一体化すると検証できないためである。
        /// </summary>
        private static bool TryExtractDbTemp(string zipPath, string workDir, out string tempDbPath)
        {
            tempDbPath = null;
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry dbEntry = null;
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    {
                        dbEntry = e;
                        break;
                    }
                }
                if (dbEntry == null)
                {
                    return false;
                }
                tempDbPath = Path.Combine(workDir, "nicorank_baseline_" + Guid.NewGuid().ToString("N") + ".db");
                // 展開失敗時は部分ファイルを残さない（旧処理の finally 削除と同等の後始末）。
                // 例外の詳細は ErrLog に残す（#43 の経路分離。表示は呼び出し側の StatusLog が担う）。
                try
                {
                    dbEntry.ExtractToFile(tempDbPath);
                }
                catch (Exception ex)
                {
                    ErrLog.GetInstance().Write(ex);
                    try { if (File.Exists(tempDbPath)) { File.Delete(tempDbPath); } } catch { }
                    tempDbPath = null;
                    return false;
                }
            }
            if (!File.Exists(tempDbPath) || new FileInfo(tempDbPath).Length == 0)
            {
                try { if (File.Exists(tempDbPath)) { File.Delete(tempDbPath); } } catch { }
                tempDbPath = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// ファイルの SHA256 照合。純粋処理のため単体テストで直接検証する。
        /// </summary>
        public static bool VerifySha256(string path, string expected)
        {
            try
            {
                if (string.IsNullOrEmpty(expected) || !File.Exists(path))
                {
                    return false;
                }
                using (var sha = SHA256.Create())
                {
                    using (var stream = File.OpenRead(path))
                    {
                        byte[] hash = sha.ComputeHash(stream);
                        string actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        return string.Equals(actual, expected.ToLowerInvariant(), StringComparison.Ordinal);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void EnsureApiXmlFile()
        {
            // パス定数は DbOptimizer に集約されているものを使い、値の食い違いを起こさない。
            // 変更時は DbOptimizer／NicoApi／ApiXmlCacheImporter／TyukanAnalyze と同時更新すること。
            string path = Path.Combine(localBaseDir, DbOptimizer.ApiXmlDbPath);
            // 新規作成したファイルは失敗時に削除し、次回リトライ可能にする。
            // なぜ削除するか：空の .db だけ残ると次回以降 File.Exists で早期 return し、
            // 表が作られないまま固定化して中間集計・取得が静かに使えなくなるため（reviewer指摘対応）。
            bool created = false;
            try
            {
                if (!File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    // SQLiteCtrl.Open は存在しないファイルを開かないため、空ファイルを作ってから開く（SnapShotDB.InitilizeDB と同一の作り方）。
                    File.Create(path).Dispose();
                    created = true;
                }
                // 表確保は毎回実行する（冪等・軽量）。前回中断で空ファイルだけ残った場合の自己回復のためである。
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (dbCtrl.Open(path))
                    {
                        ApiXmlCacheImporter.EnsureNicovideoThumbTable(dbCtrl);
                        dbCtrl.Close();
                    }
                }
            }
            catch
            {
                // 新規作成時は本体と -wal/-shm を消して次回リトライ可能にする。
                // Open は WAL 化するため異常終了経路で付随ファイルが残り得る。既存ファイルは消さない。
                if (created)
                {
                    try { if (File.Exists(path)) { File.Delete(path); } } catch { }
                    try { if (File.Exists(path + "-wal")) { File.Delete(path + "-wal"); } } catch { }
                    try { if (File.Exists(path + "-shm")) { File.Delete(path + "-shm"); } } catch { }
                }
                throw;
            }
            if (created)
            {
                StatusLog.WriteLine("動画情報キャッシュを新規作成しました");
            }
        }

        private void EnsureDailylogFile()
        {
            string path = Path.Combine(localBaseDir, DbOptimizer.DailylogDbPath);
            bool created = false;
            try
            {
                if (!File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Create(path).Dispose();
                    created = true;
                }
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (dbCtrl.Open(path))
                    {
                        EnsureDailylogTable(dbCtrl);
                        dbCtrl.Close();
                    }
                }
            }
            catch
            {
                if (created)
                {
                    try { if (File.Exists(path)) { File.Delete(path); } } catch { }
                    try { if (File.Exists(path + "-wal")) { File.Delete(path + "-wal"); } } catch { }
                    try { if (File.Exists(path + "-shm")) { File.Delete(path + "-shm"); } } catch { }
                }
                throw;
            }
            if (created)
            {
                StatusLog.WriteLine("中間集計のキャッシュを新規作成しました");
            }
        }

        /// <summary>
        /// Dailylog 表の確保。本番コードに CREATE 経路がないためここで持つ（DbOptimizer が DROP 禁止にしている理由）。
        /// 列構成は中間集計の INSERT 列と一致させること。不一致があると集計時に列不足で失敗する。
        /// </summary>
        private static void EnsureDailylogTable(ISQLiteCtrl dbCtrl)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Dailylog (
                        集計日 INTEGER,
                        ID TEXT,
                        タイトル TEXT,
                        投稿日 INTEGER,
                        再生時間 TEXT,
                        総合順位 INTEGER,
                        ポイント数 INTEGER,
                        カテゴリランク INTEGER,
                        カテゴリ TEXT,
                        人気のタグ TEXT,
                        再生ランク INTEGER,
                        再生数 INTEGER,
                        再生補正 REAL,
                        再生ポイント INTEGER,
                        コメントランク INTEGER,
                        コメント数 INTEGER,
                        コメント補正 REAL,
                        コメントポイント INTEGER,
                        マイリストランク INTEGER,
                        マイリスト数 INTEGER,
                        マイリスト補正 REAL,
                        マイリストポイント INTEGER,
                        いいねランク INTEGER DEFAULT 0,
                        いいね数 INTEGER DEFAULT 0,
                        いいね補正 INTEGER DEFAULT 1,
                        いいねポイント INTEGER DEFAULT 0,
                        イメージパス TEXT
                    )";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
