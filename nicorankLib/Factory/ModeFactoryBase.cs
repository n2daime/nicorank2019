using nicorankLib.Analyze;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option;
using nicorankLib.api;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Factory
{
    public abstract class ModeFactoryBase
    {
        protected const string OUTPUTDIR = "Output";

        public TyokiHantei TyokiHantei { get; protected set; } = null;

        public List<Ranking> RankingList { get; protected set; }

        public DateTime TargetDay { get; protected set; }
        public DateTime BaseDay { get; protected set; }

        protected RankingAnalyze RankingAnalyze { get; set; }


        public abstract bool CreateAnalyzer();

        public abstract OutputBase CreateHistory();
        public abstract OutputBase CreateOutputHTML();
        public abstract OutputBase CreateOutputWORK();
        public abstract OutputBase CreateOutputCSV();
        public abstract OutputBase CreateOutputMovieIconGet();
        public abstract OutputBase CreateOutputUserIconGet();

        public abstract OutputBase CreateNRMRank();
        public abstract OutputBase CreateNRMRankED();
        public abstract OutputBase CreateNRMRank1000();

        public abstract OutputBase CreateOutputCSV_rankDB();

        /// <summary>
        /// ランキングを解析する
        /// </summary>
        /// <returns></returns>
        public virtual bool AnalyzeRank()
        {
            if(!this.RankingAnalyze.AnalyzeRank(out var rakingList))
            {
                StatusLog.WriteLine("");
                StatusLog.WriteLine("解析失敗。データが取得できませんでした。リターンキーで終了。");
                return false;
            }
            this.RankingList = rakingList;

            //checkHistoryData();
            return true;
        }

        /// <summary>
        /// 長期も考慮した紹介動画数を算出する
        /// </summary>
        /// <returns></returns>
        protected int GetRank()
        {
            Config config = Config.GetInstance();
            if (TyokiHantei != null)
            {
                return config.Rank + TyokiHantei.tyokiRankList.Count;
            }
            else
            {
                return config.Rank;
            }
        }
    }
}
