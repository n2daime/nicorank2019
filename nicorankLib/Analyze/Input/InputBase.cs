using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Input
{
    public abstract class InputBase
    {
        /// <summary>
        /// 集計日
        /// </summary>
        public DateTime AnalyzeDay { get {return getAnalyzeDay(); } set { setAnalyzeDay(value); } }

        public abstract bool AnalyzeRank(out List<Ranking> rakingList);

        public abstract DateTime getAnalyzeDay();
        public abstract void setAnalyzeDay(DateTime analyzeDay);

        ///// <summary>
        ///// 前回のランキングの集計日を計算する
        ///// </summary>
        ///// <returns></returns>
        //public abstract EAnalyzeMode getLastRankDay(out DateTime? lastRankDay);

    }
}
