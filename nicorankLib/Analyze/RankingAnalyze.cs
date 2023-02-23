using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Official;
using nicorankLib.Analyze.Option;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;

namespace nicorankLib.Analyze
{
    public class RankingAnalyze
    {
        /// <summary>
        /// 基本となるランキングデータを取得するクラス
        /// </summary>
        InputBase Input = null;

        /// <summary>
        /// ランキングに付加情報を付与するクラス
        /// </summary>
        List<BasicOptionBase> BaseOptionList = null;

        List<IExtOptionBase> ExtOptionList = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="baseTime"></param>
        public RankingAnalyze(InputBase input, List<BasicOptionBase> optionList = null, List<IExtOptionBase> extOptionList = null)
        {
            Input = input;
            if (optionList == null)
            {
                BaseOptionList = new List<BasicOptionBase>();
            }
            else
            {
                BaseOptionList = optionList;
            }
            if (extOptionList == null)
            {
                ExtOptionList = new List<IExtOptionBase>();
            }
            else
            {
                ExtOptionList = extOptionList;
            }
        }

        public bool AnalyzeRank(out List<Ranking> rankingList)
        {

            try
            {
                //基本となるランキングデータを取得する
                if (!this.Input.AnalyzeRank(out rankingList))
                {
                    return false;
                }

                foreach (var Option in BaseOptionList)
                {
                    if (!Option.AnalyzeRank(ref rankingList))
                    {
                        StatusLog.WriteLine($"\n集計中にエラーが発生しました。エラーログを確認してください。");
                        return false;
                    }
                }

                if (!this.calcRanking(rankingList))
                {
                    StatusLog.WriteLine($"\n順位の計算に失敗しました。エラーログを確認してください。");
                    return false;
                }

                //総合順位順に並び替える
                rankingList = rankingList.OrderBy(rank => rank.RankTotal).ToList();


                foreach (var Option in ExtOptionList)
                {
                    if (!Option.AnalyzeRank(rankingList))
                    {
                        StatusLog.WriteLine($"\n集計中にエラーが発生しました。エラーログを確認してください。");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                rankingList = new List<Ranking>();
                var errLog = ErrLog.GetInstance();
                errLog.Write(ex);
            }

            return true;
        }


        /// <summary>
        /// ランキングを計算する
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        protected bool calcRanking(List<Ranking> rakingList)
        {
            try
            {
                var config = Config.GetInstance();
                StatusLog.WriteLine("カテゴリ順位を再計算しています．．．");

                // 削除されていない動画に対して、カテゴリ毎にグループ分けする
                var categoryRankList = rakingList
                    .Where(rank => !rank.isDelete)
                    .GroupBy(rank => rank.Category);

                StatusLog.WriteLine("ランキングを計算しています．．");

                var taskList = new List<Task>();
                taskList.Add(Task.Run(() =>
                {// 総合順位
                    long rank = 1;
                    var workList = rakingList.OrderByDescending(ranking => ranking.PointTotal).ToList();
                    foreach (var wRank in workList)
                    {
                        wRank.RankTotal = rank;
                        rank++;
                    }
                }));
                taskList.Add(Task.Run(() =>
                {// 再生順位
                    long rank = 1;
                    var workList = rakingList.OrderByDescending(ranking => ranking.CountPlay).ToList();
                    foreach (var wRank in workList)
                    {
                        wRank.RankPlay = rank;
                        rank++;
                    }
                }));
                taskList.Add(Task.Run(() =>
                {// コメント順位
                    long rank = 1;
                    var workList = rakingList.OrderByDescending(ranking => ranking.CountComment).ToList();
                    foreach (var wRank in workList)
                    {
                        wRank.RankComment = rank;
                        rank++;
                    }
                }));
                taskList.Add(Task.Run(() =>
                {// マイリスト順位
                    long rank = 1;
                    var workList = rakingList.OrderByDescending(ranking => ranking.CountMyList).ToList();
                    foreach (var wRank in workList)
                    {
                        wRank.RankMyList = rank;
                        rank++;
                    }
                }));
                taskList.Add(Task.Run(() =>
                {// いいね順位
                    long rank = 1;
                    var workList = rakingList.OrderByDescending(ranking => ranking.CountLike).ToList();
                    foreach (var wRank in workList)
                    {
                        wRank.RankLike = rank;
                        rank++;
                    }
                }));

                taskList.Add(Task.Run(() =>
                {// カテゴリ順位
                    foreach (var cateRankList in categoryRankList)
                    {
                        long rank = 1;
                        var workList = cateRankList.OrderByDescending(ranking => ranking.PointTotal).ToList();
                        foreach (var wRank in workList)
                        {
                            wRank.RankCategory = rank;
                            rank++;
                        }
                    }
                }));

                //全部終わるまで待つ
                foreach (Task wTask in taskList)
                {
                    wTask.Wait();
                }

            }
            catch (Exception ex)
            {
                var pLog = ErrLog.GetInstance();
                pLog.Write("ランキング計算でエラー発生(InputBase::calcRanking)");
                pLog.Write(ex);
                return false;
            }
            return true;
        }
    }
}
