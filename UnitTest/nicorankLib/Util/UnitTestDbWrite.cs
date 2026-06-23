using System;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestDbWrite
    {
        [TestMethod]
        public void 単一行をINSERTできる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES(@ID, @集計日, @再生数, @コメント数, @マイリスト数, @いいね数, @人気のタグ)";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@集計日", 20200101);
                    cmd.Parameters.AddWithValue("@再生数", 100);
                    cmd.Parameters.AddWithValue("@コメント数", 10);
                    cmd.Parameters.AddWithValue("@マイリスト数", 5);
                    cmd.Parameters.AddWithValue("@いいね数", 2);
                    cmd.Parameters.AddWithValue("@人気のタグ", "[]");
                    var count = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, count);
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void WHERE_NOT_EXISTSで重複INSERTを防止できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateSnapshotRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = @"INSERT INTO Ranking(ID, 再生数, コメント数, マイリスト数, いいね数)
                                        SELECT @ID, @再生数, @コメント数, @マイリスト数, @いいね数
                                        WHERE NOT EXISTS (SELECT * FROM Ranking WHERE ID=@ID)";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@再生数", 100);
                    cmd.Parameters.AddWithValue("@コメント数", 10);
                    cmd.Parameters.AddWithValue("@マイリスト数", 5);
                    cmd.Parameters.AddWithValue("@いいね数", 2);
                    Assert.AreEqual(1, cmd.ExecuteNonQuery());

                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@再生数", 200);
                    cmd.Parameters.AddWithValue("@コメント数", 20);
                    cmd.Parameters.AddWithValue("@マイリスト数", 10);
                    cmd.Parameters.AddWithValue("@いいね数", 4);
                    Assert.AreEqual(0, cmd.ExecuteNonQuery());
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT 再生数 FROM Ranking WHERE ID='sm1'";
                    Assert.AreEqual(100L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void ループINSERTで複数行を追加できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES(@ID, @集計日, @再生数, @コメント数, @マイリスト数, @いいね数, @人気のタグ)";
                    for (int i = 1; i <= 100; i++)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@ID", $"sm{i}");
                        cmd.Parameters.AddWithValue("@集計日", 20200101);
                        cmd.Parameters.AddWithValue("@再生数", i * 10);
                        cmd.Parameters.AddWithValue("@コメント数", i);
                        cmd.Parameters.AddWithValue("@マイリスト数", i / 2);
                        cmd.Parameters.AddWithValue("@いいね数", i / 5);
                        cmd.Parameters.AddWithValue("@人気のタグ", "[]");
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(100L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void WHERE条件でDELETEできる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, "<xml/>");
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm2", 1, "<xml/>");

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "DELETE FROM NicovideoThumb WHERE ID = @ID";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    Assert.AreEqual(1, cmd.ExecuteNonQuery());
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM NicovideoThumb";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void 条件に合致する行だけDELETEされる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateLastResultTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO LastResult(種別, 集計日, ID, タイトル, 総合ランク, ポイント, 再生数, コメント数, マイリスト数, 累計再生数, 累計コメント数, 累計マイリスト数, JSON) VALUES('Weekly',20200101,'sm1','T1',1,100,10,5,2,10,5,2,'{}')";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "INSERT INTO LastResult(種別, 集計日, ID, タイトル, 総合ランク, ポイント, 再生数, コメント数, マイリスト数, 累計再生数, 累計コメント数, 累計マイリスト数, JSON) VALUES('Weekly',20200108,'sm2','T2',2,80,8,4,1,8,4,1,'{}')";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "DELETE FROM LastResult WHERE 集計日=@日付 AND 種別=@種別";
                    cmd.Parameters.AddWithValue("@日付", 20200101);
                    cmd.Parameters.AddWithValue("@種別", "Weekly");
                    Assert.AreEqual(1, cmd.ExecuteNonQuery());
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LastResult";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void トランザクションCommitで変更が確定する()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.Transaction = db.Connection.BeginTransaction();
                    cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES('sm1',20200101,100,10,5,2,'[]')";
                    cmd.ExecuteNonQuery();
                    cmd.Transaction.Commit();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void トランザクションRollbackで変更が破棄される()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.Transaction = db.Connection.BeginTransaction();
                    cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES('sm1',20200101,100,10,5,2,'[]')";
                    cmd.ExecuteNonQuery();
                    cmd.Transaction.Rollback();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void 例外発生時にRollbackされる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var transaction = db.Connection.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SQLiteCommand(db.Connection))
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES('sm1',20200101,100,10,5,2,'[]')";
                            cmd.ExecuteNonQuery();
                            throw new InvalidOperationException("test");
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void AddWithValueとClearでパラメータを再利用できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES(@ID, @集計日, @再生数, @コメント数, @マイリスト数, @いいね数, @人気のタグ)";

                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@集計日", 20200101);
                    cmd.Parameters.AddWithValue("@再生数", 100);
                    cmd.Parameters.AddWithValue("@コメント数", 10);
                    cmd.Parameters.AddWithValue("@マイリスト数", 5);
                    cmd.Parameters.AddWithValue("@いいね数", 2);
                    cmd.Parameters.AddWithValue("@人気のタグ", "[]");
                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ID", "sm2");
                    cmd.Parameters.AddWithValue("@集計日", 20200102);
                    cmd.Parameters.AddWithValue("@再生数", 200);
                    cmd.Parameters.AddWithValue("@コメント数", 20);
                    cmd.Parameters.AddWithValue("@マイリスト数", 10);
                    cmd.Parameters.AddWithValue("@いいね数", 4);
                    cmd.Parameters.AddWithValue("@人気のタグ", "[]");
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(2L, cmd.ExecuteScalar());
                }
            }
        }
    }
}
