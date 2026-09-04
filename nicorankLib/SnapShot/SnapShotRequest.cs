using System;
using System.Globalization;
using System.Text;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// スナップショット検索API v2 の型付きリクエスト（Issue #19）
    /// 公式ガイド https://site.nicovideo.jp/search-api-docs/snapshot
    /// クエリパラメータを構造化データで保持し、正しいエンコードでURLを生成する。
    /// 将来的なCLI操作等による外部検索条件指定の下地。複雑フィルタは JsonFilterJson 経由で拡張する。
    /// </summary>
    public class SnapShotRequest
    {
        /// <summary>エンドポイント</summary>
        public const string Endpoint = "https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search";
        /// <summary>必須パラメータ _context。User-Agent と同じサービス名を指定する（最大40文字）</summary>
        public const string DefaultContext = "WeeklyNicoranProgram";
        /// <summary>既定の取得フィールド</summary>
        public const string DefaultFields = "contentId,commentCounter,viewCounter,mylistCounter,likeCounter";
        /// <summary>既定のソート順</summary>
        public const string DefaultSort = "-viewCounter";
        /// <summary>1000再生フィルタの閾値（直近1年より前の期間用）</summary>
        public const long ViewCounterThreshold = 1000;
        /// <summary>_limit の最大値（公式仕様）</summary>
        public const int MaxLimit = 100;
        /// <summary>_offset の最大値（公式仕様）</summary>
        public const long MaxOffset = 100000;

        /// <summary>検索キーワード。空文字でキーワード無し検索（q= 自体の省略は不可）</summary>
        public string Q { get; set; } = "";
        /// <summary>キーワード検索対象フィールド（カンマ区切り）。キーワード無し検索では省略可</summary>
        public string Targets { get; set; } = null;
        /// <summary>レスポンスに含めるフィールド（カンマ区切り）</summary>
        public string Fields { get; set; } = DefaultFields;
        /// <summary>投稿期間の開始（filters[startTime][gte]）</summary>
        public DateTime StartGte { get; set; }
        /// <summary>投稿期間の終了（filters[startTime][lt]）</summary>
        public DateTime StartLt { get; set; }
        /// <summary>再生数フィルタ下限（filters[viewCounter][gte]）。nullでフィルタ無し</summary>
        public long? MinViewCounter { get; set; } = ViewCounterThreshold;
        /// <summary>複雑フィルタ用JSON文字列（公式の jsonFilter。URLエンコード前の生JSON）。将来拡張口で現行フローは未使用</summary>
        public string JsonFilterJson { get; set; } = null;
        /// <summary>ソート順</summary>
        public string Sort { get; set; } = DefaultSort;
        /// <summary>取得件数（最大100。0は件数取得用）</summary>
        public int Limit { get; set; } = MaxLimit;
        /// <summary>取得オフセット（最大100000）</summary>
        public long Offset { get; set; } = 0;
        /// <summary>サービス/アプリケーション名（必須・最大40文字）</summary>
        public string Context { get; set; } = DefaultContext;

        /// <summary>
        /// 期間指定のリクエストを生成する（SnapShotAnalyze 用）
        /// </summary>
        /// <param name="startDay">投稿期間の開始日</param>
        /// <param name="endDay">投稿期間の終了日</param>
        /// <param name="limit">取得件数</param>
        /// <param name="offset">取得オフセット</param>
        /// <param name="limit1000">trueで1000再生フィルタあり、falseでフィルタ無し</param>
        public static SnapShotRequest CreateRange(DateTime startDay, DateTime endDay, int limit, long offset, bool limit1000)
        {
            return new SnapShotRequest
            {
                StartGte = startDay.Date,
                StartLt = endDay.Date,
                Limit = limit,
                Offset = offset,
                MinViewCounter = limit1000 ? (long?)ViewCounterThreshold : null
            };
        }

        /// <summary>日付を filters[startTime] 用の書式（yyyy-MM-ddT00:00:00+09:00）に変換する</summary>
        public static string ToStartTimeString(DateTime day)
        {
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00+09:00";
        }

        /// <summary>
        /// リクエストURLを生成する。キーは公式のブラケット記法のまま、値は Uri.EscapeDataString でエンコードする。
        /// </summary>
        public string ToUrl()
        {
            int limit = Limit;
            if (limit < 0) { limit = 0; }
            if (limit > MaxLimit) { limit = MaxLimit; }
            long offset = Offset;
            if (offset < 0) { offset = 0; }
            if (offset > MaxOffset) { offset = MaxOffset; }

            var query = new StringBuilder(512);
            AppendParam(query, "q", Q ?? "");
            if (!string.IsNullOrEmpty(Targets))
            {
                AppendParam(query, "targets", Targets);
            }
            AppendParam(query, "fields", Fields ?? "");
            AppendParam(query, "filters[startTime][gte]", ToStartTimeString(StartGte));
            AppendParam(query, "filters[startTime][lt]", ToStartTimeString(StartLt));
            if (MinViewCounter.HasValue)
            {
                AppendParam(query, "filters[viewCounter][gte]", MinViewCounter.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrEmpty(JsonFilterJson))
            {
                AppendParam(query, "jsonFilter", JsonFilterJson);
            }
            AppendParam(query, "_sort", Sort ?? "");
            AppendParam(query, "_offset", offset.ToString(CultureInfo.InvariantCulture));
            AppendParam(query, "_limit", limit.ToString(CultureInfo.InvariantCulture));
            AppendParam(query, "_context", Context ?? "");
            return Endpoint + "?" + query.ToString();
        }

        private static void AppendParam(StringBuilder query, string key, string value)
        {
            if (query.Length > 0)
            {
                query.Append('&');
            }
            query.Append(key);
            query.Append('=');
            query.Append(Uri.EscapeDataString(value ?? ""));
        }
    }
}
