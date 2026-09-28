using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// ベースライン配布の自動取得テスト（Issue #36）。
    /// ネットワークと配置先はフェイクに差し替え、純粋処理（TryParseManifest／VerifySha256）は直接検証する。
    /// なぜフェイクか：実サーバーへの取得は単体テストで再現できず、配布場所の有無で結果が変わるためである。
    /// </summary>
    [TestClass]
    public class UnitTestBaselineDownloader
    {
        private const string FakeManifestUrl = "https://example.invalid/nicorank/baseline/baseline.json";

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "nicorank_t036_" + Guid.NewGuid().ToString("N"));
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

        private static string BuildManifestJson(string logFile, long logSize, string logSha, string histFile, long histSize, string histSha)
        {
            return "{\"logOfficial\":{\"file\":\"" + logFile + "\",\"date\":\"20260921\",\"size\":" + logSize
                + ",\"sha256\":\"" + logSha + "\"},\"nicoranHistory\":{\"file\":\"" + histFile
                + "\",\"date\":\"20260921\",\"size\":" + histSize + ",\"sha256\":\"" + histSha + "\"}}";
        }

        [TestMethod]
        public void TryParseManifest_Valid_ReturnsTrue()
        {
            string sha = new string('a', 64);
            string json = BuildManifestJson("LogOfficial_baseline_20260921.zip", 10, sha, "NicoranHistory_baseline_20260921.zip", 20, sha);

            BaselineDownloader.BaselineManifest manifest;
            Assert.IsTrue(BaselineDownloader.TryParseManifest(json, out manifest));
            Assert.AreEqual("LogOfficial_baseline_20260921.zip", manifest.LogOfficial.File);
            Assert.AreEqual(10L, manifest.LogOfficial.Size);
            Assert.AreEqual("NicoranHistory_baseline_20260921.zip", manifest.NicoranHistory.File);
        }

        [TestMethod]
        public void TryParseManifest_BrokenJson_ReturnsFalse()
        {
            BaselineDownloader.BaselineManifest manifest;
            Assert.IsFalse(BaselineDownloader.TryParseManifest("{not json", out manifest));
            Assert.IsNull(manifest);
        }

        [TestMethod]
        public void TryParseManifest_MissingEntry_ReturnsFalse()
        {
            string sha = new string('a', 64);
            string json = "{\"logOfficial\":{\"file\":\"a.zip\",\"date\":\"20260921\",\"size\":10,\"sha256\":\"" + sha + "\"}}";

            BaselineDownloader.BaselineManifest manifest;
            Assert.IsFalse(BaselineDownloader.TryParseManifest(json, out manifest));
        }

        [TestMethod]
        public void TryParseManifest_BadSha_ReturnsFalse()
        {
            string json = BuildManifestJson("a.zip", 10, "xyz", "b.zip", 20, new string('a', 64));

            BaselineDownloader.BaselineManifest manifest;
            Assert.IsFalse(BaselineDownloader.TryParseManifest(json, out manifest));
        }

        [TestMethod]
        public void VerifySha256_MatchAndMismatch()
        {
            string dir = CreateTempDir();
            try
            {
                string path = Path.Combine(dir, "sample.bin");
                File.WriteAllText(path, "baseline-test");
                string sha = Sha256OfFile(path);

                Assert.IsTrue(BaselineDownloader.VerifySha256(path, sha));
                Assert.IsTrue(BaselineDownloader.VerifySha256(path, sha.ToUpperInvariant()));
                Assert.IsFalse(BaselineDownloader.VerifySha256(path, new string('0', 64)));
                Assert.IsFalse(BaselineDownloader.VerifySha256(Path.Combine(dir, "missing.bin"), sha));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void EnsureBaseline_BothPresent_NoDownload()
        {
            string dir = CreateTempDir();
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "DB"));
                File.WriteAllText(Path.Combine(dir, "DB", "LogOfficial.db"), "dummy");
                File.WriteAllText(Path.Combine(dir, "DB", "NicoranHistory.db"), "dummy");

                var downloader = new BaselineDownloader(
                    dir,
                    (string url, out string text) => { throw new InvalidOperationException("取得してはならない"); },
                    (string url, string localPath) => { throw new InvalidOperationException("取得してはならない"); },
                    FakeManifestUrl);

                Assert.IsTrue(downloader.EnsureBaseline());
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void EnsureBaseline_Missing_DownloadAndPlace()
        {
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string logZip = Path.Combine(work, "LogOfficial_baseline_20260921.zip");
                string histZip = Path.Combine(work, "NicoranHistory_baseline_20260921.zip");
                CreateDbZip(logZip, "LogOfficial.db", "log-content");
                CreateDbZip(histZip, "NicoranHistory.db", "hist-content");
                string manifestJson = BuildManifestJson(
                    "LogOfficial_baseline_20260921.zip", new FileInfo(logZip).Length, Sha256OfFile(logZip),
                    "NicoranHistory_baseline_20260921.zip", new FileInfo(histZip).Length, Sha256OfFile(histZip));

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

                Assert.IsTrue(downloader.EnsureBaseline());
                Assert.IsTrue(File.Exists(Path.Combine(dir, "DB", "LogOfficial.db")));
                Assert.IsTrue(File.Exists(Path.Combine(dir, "DB", "NicoranHistory.db")));
                Assert.AreEqual("log-content", File.ReadAllText(Path.Combine(dir, "DB", "LogOfficial.db")));
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void EnsureBaseline_HashMismatch_ReturnsFalse()
        {
            string dir = CreateTempDir();
            string work = CreateTempDir();
            try
            {
                string logZip = Path.Combine(work, "LogOfficial_baseline_20260921.zip");
                string histZip = Path.Combine(work, "NicoranHistory_baseline_20260921.zip");
                CreateDbZip(logZip, "LogOfficial.db", "log-content");
                CreateDbZip(histZip, "NicoranHistory.db", "hist-content");
                // 誤ったハッシュを載せる（取り違え検出の確認）
                string manifestJson = BuildManifestJson(
                    "LogOfficial_baseline_20260921.zip", new FileInfo(logZip).Length, new string('0', 64),
                    "NicoranHistory_baseline_20260921.zip", new FileInfo(histZip).Length, Sha256OfFile(histZip));

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

                Assert.IsFalse(downloader.EnsureBaseline());
                Assert.IsFalse(File.Exists(Path.Combine(dir, "DB", "LogOfficial.db")));
            }
            finally
            {
                DeleteTempDir(dir);
                DeleteTempDir(work);
            }
        }

        [TestMethod]
        public void EnsureCacheFiles_CreatesBoth()
        {
            string dir = CreateTempDir();
            try
            {
                var downloader = new BaselineDownloader(dir, null, null, FakeManifestUrl);

                Assert.IsTrue(downloader.EnsureCacheFiles());
                string apiPath = Path.Combine(dir, "DB", "ApiXML.db");
                string dailyPath = Path.Combine(dir, "DB", "Dailylog.db");
                Assert.IsTrue(File.Exists(apiPath));
                Assert.IsTrue(File.Exists(dailyPath));

                // 表が使えること（ApiXML は取得の前提、Dailylog は中間集計の前提）
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(apiPath));
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM NicovideoThumb;";
                        Assert.AreEqual(0L, Convert.ToInt64(cmd.ExecuteScalar()));
                    }
                    dbCtrl.Close();
                }
                using (var dbCtrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(dbCtrl.Open(dailyPath));
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM Dailylog;";
                        Assert.AreEqual(0L, Convert.ToInt64(cmd.ExecuteScalar()));
                    }
                    dbCtrl.Close();
                }

                // 二重実行でも壊れないこと（冪等）
                Assert.IsTrue(downloader.EnsureCacheFiles());
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Config_BaselineManifestUrl_WithoutElement_FallsBackToDefault()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString(
                    "<nicorank><RANK Num=\"20\" Tyouki=\"1\"/><SYSTEM></SYSTEM></nicorank>");
                Assert.AreEqual(Config.DefaultBaselineManifestUrl, Config.GetInstance().BaselineManifestUrl);
            }
            finally
            {
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void Config_BaselineManifestUrl_WithElement_UsesCustom()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString(
                    "<nicorank><RANK Num=\"20\" Tyouki=\"1\"/>"
                    + "<SYSTEM><URL_BASELINE Url=\"https://example.invalid/custom/baseline.json\"/></SYSTEM></nicorank>");
                Assert.AreEqual("https://example.invalid/custom/baseline.json", Config.GetInstance().BaselineManifestUrl);
            }
            finally
            {
                TestConfigBuilder.ResetInstance();
            }
        }
    }
}
