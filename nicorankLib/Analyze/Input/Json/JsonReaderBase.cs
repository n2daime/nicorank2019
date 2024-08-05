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

        protected const string URL_JSON_TARGET = @"https://dcdn.cdn.nimg.jp/nicovideo/old-ranking/{0}/{1}/";

        protected DateTime  URL_CheckDate1 = DateTime.Parse("2024/08/06 00:00:00");
        protected const string URL_JSON_TARGET_20240806 = @"https://2daime.myds.me/old-ranking/{0}/{1}/";

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
                    if (!InternetUtil.TxtDownLoad(genreURL, out string logListJsonText))
                    {
                        //失敗
                        continue;
                    }
                    try
                    {

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
                                PlayTime = movieInfo.PlayTime,
                                Category = rankGenre.Genre
                            };
                            if (rankGenre.Tag != null)
                            {
                                rank.FavoriteTags.Add(rankGenre.Tag.ToString());
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
            try
            {
                var config = Config.GetInstance();

                // 基準となるURLを計算する
                string jsonTargetURL = this.calcJsonTarget();
                rankings = new List<Ranking>();

                // ジャンルリストのURLを計算する
                string fileURL = jsonTargetURL + URL_FILENAME;

                if (!InternetUtil.TxtDownLoad(fileURL, out string fileListJsonText))
                {
                    //失敗
                    return false;
                }

                //file_name_list.jsonの結果
                RankGenreJson[] rankGenres = RankGenreJson.FromJson(fileListJsonText);

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
                ErrLog.GetInstance().Write(ex);
                return false;
            }

            return true;

        }
    }

}
