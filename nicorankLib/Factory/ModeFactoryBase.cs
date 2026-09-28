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
    /// <summary>
    /// モード別の集計組み立て。保持する RankingAnalyze の破棄責任を持つ。
    /// なぜ Factory で委譲するか: Reader の生成は各派生の CreateAnalyzer が行うが、生成物の保持先は
    /// 基底の RankingAnalyze プロパティであり、UI からは Factory 経由でしか後片付けできないから。
    /// RankingList は破棄しない（マネージの結果列表であり、出力処理が集計後に使うため）。
    /// </summary>
    public abstract class ModeFactoryBase : IDisposable
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

        public abstract OutputBase CreateOutputJson_rankDB();

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

        private bool _disposed = false;

        /// <summary>
        /// 保持する RankingAnalyze（ひいては BasicOption 群）を破棄する。二重呼び出しでも例外を出さない。
        /// CreateAnalyzer 失敗時など RankingAnalyze が未生成の場合は何もしない。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                try
                {
                    RankingAnalyze?.Dispose();
                }
                catch (Exception ex)
                {
                    ErrLog.GetInstance().Write(ex);
                }
                finally
                {
                    RankingAnalyze = null;
                }
            }
            _disposed = true;
        }
    }
}
