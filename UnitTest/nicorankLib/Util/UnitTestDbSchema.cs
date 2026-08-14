using System;
using System.Data.SQLite;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestDbSchema
    {
        [TestMethod]
        public void CREATE_TABLEでRankingテーブルを作成できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateSnapshotRankingTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='Ranking'";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO Ranking(ID, 再生数, コメント数, マイリスト数, いいね数) VALUES('sm1',100,10,5,2)";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Ranking";
                    Assert.AreEqual(1L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void CREATE_TABLEでDBVersionテーブルを作成できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateDBVersionTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "INSERT INTO DBVersion(集計日, Ver) VALUES(20200101, '1.0.0')";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT Ver FROM DBVersion";
                    Assert.AreEqual("1.0.0", cmd.ExecuteScalar().ToString());
                }
            }
        }

        [TestMethod]
        public void PRAGMA_table_infoでカラム情報を取得できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateLastResultTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "PRAGMA table_info('LastResult');";
                    using (var reader = cmd.ExecuteReader())
                    {
                        bool found = false;
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("いいね数"))
                            {
                                found = true;
                                break;
                            }
                        }
                        Assert.IsTrue(found);
                    }
                }
            }
        }

        [TestMethod]
        public void PRAGMAでカラム有無を確認できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateLastResultTable(db);

                bool likeFieldExists = false;
                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "PRAGMA table_info('LastResult');";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("いいね数"))
                            {
                                likeFieldExists = true;
                                break;
                            }
                        }
                    }
                }
                Assert.IsTrue(likeFieldExists);

                bool nonExistentFieldExists = false;
                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "PRAGMA table_info('LastResult');";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("存在しないカラム"))
                            {
                                nonExistentFieldExists = true;
                                break;
                            }
                        }
                    }
                }
                Assert.IsFalse(nonExistentFieldExists);
            }
        }

        [TestMethod]
        public void ALTER_TABLE_ADD_COLUMNでカラムを追加できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "CREATE TABLE TestAlter (ID TEXT PRIMARY KEY, 名前 TEXT)";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "ALTER TABLE TestAlter ADD 備考 TEXT DEFAULT ''";
                    cmd.ExecuteNonQuery();
                }

                bool columnExists = false;
                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "PRAGMA table_info('TestAlter');";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString().Equals("備考"))
                            {
                                columnExists = true;
                                break;
                            }
                        }
                    }
                }
                Assert.IsTrue(columnExists);
            }
        }

        [TestMethod]
        public void 複数のALTER_TABLEを連続実行できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "CREATE TABLE TestCols (ID TEXT PRIMARY KEY)";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText =
                        "ALTER TABLE TestCols ADD Col1 INTEGER DEFAULT 0; " +
                        "ALTER TABLE TestCols ADD Col2 INTEGER DEFAULT 0;" +
                        "ALTER TABLE TestCols ADD Col3 INTEGER DEFAULT 0;";
                    cmd.ExecuteNonQuery();
                }

                int columnCount = 0;
                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "PRAGMA table_info('TestCols');";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) columnCount++;
                    }
                }
                Assert.AreEqual(4, columnCount);
            }
        }

        [TestMethod]
        public void sqlite_masterでテーブル存在確認できる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingDateTable(db);

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM sqlite_master WHERE TYPE='table' AND name='RankingDate'";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                    }
                }

                using (var cmd = new SQLiteCommand(db.Connection))
                {
                    cmd.CommandText = "SELECT * FROM sqlite_master WHERE TYPE='table' AND name='NonExistentTable'";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsFalse(reader.Read());
                    }
                }
            }
        }

        [TestMethod]
        public void ATTACH_DETACHで別DBにアクセスできる()
        {
            var attachFile = Path.GetTempFileName();
            try
            {
                SQLiteConnection.CreateFile(attachFile);
                using (var mainDb = new SQLiteCtrl())
                using (var attachDb = new SQLiteCtrl())
                {
                    mainDb.OpenInMemory();
                    attachDb.Open(attachFile);

                    using (var cmd = new SQLiteCommand(attachDb.Connection))
                    {
                        cmd.CommandText = "CREATE TABLE AttachedTbl (ID TEXT PRIMARY KEY)";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO AttachedTbl VALUES('attached_data')";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SQLiteCommand(mainDb.Connection))
                    {
                        cmd.CommandText = $"ATTACH DATABASE '{attachFile.Replace("'", "''")}' AS Extra";
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SQLiteCommand(mainDb.Connection))
                    {
                        cmd.CommandText = "SELECT ID FROM Extra.AttachedTbl";
                        Assert.AreEqual("attached_data", cmd.ExecuteScalar().ToString());
                    }

                    using (var cmd = new SQLiteCommand(mainDb.Connection))
                    {
                        cmd.CommandText = "DETACH DATABASE Extra";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                File.Delete(attachFile);
            }
        }
    }
}
