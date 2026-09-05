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
        /// <summary>マイリスト数フィルタ下限（filters[mylistCounter][gte]）。nullでフィルタ無し</summary>
        public long? MinMylistCounter { get; set; } = null;
        /// <summary>いいね数フィルタ下限（filters[likeCounter][gte]）。nullでフィルタ無し</summary>
        public long? MinLikeCounter { get; set; } = null;
        /// <summary>コメント数フィルタ下限（filters[commentCounter][gte]）。nullでフィルタ無し</summary>
        public long? MinCommentCounter { get; set; } = null;
        /// <summary>動画種別フィルタ（filters[contentType][0]）。null・空でフィルタ無し（long/shortのみ有効）</summary>
        public string ContentType { get; set; } = null;
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

        /// <summary>タグ検索で日付フィルタを使わない場合の中立の開始日（全動画を含む）</summary>
        public static readonly DateTime NeutralStartGte = new DateTime(2000, 1, 1);
        /// <summary>タグ検索で日付フィルタを使わない場合の中立の終了日（全動画を含む）</summary>
        public static readonly DateTime NeutralStartLt = new DateTime(2100, 1, 1);

        /// <summary>
        /// タグ検索のリクエストを生成する（TagRankAnalyze 用）。
        /// タグ条件は jsonFilter、数値・日付・種別は filters[] の実証済み記法で指定する。
        /// </summary>
        /// <param name="jsonFilterJson">タグ条件の jsonFilter（TagConditionParser 製）</param>
        /// <param name="viewMin">再生数下限（0以下でフィルタ無し）</param>
        /// <param name="mylistMin">マイリスト数下限（0以下でフィルタ無し）</param>
        /// <param name="likeMin">いいね数下限（0以下でフィルタ無し）</param>
        /// <param name="commentMin">コメント数下限（0以下でフィルタ無し）</param>
        /// <param name="startGte">投稿期間の開始日</param>
        /// <param name="startLt">投稿期間の終了日</param>
        /// <param name="contentType">動画種別（long/short。それ以外はフィルタ無し）</param>
        /// <param name="limit">取得件数</param>
        /// <param name="offset">取得オフセット</param>
        public static SnapShotRequest CreateTagSearch(string jsonFilterJson, long viewMin, long mylistMin, long likeMin, long commentMin,
            DateTime startGte, DateTime startLt, string contentType, int limit, long offset)
        {
            string validContentType = null;
            if (contentType == "long" || contentType == "short")
            {
                validContentType = contentType;
            }
            return new SnapShotRequest
            {
                Q = "",
                Targets = null,
                Fields = DefaultFields,
                StartGte = startGte.Date,
                StartLt = startLt.Date,
                MinViewCounter = viewMin > 0 ? (long?)viewMin : null,
                MinMylistCounter = mylistMin > 0 ? (long?)mylistMin : null,
                MinLikeCounter = likeMin > 0 ? (long?)likeMin : null,
                MinCommentCounter = commentMin > 0 ? (long?)commentMin : null,
                ContentType = validContentType,
                JsonFilterJson = jsonFilterJson,
                Sort = DefaultSort,
                Limit = limit,
                Offset = offset,
                Context = DefaultContext
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
            if (MinMylistCounter.HasValue)
            {
                AppendParam(query, "filters[mylistCounter][gte]", MinMylistCounter.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (MinLikeCounter.HasValue)
            {
                AppendParam(query, "filters[likeCounter][gte]", MinLikeCounter.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (MinCommentCounter.HasValue)
            {
                AppendParam(query, "filters[commentCounter][gte]", MinCommentCounter.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrEmpty(ContentType))
            {
                AppendParam(query, "filters[contentType][0]", ContentType);
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
