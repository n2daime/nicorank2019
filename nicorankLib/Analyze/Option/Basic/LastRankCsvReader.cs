using Newtonsoft.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Option
{
    /// <summary>
    /// 先週(前回）のランキングを付与する
    /// </summary>
    public class LastRankCsvReader : BasicOptionBase
    {
        /// <summary>
        /// 
        /// </summary>
        protected string LastResultCsvFile;


        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="analyzeMode">集計モード</param>
        /// <param name="lastDay">前回集計日</param>
        /// <param name="otherOption"></param>
        public LastRankCsvReader(string lastResultCsvFile)
        {
            this.LastResultCsvFile = lastResultCsvFile;
        }

        /// <summary>
        /// 先週(前回）のランキングを付与する
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(ref List<Ranking> rakingList)
        {
            try
            {
                var config = Config.GetInstance();

                //先週のランキングを集計する
                StatusLog.WriteLine($"{this.LastResultCsvFile} から前回順位を取得しています...");

                //前回結果CSVを読み取る（カラム名から動的に検出するため全列読取でよい）
                TextUtil.ReadCsv(this.LastResultCsvFile, out Dictionary<string, Ranking> lastRankList);

                //Parallel.ForEach(rakingList, new ParallelOptions() { MaxDegreeOfParallelism = config.ThreadMax }, wRank =>
                foreach(var wRank in rakingList)
                {
                    // 前回のランキングが存在するかチェックする
                    if(lastRankList.ContainsKey(wRank.ID))
                    {//前回順位が存在する
                        var lastRank = lastRankList[wRank.ID];
                        wRank.LastRank = lastRank.RankTotal;
                        wRank.LastPoint = lastRank.PointTotal;
                    }
                }
                //);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }
    }
}
