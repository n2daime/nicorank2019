using System;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Official;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Official
{
    /// <summary>
    /// Issue #31：SoHistory併設＋1年保持のテスト。
    /// 保持境界はDB内最新集計日起点のため、テストデータのMAXから逆算して検証する。
    /// </summary>
    [TestClass]
    public class UnitTestRankingHistorySoHistory
    {
        private static long ScalarLong(ISQLiteCtrl db, string sql)
        {
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = sql;
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        private static bool TableExists(ISQLiteCtrl db, string name)
        {
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name=@Name;";
                cmd.Parameters.AddWithValue("@Name", name);
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
        }

        [TestMethod]
        public void Ver1で境界より古いso行だけSoHistoryに退避する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                // 最新20210101起点→境界は20200102。退避対象は境界未満のみ
                TestDbHelper.InsertRankingData(db, "so1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "so1", 20210101, 200, 20, 10, 4);
                TestDbHelper.InsertRankingData(db, "so2", 20210101, 300, 30, 15, 6);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 400, 40, 20, 8);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());

                Assert.IsTrue(TableExists(db, "SoHistory"));
                // so1の古い行だけ退避。境界当日以降の行・smは対象外
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory;"));
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE ID='sm1';"));
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE ID='so2';"));
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT 集計日, 再生数 FROM SoHistory WHERE ID='so1';";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200101, Convert.ToInt32(reader["集計日"]));
                        Assert.AreEqual(100L, Convert.ToInt64(reader["再生数"]));
                    }
                }
            }
        }

        [TestMethod]
        public void Ver1で保持境界より古いRankingを消し境界当日を残す()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                // 最新20210101起点→境界は20200102。0101は消え、0102と最新は残る
                TestDbHelper.InsertRankingData(db, "smOld", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "smCut", 20200102, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "smNew", 20210101, 100, 10, 5, 2);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());

                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE ID='smOld';"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE ID='smCut';"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE ID='smNew';"));
                // しおりは消さない
                Assert.AreEqual(3L, ScalarLong(db, "SELECT COUNT(*) FROM RankingDate;"));
            }
        }

        [TestMethod]
        public void Ver1でMovie表を廃止する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateMovieTable(db);
                TestDbHelper.InsertMovieData(db, "sm1", 20191201000000, "TestMovie");

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());

                Assert.IsFalse(TableExists(db, "Movie"));
            }
        }

        [TestMethod]
        public void Ver1でMovie表がなくても成功する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20210101, 100, 10, 5, 2);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());
                Assert.AreEqual(1L, ScalarLong(db, "SELECT Ver FROM DBVersion LIMIT 1;"));
            }
        }

        [TestMethod]
        public void Ver1移行は冪等である()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "so1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "so1", 20210101, 200, 20, 10, 4);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());
                Assert.IsTrue(history.EnsureMigrated());

                Assert.AreEqual(1L, ScalarLong(db, "SELECT Ver FROM DBVersion LIMIT 1;"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM DBVersion;"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE ID='so1';"));
            }
        }

        [TestMethod]
        public void CheckSoMovieNeedSabunはRankingになくSoHistoryにあれば差分元を返す()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                // Rankingには別IDのみ、SoHistoryにso1の古い差分元
                TestDbHelper.InsertRankingData(db, "sm9", 20210101, 10, 1, 1, 1);
                TestDbHelper.InsertSoHistoryData(db, "so1", 20200101, 500, 50, 25, 10);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                Assert.IsTrue(history.CheckSoMovieNeedSabun("so1", 20210101, out var ranking));
                Assert.IsNotNull(ranking);
                Assert.AreEqual(500L, ranking.CountPlay);
                Assert.AreEqual(50L, ranking.CountComment);
                Assert.AreEqual(25L, ranking.CountMyList);
                Assert.AreEqual(10L, ranking.CountLike);
            }
        }

        [TestMethod]
        public void CheckSoMovieNeedSabunはRanking優先でSoHistoryより新しい値を返す()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                TestDbHelper.InsertRankingData(db, "so1", 20210101, 200, 20, 10, 4);
                TestDbHelper.InsertSoHistoryData(db, "so1", 20200101, 500, 50, 25, 10);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                Assert.IsTrue(history.CheckSoMovieNeedSabun("so1", 20210101, out var ranking));
                Assert.IsNotNull(ranking);
                Assert.AreEqual(200L, ranking.CountPlay);
            }
        }

        [TestMethod]
        public void CheckSoMovieNeedSabunは両方になければnullで成功する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                TestDbHelper.InsertRankingData(db, "sm9", 20210101, 10, 1, 1, 1);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                Assert.IsTrue(history.CheckSoMovieNeedSabun("soNew", 20210101, out var ranking));
                Assert.IsNull(ranking);
            }
        }

        [TestMethod]
        public void Ver1移行で境界以降のSoHistory混入行を清掃する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                // 最新20210101起点→境界は20200102
                TestDbHelper.InsertRankingData(db, "so1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "so1", 20210101, 200, 20, 10, 4);
                // 旧方式の混入行（境界当日以降）と休眠IDの正規行
                TestDbHelper.InsertSoHistoryData(db, "so1", 20210101, 200, 20, 10, 4);
                TestDbHelper.InsertSoHistoryData(db, "so9", 20200101, 111, 11, 6, 3);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());

                // 混入行は消え、休眠IDの行は残る。so1は退避で正規行に戻る
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT 集計日, 再生数 FROM SoHistory WHERE ID='so1';";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200101, Convert.ToInt32(reader["集計日"]));
                        Assert.AreEqual(100L, Convert.ToInt64(reader["再生数"]));
                    }
                }
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE ID='so9';"));
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE 集計日 >= 20200102;"));
            }
        }

        [TestMethod]
        public void CheckSoMovieNeedSabunは基準日より新しいSoHistory行を使わない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                TestDbHelper.InsertRankingData(db, "sm9", 20210101, 10, 1, 1, 1);
                TestDbHelper.InsertSoHistoryData(db, "so1", 20200101, 500, 50, 25, 10);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                // 基準日がSoHistory行より古い再集計では使わない（新着扱い）
                Assert.IsTrue(history.CheckSoMovieNeedSabun("so1", 20191201, out var oldRanking));
                Assert.IsNull(oldRanking);
                // 通常の直近基準日では使う
                Assert.IsTrue(history.CheckSoMovieNeedSabun("so1", 20210101, out var ranking));
                Assert.IsNotNull(ranking);
                Assert.AreEqual(500L, ranking.CountPlay);
            }
        }

        [TestMethod]
        public void CheckSoMovieNeedSabunはSoHistory表なしでも新着扱いで成功する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm9", 20210101, 10, 1, 1, 1);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsFalse(TableExists(db, "SoHistory"));

                // 表なしをDB異常にせず、新着（null）として正常終了する
                Assert.IsTrue(history.CheckSoMovieNeedSabun("soNew", 20210101, out var ranking));
                Assert.IsNull(ranking);
            }
        }

        [TestMethod]
        public void prune用の集計日索引が作成される()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20210101, 100, 10, 5, 2);

                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());
                Assert.IsTrue(history.EnsureMigrated());

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='index' AND name='idx_Ranking_集計日';";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// 日次更新相当：消える行のSoHistory拾い上げ＋古い分の削除が同一トランザクションで確定すること。
        /// updateOfficialRankingDB_Daily自体はネットワーク依存のため、内部処理をリフレクションで直接呼ぶ。
        /// </summary>
        private static void CallRefreshSoHistoryAndPrune(ISQLiteCtrl db)
        {
            using (var cmd = db.Connection.CreateCommand())
            {
                cmd.Transaction = (SqliteTransaction)db.Connection.BeginTransaction();
                try
                {
                    var method = typeof(RankingHistory).GetMethod("RefreshSoHistoryAndPrune", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.IsNotNull(method);
                    var history = new RankingHistory(db);
                    Assert.IsTrue(history.Open());
                    method.Invoke(history, new object[] { cmd });
                    cmd.Transaction.Commit();
                }
                catch
                {
                    try { cmd.Transaction?.Rollback(); } catch { }
                    throw;
                }
                finally
                {
                    cmd.Transaction = null;
                }
            }
        }

        [TestMethod]
        public void 日次更新で消えるso行を拾い当日分はSoHistoryに入れない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateRankingDateTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                // 最新20210101起点→境界は20200102。0101は消え、0102・当日が残る
                TestDbHelper.InsertRankingData(db, "soOld", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "smCut", 20200102, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "soToday", 20210101, 200, 20, 10, 4);
                TestDbHelper.InsertRankingDateData(db, 20200101);
                TestDbHelper.InsertRankingDateData(db, 20200102);
                TestDbHelper.InsertRankingDateData(db, 20210101);
                // 事前の古いSoHistory行は消去行で上書きされること
                TestDbHelper.InsertSoHistoryData(db, "soOld", 20191201, 111, 11, 6, 3);

                CallRefreshSoHistoryAndPrune(db);

                // 消えるsoOld行がSoHistoryに拾われる（上書き）
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT 集計日, 再生数 FROM SoHistory WHERE ID='soOld';";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200101, Convert.ToInt32(reader["集計日"]));
                        Assert.AreEqual(100L, Convert.ToInt64(reader["再生数"]));
                    }
                }
                // 当日分・smはSoHistoryに混ざらない
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE ID='soToday';"));
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM SoHistory WHERE ID='smCut';"));
                // 古い日だけ消える
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE 集計日=20200101;"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE 集計日=20200102;"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE 集計日=20210101;"));
            }
        }

        [TestMethod]
        public void 日次更新でSoHistoryより古い消去行では上書きしない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateRankingDateTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                TestDbHelper.InsertRankingData(db, "so1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingDateData(db, 20200101);
                TestDbHelper.InsertRankingDateData(db, 20210101);
                // 入っている日付より新しい消去行だけ置き換えるため、仮に新しい行が入っていれば残る
                TestDbHelper.InsertSoHistoryData(db, "so1", 20200105, 555, 55, 33, 11);

                CallRefreshSoHistoryAndPrune(db);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT 集計日, 再生数 FROM SoHistory WHERE ID='so1';";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200105, Convert.ToInt32(reader["集計日"]));
                        Assert.AreEqual(555L, Convert.ToInt64(reader["再生数"]));
                    }
                }
                // 消去自体は行われる
                Assert.AreEqual(0L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE 集計日=20200101;"));
            }
        }

        [TestMethod]
        public void 日次更新で複数日の古い分がまとめて消える()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateRankingDateTable(db);
                TestDbHelper.CreateSoHistoryTable(db);
                TestDbHelper.InsertRankingData(db, "smA", 20190101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "smB", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "smC", 20210101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingDateData(db, 20190101);
                TestDbHelper.InsertRankingDateData(db, 20200101);
                TestDbHelper.InsertRankingDateData(db, 20210101);

                CallRefreshSoHistoryAndPrune(db);

                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking;"));
                Assert.AreEqual(1L, ScalarLong(db, "SELECT COUNT(*) FROM Ranking WHERE ID='smC';"));
                // しおりは消さない
                Assert.AreEqual(3L, ScalarLong(db, "SELECT COUNT(*) FROM RankingDate;"));
            }
        }
    }
}
