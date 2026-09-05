using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Official;
using nicorankLib.output;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestDbMigrationCoordinator
    {
        private class FakeMigratable : IDbMigratable
        {
            public string TargetDb { get; set; } = "Fake.db";
            public bool Result { get; set; } = true;
            public int CallCount { get; private set; }

            public bool EnsureMigrated()
            {
                CallCount++;
                return Result;
            }
        }

        [TestMethod]
        public void 空リストならtrueを返す()
        {
            var coordinator = new DbMigrationCoordinator(new List<IDbMigratable>());
            Assert.IsTrue(coordinator.EnsureAllAtAnalyzeStart());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void nullなら例外になる()
        {
            new DbMigrationCoordinator(null);
        }

        [TestMethod]
        public void 全て成功ならtrueで全て1回呼ばれる()
        {
            var first = new FakeMigratable { TargetDb = "First.db" };
            var second = new FakeMigratable { TargetDb = "Second.db" };
            var coordinator = new DbMigrationCoordinator(new List<IDbMigratable> { first, second });

            Assert.IsTrue(coordinator.EnsureAllAtAnalyzeStart());
            Assert.AreEqual(1, first.CallCount);
            Assert.AreEqual(1, second.CallCount);
        }

        [TestMethod]
        public void 失敗があればfalseで後続は呼ばれない()
        {
            var first = new FakeMigratable { TargetDb = "First.db", Result = false };
            var second = new FakeMigratable { TargetDb = "Second.db" };
            var coordinator = new DbMigrationCoordinator(new List<IDbMigratable> { first, second });

            Assert.IsFalse(coordinator.EnsureAllAtAnalyzeStart());
            Assert.AreEqual(1, first.CallCount);
            Assert.AreEqual(0, second.CallCount);
        }

        [TestMethod]
        public void RankingHistoryは未Openならfalseを返す()
        {
            var history = new RankingHistory();
            Assert.IsFalse(history.EnsureMigrated());
        }

        [TestMethod]
        public void RankingHistoryはRankingテーブルなしならfalseを返し何も変更しない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                Assert.IsFalse(history.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='DBVersion';";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void RankingHistoryはDBVersionをVer0で作成し冪等である()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                Assert.IsTrue(history.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Ver FROM DBVersion LIMIT 1;";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }

                // 2回目は冪等
                Assert.IsTrue(history.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM DBVersion;";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void RankingHistoryは記録Verが新しくても何もせずtrueを返す()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE DBVersion (Ver INTEGER); INSERT INTO DBVersion (Ver) VALUES (99);";
                    cmd.ExecuteNonQuery();
                }
                var history = new RankingHistory(db);
                Assert.IsTrue(history.Open());

                Assert.IsTrue(history.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Ver FROM DBVersion LIMIT 1;";
                    Assert.AreEqual(99L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void ResultHistoryはテーブルなしならfalseを返し何も変更しない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                var resultHistory = new ResultHistory(EAnalyzeMode.Weekly, db);
                Assert.IsFalse(resultHistory.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='DBVersion';";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void ResultHistoryは記録Verが新しくても何もせずtrueを返す()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(db);
                TestDbHelper.CreateHistoryTable(db);
                TestDbHelper.CreateLastResultInfoTable(db);
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE DBVersion (Ver INTEGER); INSERT INTO DBVersion (Ver) VALUES (99);";
                    cmd.ExecuteNonQuery();
                }

                var resultHistory = new ResultHistory(EAnalyzeMode.Weekly, db);
                Assert.IsTrue(resultHistory.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Ver FROM DBVersion LIMIT 1;";
                    Assert.AreEqual(99L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void ResultHistoryは旧スキーマを移行しJSONを空文字化する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTableWithoutLikeColumns(db);
                TestDbHelper.CreateHistoryTableWithoutLikeColumn(db);
                TestDbHelper.CreateLastResultInfoTable(db);
                TestDbHelper.InsertLastResultData(db, "Weekly", 20200101, "sm1", 3, 300, "{\"ID\":\"sm1\"}");
                TestDbHelper.InsertLastResultData(db, "SP", 20200101, "smSP", 1, 100, "{\"ID\":\"smSP\"}");
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO LastResultInfo(種別, 集計日, XML) VALUES('Weekly', 20200101, '<xml/>');";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT INTO LastResultInfo(種別, 集計日, XML) VALUES('SP', 20200101, '<xml/>');";
                    cmd.ExecuteNonQuery();
                }

                var resultHistory = new ResultHistory(EAnalyzeMode.Weekly, db);
                Assert.IsTrue(resultHistory.EnsureMigrated());

                // いいね列が追加されている
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info('LastResult');";
                    bool found = false;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("いいね数"))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    Assert.IsTrue(found);
                }

                // JSON列が削除されている
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info('LastResult');";
                    bool found = false;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("JSON"))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    Assert.IsFalse(found);
                }

                // 旧SPデータが両テーブルから削除され、Weekly行は残っている
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResult WHERE 種別='SP';";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResult WHERE 種別='Weekly';";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResultInfo WHERE 種別='SP';";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResultInfo WHERE 種別='Weekly';";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }

                // 読み側のSQL（LastRankReaderと同一形）が動く
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT 総合ランク,ポイント FROM LastResult WHERE 種別=@種別 and 集計日=@集計日 and ID=@ID;";
                    cmd.Parameters.AddWithValue("@種別", "Weekly");
                    cmd.Parameters.AddWithValue("@集計日", 20200101);
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(3L, Convert.ToInt64(reader["総合ランク"]));
                        Assert.AreEqual(300L, Convert.ToInt64(reader["ポイント"]));
                    }
                }

                // DBVersionがVer=0で記録されている
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Ver FROM DBVersion LIMIT 1;";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }

                // 2回目は冪等（行数維持）
                Assert.IsTrue(resultHistory.EnsureMigrated());
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResult;";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM DBVersion;";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }
        [TestMethod]
        public void ResultHistoryはJSON列なしの最古スキーマでも移行できる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTableWithoutJsonColumn(db);
                TestDbHelper.CreateHistoryTableWithoutLikeColumn(db);
                TestDbHelper.CreateLastResultInfoTable(db);
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO LastResult(種別, 集計日, ID) VALUES('Weekly', 20200101, 'sm1');";
                    cmd.ExecuteNonQuery();
                }

                // JSON列なしはDROPスキップで成功する
                var resultHistory = new ResultHistory(EAnalyzeMode.Weekly, db);
                Assert.IsTrue(resultHistory.EnsureMigrated());

                // いいね列は追加されている
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info('LastResult');";
                    bool found = false;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("いいね数"))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    Assert.IsTrue(found);
                }

                // バージョン記録あり
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Ver FROM DBVersion LIMIT 1;";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }

                // 既存行は無変更
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResult;";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }
    }
}
