using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.Option;
using nicorankLib.Analyze.Option.Basic;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.SnapShot;
using System;
using System.Collections.Generic;

namespace nicorankLib.Factory
{
    /// <summary>
    /// タグ検索集計モードのファクトリ。SPモード相当の出力・差分方式で、
    /// 入力のみスナップショットv2のライブ検索（TagRankAnalyze）に差し替える。
    /// 前回結果CSVは任意（未指定なら前回順位なし）。
    /// </summary>
    public class ModeFactoryTagRank : ModeFactoryWeekly
    {
        public const Analyze.model.EAnalyzeMode AnalyzeMode = Analyze.model.EAnalyzeMode.TagRank;

        public string AnalyzeDB;
        public string BaseDB;
        public TagSearchQuery Query;
        public string LastResultCsvFile;

        public void SetInputFile(string analyzeDB, string baseDB, TagSearchQuery query, string lastResultCsvFile)
        {
            AnalyzeDB = analyzeDB;
            BaseDB = baseDB;
            Query = query;
            LastResultCsvFile = lastResultCsvFile;
        }

        public override bool CreateAnalyzer()
        {
            var snapShotSabunReader = new SnapShotSabunReader(
                AnalyzeDB, BaseDB);

            if (!snapShotSabunReader.Open())
            {
                return false;
            }
            TargetDay = snapShotSabunReader.AnalyzeTime;
            BaseDay = snapShotSabunReader.BaseTime;

            // ランキングのベースはタグ検索の結果ID列から取得する
            var inputBase = new TagRankAnalyze(snapShotSabunReader.AnalyzeTime, Query);

            //集計に必要なオプションを作成する
            var options = new List<BasicOptionBase>()
            {
                snapShotSabunReader                                 //差分計算
            };
            if (!string.IsNullOrWhiteSpace(LastResultCsvFile))
            {
                options.Add(new LastRankCsvReader(LastResultCsvFile));  //先週の順位（任意）
            }

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

        public override OutputBase CreateOutputJson_rankDB()
        {
            var output = new ResultJsonRankDB();
            output.SetOutput(OUTPUTDIR, "result_DB登録用(UTF8).json");
            return output;
        }
    }
}
