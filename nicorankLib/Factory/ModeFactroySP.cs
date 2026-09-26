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
    public class ModeFactroySP: ModeFactoryWeekly
    {

        public String AnalyzeDB;
        public String BaseDB;
        public String MovieListFile;
        public string LastResultCsvFile;

        public void SetInputFile(string analyzeDB, string baseDB, string movieListFile,string lastResultCsvFile)
        {
            AnalyzeDB = analyzeDB;
            BaseDB = baseDB;
            MovieListFile = movieListFile;
            LastResultCsvFile = lastResultCsvFile;
        }

        public override bool CreateAnalyzer()
        {
            var snapShotSabunReader = new SnapShotSabunReader(
                AnalyzeDB , BaseDB);

            if (!snapShotSabunReader.Open())
            {
                // 開けなかった Reader 自体も破棄する。なぜここで閉じるか:
                // 生成済みの SQLiteCtrl（未接続・fallback 未開）は残っても実害は小さいが、
                // 所有権を Factory が持つ以上、失敗経路でも残さないのが一貫するため。
                snapShotSabunReader.Dispose();
                return false;
            }
            TargetDay = snapShotSabunReader.AnalyzeTime;
            BaseDay = snapShotSabunReader.BaseTime;

            // ランキングのベースはMovieListFileから取得する
            var inputBase = new SPAnalyze(snapShotSabunReader.AnalyzeTime , MovieListFile);

            
            //集計に必要なオプションを作成する
            var options = new List<BasicOptionBase>()
            {
                snapShotSabunReader                                 //差分計算
                ,new LastRankCsvReader(LastResultCsvFile)          //先週の順位                
            };

            Config config = Config.GetInstance();
            //集計後に実行する（ランキング順位などを参照する）オプションを作成する
            var extoptions = new List<IExtOptionBase>()
            {
                new FavoriteTagReader(config.UserNum,BaseDay, TargetDay)           //人気のタグ
            };

            this.RankingAnalyze = new RankingAnalyze(inputBase, options, extoptions);

            return true;
        }

        public override OutputBase CreateHistory()
        {
            return null;
        }
        public override OutputBase CreateOutputJson_rankDB()
        {
            var output = new ResultJsonRankDB();
            output.SetOutput(OUTPUTDIR, "result_DB登録用(UTF8).json");
            return output;
        }
    }
}
