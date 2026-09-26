using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option;
using nicorankLib.Analyze.Option.Basic;
using nicorankLib.Factory;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze
{
    /// <summary>
    /// 破棄経路の整備（Issue #44。提案元 #42）の再発防止テスト。
    /// なぜこのテストが必要か: 集計後に残るDB接続の持ち越しは、通常の単発集計では症状が出ず、
    /// 同一プロセスでの再集計時にだけ積み上がるため、テストで縛らないと再発するから。
    /// </summary>
    [TestClass]
    public class UnitTestBasicOptionDispose
    {
        /// <summary>
        /// 破棄呼び出しを数えるための stub。基底の仮想 Dispose を上書きする。
        /// </summary>
        private sealed class StubOption : BasicOptionBase
        {
            public int DisposeCount { get; private set; }

            public override bool AnalyzeRank(ref List<Ranking> rankingList)
            {
                return true;
            }

            public override void Dispose()
            {
                DisposeCount++;
            }
        }

        [TestMethod]
        public void EmptyDispose_NonResourceOptions_NoThrow()
        {
            // 資源を持たない7件は基底の空実装のまま呼べること（将来の追加時の契約確認）。
            // なぜ基底参照で呼ぶか: 呼び出し側（RankingAnalyze）はリストとして一括破棄するため。
            var today = DateTime.Today;
            var options = new List<BasicOptionBase>()
            {
                new HiddenMovieDelete(),
                new SabunReader(today),
                new LastRankReader(EAnalyzeMode.Weekly, today),
                new LastRankCsvReader(string.Empty),
                new GenreInfoReader(today),
                new MovieInfoReader(today),
                new TagRankLiveTotalReader(
                    new TagRankAnalyze(today, new TagSearchQuery() { TagCondition = "A" }), today),
            };
            foreach (var option in options)
            {
                option.Dispose();
            }
            foreach (var option in options)
            {
                option.Dispose();
            }
        }

        [TestMethod]
        public void RankingAnalyze_Dispose_DisposesAllOptionsOnce()
        {
            var stub1 = new StubOption();
            var stub2 = new StubOption();
            var analyze = new RankingAnalyze(
                null,
                new List<BasicOptionBase>() { stub1, stub2 });

            analyze.Dispose();

            Assert.AreEqual(1, stub1.DisposeCount);
            Assert.AreEqual(1, stub2.DisposeCount);

            // 二重破棄は無視されること（UI の付け替え前と集計終了後の両方で呼ぶため）。
            analyze.Dispose();

            Assert.AreEqual(1, stub1.DisposeCount);
            Assert.AreEqual(1, stub2.DisposeCount);
        }

        [TestMethod]
        public void RankingAnalyze_Dispose_NullListsSafe()
        {
            var analyze = new RankingAnalyze(null);

            analyze.Dispose();
            analyze.Dispose();
        }

        [TestMethod]
        public void SnapShotSabunReader_Dispose_KeepsInjectedFallbackConnectionsOpen()
        {
            // 注入接続は呼び出し側の所有物のため閉じないこと（SpMovieInfoFallback の流儀）。
            // なぜ Reader 経由で確認するか: 実運用では Reader の Dispose から fallback の Close が呼ばれるため。
            var historyDb = TestDbHelper.CreateInMemoryDb();
            var officialDb = TestDbHelper.CreateInMemoryDb();
            var reader = new SnapShotSabunReader(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                null,
                new SpMovieInfoFallback(historyDb, officialDb));

            reader.Dispose();
            reader.Dispose();

            Assert.IsTrue(historyDb.IsOpen);
            Assert.IsTrue(officialDb.IsOpen);
            historyDb.Dispose();
            officialDb.Dispose();
        }

        [TestMethod]
        public void TagRankTotalReader_Dispose_TwiceSafe()
        {
            var reader = new TagRankTotalReader(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"));

            Assert.IsFalse(reader.Open());
            reader.Dispose();
            reader.Dispose();
        }

        [TestMethod]
        public void ModeFactoryTagRank_Dispose_DisposesRankingAnalyzeTwiceSafe()
        {
            // v2最新値・基準DBなし経路はDBに触れず CreateAnalyzer が成功するため、
            // 委譲破棄（Factory→RankingAnalyze→Option）を例外なく二重呼び出しできること。
            var factory = new ModeFactoryTagRank();
            factory.SetInputFile(
                string.Empty,
                string.Empty,
                new TagSearchQuery() { TagCondition = "A", UseLiveCounter = true },
                string.Empty);

            Assert.IsTrue(factory.CreateAnalyzer());
            factory.Dispose();
            factory.Dispose();
        }

        [TestMethod]
        public void ModeFactoryTyukan_Dispose_WithoutAnalyzer_NoThrow()
        {
            // CreateAnalyzer 前（RankingAnalyze 未生成）の破棄は何もしないこと。
            var factory = new ModeFactoryTyukan();

            factory.Dispose();
            factory.Dispose();
        }
    }
}
