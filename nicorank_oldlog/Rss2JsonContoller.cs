using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorank_oldlog
{
    public class Rss2JsonContoller
    {
        DateTime today;
        string optionFolderAppend = "";

        public Rss2JsonContoller()
        {
            //日付は一回の実行で共通のものを使用する（途中で日付が変わることを考慮）
            this.today = DateTime.Now;
        }

        /// <summary>
        /// 取得するランキングの種類を決定する(daily/weekly/monthly/total)
        /// </summary>
        /// <param name="today"></param>
        /// <returns></returns>
        public List<Ranking_Info> GetRankingInfo(in string[] args)
        {
            var convConfig = ConvertConfig.GetInstance();

            var getRankingList = new List<Ranking_Info>(); // 取得するランキングの種類

            if (convConfig != null)
            {
                if (args.Length < 1)
                {
                    // daily / totalは毎日更新
                    getRankingList.Add(convConfig.ranking_daily);
                    getRankingList.Add(convConfig.ranking_total);

                    // 今日の日付を取得
                    // 有効な集計日になるまでループ

                    if (today.DayOfWeek == DayOfWeek.Monday)
                    {
                        //月曜日はweekly
                        getRankingList.Add(convConfig.ranking_weekly);
                    }
                    if (today.Day == 1)
                    {
                        //毎月１日はmonthly
                        getRankingList.Add(convConfig.ranking_monthly);
                    }
                }
                else
                {
                    Ranking_Info? temp = null;
                    
                    foreach (var arg in args)
                    {
                        if (arg.StartsWith("/term:"))
                        {
                            var workOptiopn = arg.Substring("/term:".Length);

                            switch (workOptiopn)
                            {
                                case "daily":
                                    temp = convConfig.ranking_daily;
                                    break;
                                case "weekly":
                                    temp = convConfig.ranking_weekly;
                                    break;
                                case "monthly":
                                    temp = convConfig.ranking_monthly;
                                    break;
                                case "total":
                                    temp = convConfig.ranking_total;
                                    break;
                            }
                        }
                        if (arg.StartsWith("/folderappend:"))
                        {
                            optionFolderAppend = arg.Substring("/folderappend:".Length);
                        }
                    }
                    if (temp != null)
                    {
                        getRankingList.Add(temp);
                    }

                }
            }
            return getRankingList;

        }

        public async Task< List<Rss2Json>> AsyncExecuteAnalyzeRank(List<Ranking_Info> rssgetRankingList, List<GenreInfo> genreList)
        {
            var rss2jsonList = new List<Rss2Json>(rssgetRankingList.Count);

            var taskList = new List<Task<bool>>();

            foreach (var rankingInfo in rssgetRankingList)
            {
                Rss2Json rss2jsonWork;
                if (rankingInfo.rss == "24h")
                {
                    rss2jsonWork = new Rss2JsonDaily(rankingInfo);
                }
                else if (rankingInfo.rss == "week" )
                { 
                    rss2jsonWork = new Rss2Json(rankingInfo, "lastweekly_all.json");
                }
                else if (rankingInfo.rss == "month")
                {
                    rss2jsonWork = new Rss2Json(rankingInfo, "lastmonthly_all.json");
                }
                else
                {
                    rss2jsonWork = new Rss2Json(rankingInfo);
                }

                rss2jsonWork.Initilize(genreList, today, optionFolderAppend);

                taskList.Add(Task.Run(() => rss2jsonWork.AnalyzeRank())); 
                /*
                if (rss2jsonWork.isRetry)
                {
                    // Retryが必要な集計は、Task化してあとから待つ
                    taskList.Add( Task.Run(() => rss2jsonWork.AnalyzeRank()));
                }
                else
                {
                    //Retryが不要な集計は、実行完了までまつ
                    rss2jsonWork.AnalyzeRank();
                }
                */
                rss2jsonList.Add(rss2jsonWork);
            }

            //各タスクが終わるまで待つ
            await Task.WhenAll(taskList);

            return rss2jsonList;
        }

    }
}
