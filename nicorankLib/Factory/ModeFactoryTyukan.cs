using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.Json;
using nicorankLib.Analyze.Option;
using nicorankLib.api;
using nicorankLib.Common;
using nicorankLib.output;

namespace nicorankLib.Factory
{
    public class ModeFactoryTyukan : ModeFactoryBase
    {
        public const Analyze.model.EAnalyzeMode AnalyzeMode = Analyze.model.EAnalyzeMode.Weekly;

        protected DateTime LastWeekDay;

        public void SetLastWeekDay(DateTime lastweekDay)
        {
            this.LastWeekDay = lastweekDay;
        }

        public override bool CreateAnalyzer()
        {

            // ランキングのベースは週間JSON
            var inputBase = new TyukanAnalyze(LastWeekDay.AddDays(7).Date);

            inputBase.setAnalyzeDay(DateTime.Today);

            //集計日を計算する
            this.TargetDay = inputBase.AnalyzeDay;
            this.BaseDay = LastWeekDay.Date;

            //長期判定
            this.TyokiHantei = null;

            //集計に必要なオプションを作成する
            var options = new List<BasicOptionBase>()
            {
                 new LastRankReader(Analyze.model.EAnalyzeMode.Weekly, BaseDay)          //先週の順位
                
            };

            Config config = Config.GetInstance();
            //集計後に実行する（ランキング順位などを参照する）オプションを作成する
            var extoptions = new List<IExtOptionBase>()
            {
                new FavoriteTagReader(config.UserNum, BaseDay, TargetDay)           //人気のタグ
            };

            this.RankingAnalyze = new RankingAnalyze(inputBase, options, extoptions);

            return true;
        }

        public override OutputBase CreateHistory()
        {
            return null;
        }

        public override OutputBase CreateNRMRank()
        {
            var nrm = new NrmOutput();
            nrm.Set(OUTPUTDIR, "rank.txt", 0, GetRank());
            return nrm;
        }

        public override OutputBase CreateNRMRank1000()
        {
            var nrm = new NrmOutput();
            nrm.Set(OUTPUTDIR, "rank1000.txt", 0, 1000);
            return nrm;
        }

        public override OutputBase CreateNRMRankED()
        {
            var nrm = new NrmOutput();
            int rank = GetRank();
            Config config = Config.GetInstance();

            nrm.Set(OUTPUTDIR, "rankED.txt", rank, rank + config.RankED);
            return nrm;
        }

        public override OutputBase CreateOutputCSV()
        {
            var output = new ResultCsv();
            output.SetOutput(OUTPUTDIR, new List<ResultCsv.CsvConfig>() {
                new ResultCsv.CsvConfig() { csvName = "result(UTF8).csv", isUnicode = true, isOverwrite = true },
                new ResultCsv.CsvConfig() { csvName = "result(SJIS).csv", isUnicode = false, isOverwrite = true }
            });
            return output;
        }

        public override OutputBase CreateOutputCSV_rankDB()
        {
            return null;
        }

        public override OutputBase CreateOutputHTML()
        {
            //未実装
            return null;
        }

        public override OutputBase CreateOutputMovieIconGet()
        {
            return null;
        }

        public override OutputBase CreateOutputUserIconGet()
        {
            return null;
        }

        public override OutputBase CreateOutputWORK()
        {
            //未実装
            return null;
        }
    }
}
