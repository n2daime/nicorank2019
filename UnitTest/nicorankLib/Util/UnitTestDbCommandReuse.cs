using System;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// 同一 SqliteCommand 再利用パターンの回帰テスト（Issue #22）。
    /// System.Data.SQLite では黙認された同名パラメータの重複追加が
    /// Microsoft.Data.Sqlite では例外になるため、Clear 漏れを検出する。
    /// 各テストは本番コードの代表パターン（NicoApi / RankingHistory / TyukanAnalyze / SnapShotSabunReader）を模す。
    /// </summary>
    [TestClass]
    public class UnitTestDbCommandReuse
    {
        [TestMethod]
        public void Clearなしで同名パラメータを追加し続けると例外になる()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO Ranking(ID, 集計日, 再生数, コメント数, マイリスト数, いいね数, 人気のタグ) VALUES(@ID, @集計日, @再生数, @コメント数, @マイリスト数, @いいね数, @人気のタグ)";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@集計日", 20200101);
                    cmd.Parameters.AddWithValue("@再生数", 100);
                    cmd.Parameters.AddWithValue("@コメント数", 10);
                    cmd.Parameters.AddWithValue("@マイリスト数", 5);
                    cmd.Parameters.AddWithValue("@いいね数", 2);
                    cmd.Parameters.AddWithValue("@人気のタグ", "[]");
                    cmd.ExecuteNonQuery();

                    // Clear せずに同名パラメータを追加し直す（旧コードの漏れパターン）。
                    // Microsoft.Data.Sqlite では重複バインドが InvalidOperationException（Must add values...）になる。
                    cmd.Parameters.AddWithValue("@ID", "sm2");
                    cmd.Parameters.AddWithValue("@集計日", 20200102);
                    cmd.Parameters.AddWithValue("@再生数", 200);
                    cmd.Parameters.AddWithValue("@コメント数", 20);
                    cmd.Parameters.AddWithValue("@マイリスト数", 10);
                    cmd.Parameters.AddWithValue("@いいね数", 4);
                    cmd.Parameters.AddWithValue("@人気のタグ", "[]");

                    Assert.ThrowsException<InvalidOperationException>(() => cmd.ExecuteNonQuery());
                }
            }
        }

        [TestMethod]
        public void DELETEループでClear毎回なら複数行を削除できる_NicoApi型()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, "<xml>1</xml>");
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm2", 1, "<xml>2</xml>");

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE From NicovideoThumb WHERE ID = @ID";
                    cmd.Parameters.Clear();
                    string[] ids = { "sm1", "sm2" };
                    foreach (var id in ids)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@ID", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM NicovideoThumb";
                    Assert.AreEqual(0L, cmd.ExecuteScalar());
                }
            }
        }

        [TestMethod]
        public void CommandText差し替え_DELETEからINSERTへ_NicoApi型()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateNicovideoThumbTable(db);
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm1", 1, "<xml>old1</xml>");
                TestDbHelper.InsertNicovideoThumbData(db, 20200101, "sm2", 1, "<xml>old2</xml>");

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE From NicovideoThumb WHERE ID = @ID";
                    cmd.Parameters.Clear();
                    string[] ids = { "sm1", "sm2" };
                    foreach (var id in ids)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@ID", id);
                        cmd.ExecuteNonQuery();
                    }

                    cmd.CommandText = "INSERT INTO NicovideoThumb(取得日,ID,Status,XML) VALUES(@取得日,@ID,@Status,@XML)";
                    cmd.Parameters.Clear();
                    var todayStr = "20200102";
                    foreach (var id in ids)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@取得日", todayStr);
                        cmd.Parameters.AddWithValue("@ID", id);
                        cmd.Parameters.AddWithValue("@Status", 1);
                        cmd.Parameters.AddWithValue("@XML", "<xml>new</xml>");
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM NicovideoThumb";
                    Assert.AreEqual(2L, cmd.ExecuteScalar());

                    cmd.CommandText = "SELECT XML FROM NicovideoThumb WHERE ID = @ID";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    Assert.AreEqual("<xml>new</xml>", cmd.ExecuteScalar().ToString());
                }
            }
        }

        [TestMethod]
        public void SELECT切替_BETWEEN不発から範囲外フォールバック_GetRankingSabun型()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "sm1", 20200103, 300, 30, 15, 6);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 500, 50, 20, 10);

                using (var cmd = db.Connection.CreateCommand())
                {
                    // 1発目: 範囲内にデータなし
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID AND 集計日 BETWEEN @Date2 AND @Date1 ORDER BY 集計日 DESC LIMIT 1";
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@Date2", 20200106);
                    cmd.Parameters.AddWithValue("@Date1", 20200108);
                    bool found;
                    using (var reader = cmd.ExecuteReader())
                    {
                        found = reader.Read();
                    }
                    Assert.IsFalse(found);

                    // 2発目: 同一コマンドを Clear して条件を差し替え（フォールバック）
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID AND 集計日>=@Date ORDER BY 集計日 LIMIT 1";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@Date", 20200106);
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsFalse(reader.Read());
                    }

                    // 3発目: 同一文の再利用でも Clear すれば問題ないことの追加確認（本番の2発を超える拡張）
                    cmd.CommandText = "SELECT * FROM Ranking WHERE ID=@ID AND 集計日>=@Date ORDER BY 集計日 LIMIT 1";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ID", "sm1");
                    cmd.Parameters.AddWithValue("@Date", 20200102);
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        Assert.AreEqual(20200103, Convert.ToInt32(reader["集計日"]));
                    }
                }
            }
        }

        [TestMethod]
        public void PRAGMA判定からALTERしてINSERTする同一トランザクション成功系_calcDailyRank型()
        {
            // 成功系のみ再現。失敗時ロールバック→再実行の連鎖検証は対象外（Issue #22 残課題）。
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE Dailylog(集計日 INTEGER, ID TEXT, タイトル TEXT)";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.Transaction = (SqliteTransaction)db.Connection.BeginTransaction();
                    try
                    {
                        // PRAGMA で列存在確認（いいね数 は未作成のはず。本番は PRAGMA table_info＋ExecuteReaderループ、ここでは等価なテーブル値関数で確認）
                        cmd.CommandText = "SELECT COUNT(*) FROM PRAGMA_TABLE_INFO('Dailylog') WHERE name='いいね数'";
                        bool hasLike = Convert.ToInt64(cmd.ExecuteScalar()) > 0;
                        Assert.IsFalse(hasLike);

                        // ALTER を同一トランザクション内で実行
                        cmd.CommandText = "ALTER TABLE Dailylog ADD COLUMN いいね数 INTEGER DEFAULT 0";
                        cmd.ExecuteNonQuery();

                        // INSERT も同一コマンドを使い回す
                        cmd.CommandText = "INSERT INTO Dailylog(集計日, ID, タイトル, いいね数) VALUES(@集計日, @ID, @タイトル, @いいね数)";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@集計日", 20200101);
                        cmd.Parameters.AddWithValue("@ID", "sm1");
                        cmd.Parameters.AddWithValue("@タイトル", "T1");
                        cmd.Parameters.AddWithValue("@いいね数", 7);
                        cmd.ExecuteNonQuery();

                        cmd.Transaction.Commit();
                    }
                    catch
                    {
                        try { cmd.Transaction?.Rollback(); } catch { }
                        throw;
                    }
                }

                using (var cmd = db.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT いいね数 FROM Dailylog WHERE ID='sm1'";
                    Assert.AreEqual(7L, Convert.ToInt64(cmd.ExecuteScalar()));
                }
            }
        }

        [TestMethod]
        public void 外部コマンドを使い回してExecuteReaderできる_GetMovieData型()
        {
            using (var db = new SQLiteCtrl())
            {
                db.OpenInMemory();
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200101, 100, 10, 5, 2);
                TestDbHelper.InsertRankingData(db, "sm2", 20200101, 200, 20, 10, 4);

                using (var cmd = db.Connection.CreateCommand())
                {
                    // SnapShotSabunReader.GetMovieData(aCmd, id) 相当: 注入された1本を使い回す
                    Assert.AreEqual(100L, GetPlayCountById(cmd, "sm1"));
                    Assert.AreEqual(200L, GetPlayCountById(cmd, "sm2"));
                }
            }
        }

        private static long GetPlayCountById(SqliteCommand cmd, string id)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT 再生数 FROM Ranking WHERE ID=@ID ORDER BY 集計日 DESC LIMIT 1";
            cmd.Parameters.AddWithValue("@ID", id);
            using (var reader = cmd.ExecuteReader())
            {
                Assert.IsTrue(reader.Read());
                return Convert.ToInt64(reader["再生数"]);
            }
        }
    }
}
