using nicorankLib.Analyze.model;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Input
{
    /// <summary>
    /// タグ検索集計用の入力。スナップショット検索API v2 へのライブ検索で動画IDリストを生成し、
    /// SPモードの動画IDリスト相当の Ranking 列表にする（差分計算は SnapShotSabunReader が行う）。
    /// </summary>
    public class TagRankAnalyze : InputBase
    {
        /// <summary>検索結果の自主規制上限。これを超えたら集計しない</summary>
        public const long MaxTotalCount = 50000;
        /// <summary>1回の取得件数（API上限）</summary>
        protected const int PageSize = 100;
        /// <summary>ページ取得の並列数</summary>
        protected const int MaxDegreeOfParallelism = 4;
        /// <summary>1リクエストあたりの最大リトライ回数</summary>
        protected const int MaxRetryCount = 20;

        public DateTime AnalyzeTime { get; protected set; }
        public TagSearchQuery Query { get; protected set; }

        public TagRankAnalyze(DateTime analyzeTime, TagSearchQuery query)
        {
            AnalyzeTime = analyzeTime;
            Query = query;
        }

        public override bool AnalyzeRank(out List<Ranking> rakingList)
        {
            rakingList = new List<Ranking>();

            if (Query == null)
            {
                StatusLog.WriteLine("検索条件が設定されていません");
                return false;
            }
            if (!TagConditionParser.TryParse(Query.TagCondition, out string jsonFilter, out string error))
            {
                StatusLog.WriteLine(error);
                return false;
            }
            if (!GetTotalCount(jsonFilter, out long totalCount))
            {
                return false;
            }
            StatusLog.WriteLine($"タグ検索のヒット件数: {totalCount} 件");
            if (totalCount > MaxTotalCount)
            {
                StatusLog.WriteLine($"検索結果が多すぎます。({totalCount}件) {MaxTotalCount}件以下になるように条件を追加して下さい");
                return false;
            }
            var ids = CollectContentIds(jsonFilter, totalCount);
            if (ids == null)
            {
                return false;
            }
            foreach (var id in ids)
            {
                var wRank = new Ranking();
                wRank.ID = id;
                rakingList.Add(wRank);
            }
            return true;
        }

        /// <summary>
        /// 検索のヒット件数を取得する（UI の件数確認用）
        /// </summary>
        public bool GetTotalCount(out long totalCount)
        {
            totalCount = 0;
            if (Query == null)
            {
                StatusLog.WriteLine("検索条件が設定されていません");
                return false;
            }
            if (!TagConditionParser.TryParse(Query.TagCondition, out string jsonFilter, out string parseError))
            {
                StatusLog.WriteLine(parseError);
                return false;
            }
            return GetTotalCount(jsonFilter, out totalCount);
        }

        /// <summary>
        /// 検索のヒット件数を取得する
        /// </summary>
        protected bool GetTotalCount(string jsonFilter, out long totalCount)
        {
            totalCount = 0;
            string url = CreateRequestUrl(jsonFilter, 0, 0);
            for (int retry = 0; retry < MaxRetryCount; retry++)
            {
                if (!DownloadText(url, out string jsonText))
                {
                    continue;
                }
                var info = ParseResponse(jsonText);
                if (info?.Meta == null || info.Meta.Status != 200)
                {
                    continue;
                }
                totalCount = info.Meta.TotalCount;
                return true;
            }
            StatusLog.WriteLine("タグ検索の件数取得に失敗しました");
            return false;
        }

        /// <summary>
        /// 全ページを取得して動画ID列（重複除去・ID順）を生成する
        /// </summary>
        protected List<string> CollectContentIds(string jsonFilter, long totalCount)
        {
            var idSet = new HashSet<string>();
            var lockObj = new object();
            bool failed = false;
            var offsets = new List<long>();
            for (long offset = 0; offset < totalCount; offset += PageSize)
            {
                offsets.Add(offset);
            }
            Parallel.ForEach(offsets, new ParallelOptions() { MaxDegreeOfParallelism = MaxDegreeOfParallelism }, (offset) =>
            {
                string url = CreateRequestUrl(jsonFilter, PageSize, offset);
                bool ok = false;
                for (int retry = 0; retry < MaxRetryCount; retry++)
                {
                    if (!DownloadText(url, out string jsonText))
                    {
                        continue;
                    }
                    var info = ParseResponse(jsonText);
                    if (info?.Meta == null || info.Meta.Status != 200 || info.Data == null)
                    {
                        continue;
                    }
                    lock (lockObj)
                    {
                        foreach (var data in info.Data)
                        {
                            if (!string.IsNullOrEmpty(data.ID))
                            {
                                idSet.Add(data.ID);
                            }
                        }
                    }
                    ok = true;
                    break;
                }
                if (!ok)
                {
                    lock (lockObj)
                    {
                        failed = true;
                    }
                    ErrLog.GetInstance().Write(new Exception($"タグ検索のページ取得に失敗しました offset={offset}"));
                }
            });
            if (failed)
            {
                StatusLog.WriteLine("タグ検索の取得に失敗しました");
                return null;
            }
            return idSet.OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// タグ検索のリクエストURLを生成する
        /// </summary>
        protected string CreateRequestUrl(string jsonFilter, int limit, long offset)
        {
            DateTime gte = Query.UseDateFilter ? Query.StartGte : SnapShotRequest.NeutralStartGte;
            DateTime lt = Query.UseDateFilter ? Query.StartLt : SnapShotRequest.NeutralStartLt;
            return SnapShotRequest.CreateTagSearch(jsonFilter, Query.ViewMin, Query.MylistMin, Query.LikeMin, Query.CommentMin,
                gte, lt, Query.ContentType, limit, offset).ToUrl();
        }

        /// <summary>
        /// HTTP取得（単体テストで差し替える想定）
        /// </summary>
        protected virtual bool DownloadText(string url, out string text)
        {
            return InternetUtil.TxtDownLoad(url, out text);
        }

        /// <summary>
        /// 検索レスポンスをパースする（カウンタの null は 0 扱い）
        /// </summary>
        protected static SnapShotJson ParseResponse(string jsonText)
        {
            try
            {
                return SnapShotJson.FromJson(jsonText.Replace(":null", ":0"));
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return null;
            }
        }

        public override DateTime getAnalyzeDay()
        {
            return AnalyzeTime;
        }

        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            AnalyzeTime = analyzeDay;
        }
    }
}
