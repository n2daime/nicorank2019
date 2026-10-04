using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.IO;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// アプリ自動更新の確認テスト（Issue #47）。
    /// ネットワーク・時計・TEMP配置はフェイクに差し替え、純粋処理（TryParseManifest／TryNormalizeVersion／IsNewerVersion）は直接検証する。
    /// なぜフェイクか：実サーバーへの取得は単体テストで再現できず、配布場所の有無で結果が変わるためである（#36 と同一の考え方）。
    /// </summary>
    [TestClass]
    public class UnitTestAppUpdateChecker
    {
        private const string FakeManifestUrl = "https://example.invalid/nicorank/update/version.json";
        private const string FakeSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static readonly DateTimeOffset FakeNow = new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.FromHours(9));

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "nicorank_t047_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteTempDir(string dir)
        {
            try { if (Directory.Exists(dir)) { Directory.Delete(dir, true); } } catch { }
        }

        private static string BuildManifestJson(string version, int schema = 1, string sha256 = FakeSha256, string url = "https://example.invalid/nicorank2019.zip", long size = 15790339)
        {
            return "{\"schema\":" + schema + ",\"apps\":{\"nicorank2019\":{\"tag\":\"v20261003_nicorank\""
                + ",\"version\":\"" + version + "\""
                + ",\"url\":\"" + url + "\""
                + ",\"sha256\":\"" + sha256 + "\""
                + ",\"size\":" + size
                + ",\"notes\":\"https://example.invalid/notes\"}}}";
        }

        private static AppUpdateChecker.TextFetch OkFetch(string json)
        {
            return (string url, out string text) => { text = json; return true; };
        }

        private static AppUpdateChecker.TextFetch FailFetch()
        {
            return (string url, out string text) => { text = null; return false; };
        }

        private static AppUpdateChecker.TextFetch ForbiddenFetch()
        {
            return (string url, out string text) => { throw new InvalidOperationException("取得してはならない"); };
        }

        [TestMethod]
        public void TryParseManifest_Valid_ReturnsEntry()
        {
            AppUpdateChecker.UpdateManifest manifest;
            bool ok = AppUpdateChecker.TryParseManifest(BuildManifestJson("2026.10.03"), out manifest);
            Assert.IsTrue(ok);
            Assert.IsNotNull(manifest);
            Assert.AreEqual(1, manifest.Schema);
            Assert.AreEqual("2026.10.03", manifest.Apps["nicorank2019"].Version);
        }

        [TestMethod]
        public void TryParseManifest_BrokenJson_ReturnsFalse()
        {
            AppUpdateChecker.UpdateManifest manifest;
            bool ok = AppUpdateChecker.TryParseManifest("{broken", out manifest);
            Assert.IsFalse(ok);
            Assert.IsNull(manifest);
        }

        [TestMethod]
        public void TryParseManifest_MissingApp_ReturnsFalse()
        {
            AppUpdateChecker.UpdateManifest manifest;
            bool ok = AppUpdateChecker.TryParseManifest("{\"schema\":1,\"apps\":{}}", out manifest);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void TryParseManifest_BadSha_ReturnsFalse()
        {
            AppUpdateChecker.UpdateManifest manifest;
            bool ok = AppUpdateChecker.TryParseManifest(BuildManifestJson("2026.10.03", 1, "not-hex"), out manifest);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void TryParseManifest_RelativeUrl_ReturnsFalse()
        {
            // url は完全 URL でなければならず、組み立て前提の相対指定は拒否する（Issue 合意）。
            AppUpdateChecker.UpdateManifest manifest;
            bool ok = AppUpdateChecker.TryParseManifest(BuildManifestJson("2026.10.03", 1, FakeSha256, "nicorank2019.zip"), out manifest);
            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void TryNormalizeVersion_ReleaseFormat_NormalizesToFourParts()
        {
            // version.json の 3成分と Assembly の4成分を同じ物差しにする（欠落は 0 扱い）。
            Version version;
            bool ok = AppUpdateChecker.TryNormalizeVersion("2026.10.03", out version);
            Assert.IsTrue(ok);
            Assert.AreEqual(new Version(2026, 10, 3, 0), version);
        }

        [TestMethod]
        public void TryNormalizeVersion_Invalid_ReturnsFalse()
        {
            Version version;
            Assert.IsFalse(AppUpdateChecker.TryNormalizeVersion("v20261003", out version));
            Assert.IsFalse(AppUpdateChecker.TryNormalizeVersion("", out version));
            Assert.IsFalse(AppUpdateChecker.TryNormalizeVersion("1.2.3.4.5", out version));
            Assert.IsFalse(AppUpdateChecker.TryNormalizeVersion("1..2", out version));
        }

        [TestMethod]
        public void IsNewerVersion_Newer_ReturnsTrue()
        {
            Assert.IsTrue(AppUpdateChecker.IsNewerVersion("2026.10.03", new Version(2026, 10, 2, 0)));
        }

        [TestMethod]
        public void IsNewerVersion_Same_ReturnsFalse()
        {
            // 「2026.10.03」と Assembly「2026.10.3.0」は等価であり、新版扱いにしない。
            Assert.IsFalse(AppUpdateChecker.IsNewerVersion("2026.10.03", new Version(2026, 10, 3, 0)));
        }

        [TestMethod]
        public void IsNewerVersion_OlderOrNull_ReturnsFalse()
        {
            Assert.IsFalse(AppUpdateChecker.IsNewerVersion("2026.10.02", new Version(2026, 10, 3, 0)));
            Assert.IsFalse(AppUpdateChecker.IsNewerVersion("2026.10.03", null));
            Assert.IsFalse(AppUpdateChecker.IsNewerVersion("not-a-version", new Version(2026, 10, 3, 0)));
        }

        [TestMethod]
        public void Check_Throttled_WhenCheckedToday()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, ForbiddenFetch(), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                checker.WriteState(new AppUpdateChecker.UpdateCheckState() { LastCheck = "2026-10-04", LastNotified = "2026.10.02" });
                AppUpdateChecker.UpdateCheckResult result = checker.Check();
                Assert.AreEqual(AppUpdateChecker.UpdateCheckStatus.Throttled, result.Status);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Check_UpToDate_WhenSameVersion()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, OkFetch(BuildManifestJson("2026.10.03")), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 3, 0));
                AppUpdateChecker.UpdateCheckResult result = checker.Check();
                Assert.AreEqual(AppUpdateChecker.UpdateCheckStatus.UpToDate, result.Status);
                Assert.AreEqual("2026-10-04", checker.ReadState().LastCheck);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Check_Available_WhenNewer()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, OkFetch(BuildManifestJson("2026.10.03")), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                AppUpdateChecker.UpdateCheckResult result = checker.Check();
                Assert.AreEqual(AppUpdateChecker.UpdateCheckStatus.Available, result.Status);
                Assert.IsNotNull(result.Entry);
                Assert.AreEqual("2026.10.03", result.Entry.Version);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Check_Unknown_WhenFetchFails()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, FailFetch(), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                AppUpdateChecker.UpdateCheckResult result = checker.Check();
                Assert.AreEqual(AppUpdateChecker.UpdateCheckStatus.Unknown, result.Status);
                // 確認不能時は記録を更新しない（次回も確認する）。
                Assert.IsNull(checker.ReadState().LastCheck);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Check_Unknown_WhenSchemaTooNew()
        {
            // 未知の形式なら更新せず旧版のまま動かす（Issue 合意）。更新要否の判定には使わない。
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, OkFetch(BuildManifestJson("2026.10.03", 2)), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                AppUpdateChecker.UpdateCheckResult result = checker.Check();
                Assert.AreEqual(AppUpdateChecker.UpdateCheckStatus.Unknown, result.Status);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Check_Force_IgnoresThrottle()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, OkFetch(BuildManifestJson("2026.10.03")), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                checker.WriteState(new AppUpdateChecker.UpdateCheckState() { LastCheck = "2026-10-04" });
                AppUpdateChecker.UpdateCheckResult result = checker.Check(true);
                Assert.AreEqual(AppUpdateChecker.UpdateCheckStatus.Available, result.Status);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void MarkNotified_RecordsVersion()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, ForbiddenFetch(), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                checker.MarkNotified("2026.10.03");
                AppUpdateChecker.UpdateCheckState state = checker.ReadState();
                Assert.AreEqual("2026.10.03", state.LastNotified);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void WriteTaskFile_Valid_WritesFile()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, ForbiddenFetch(), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                AppUpdateChecker.UpdateManifest manifest;
                Assert.IsTrue(AppUpdateChecker.TryParseManifest(BuildManifestJson("2026.10.03"), out manifest));
                string exePath = Path.Combine(dir, "nicorank2019.exe");
                File.WriteAllText(exePath, "dummy");
                bool ok = checker.WriteTaskFile(manifest.Apps["nicorank2019"], dir, exePath, 1234);
                Assert.IsTrue(ok);
                Assert.IsTrue(File.Exists(checker.TaskPath));
                string task = File.ReadAllText(checker.TaskPath);
                Assert.IsTrue(task.Contains("https://example.invalid/nicorank2019.zip"));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void WriteTaskFile_NullEntry_ReturnsFalse()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, ForbiddenFetch(), FakeManifestUrl, () => FakeNow, new Version(2026, 10, 2, 0));
                Assert.IsFalse(checker.WriteTaskFile(null, dir, Path.Combine(dir, "nicorank2019.exe"), 1234));
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void TryParseManifest_FileUrl_ReturnsTrue()
        {
            // NAS・GitHub 配置前のローカル検証のため file スキームも許す。
            AppUpdateChecker.UpdateManifest manifest;
            bool ok = AppUpdateChecker.TryParseManifest(BuildManifestJson("2026.10.03", 1, FakeSha256, "file:///C:/test/nicorank2019.zip"), out manifest);
            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void FetchText_FileUrl_ReturnsContent()
        {
            string dir = CreateTempDir();
            try
            {
                string path = Path.Combine(dir, "version.json");
                string json = BuildManifestJson("2026.10.03");
                File.WriteAllText(path, json);
                string text;
                bool ok = AppUpdateChecker.FetchText(new Uri(path).AbsoluteUri, out text);
                Assert.IsTrue(ok);
                Assert.AreEqual(json, text);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void FetchText_MissingFile_ReturnsFalse()
        {
            string dir = CreateTempDir();
            try
            {
                string text = "dummy";
                bool ok = AppUpdateChecker.FetchText(new Uri(Path.Combine(dir, "none.json")).AbsoluteUri, out text);
                Assert.IsFalse(ok);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void ManifestUrl_Override_ReturnsOverride()
        {
            string dir = CreateTempDir();
            try
            {
                var checker = new AppUpdateChecker(dir, ForbiddenFetch(), FakeManifestUrl, () => FakeNow);
                Assert.AreEqual(FakeManifestUrl, checker.ManifestUrl);
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void AppUpdateManifestUrl_WithoutElement_FallsBackToDefault()
        {
            // SYSTEM/URL_APPUPDATE がない既存 nicorank.xml でも既定URLで動く（URL_BASELINE と同型の任意要素方式）。
            try
            {
                TestConfigBuilder.LoadFromXmlString("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                    + "<nicorank><RANK Num=\"20\" Tyouki=\"true\" /><RANKED Num=\"200\" />"
                    + "<UserInfo Num=\"1000\" /><ICONDL_PATH>c:\\tmp</ICONDL_PATH>"
                    + "<POINT><CALC_MYLIST>40</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>"
                    + "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>10</CALC_LIKE></POINT>"
                    + "<COMMENT_OFFSET Mode=\"2\" UnderLimit=\"0.01\" /><MYLIST_OFFSET Mode=\"1\" />"
                    + "<PLAY_OFFSET Mode=\"2\" /><POINTALL_OFFSET Mode=\"0\" />"
                    + "<SYSTEM><ResultCsv Code=\"0\" /><Thread Max=\"6\" />"
                    + "<Download><NicoAPI Retry=\"20\" /><UserIcon Retry=\"1\" /></Download>"
                    + "<URL_JSON_TARGET Url=\"https://example.invalid/{0}/{1}/\" />"
                    + "</SYSTEM></nicorank>");
                Assert.AreEqual(Config.DefaultAppUpdateManifestUrl, Config.GetInstance().AppUpdateManifestUrl);
            }
            finally
            {
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void AppUpdateManifestUrl_WithElement_UsesElement()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                    + "<nicorank><RANK Num=\"20\" Tyouki=\"true\" /><RANKED Num=\"200\" />"
                    + "<UserInfo Num=\"1000\" /><ICONDL_PATH>c:\\tmp</ICONDL_PATH>"
                    + "<POINT><CALC_MYLIST>40</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>"
                    + "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>10</CALC_LIKE></POINT>"
                    + "<COMMENT_OFFSET Mode=\"2\" UnderLimit=\"0.01\" /><MYLIST_OFFSET Mode=\"1\" />"
                    + "<PLAY_OFFSET Mode=\"2\" /><POINTALL_OFFSET Mode=\"0\" />"
                    + "<SYSTEM><ResultCsv Code=\"0\" /><Thread Max=\"6\" />"
                    + "<Download><NicoAPI Retry=\"20\" /><UserIcon Retry=\"1\" /></Download>"
                    + "<URL_JSON_TARGET Url=\"https://example.invalid/{0}/{1}/\" />"
                    + "<URL_APPUPDATE Url=\"https://example.invalid/update/version.json\" />"
                    + "</SYSTEM></nicorank>");
                Assert.AreEqual("https://example.invalid/update/version.json", Config.GetInstance().AppUpdateManifestUrl);
            }
            finally
            {
                TestConfigBuilder.ResetInstance();
            }
        }
    }
}
