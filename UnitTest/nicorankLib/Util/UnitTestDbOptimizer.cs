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
        // 使い終わった一時ファイルは-wal/-shmごと消す（WALモードのため副ファイルが残りうる）。
        private static string CreateTempDbPath()
        {
            return Path.Combine(Path.GetTempPath(), "nicorank_test_" + Guid.NewGuid().ToString("N") + ".db");
        }

        private static void DeleteTempDbFiles(string dbPath)
        {
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
            {
                try { if (File.Exists(path)) { File.Delete(path); } } catch { }
            }
        }

        private static void CreateFragmentedDb(string dbPath)
        {
            File.Create(dbPath).Dispose();
            using (var dbCtrl = new SQLiteCtrl())
            {
                Assert.IsTrue(dbCtrl.Open(dbPath), "一時DBを開けること");
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
        }

        [TestMethod]
        public void Optimize_FragmentedDb_SucceedsAndKeepsData()
        {
            // 断片化DBでVACUUMが成功し、データが壊れないこと（手動最適化の中核）
            string dbPath = CreateTempDbPath();
            try
            {
                CreateFragmentedDb(dbPath);
                long sizeBefore = new FileInfo(dbPath).Length;
                Assert.IsTrue(sizeBefore > 0, "最適化前のサイズが取れること");

                DbOptimizeResult result = DbOptimizer.Optimize(dbPath);

                Assert.IsTrue(result.Executed, "実行済みになること");
                Assert.IsTrue(result.Success, "成功すること: " + result.ErrorMessage);
                Assert.AreEqual(sizeBefore, result.SizeBefore, "実行前サイズが一致すること");
                Assert.IsTrue(result.SizeAfter > 0, "実行後サイズが取れること");
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(dbPath), "最適化後も開けること");
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM T;";
                        Assert.AreEqual(100L, Convert.ToInt64(cmd.ExecuteScalar()), "行数が保たれること");
                    }
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

            DbOptimizeResult result = DbOptimizer.Optimize(dbPath);

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
                DbOptimizeResult result = DbOptimizer.Optimize(dbPath);

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
