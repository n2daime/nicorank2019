using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// Issue #53: 空の file_name_list.json（データなしマーカー）の配置。
    /// nicorank2019 は 404 ではなくこのマーカーだけをメンテナンス日として扱うため、
    /// 「作れること」「既存データを壊さないこと」「BOM を付けないこと」を確認する。
    /// </summary>
    [TestClass]
    public class UnitTestOldRankingMarker
    {
        private string _tempDir;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "nicorank_marker_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [TestMethod]
        public void TryWrite_CreatesEmptyArrayWithoutBom()
        {
            var date = new DateTime(2026, 10, 22);

            bool created = OldRankingMarker.TryWrite(
                _tempDir, "daily", date, out string path, out var result, out string error);

            Assert.IsTrue(created);
            Assert.AreEqual(OldRankingMarkerWriteResult.Created, result);
            Assert.IsNull(error);
            Assert.IsTrue(File.Exists(path));
            // [] の2バイトだけ。BOM が付くと他ツールで中身を確認したときに紛らわしい
            CollectionAssert.AreEqual(new byte[] { 0x5B, 0x5D }, File.ReadAllBytes(path));
        }

        [TestMethod]
        public void TryWrite_DoesNotOverwriteExistingData()
        {
            var date = new DateTime(2026, 10, 22);
            string dir = Path.Combine(_tempDir, "daily", "2026-10-22");
            Directory.CreateDirectory(dir);
            string existing = Path.Combine(dir, "all.json");
            File.WriteAllText(existing, "{\"x\":1}", new UTF8Encoding(false));

            bool created = OldRankingMarker.TryWrite(
                _tempDir, "daily", date, out _, out var result, out string error);

            Assert.IsFalse(created);
            Assert.AreEqual(OldRankingMarkerWriteResult.SkippedExistingData, result);
            Assert.IsNull(error);
            Assert.IsFalse(File.Exists(Path.Combine(dir, OldRankingMarker.FileName)));
            Assert.AreEqual("{\"x\":1}", File.ReadAllText(existing));
        }

        [TestMethod]
        public void TryWrite_SecondCall_DoesNotOverwriteMarker()
        {
            var date = new DateTime(2026, 10, 22);

            Assert.IsTrue(OldRankingMarker.TryWrite(
                _tempDir, "daily", date, out string path, out _, out _));

            // 既にマーカーがある状態で再度失敗しても上書きしない
            Assert.IsFalse(OldRankingMarker.TryWrite(
                _tempDir, "daily", date, out _, out var result, out _));
            Assert.AreEqual(OldRankingMarkerWriteResult.SkippedExistingData, result);
            CollectionAssert.AreEqual(new byte[] { 0x5B, 0x5D }, File.ReadAllBytes(path));
        }
    }
}
