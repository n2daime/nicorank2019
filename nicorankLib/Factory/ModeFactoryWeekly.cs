using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.Json;
using nicorankLib.Analyze.Option;
using nicorankLib.Analyze.Option.Basic;
using nicorankLib.api;
using nicorankLib.Common;
using nicorankLib.output;
using System;
using System.Collections.Generic;

namespace nicorankLib.Factory
{
    public class ModeFactoryWeekly : ModeFactoryBase
    {
        public const Analyze.model.EAnalyzeMode AnalyzeMode = Analyze.model.EAnalyzeMode.Weekly;


        public ModeFactoryWeekly()
        {
            //集計日を計算する
            this.TargetDay = DateTime.Now.AddDays(0);
        }

        public void SetTargetTime(DateTime AnalyzeDay)
        {
            this.TargetDay = AnalyzeDay.AddDays(0);
        }
        public override bool CreateAnalyzer()
        {

            // ランキングのベースは週間JSON
            InputBase inputBase = new JsonReaderWeekly(this.TargetDay);

            //集計日を計算する
            this.BaseDay = TargetDay.AddDays(-7); //7日前

            //長期判定
            this.TyokiHantei = new TyokiHantei();
            TyokiHantei.Set(OUTPUTDIR, this.TargetDay);

            //集計に必要なオプションを作成する
            var options = new List<BasicOptionBase>()
            {
                new HiddenMovieDelete()
                ,new SabunReader(BaseDay)                          //差分計算
                ,new LastRankReader(AnalyzeMode, BaseDay)          //先週の順位
            };

            Config config = Config.GetInstance();
            //集計後に実行する（ランキング順位などを参照する）オプションを作成する
            var extoptions = new List<IExtOptionBase>()
            {
                new FavoriteTagReader(config.UserNum,BaseDay, TargetDay)           //人気のタグ
                ,new UserInfoReader(config.UserNum, TargetDay),
                this.TyokiHantei
            };

            this.RankingAnalyze = new RankingAnalyze(inputBase, options, extoptions);

            return true;
        }

        public override OutputBase CreateHistory()
        {
            var history = new ResultHistory(Analyze.model.EAnalyzeMode.Weekly);
            history.SetSyuukeiBi(this.TargetDay);
            return history;
        }

        public override OutputBase CreateNRMRank()
        {
            var nrm = new NrmOutput();
            nrm.Set( OUTPUTDIR, "rank.txt", 0, GetRank() );
            return nrm;
        }

        public override OutputBase CreateNRMRank1000()
        {
            var nrm = new NrmOutput();
            var config = Config.GetInstance();
            nrm.Set(OUTPUTDIR, $"rank{config.UserNum}.txt", 0, config.UserNum);
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
            var output = new ResultCsvRankDB();
            output.SetOutput(OUTPUTDIR, new List<ResultCsv.CsvConfig>() {
                new ResultCsv.CsvConfig() { csvName = "result_DB登録用(UTF8).csv", isUnicode = true, isOverwrite = true },
                new ResultCsv.CsvConfig() { csvName = "result_DB登録用(SJIS).csv", isUnicode = false, isOverwrite = true }
            });
            return output;
        }

        public override OutputBase CreateOutputHTML()
        {
            //未実装
            return null;
        }

        public override OutputBase CreateOutputMovieIconGet()
        {
            Config config = Config.GetInstance();
            int rank = GetRank();

            var output = new ResultImagegetMovieIcon(OUTPUTDIR, "queue.irv", rank + config.RankED);
            return output;
        }

        public override OutputBase CreateOutputUserIconGet()
        {
            var output = new ResultImagegetUserIcon(OUTPUTDIR, "queue_UserIcon.irv");
            return output;
        }

        public override OutputBase CreateOutputWORK()
        {
            //未実装
            return null;
        }
    }
}
