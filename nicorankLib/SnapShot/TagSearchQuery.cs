using System;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// タグ検索集計の検索条件（UI から TagRankAnalyze への受け渡し用）。
    /// 数値下限の 0・日付フィルタOFF・種別 null はすべて「指定なし」を表す。
    /// </summary>
    public class TagSearchQuery
    {
        /// <summary>タグ条件式（例: タグ1&amp;タグ2|タグ3*）</summary>
        public string TagCondition { get; set; } = "";
        /// <summary>再生数下限</summary>
        public long ViewMin { get; set; } = 0;
        /// <summary>マイリスト数下限</summary>
        public long MylistMin { get; set; } = 0;
        /// <summary>いいね数下限</summary>
        public long LikeMin { get; set; } = 0;
        /// <summary>コメント数下限</summary>
        public long CommentMin { get; set; } = 0;
        /// <summary>投稿日で絞り込むか</summary>
        public bool UseDateFilter { get; set; } = false;
        /// <summary>投稿期間の開始日</summary>
        public DateTime StartGte { get; set; }
        /// <summary>投稿期間の終了日</summary>
        public DateTime StartLt { get; set; }
        /// <summary>動画種別（null・long・short）</summary>
        public string ContentType { get; set; } = null;
        /// <summary>
        /// スナップショットv2の最新値をそのまま集計値に使うか。
        /// trueなら集計日DBを使わず、ライブ検索で得た4数値を累積値として採用する（UIのchkUseLiveCounterに対応）。
        /// falseなら従来通りSnapshotDBから数値を読む。既定falseで既存動作を保つ。
        /// </summary>
        public bool UseLiveCounter { get; set; } = false;
    }
}
