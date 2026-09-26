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

        /// <summary>
        /// 破棄時に例外を投げる stub。1件失敗でも残りを続ける保証の検証用。
        /// </summary>
        private sealed class ThrowingOption : BasicOptionBase
        {
            public override bool AnalyzeRank(ref List<Ranking> rankingList)
            {
                return true;
            }

            public override void Dispose()
            {
                throw new InvalidOperationException("dispose failure");
            }
        }

        /// <summary>
        /// スナップショットDB形式の一時ファイルを作る。Reader の Open が成功する最小構成。
        /// </summary>
        private static string CreateSnapshotDbFile(int syuukeiBi)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
            File.Create(path).Dispose();
            var dbCtrl = new SQLiteCtrl();
            Assert.IsTrue(dbCtrl.Open(path), "temp db open");
            TestDbHelper.CreateSnapshotRankingTable(dbCtrl);
            TestDbHelper.CreateDBVersionTable(dbCtrl);
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO DBVersion(集計日, Ver) VALUES(@日, '1.0')";
                cmd.Parameters.AddWithValue("@日", syuukeiBi);
                cmd.ExecuteNonQuery();
            }
            dbCtrl.Close();
            dbCtrl.Dispose();
            return path;
        }

        private static TagRankAnalyze LiveInput()
        {
            var input = new TagRankAnalyze(new DateTime(2024, 1, 1), new TagSearchQuery() { TagCondition = "A", UseLiveCounter = true });
            input.LiveCounters["sm1"] = new SnapShotJson.SnapShotJsonData() { ID = "sm1", CountPlay = 100, CountComment = 10, CountMylist = 5, CountLike = 2 };
            return input;
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
        public void RankingAnalyze_Dispose_ContinuesAfterOneFailure()
        {
            // 1件の破棄が例外でも残りを諦めないこと。なぜ必要か: 破棄時の例外で正常な接続まで残すと漏れに戻るため。
            var stub1 = new StubOption();
            var throwing = new ThrowingOption();
            var stub2 = new StubOption();
            var analyze = new RankingAnalyze(
                null,
                new List<BasicOptionBase>() { stub1, throwing, stub2 });

            analyze.Dispose();

            Assert.AreEqual(1, stub1.DisposeCount);
            Assert.AreEqual(1, stub2.DisposeCount);
        }

        [TestMethod]
        public void SnapShotSabunReader_Dispose_KeepsInjectedConnectionsOpen()
        {
            // 注入接続は呼び出し側の所有物のため閉じないこと（SpMovieInfoFallback と同一の流儀）。
            // なぜ Reader 経由で確認するか: 実運用では Reader の Dispose から dbCtrl と fallback の Close が呼ばれるため。
            var injectedDb = TestDbHelper.CreateInMemoryDb();
            var historyDb = TestDbHelper.CreateInMemoryDb();
            var officialDb = TestDbHelper.CreateInMemoryDb();
            var reader = new SnapShotSabunReader(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                injectedDb,
                new SpMovieInfoFallback(historyDb, officialDb));

            reader.Dispose();
            reader.Dispose();

            Assert.IsTrue(injectedDb.IsOpen);
            Assert.IsTrue(historyDb.IsOpen);
            Assert.IsTrue(officialDb.IsOpen);
            injectedDb.Dispose();
            historyDb.Dispose();
            officialDb.Dispose();
        }

        [TestMethod]
        public void SnapShotSabunReader_Dispose_ReleasesSelfOpenedConnections()
        {
            // 自前で開いた接続は実際に閉じること。ハンドルが離れていることはファイル削除の成功で見る。
            // なぜ削除で見るか: 開きっぱなしだと Windows のファイルロックで削除が失敗し、漏れが検出できるため。
            string analyzePath = CreateSnapshotDbFile(20240102);
            string basePath = CreateSnapshotDbFile(20240101);
            try
            {
                var reader = new SnapShotSabunReader(analyzePath, basePath);
                Assert.IsTrue(reader.Open());
                reader.Dispose();
                reader.Dispose();

                File.Delete(analyzePath);
                analyzePath = null;
                File.Delete(basePath);
                basePath = null;
            }
            finally
            {
                // 後始末は best-effort とする。なぜ try/catch で包むか:
                // 本体側の削除失敗（＝ハンドル残留の検出）が finally 側の二次例外で上書きされると切り分けが難しくなるため。
                try
                {
                    if (analyzePath != null && File.Exists(analyzePath))
                    {
                        File.Delete(analyzePath);
                    }
                    if (basePath != null && File.Exists(basePath))
                    {
                        File.Delete(basePath);
                    }
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public void TagRankLiveSabunReader_Dispose_ReleasesSelfOpenedConnection()
        {
            // 基準日DBの自前接続が実際に閉じること（v2最新値の差分あり経路）。
            string basePath = CreateSnapshotDbFile(20240101);
            try
            {
                var reader = new TagRankLiveSabunReader(LiveInput(), new DateTime(2024, 1, 2), basePath);
                Assert.IsTrue(reader.Open());
                reader.Dispose();
                reader.Dispose();

                File.Delete(basePath);
                basePath = null;
            }
            finally
            {
                // 後始末は best-effort とする（SnapShot 側と同一理由）。
                try
                {
                    if (basePath != null && File.Exists(basePath))
                    {
                        File.Delete(basePath);
                    }
                }
                catch
                {
                }
            }
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
