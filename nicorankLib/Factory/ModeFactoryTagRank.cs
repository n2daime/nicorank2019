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
    /// 基準日DBは任意（未指定なら差分なしで累積値をそのまま使う）。
    /// Query.UseLiveCounterが真ならv2最新値モードになり、集計日DBを使わずライブ検索の数値を集計値にする（集計日は実行日）。
    /// </summary>
    public class ModeFactoryTagRank : ModeFactoryWeekly
    {
        public new const Analyze.model.EAnalyzeMode AnalyzeMode = Analyze.model.EAnalyzeMode.TagRank;

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
            BasicOptionBase totalOrSabunReader;
            DateTime analyzeTime;
            TagRankAnalyze inputBase;
            if (Query != null && Query.UseLiveCounter)
            {
                // v2最新値モードは集計日DBを使わない。集計日は実行日とし、数値はライブ検索結果を使う
                analyzeTime = DateTime.Today;
                TargetDay = analyzeTime;
                // ランキングのベースはタグ検索の結果ID列から取得する（LiveCountersはReaderと共有参照）
                inputBase = new TagRankAnalyze(analyzeTime, Query);
                if (string.IsNullOrWhiteSpace(BaseDB))
                {
                    // 基準日DBなしは差分なし。ライブ値をそのまま使う
                    var liveTotalReader = new TagRankLiveTotalReader(inputBase, analyzeTime);
                    if (!liveTotalReader.Open())
                    {
                        // 失敗経路でも生成物を残さない（SP 側と同一理由。LiveTotal は資源なしだが一貫のため）。
                        liveTotalReader.Dispose();
                        return false;
                    }
                    BaseDay = analyzeTime;
                    totalOrSabunReader = liveTotalReader;
                }
                else
                {
                    var liveSabunReader = new TagRankLiveSabunReader(inputBase, analyzeTime, BaseDB);

                    if (!liveSabunReader.Open())
                    {
                        // 失敗経路でも生成物を残さない（基準日DB・fallback の持ち越し防止）。
                        liveSabunReader.Dispose();
                        return false;
                    }
                    BaseDay = liveSabunReader.BaseTime;
                    totalOrSabunReader = liveSabunReader;
                }
            }
            else if (string.IsNullOrWhiteSpace(BaseDB))
            {
                // 基準日DBなしは差分なし。累積値をそのまま使う
                var totalReader = new TagRankTotalReader(AnalyzeDB);
                if (!totalReader.Open())
                {
                    // 失敗経路でも生成物を残さない（集計日DB の持ち越し防止）。
                    totalReader.Dispose();
                    return false;
                }
                analyzeTime = totalReader.AnalyzeTime;
                TargetDay = analyzeTime;
                BaseDay = analyzeTime;
                totalOrSabunReader = totalReader;
                // ランキングのベースはタグ検索の結果ID列から取得する
                inputBase = new TagRankAnalyze(analyzeTime, Query);
            }
            else
            {
                var snapShotSabunReader = new SnapShotSabunReader(
                    AnalyzeDB, BaseDB);

                if (!snapShotSabunReader.Open())
                {
                    // 失敗経路でも生成物を残さない（集計日DB・基準日DB・fallback の持ち越し防止）。
                    snapShotSabunReader.Dispose();
                    return false;
                }
                analyzeTime = snapShotSabunReader.AnalyzeTime;
                TargetDay = snapShotSabunReader.AnalyzeTime;
                BaseDay = snapShotSabunReader.BaseTime;
                totalOrSabunReader = snapShotSabunReader;
                // ランキングのベースはタグ検索の結果ID列から取得する
                inputBase = new TagRankAnalyze(analyzeTime, Query);
            }

            //集計に必要なオプションを作成する
            var options = new List<BasicOptionBase>()
            {
                totalOrSabunReader                                 //差分計算（基準なし時は累積値）
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
