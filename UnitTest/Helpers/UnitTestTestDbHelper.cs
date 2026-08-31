using System;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.Helpers
{
    [TestClass]
    public class UnitTestTestDbHelper
    {
        [TestMethod]
        public void CreateInMemoryDb_インメモリDBを作成できる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                Assert.IsTrue(db.IsOpen);
                Assert.IsNotNull(db.Connection);
            }
        }

        [TestMethod]
        public void CreateRankingTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking WHERE ID='sm1'";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void CreateMovieTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateMovieTable(db);
                TestDbHelper.InsertMovieData(db, "sm1", 20191201000000, "TestMovie");

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT タイトル FROM Movie WHERE ID='sm1'";
                    Assert.AreEqual("TestMovie", cmd.ExecuteScalar().ToString());
                }
            }
        }

        [TestMethod]
        public void CreateRankingDateTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingDateTable(db);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO RankingDate(集計日) VALUES(20200101)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "SELECT 集計日 FROM RankingDate";
                    Assert.AreEqual(20200101, Convert.ToInt32(cmd.ExecuteScalar()));
                }
            }
        }

        [TestMethod]
        public void CreateNicovideoThumbTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, "<thumb><status>ok</status></thumb>");

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT Status FROM NicovideoThumb WHERE ID='sm1'";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void CreateHistoryTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateHistoryTable(db);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO History(集計日, ID, 総合ランク, ポイント, 再生数, コメント数, マイリスト数) VALUES(20200101,'sm1',1,100,10,5,2)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "SELECT COUNT(*) FROM History";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void CreateLastResultTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(db);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO LastResult(種別, ID, ポイント) VALUES('genre','sm1',100)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResult";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void CreateDBVersionTable_作成後にINSERTとSELECTができる()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateDBVersionTable(db);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO DBVersion(集計日, Ver) VALUES(20200101, '1.0.0')";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "SELECT Ver FROM DBVersion";
                    Assert.AreEqual("1.0.0", cmd.ExecuteScalar().ToString());
                }
            }
        }
    }
}
