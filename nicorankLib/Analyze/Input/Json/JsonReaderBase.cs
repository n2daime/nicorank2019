using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.api;
using nicorankLib.api.model;
using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Json
{
    /// <summary>
    /// 公式過去ランキングデータ（JSON）からデータを取得する
    /// </summary>
    public abstract class JsonReaderBase : InputBase
    {
        protected const string URL_FILENAME = @"file_name_list.json";

        /// <summary>
        /// 計算された集計日
        /// </summary>
        protected DateTime calcAnalyzeDay;
        public override DateTime getAnalyzeDay()
        {
            return calcAnalyzeDay;
        }

        /// <summary>
        /// 直近の file_name_list.json 取得結果（Issue #53）。
        /// 404（データ未提供）と通信エラーを呼び出し側で区別するために保持する。
        /// </summary>
        public TxtDownloadStatus LastFileListStatus { get; private set; } = TxtDownloadStatus.Failed;

        /// <summary>
        /// file_name_list.json が 200 かつ [] だったか（Issue #53）。
        /// 空配列は「その日のデータなし（メンテナンス）」という運用者の明示マーカーであり、
        /// 404 からの推測とは区別する。
        /// </summary>
        public bool LastFileListWasEmpty { get; private set; }

        /// <summary>
        /// 集計日を設定したコンストラクタ
        /// </summary>
        /// <param name="analyzeDay"></param>
        public JsonReaderBase(DateTime analyzeDay)
        {
            this.AnalyzeDay = analyzeDay;
        }

        public JsonReaderBase()
        {
            this.AnalyzeDay = DateTime.Today;
        }

        /// <summary>
        /// 集計可能な時刻かチェックする
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static bool CheckAnalyzeTime(DateTime dateTime)
        {
            if (DateTime.Now.Date == dateTime.Date && DateTime.Now.Hour <= 0 && DateTime.Now.Minute < 60)
            {//当日の1:00 前＝まだ集計されていない可能性がある

                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// 対象の過去ログURLを計算する
        /// </summary>
        /// <returns></returns>
        protected abstract string calcJsonTarget();

        /// <summary>
        /// テキストを取得する（Issue #53）。
        /// テストではこのメソッドを差し替えて、実際の HTTP なしで 404・空配列・ジャンル取得失敗を再現する。
        /// retryOnNotFound: false により、404 は20回リトライせず即座に返す。
        /// 過去ランキングJSONは静的ファイルなので、応答ヘッダー待ちを5秒に短縮する。
        /// </summary>
        protected virtual bool TryDownloadText(string url, out string text, out TxtDownloadStatus status)
        {
            return InternetUtil.TxtDownLoad(
                url, out text, out status, retryOnNotFound: false, timeoutMs: DownloadTimeoutMs);
        }

        /// <summary>
        /// 過去ランキングJSON取得の応答ヘッダー待ち上限（ミリ秒）。
        /// 静的ファイルなので短くてよい。無応答時に1回100秒待つのを避ける（Issue #53）。
        /// </summary>
        private const int DownloadTimeoutMs = 5 * 1000;

        /// <summary>
        /// マルチスレッド対応解析関数
        /// </summary>
        /// <returns></returns>
        protected async Task<List<Ranking>> AnalyzeRankAsync(string jsonTargetURL, List<RankGenreJson> rankGenreJsons)
        {
            var rankings = new List<Ranking>();
            await Task.Run(() =>
            {
                foreach (RankGenreJson rankGenre in rankGenreJsons)
                {
                    string genreURL = jsonTargetURL + rankGenre.File;
                    if (!TryDownloadText(genreURL, out string logListJsonText, out TxtDownloadStatus ignoredGenreStatus))
                    {
                        //失敗
                        continue;
                    }
                    try
                    {
                        bool isAllGenre = rankGenre.Genre == "全ジャンル";
                        string workTag = rankGenre.Tag?.ToString().Trim();

                        RankLogJson[] movieList = RankLogJson.FromJson(logListJsonText);
                        foreach (RankLogJson movieInfo in movieList)
                        {
                            Ranking rank = new Ranking()
                            {
                                ID = movieInfo.Id,
                                Title = movieInfo.title,
                                Date = movieInfo.RegisteredAt.DateTime,
                                CountPlay = movieInfo.Count.View,
                                CountMyList = movieInfo.Count.Mylist,
                                CountComment = movieInfo.Count.Comment,
                                CountLike = movieInfo.Count.Like,
                                CountPlayTotal = movieInfo.Count.View,
                                CountMyListTotal = movieInfo.Count.Mylist,
                                CountCommentTotal = movieInfo.Count.Comment,
                                CountLikeTotal = movieInfo.Count.Like,
                                ThumbnailURL = movieInfo.Thumbnail.GetBestUrl(),
                                  
                            };
                            if (!string.IsNullOrEmpty(movieInfo.PlayTime))
                            {
                                rank.SetPlayTime(movieInfo.PlayTime);
                            }
                            if (!isAllGenre)
                            {
                                if (rankGenre.isGenre)
                                {
                                    rank.Category = rankGenre.Genre;
                                }
                                else 
                                {
                                    if (!string.IsNullOrEmpty(workTag) && !rank.FavoriteTags.Contains(workTag))
                                    {
                                        rank.FavoriteTags.Add(workTag);
                                    }
                                }
                            }

                            rankings.Add(rank);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrLog.GetInstance().Write(ex);
                    }
                }
            });

            return rankings;
        }

        /// <summary>
        /// ランキング情報を取得する
        /// </summary>
        /// <param name="rankings"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(out List<Ranking> rankings)
        {
            rankings = new List<Ranking>();
            LastFileListStatus = TxtDownloadStatus.Failed;
            LastFileListWasEmpty = false;
            try
            {
                var config = Config.GetInstance();

                // 基準となるURLを計算する
                string jsonTargetURL = this.calcJsonTarget();

                // ジャンルリストのURLを計算する
                string fileURL = jsonTargetURL + URL_FILENAME;

                if (!TryDownloadText(fileURL, out string fileListJsonText, out TxtDownloadStatus fileListStatus))
                {
                    //失敗。404 と通信エラーを呼び出し側が区別できるよう、理由を保持する（Issue #53）
                    LastFileListStatus = fileListStatus;
                    return false;
                }

                //file_name_list.jsonの結果
                RankGenreJson[] rankGenres = RankGenreJson.FromJson(fileListJsonText);
                if (rankGenres == null)
                {
                    // [] ではなく空文字・壊れた JSON。データなしマーカーと誤認しないよう失敗扱いにする
                    LastFileListStatus = TxtDownloadStatus.Failed;
                    return false;
                }
                LastFileListStatus = TxtDownloadStatus.Success;
                LastFileListWasEmpty = rankGenres.Length == 0;

                // マルチスレッドで取得する
                int threadMax = config.ThreadMax;
                var rankGenreTaskList = new List<RankGenreJson>[threadMax];

                // ジャンルをタスクの数だけ分割する
                long ListNo = 0;
                foreach (var rankGenre in rankGenres)
                {
                    if (rankGenreTaskList[ListNo] == null)
                    {
                        rankGenreTaskList[ListNo] = new List<RankGenreJson>();
                    }
                    rankGenreTaskList[ListNo].Add(rankGenre);
                    ListNo++;
                    if (ListNo >= threadMax)
                    {
                        ListNo = 0;
                    }
                }

                // 実行と結果を受け取る
                var taskResultList = new List<Task<List<Ranking>>>(threadMax);
                foreach (var rankGenreTask in rankGenreTaskList)
                {
                    if (rankGenreTask != null)
                    {
                        taskResultList.Add(AnalyzeRankAsync(jsonTargetURL, rankGenreTask));
                    }
                }

                var taskRankList = new List<List<Ranking>>(taskResultList.Count);

                //重複IDの削除
                var movieDic = new Dictionary<string, Ranking>();
                foreach (var taskResult in taskResultList)
                {
                    taskRankList.Add(taskResult.Result);
                }

                rankings = Ranking.MergeRankingList(taskRankList);
            }
            catch(Exception ex)
            {
                rankings = null;
                LastFileListStatus = TxtDownloadStatus.Failed;
                ErrLog.GetInstance().Write(ex);
                return false;
            }

            return true;

        }
    }

}
