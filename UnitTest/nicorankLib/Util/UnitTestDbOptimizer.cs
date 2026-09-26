using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.IO;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestDbOptimizer
    {
        // VACUUMとサイズ取得はファイル実体が前提のため、インメモリDBではなく一時ファイルで検証する。
        // DBごとのpruneはパス判別で分岐するため、作業ディレクトリごと一時場所に移して相対パス（DB/ApiXML.db等）で検証する。
        // AssemblyInfoにParallelize指定がなく逐次実行のため、カレント変更は安全。
        private static string CreateTempWorkDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "nicorank_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "DB"));
            return dir;
        }

        private static void DeleteTempWorkDir(string dir)
        {
            try { if (Directory.Exists(dir)) { Directory.Delete(dir, true); } } catch { }
        }

        private static void DeleteTempDbFiles(string dbPath)
        {
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
            {
                try { if (File.Exists(path)) { File.Delete(path); } } catch { }
            }
        }

        private static string CreateTempDbPath()
        {
            return Path.Combine(Path.GetTempPath(), "nicorank_test_" + Guid.NewGuid().ToString("N") + ".db");
        }

        private static ISQLiteCtrl OpenNewDb(string dbPath)
        {
            File.Create(dbPath).Dispose();
            var dbCtrl = new SQLiteCtrl();
            Assert.IsTrue(dbCtrl.Open(dbPath), "一時DBを開けること: " + dbPath);
            return dbCtrl;
        }

        private static long CountRows(ISQLiteCtrl dbCtrl, string table)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM " + table + ";";
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        private static bool TableExists(ISQLiteCtrl dbCtrl, string table)
        {
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='" + table + "';";
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
        }

        // 壁打ち準拠の固定日。cutoffは20250926になる。
        private static readonly DateTime FixedToday = new DateTime(2026, 9, 26);
        private const long FixedCutoff = 20250926;

        [TestMethod]
        public void CutoffOneYearAgo_RollsBackOneYear()
        {
            Assert.AreEqual(20250926L, DbOptimizer.CutoffOneYearAgo(new DateTime(2026, 9, 26)));
            // うるう日は存在しない前年同日になる（AddYearsの仕様。2023-02-28）
            Assert.AreEqual(20230228L, DbOptimizer.CutoffOneYearAgo(new DateTime(2024, 2, 29)));
        }

        [TestMethod]
        public void Optimize_ApiXml_DeletesOldRowsAndDropsIdConvert()
        {
            // ApiXML：IDConvertのDROP＋1年以上未更新行の削除。境界当日・新行は残し、2回目は空振りで成功すること
            string workDir = CreateTempWorkDir();
            string savedDir = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workDir;
                using (var dbCtrl = OpenNewDb(DbOptimizer.ApiXmlDbPath))
                {
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE NicovideoThumb (取得日 INTEGER, ID TEXT, Status INTEGER, XML TEXT, PRIMARY KEY (ID, 取得日));";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "CREATE TABLE IDConvert (ID TEXT, ThreadID TEXT);";
                        cmd.ExecuteNonQuery();
                        // INTEGER格納と文字列バインドの両形で古行を置く（文字列で渡しても列アフィニティで数値比較されることの確認）
                        cmd.CommandText = "INSERT INTO NicovideoThumb (取得日, ID, Status, XML) VALUES (20200101, 'sm1', 1, '<x/>');";
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.AddWithValue("@取得日", "20200101");
                        cmd.CommandText = "INSERT INTO NicovideoThumb (取得日, ID, Status, XML) VALUES (@取得日, 'sm2', 1, '<x/>');";
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();
                        cmd.CommandText = "INSERT INTO NicovideoThumb (取得日, ID, Status, XML) VALUES (20250926, 'sm3', 1, '<x/>');";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO NicovideoThumb (取得日, ID, Status, XML) VALUES (20260901, 'sm4', 1, '<x/>');";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO IDConvert (ID, ThreadID) VALUES ('so1', 't1');";
                        cmd.ExecuteNonQuery();
                    }
                    dbCtrl.Close();
                }

                DbOptimizeResult first = DbOptimizer.Optimize(DbOptimizer.ApiXmlDbPath, FixedToday);

                Assert.IsTrue(first.Success, "成功すること: " + first.ErrorMessage);
                Assert.AreEqual(2L, first.DeletedRows, "古行2件の削除を数えること");
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(DbOptimizer.ApiXmlDbPath));
                    Assert.IsFalse(TableExists(dbCtrl, "IDConvert"), "IDConvertがDROPされること");
                    Assert.AreEqual(2L, CountRows(dbCtrl, "NicovideoThumb"), "境界当日と新行だけ残ること");
                    dbCtrl.Close();
                }

                DbOptimizeResult second = DbOptimizer.Optimize(DbOptimizer.ApiXmlDbPath, FixedToday);

                Assert.IsTrue(second.Success, "2回目（DROP対象なし・削除0件）も成功すること");
                Assert.AreEqual(0L, second.DeletedRows, "削除0件になること");
            }
            finally
            {
                Environment.CurrentDirectory = savedDir;
                DeleteTempWorkDir(workDir);
            }
        }

        [TestMethod]
        public void Optimize_Dailylog_DeletesAllRowsKeepsTable()
        {
            // Dailylog：全行削除＋表は残す（DROP禁止）。再集計で自己回復する前提
            string workDir = CreateTempWorkDir();
            string savedDir = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workDir;
                using (var dbCtrl = OpenNewDb(DbOptimizer.DailylogDbPath))
                {
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE Dailylog (集計日 INTEGER, ID TEXT);";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO Dailylog (集計日, ID) VALUES (20260920, 'sm1');";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO Dailylog (集計日, ID) VALUES (20260921, 'sm2');";
                        cmd.ExecuteNonQuery();
                    }
                    dbCtrl.Close();
                }

                DbOptimizeResult result = DbOptimizer.Optimize(DbOptimizer.DailylogDbPath, FixedToday);

                Assert.IsTrue(result.Success, "成功すること: " + result.ErrorMessage);
                Assert.AreEqual(2L, result.DeletedRows, "全2行の削除を数えること");
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(DbOptimizer.DailylogDbPath));
                    Assert.IsTrue(TableExists(dbCtrl, "Dailylog"), "表は残ること");
                    Assert.AreEqual(0L, CountRows(dbCtrl, "Dailylog"), "行は空になること");
                    dbCtrl.Close();
                }

                DbOptimizeResult second = DbOptimizer.Optimize(DbOptimizer.DailylogDbPath, FixedToday);

                Assert.IsTrue(second.Success, "2回目（削除0件）も成功すること");
                Assert.AreEqual(0L, second.DeletedRows, "削除0件になること");
            }
            finally
            {
                Environment.CurrentDirectory = savedDir;
                DeleteTempWorkDir(workDir);
            }
        }

        [TestMethod]
        public void Optimize_NicoranHistory_PrunesOnlyOldLowWeekly()
        {
            // NicoranHistory：Weeklyの1年以上前・1001位以下だけ削除。境界（1000位・当日）と他種別・設定XMLは残す
            string weekly = EAnalyzeMode.Weekly.ToString();
            string workDir = CreateTempWorkDir();
            string savedDir = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workDir;
                using (var dbCtrl = OpenNewDb(DB.NiCORAN_HISTORY))
                {
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE LastResult (種別 TEXT, 集計日 INTEGER, ID TEXT, 総合ランク INTEGER);";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "CREATE TABLE LastResultInfo (種別 TEXT, 集計日 INTEGER, XML TEXT);";
                        cmd.ExecuteNonQuery();
                        InsertLastResult(cmd, weekly, 20200101, "old-low", 5000);
                        InsertLastResult(cmd, weekly, FixedCutoff, "cutoff-low", 1001);
                        InsertLastResult(cmd, weekly, FixedCutoff, "cutoff-1000", 1000);
                        InsertLastResult(cmd, weekly, 20260901, "new-low", 5000);
                        InsertLastResult(cmd, "SP", 20200101, "sp-old-low", 5000);
                        cmd.CommandText = "INSERT INTO LastResultInfo (種別, XML) VALUES ('" + weekly + "', '<config/>');";
                        cmd.ExecuteNonQuery();
                    }
                    dbCtrl.Close();
                }

                DbOptimizeResult result = DbOptimizer.Optimize(DB.NiCORAN_HISTORY, FixedToday);

                Assert.IsTrue(result.Success, "成功すること: " + result.ErrorMessage);
                Assert.AreEqual(2L, result.DeletedRows, "古下位と境界下位の2件だけ数えること");
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(DB.NiCORAN_HISTORY));
                    Assert.AreEqual(3L, CountRows(dbCtrl, "LastResult"), "1000位・新行・SP行が残ること");
                    Assert.AreEqual(1L, CountRows(dbCtrl, "LastResultInfo"), "設定XMLに触れないこと");
                    dbCtrl.Close();
                }

                DbOptimizeResult second = DbOptimizer.Optimize(DB.NiCORAN_HISTORY, FixedToday);

                Assert.IsTrue(second.Success, "2回目（削除0件）も成功すること");
                Assert.AreEqual(0L, second.DeletedRows, "削除0件になること");
            }
            finally
            {
                Environment.CurrentDirectory = savedDir;
                DeleteTempWorkDir(workDir);
            }
        }

        private static void InsertLastResult(Microsoft.Data.Sqlite.SqliteCommand cmd, string syubetsu, long syuukeiBi, string id, long rank)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@種別", syubetsu);
            cmd.Parameters.AddWithValue("@集計日", syuukeiBi);
            cmd.Parameters.AddWithValue("@ID", id);
            cmd.Parameters.AddWithValue("@総合ランク", rank);
            cmd.CommandText = "INSERT INTO LastResult (種別, 集計日, ID, 総合ランク) VALUES (@種別, @集計日, @ID, @総合ランク);";
            cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
        }

        [TestMethod]
        public void Optimize_LogOfficial_VacuumOnlyKeepsRows()
        {
            // LogOfficial：VACUUMのみで行は残す
            string workDir = CreateTempWorkDir();
            string savedDir = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workDir;
                using (var dbCtrl = OpenNewDb(DB.LOG_OFFICEIAL))
                {
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE Ranking (ID TEXT, 集計日 INTEGER);";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "INSERT INTO Ranking (ID, 集計日) VALUES ('sm1', 20200101);";
                        cmd.ExecuteNonQuery();
                    }
                    dbCtrl.Close();
                }

                DbOptimizeResult result = DbOptimizer.Optimize(DB.LOG_OFFICEIAL, FixedToday);

                Assert.IsTrue(result.Success, "成功すること: " + result.ErrorMessage);
                Assert.AreEqual(0L, result.DeletedRows, "削除0件であること");
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(DB.LOG_OFFICEIAL));
                    Assert.AreEqual(1L, CountRows(dbCtrl, "Ranking"), "行が残ること");
                    dbCtrl.Close();
                }
            }
            finally
            {
                Environment.CurrentDirectory = savedDir;
                DeleteTempWorkDir(workDir);
            }
        }

        [TestMethod]
        public void Optimize_FragmentedDb_SucceedsAndKeepsData()
        {
            // 未知パスはVACUUMのみで成功し、データが壊れないこと
            string dbPath = CreateTempDbPath();
            try
            {
                using (var dbCtrl = OpenNewDb(dbPath))
                {
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE T (ID TEXT PRIMARY KEY, N INTEGER, Body TEXT);";
                        cmd.ExecuteNonQuery();
                        for (int i = 0; i < 500; i++)
                        {
                            cmd.CommandText = "INSERT INTO T (ID, N, Body) VALUES ('id" + i + "', " + i + ", '" + new string('x', 500) + "');";
                            cmd.ExecuteNonQuery();
                        }
                        // 数値列で400件消して100件残す（IDは文字列のため数値比較にしないと桁違いでずれる）
                        cmd.CommandText = "DELETE FROM T WHERE N < 400;";
                        cmd.ExecuteNonQuery();
                    }
                    dbCtrl.Close();
                }
                long sizeBefore = new FileInfo(dbPath).Length;
                Assert.IsTrue(sizeBefore > 0, "最適化前のサイズが取れること");

                DbOptimizeResult result = DbOptimizer.Optimize(dbPath, FixedToday);

                Assert.IsTrue(result.Executed, "実行済みになること");
                Assert.IsTrue(result.Success, "成功すること: " + result.ErrorMessage);
                Assert.AreEqual(sizeBefore, result.SizeBefore, "実行前サイズが一致すること");
                Assert.IsTrue(result.SizeAfter > 0, "実行後サイズが取れること");
                // 前提：SQLiteCtrl.Open は auto_vacuum を設定しないため、DELETE分の断片化はVACUUMまで残る。
                // 将来 auto_vacuum を有効化するとDELETE時点で縮み、このassertは失敗しうる。その場合は前提に合わせて見直すこと。
                Assert.IsTrue(result.SizeAfter < result.SizeBefore, "断片化の解消で縮小すること（500行中400行削除のため確実に縮む）");
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(dbPath), "最適化後も開けること");
                    Assert.AreEqual(100L, CountRows(dbCtrl, "T"), "行数が保たれること");
                    dbCtrl.Close();
                }
            }
            finally
            {
                DeleteTempDbFiles(dbPath);
            }
        }

        [TestMethod]
        public void Optimize_MissingFile_SkippedWithoutFailure()
        {
            // Dailylog.db未作成時など、ファイル不在は異常ではないためスキップ扱いにすること
            string dbPath = CreateTempDbPath();
            DeleteTempDbFiles(dbPath);

            DbOptimizeResult result = DbOptimizer.Optimize(dbPath, FixedToday);

            Assert.IsFalse(result.Executed, "未実行（スキップ）になること");
            Assert.IsTrue(result.Success, "失敗にしないこと");
        }

        [TestMethod]
        public void Optimize_NonDbFile_FailsWithMessage()
        {
            // DBでないファイルは失敗として理由を残すこと（呼び出し側が続行判断できるため）
            string dbPath = CreateTempDbPath();
            try
            {
                File.WriteAllText(dbPath, "this is not a database");
                DbOptimizeResult result = DbOptimizer.Optimize(dbPath, FixedToday);

                Assert.IsTrue(result.Executed, "実行扱いになること");
                Assert.IsFalse(result.Success, "失敗すること");
                Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage), "理由が残ること");
            }
            finally
            {
                DeleteTempDbFiles(dbPath);
            }
        }

        [TestMethod]
        public void FormatFileSize_Units()
        {
            Assert.AreEqual("0 B", DbOptimizer.FormatFileSize(0));
            Assert.AreEqual("1023 B", DbOptimizer.FormatFileSize(1023));
            Assert.AreEqual("1.00 KB", DbOptimizer.FormatFileSize(1024));
            Assert.AreEqual("1.50 KB", DbOptimizer.FormatFileSize(1536));
            Assert.AreEqual("5.00 MB", DbOptimizer.FormatFileSize(5L * 1024 * 1024));
            Assert.AreEqual("2.00 GB", DbOptimizer.FormatFileSize(2L * 1024 * 1024 * 1024));
            Assert.AreEqual("—", DbOptimizer.FormatFileSize(-1), "負値は未取得表示にすること");
        }

        [TestMethod]
        public void GetDefaultTargets_MatchesKnownPaths()
        {
            // パスずれは最適化対象と集計参照先の食い違いになるため、既知値との一致で縛る
            var targets = DbOptimizer.GetDefaultTargets();

            Assert.AreEqual(4, targets.Count, "対象は4DBであること");
            Assert.AreEqual(DB.LOG_OFFICEIAL, targets[0].DbPath);
            Assert.AreEqual(DB.NiCORAN_HISTORY, targets[1].DbPath);
            Assert.AreEqual(@"DB/ApiXML.db", targets[2].DbPath);
            Assert.AreEqual("DB/Dailylog.db", targets[3].DbPath);
        }
    }
}
