using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using nicorankLib.Util;
using System;
using System.Globalization;
using System.IO;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// Snapshot API v2 の更新判定結果（Issue #38）。
    /// Updated＝本日分に更新済み、NotUpdated＝未更新、Unknown＝version取得・パース失敗で確認不能。
    /// 取得失敗を bool ではなく3値で返すのは、呼び出し側（WinFormのダイアログ／CLIのリトライ）で
    /// 「未更新」と「確認不能」を別扱いにするためである。
    /// </summary>
    public enum SnapShotVersionStatus
    {
        Updated,
        NotUpdated,
        Unknown
    }

    /// <summary>
    /// 更新チェックの結果。判定（Status）に加え、ログ・ダイアログ表示用に
    /// last_modified の生文字列とパース済み値をそのまま保持する。
    /// </summary>
    public class SnapShotVersionResult
    {
        public SnapShotVersionStatus Status { get; set; } = SnapShotVersionStatus.Unknown;
        public string LastModifiedRaw { get; set; }
        public DateTimeOffset? LastModified { get; set; }
    }

    /// <summary>
    /// Snapshot API v2 の更新チェック（Issue #38）。
    /// 公式ガイド「データの更新について」の切り替え日時エンドポイントの last_modified と
    /// 実行日を比べ、前日データでの取得を防ぐ。取得実行そのものは行わず判定だけに専念し、
    /// 事後動作（ダイアログ／リトライ）は呼び出し側に委ねる。理由は、UIとCLIで事後動作が
    /// 異なるため判定部品だけを共通化する方が分岐が素直になるからである。
    /// </summary>
    public class SnapShotVersionChecker
    {
        /// <summary>切り替え日時取得のエンドポイント（公式ガイド記載）</summary>
        public const string VersionEndpoint = "https://snapshot.search.nicovideo.jp/api/v2/snapshot/version";

        /// <summary>判定基準のタイムゾーン（日本標準時）。実行環境のTZに依存しないため固定値で持つ</summary>
        public static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

        /// <summary>テキスト取得処理の差し替え口。既定は InternetUtil 経由、単体テストではフェイクを注入する</summary>
        /// <param name="url">取得URL</param>
        /// <param name="text">取得テキスト（失敗時は不定）</param>
        /// <returns>取得成否</returns>
        public delegate bool DownloadTextDelegate(string url, out string text);

        private readonly DownloadTextDelegate _downloader;
        private readonly Func<DateTimeOffset> _nowProvider;

        /// <summary>
        /// チェッカーを生成する。引数なしなら本番動作（実通信＋実行時計）、
        /// 引数ありならテスト用フェイクに差し替える（_dbCtrlOverride パターンと同様の方式）。
        /// </summary>
        public SnapShotVersionChecker(DownloadTextDelegate downloader = null, Func<DateTimeOffset> nowProvider = null)
        {
            _downloader = downloader ?? DefaultDownloader;
            _nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
        }

        private static bool DefaultDownloader(string url, out string text)
        {
            // 既存の取得経路（UA WeeklyNicoranProgram 付き）を流用する。新規のHTTP実装は増やさない
            return InternetUtil.TxtDownLoad(url, out text);
        }

        /// <summary>
        /// 更新有無を判定する。通信失敗・JSON不正・last_modified欠落・日付パース失敗は
        /// すべて Unknown（確認不能）で返す。例外を投げないのは、呼び出し側が Unknown として
        /// リトライ／中断の通常フローに乗せられるようにするためである。
        /// </summary>
        public SnapShotVersionResult Check()
        {
            var result = new SnapShotVersionResult();

            string text;
            bool ok;
            try
            {
                ok = _downloader(VersionEndpoint, out text);
            }
            catch
            {
                // ダウンローダの例外も確認不能扱いにする（握りつぶしではなく Unknown への正規化）
                return result;
            }
            if (!ok)
            {
                return result;
            }

            DateTimeOffset parsed;
            try
            {
                // 日付の自動パースを止めて文字列のまま読む。JObject.Parse 既定では
                // ISO日時が Date トークンに化けて +09:00 のオフセット情報が落ちるため、
                // TryParse（オフセット保持）に回す前提で None を指定する
                JObject obj;
                using (var reader = new JsonTextReader(new StringReader(text)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    obj = JObject.Load(reader);
                }
                var token = obj["last_modified"];
                if (token == null || token.Type != JTokenType.String)
                {
                    return result;
                }
                // 生値は last_modified の値そのものを残す（レスポンス本文全体ではない）。
                // ログ・ダイアログに載せるのは日時だけでよく、本文全体は可読性を下げるため
                result.LastModifiedRaw = token.Value<string>();
                if (!DateTimeOffset.TryParse(result.LastModifiedRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    return result;
                }
            }
            catch
            {
                // JSON破損・型不一致も確認不能扱いにする
                return result;
            }
            result.LastModified = parsed;

            // 両方をJSTに寄せてから日付比較する。NAS（Linux）のTZがJSTでない場合でも
            // 日付境界がずれないようにするため、実行環境の Today には依存しない
            DateTime todayJst = ToJst(_nowProvider()).Date;
            result.Status = ToJst(parsed).Date == todayJst
                ? SnapShotVersionStatus.Updated
                : SnapShotVersionStatus.NotUpdated;
            return result;
        }

        /// <summary>指定値を日本標準時に変換する</summary>
        public static DateTimeOffset ToJst(DateTimeOffset value)
        {
            return value.ToOffset(JstOffset);
        }

        /// <summary>
        /// ログ1行分の文面を生成する（例：Snapshot version: last_modified=2026-09-20T07:08:34+09:00（更新済み））。
        /// 取得開始時に StatusLogへ出し、NASメールの実行ログに残す。余裕時間の追跡が目的のため生値を残す。
        /// </summary>
        public static string ToStatusLogLine(SnapShotVersionResult result)
        {
            // 未取得時は取得失敗・解析失敗の区別が付かないため両方を含む文言にする
            string raw = result.LastModifiedRaw ?? "(取得失敗または解析失敗)";
            string judgment = result.Status == SnapShotVersionStatus.Updated ? "更新済み"
                : result.Status == SnapShotVersionStatus.NotUpdated ? "未更新" : "確認不能";
            return $"Snapshot version: last_modified={raw}（{judgment}）";
        }
    }
}
