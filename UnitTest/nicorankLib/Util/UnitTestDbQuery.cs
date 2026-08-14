using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestDbQuery
    {
        [TestMethod]
        public void PRIMARY_KEYで単一行を取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID = @ID LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual("sm1", reader["ID"].ToString());
                        Assert.AreEqual(100L, Convert.ToInt64(reader["再生数"]));
                        Assert.AreEqual(10L, Convert.ToInt64(reader["コメント数"]));
                        Assert.AreEqual(5L, Convert.ToInt64(reader["マイリスト数"]));
                        Assert.AreEqual(2L, Convert.ToInt64(reader["いいね数"]));
                    }
                }
            }
        }

        [TestMethod]
        public void 存在しないIDは空()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID = @ID LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm999");
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsFalse(reader.Read());
                    }
                }
            }
        }

        [TestMethod]
        public void 過去方向の最新データを取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200103, 300, 30, 15, 6);
                TestDbHelper.InsertRankingData(db, "sm1", 20200102, 200, 20, 10, 4);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID AND 集計日<=@Date ORDER BY 集計日 DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@Date", 20200102);
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200102, Convert.ToInt32(reader["集計日"]));
                        Assert.AreEqual(200L, Convert.ToInt64(reader["再生数"]));
                    }
                }
            }
        }

        [TestMethod]
        public void 未来方向の最古データを取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "sm1", 20200102, 200, 20, 10, 4);
                TestDbHelper.InsertRankingData(db, "sm1", 20200103, 300, 30, 15, 6);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID AND 集計日>=@Date ORDER BY 集計日 LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@Date", 20200102);
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200102, Convert.ToInt32(reader["集計日"]));
                    }
                }
            }
        }

        [TestMethod]
        public void BETWEENで範囲内の最新データを取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "sm1", 20200103, 300, 30, 15, 6);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 500, 50, 20, 10);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID AND 集計日 BETWEEN @Date2 AND @Date1 ORDER BY 集計日 DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@Date2", 20200102);
                    cmd.Parameters.AddWithValue("@Date1", 20200104);
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200103, Convert.ToInt32(reader["集計日"]));
                    }
                }
            }
        }

        [TestMethod]
        public void MAXで最新取得日を取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, "<thumb><status>ok</status></thumb>");
                TestDbHelper.InsertNicovideoThumbData(db, 20200103, "sm1", 1, "<thumb><status>ok</status></thumb>");

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT MAX(取得日) as 取得日 FROM NicovideoThumb WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200103, Convert.ToInt32(reader["取得日"]));
                    }
                }
            }
        }

        [TestMethod]
        public void COUNTで件数を取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateHistoryTable(db);
                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO History(集計日, ID, 総合ランク, ポイント, 再生数, コメント数, マイリスト数) VALUES(20200101,'sm1',1,100,10,5,2)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT INTO History(集計日, ID, 総合ランク, ポイント, 再生数, コメント数, マイリスト数) VALUES(20200108,'sm1',2,80,8,4,1)";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) as 件数 FROM History WHERE ID = @ID AND 集計日 < @集計日";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@集計日", 20200108);
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(1L, Convert.ToInt64(reader["件数"]));
                    }
                }
            }
        }

        [TestMethod]
        public void IFNULLでデフォルト値を取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingDateTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT IFNULL(Max(集計日), 20190610) as 集計日 FROM RankingDate";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20190610, Convert.ToInt32(reader["集計日"]));
                    }
                }
            }
        }

        [TestMethod]
        public void JOINで複数テーブルを結合できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.CreateMovieTable(db);

                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertMovieData(db, "sm1", 20191201000000, "TestMovie");
                TestDbHelper.InsertMovieData(db, "sm2", 20191202000000, "OtherMovie");

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = @"
                        SELECT Movie.ID as ID, Movie.タイトル, Movie.投稿日 FROM Movie
                        JOIN (SELECT ID FROM Ranking WHERE ID='sm1' GROUP BY ID) AS IDLIST
                        ON Movie.ID = IDLIST.ID";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual("sm1", reader["ID"].ToString());
                        Assert.AreEqual("TestMovie", reader["タイトル"].ToString());
                        Assert.IsFalse(reader.Read());
                    }
                }
            }
        }

        [TestMethod]
        public void ORDER_BY_DESC_LIMIT_1で最新1行を取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "sm1", 20200102, 200, 20, 10, 4);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID ORDER BY 集計日 DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200102, Convert.ToInt32(reader["集計日"]));
                        Assert.IsFalse(reader.Read());
                    }
                }
            }
        }
    }
}
