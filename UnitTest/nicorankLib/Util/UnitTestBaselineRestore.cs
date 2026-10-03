using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// ベースラインDBによる復旧テスト（Issue #45）。
    /// 陳腐化判定と退避は純粋に検証し、配置全体はフェイクの取得で検証する。
    /// なぜフェイクか：実サーバーへの取得は単体テストで再現できず、配布場所の有無で結果が変わるためである（#36 と同一）。
    /// </summary>
    [TestClass]
    public class UnitTestBaselineRestore
    {
        private const string FakeManifestUrl = "https://example.invalid/nicorank/baseline/baseline.json";

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "nicorank_t045_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteTempDir(string dir)
        {
            try { if (Directory.Exists(dir)) { Directory.Delete(dir, true); } } catch { }
        }

        private static string Sha256OfFile(string path)
        {
            using (var sha = SHA256.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private static void CreateDbZipFromFile(string zipPath, string entryName, string srcPath)
        {
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(entryName);
                using (var dst = entry.Open())
                using (var src = File.OpenRead(srcPath))
                {
                    src.CopyTo(dst);
                }
            }
        }

        private static string CreateHistContentDb(string dir, int syuukeiBi)
        {
            // 配布内容の実DBを作る。SQLiteCtrl.Open は存在しないファイルを開かないため空作成してから開く。
            string path = Path.Combine(dir, "content_NicoranHistory.db");
            File.Create(path).Dispose();
            using (var ctrl = new SQLiteCtrl())
            {
                Assert.IsTrue(ctrl.Open(path));
                TestDbHelper.CreateLastResultTable(ctrl);
                TestDbHelper.InsertLastResultData(ctrl, "Weekly", syuukeiBi, "sm1", 1, 100, "{}");
                ctrl.Close();
            }
            return path;
        }

        private static string BuildManifestJson(string logDate, string logFile, long logSize, string logSha,
            string histDate, string histFile, long histSize, string histSha)
        {
            return "{\"logOfficial\":{\"file\":\"" + logFile + "\",\"date\":\"" + logDate + "\",\"size\":" + logSize
                + ",\"sha256\":\"" + logSha + "\"},\"nicoranHistory\":{\"file\":\"" + histFile
                + "\",\"date\":\"" + histDate + "\",\"size\":" + histSize + ",\"sha256\":\"" + histSha + "\"}}";
        }

        private static BaselineDownloader.BaselineManifest BuildManifest(string logDate, string histDate)
        {
            string sha = new string('a', 64);
            string json = BuildManifestJson(logDate, "LogOfficial.zip", 10, sha, histDate, "NicoranHistory.zip", 20, sha);
            BaselineDownloader.BaselineManifest manifest;
            Assert.IsTrue(BaselineDownloader.TryParseManifest(json, out manifest));
            return manifest;
        }

        [TestMethod]
        public void TryParseDateToLong_Valid_ReturnsValue()
        {
            Assert.AreEqual(20260921L, BaselineDownloader.TryParseDateToLong("20260921"));
            Assert.IsNull(BaselineDownloader.TryParseDateToLong("2026-09-21"));
            Assert.IsNull(BaselineDownloader.TryParseDateToLong(null));
            Assert.IsNull(BaselineDownloader.TryParseDateToLong(""));
        }

        [TestMethod]
        public void IsBaselineStale_FreshManifest_ReturnsFalse()
        {
            // 配布日が本地と同じか新しい場合は復旧を許す。
            var manifest = BuildManifest("20260928", "20260928");

            Assert.IsFalse(BaselineDownloader.IsBaselineStale(manifest, 20260928L));
            Assert.IsFalse(BaselineDownloader.IsBaselineStale(manifest, 20260921L));
        }

        [TestMethod]
        public void IsBaselineStale_OlderHist_BlocksRestore()
        {
            // NicoranHistory の配布が本地より古い場合は、被せると抜けが増えるため中断する（必須仕様）。
            var manifest = BuildManifest("20260928", "20260921");

            Assert.IsTrue(BaselineDownloader.IsBaselineStale(manifest, 20260928L));
        }

        [TestMethod]
        public void IsBaselineStale_NoLocalData_AllowsRestore()
        {
            // 本地に何もなければ比較不能であり、配置を妨げない。
            var manifest = BuildManifest("20260921", "20260921");

            Assert.IsFalse(BaselineDownloader.IsBaselineStale(manifest, null));
        }

        [TestMethod]
        public void IsBaselineStale_NullManifest_BlocksRestore()
        {
            // 不明な物を被せないため、安全側に倒して中断する。
            Assert.IsTrue(BaselineDownloader.IsBaselineStale(null, 20260921L));
        }

        [TestMethod]
        public void IsBaselineStale_BadDate_BlocksRestore()
        {
            // 配布日が読めない場合も安全側に倒して中断する。
            var manifest = BuildManifest("20260928", "2026-09-28");

            Assert.IsTrue(BaselineDownloader.IsBaselineStale(manifest, 20260921L));
        }

        [TestMethod]
        public void IsBaselineStale_NullEntry_BlocksRestore()
        {
            var manifest = new BaselineDownloader.BaselineManifest { NicoranHistory = null };

            Assert.IsTrue(BaselineDownloader.IsBaselineStale(manifest, 20260921L));
        }

        [TestMethod]
        public void TryGetMaxWeeklyDate_WithRows_ReturnsMax()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(db);
                TestDbHelper.InsertLastResultData(db, "Weekly", 20260921, "sm1", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(db, "Weekly", 20260928, "sm2", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(db, "SP", 20261005, "sm9", 1, 100, "{}");

                // SP の日付が混ざっても Weekly の最大だけ返す。
                long? max;
                Assert.IsTrue(BaselineDownloader.TryGetMaxWeeklyDate(db, out max));
                Assert.AreEqual(20260928L, max);
            }
        }

        [TestMethod]
        public void TryGetMaxWeeklyDate_EmptyTable_ReturnsTrueWithNull()
        {
            // 行なしは正常系であり、失敗ではない（null の最新日と区別する）。
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(db);

                long? max = 0;
                Assert.IsTrue(BaselineDownloader.TryGetMaxWeeklyDate(db, out max));
                Assert.IsNull(max);
            }
        }

        [TestMethod]
        public void TryGetMaxWeeklyDate_MissingTable_ReturnsFalse()
        {
            // 表なし（破損・移行前）は読取失敗であり、データ無しと区別する。
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                long? max;
                Assert.IsFalse(BaselineDownloader.TryGetMaxWeeklyDate(db, out max));
            }
        }

        [TestMethod]
        public void BackupExistingFile_Missing_ReturnsNull()
        {
            string dir = CreateTempDir();
            try
            {
                Assert.IsNull(BaselineDownloader.BackupExistingFile(Path.Combine(dir, "DB", "LogOfficial.db")));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void BackupExistingFile_Present_CopiedUnderBackup()
        {
            string dir = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string path = Path.Combine(dbDir, "LogOfficial.db");
                File.WriteAllText(path, "original");

                string backed = BaselineDownloader.BackupExistingFile(path);

                Assert.IsNotNull(backed);
                Assert.IsTrue(File.Exists(backed));
                Assert.AreEqual("original", File.ReadAllText(backed));
                // 元ファイルは残す（退避であり移動ではない）。
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void RestoreBaseline_StaleManifest_BlockedWithoutOverwrite()
        {
            // 本地より古い配布では、既存DBに触れず中断する。
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string histPath = Path.Combine(dbDir, "NicoranHistory.db");
                // SQLiteCtrl.Open は存在しないファイルを開かないため、空ファイルを作ってから開く（SnapShotDB.InitilizeDB と同一の作り方）。
                File.Create(histPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260928, "sm1", 1, 100, "{}");
                    ctrl.Close();
                }
                string sha = new string('a', 64);
                string manifestJson = BuildManifestJson("20260921", "LogOfficial.zip", 10, sha,
                    "20260921", "NicoranHistory.zip", 20, sha);

                var downloader = new BaselineDownloader(
                    dir,
                    (string url, out string text) => { text = manifestJson; return true; },
                    (string url, string localPath) => { throw new InvalidOperationException("古い配布では取得してはならない"); },
                    FakeManifestUrl);

                var result = downloader.RestoreBaseline();

                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.StaleBlocked);
                Assert.AreEqual("配布が古い", result.StaleReason);
                Assert.AreEqual(0, result.BackedUpPaths.Count);
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void RestoreBaseline_FreshManifest_OverwritesHistOnly()
        {
            // 配布が本地と同じか新しい場合は、NicoranHistory だけ退避してから置き換える。
            // LogOfficial には触れない（日次更新で自己回復するため対象外）。
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string histZip = Path.Combine(work, "NicoranHistory.zip");
                string contentDb = CreateHistContentDb(work, 20260928);
                CreateDbZipFromFile(histZip, "NicoranHistory.db", contentDb);
                // LogOfficial の zip がなくても復旧は進む（対象外のため取得しない）。
                string manifestJson = BuildManifestJson("20260921", "LogOfficial.zip", 10, new string('a', 64),
                    "20260928", "NicoranHistory.zip", new FileInfo(histZip).Length, Sha256OfFile(histZip));
                string histPath = Path.Combine(dbDir, "NicoranHistory.db");
                // SQLiteCtrl.Open は存在しないファイルを開かないため、空ファイルを作ってから開く（SnapShotDB.InitilizeDB と同一の作り方）。
                File.Create(histPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260921, "sm1", 1, 100, "{}");
                    ctrl.Close();
                }
                string logPath = Path.Combine(dbDir, "LogOfficial.db");
                File.WriteAllText(logPath, "old-log");

                var downloader = new BaselineDownloader(
                    dir,
                    (string url, out string text) => { text = manifestJson; return true; },
                    (string url, string localPath) =>
                    {
                        string name = url.Substring(url.LastIndexOf('/') + 1);
                        File.Copy(Path.Combine(work, name), localPath);
                        return true;
                    },
                    FakeManifestUrl);

                var result = downloader.RestoreBaseline();

                Assert.IsTrue(result.Success);
                Assert.IsFalse(result.StaleBlocked);
                Assert.AreEqual(1, result.BackedUpPaths.Count);
                // 配置されたのは配布内容（20260928 の週あり）である。
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    long? max;
                    Assert.IsTrue(BaselineDownloader.TryGetMaxWeeklyDate(ctrl, out max));
                    Assert.AreEqual(20260928L, max);
                    ctrl.Close();
                }
                // LogOfficial は対象外のため置き換わらない。
                Assert.AreEqual("old-log", File.ReadAllText(logPath));
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void BackupExistingFile_WithWal_CopiedTogether()
        {
            // WAL モードでは本体だけ戻しても付随ファイルとの不整合で開けなくなる場合があるため、一緒に運ぶ。
            string dir = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string path = Path.Combine(dbDir, "NicoranHistory.db");
                File.WriteAllText(path, "original");
                File.WriteAllText(path + "-wal", "wal-content");

                string backed = BaselineDownloader.BackupExistingFile(path);

                Assert.IsNotNull(backed);
                Assert.AreEqual("original", File.ReadAllText(backed));
                Assert.IsTrue(File.Exists(backed + "-wal"));
                Assert.AreEqual("wal-content", File.ReadAllText(backed + "-wal"));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void RestoreBaseline_ContentOlderThanLocal_Blocked()
        {
            // manifest の日付は新しくても内容が古い場合は中断する。
            // なぜ内容で見るか：manifest の date は梱包日であり、休止後の梱包では内容だけ古くなるためである。
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string histPath = Path.Combine(dbDir, "NicoranHistory.db");
                File.Create(histPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260928, "sm1", 1, 100, "{}");
                    ctrl.Close();
                }
                string histZip = Path.Combine(work, "NicoranHistory.zip");
                CreateDbZipFromFile(histZip, "NicoranHistory.db", CreateHistContentDb(work, 20260921));
                // 梱包日だけ新しく、内容は本地より古い配布。
                string manifestJson = BuildManifestJson("20260928", "LogOfficial.zip", 10, new string('a', 64),
                    "20260928", "NicoranHistory.zip", new FileInfo(histZip).Length, Sha256OfFile(histZip));

                var downloader = new BaselineDownloader(
                    dir,
                    (string url, out string text) => { text = manifestJson; return true; },
                    (string url, string localPath) =>
                    {
                        string name = url.Substring(url.LastIndexOf('/') + 1);
                        File.Copy(Path.Combine(work, name), localPath);
                        return true;
                    },
                    FakeManifestUrl);

                var result = downloader.RestoreBaseline();

                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.StaleBlocked);
                Assert.AreEqual("配布内容が古い", result.StaleReason);
                Assert.AreEqual(0, result.BackedUpPaths.Count);
                // 本地は置き換わらない。
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    long? max;
                    Assert.IsTrue(BaselineDownloader.TryGetMaxWeeklyDate(ctrl, out max));
                    Assert.AreEqual(20260928L, max);
                    ctrl.Close();
                }
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void RestoreBaseline_ContentUnreadable_Blocked()
        {
            // 配布内容が壊れていて最新日を読めない場合も中断する（不明な物を被せないため）。
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string histPath = Path.Combine(dbDir, "NicoranHistory.db");
                File.Create(histPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260921, "sm1", 1, 100, "{}");
                    ctrl.Close();
                }
                string histZip = Path.Combine(work, "NicoranHistory.zip");
                string broken = Path.Combine(work, "broken.db");
                File.WriteAllText(broken, "not-a-database");
                CreateDbZipFromFile(histZip, "NicoranHistory.db", broken);
                string manifestJson = BuildManifestJson("20260928", "LogOfficial.zip", 10, new string('a', 64),
                    "20260928", "NicoranHistory.zip", new FileInfo(histZip).Length, Sha256OfFile(histZip));

                var downloader = new BaselineDownloader(
                    dir,
                    (string url, out string text) => { text = manifestJson; return true; },
                    (string url, string localPath) =>
                    {
                        string name = url.Substring(url.LastIndexOf('/') + 1);
                        File.Copy(Path.Combine(work, name), localPath);
                        return true;
                    },
                    FakeManifestUrl);

                var result = downloader.RestoreBaseline();

                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.StaleBlocked);
                Assert.AreEqual("配布内容を確認不能", result.StaleReason);
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void RestoreBaseline_ContentEmpty_Blocked()
        {
            // 配布内容に Weekly の行が1つもない場合は、梱包日が新しくても中断する。
            // なぜ中断か：空DBで本地の実績を置き換えると「復旧完了」と誤表示され、誤りに気づけないためである。
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string histPath = Path.Combine(dbDir, "NicoranHistory.db");
                File.Create(histPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260928, "sm1", 1, 100, "{}");
                    ctrl.Close();
                }
                string emptyDb = Path.Combine(work, "empty.db");
                File.Create(emptyDb).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(emptyDb));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    ctrl.Close();
                }
                string histZip = Path.Combine(work, "NicoranHistory.zip");
                CreateDbZipFromFile(histZip, "NicoranHistory.db", emptyDb);
                string manifestJson = BuildManifestJson("20260928", "LogOfficial.zip", 10, new string('a', 64),
                    "20260928", "NicoranHistory.zip", new FileInfo(histZip).Length, Sha256OfFile(histZip));

                var downloader = new BaselineDownloader(
                    dir,
                    (string url, out string text) => { text = manifestJson; return true; },
                    (string url, string localPath) =>
                    {
                        string name = url.Substring(url.LastIndexOf('/') + 1);
                        File.Copy(Path.Combine(work, name), localPath);
                        return true;
                    },
                    FakeManifestUrl);

                var result = downloader.RestoreBaseline();

                Assert.IsFalse(result.Success);
                Assert.IsTrue(result.StaleBlocked);
                Assert.AreEqual("配布内容にデータなし", result.StaleReason);
                Assert.AreEqual(0, result.BackedUpPaths.Count);
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }
    }
}
