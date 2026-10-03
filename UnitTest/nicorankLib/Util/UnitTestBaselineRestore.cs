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

        private static void CreateDbZip(string zipPath, string entryName, string content)
        {
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(entryName);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(content);
                }
            }
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

            Assert.IsFalse(BaselineDownloader.IsBaselineStale(manifest, 20260928L, 20260928L));
            Assert.IsFalse(BaselineDownloader.IsBaselineStale(manifest, 20260921L, 20260921L));
        }

        [TestMethod]
        public void IsBaselineStale_OlderHist_BlocksRestore()
        {
            // NicoranHistory の配布が本地より古い場合は、被せると抜けが増えるため中断する（必須仕様）。
            var manifest = BuildManifest("20260928", "20260921");

            Assert.IsTrue(BaselineDownloader.IsBaselineStale(manifest, 20260928L, 20260928L));
        }

        [TestMethod]
        public void IsBaselineStale_OlderOfficial_BlocksRestore()
        {
            var manifest = BuildManifest("20260921", "20260928");

            Assert.IsTrue(BaselineDownloader.IsBaselineStale(manifest, 20260921L, 20260928L));
        }

        [TestMethod]
        public void IsBaselineStale_NoLocalData_AllowsRestore()
        {
            // 本地に何もなければ比較不能であり、配置を妨げない。
            var manifest = BuildManifest("20260921", "20260921");

            Assert.IsFalse(BaselineDownloader.IsBaselineStale(manifest, null, null));
        }

        [TestMethod]
        public void IsBaselineStale_NullManifest_BlocksRestore()
        {
            // 不明な物を被せないため、安全側に倒して中断する。
            Assert.IsTrue(BaselineDownloader.IsBaselineStale(null, 20260921L, 20260921L));
        }

        [TestMethod]
        public void GetMaxWeeklyDate_OnlyWeeklyMax()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(db);
                TestDbHelper.InsertLastResultData(db, "Weekly", 20260921, "sm1", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(db, "Weekly", 20260928, "sm2", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(db, "SP", 20261005, "sm9", 1, 100, "{}");

                // SP の日付が混ざっても Weekly の最大だけ返す。
                Assert.AreEqual(20260928L, BaselineDownloader.GetMaxWeeklyDate(db));
            }
        }

        [TestMethod]
        public void GetMaxOfficialDate_ReturnsMax()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingDateTable(db);
                TestDbHelper.InsertRankingDateData(db, 20260921, 0);
                TestDbHelper.InsertRankingDateData(db, 20260928, 1);

                Assert.AreEqual(20260928L, BaselineDownloader.GetMaxOfficialDate(db));
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
                string logPath = Path.Combine(dbDir, "LogOfficial.db");
                // 空ファイルを作ってから開く（SQLiteCtrl.Open は存在しないファイルを開かないため）。
                File.Create(logPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(logPath));
                    TestDbHelper.CreateRankingDateTable(ctrl);
                    TestDbHelper.InsertRankingDateData(ctrl, 20260928, 0);
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
                Assert.AreEqual(0, result.BackedUpPaths.Count);
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void RestoreBaseline_FreshManifest_OverwritesWithBackup()
        {
            // 配布が本地と同じか新しい場合は、退避してから置き換える。
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string dbDir = Path.Combine(dir, "DB");
                Directory.CreateDirectory(dbDir);
                string logZip = Path.Combine(work, "LogOfficial.zip");
                string histZip = Path.Combine(work, "NicoranHistory.zip");
                CreateDbZip(logZip, "LogOfficial.db", "new-log");
                CreateDbZip(histZip, "NicoranHistory.db", "new-hist");
                string manifestJson = BuildManifestJson("20260928", "LogOfficial.zip", new FileInfo(logZip).Length, Sha256OfFile(logZip),
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
                File.WriteAllText(Path.Combine(dbDir, "LogOfficial.db"), "old-log");

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
                Assert.AreEqual(2, result.BackedUpPaths.Count);
                Assert.AreEqual("new-hist", File.ReadAllText(histPath));
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }
    }
}
