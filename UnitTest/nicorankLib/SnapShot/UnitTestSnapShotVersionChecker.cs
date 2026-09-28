using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;
using System;

namespace UnitTest.nicorankLib.SnapShot
{
    /// <summary>
    /// SnapShotVersionChecker のテスト（Issue #38）。
    /// 実通信は行わず、ダウンローダと時計にフェイクを注入して判定だけを検証する。
    /// </summary>
    [TestClass]
    public class UnitTestSnapShotVersionChecker
    {
        // 固定の「現在（JST）」。2026-09-20 09:00+09:00（9時タスク想定）
        private static readonly DateTimeOffset NowJst = new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9));

        private static SnapShotVersionChecker.DownloadTextDelegate OkDownloader(string json)
        {
            return (string url, out string text) => { text = json; return true; };
        }

        private static SnapShotVersionChecker CreateChecker(string json, DateTimeOffset now)
        {
            return new SnapShotVersionChecker(OkDownloader(json), () => now);
        }

        [TestMethod]
        public void Check_SameDayAsToday_ReturnsUpdated()
        {
            var checker = CreateChecker("{\"last_modified\":\"2026-09-20T07:08:34+09:00\"}", NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Updated, result.Status);
            // 生値は last_modified の値そのもの（レスポンス本文全体ではない）
            Assert.AreEqual("2026-09-20T07:08:34+09:00", result.LastModifiedRaw);
            Assert.IsTrue(result.LastModified.HasValue);
        }

        [TestMethod]
        public void Check_PreviousDay_ReturnsNotUpdated()
        {
            var checker = CreateChecker("{\"last_modified\":\"2026-09-19T08:34:12+09:00\"}", NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.NotUpdated, result.Status);
        }

        [TestMethod]
        public void Check_MonthBoundary_ReturnsNotUpdated()
        {
            // 月またぎでも日付比較が正しいこと（8/31→9/1）
            var now = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(9));
            var checker = CreateChecker("{\"last_modified\":\"2026-08-31T23:50:00+09:00\"}", now);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.NotUpdated, result.Status);
        }

        [TestMethod]
        public void Check_DifferentMachineTimezone_ComparesInJst()
        {
            // 実行環境がJSTでなくてもJST換算で判定すること。
            // 2026-09-19T16:00-04:00 は JSTで2026-09-20T05:00 と同 instant のため更新済みになる
            var now = new DateTimeOffset(2026, 9, 19, 16, 0, 0, TimeSpan.FromHours(-4));
            var checker = CreateChecker("{\"last_modified\":\"2026-09-20T05:00:00+09:00\"}", now);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Updated, result.Status);
        }

        [TestMethod]
        public void Check_DownloadFailure_ReturnsUnknown()
        {
            var checker = new SnapShotVersionChecker(
                (string url, out string text) => { text = null; return false; },
                () => NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Unknown, result.Status);
            Assert.IsNull(result.LastModifiedRaw);
            Assert.IsFalse(result.LastModified.HasValue);
        }

        [TestMethod]
        public void Check_DownloaderThrows_ReturnsUnknown()
        {
            var checker = new SnapShotVersionChecker(
                (string url, out string text) => { throw new InvalidOperationException("network down"); },
                () => NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Unknown, result.Status);
        }

        [TestMethod]
        public void Check_BrokenJson_ReturnsUnknown()
        {
            var checker = CreateChecker("{not json", NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Unknown, result.Status);
        }

        [TestMethod]
        public void Check_MissingLastModified_ReturnsUnknown()
        {
            var checker = CreateChecker("{\"foo\":\"bar\"}", NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Unknown, result.Status);
        }

        [TestMethod]
        public void Check_UnparsableDate_ReturnsUnknown()
        {
            var checker = CreateChecker("{\"last_modified\":\"not-a-date\"}", NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Unknown, result.Status);
        }

        [TestMethod]
        public void Check_NonJstOffset_KeepsOffsetAndComparesInJst()
        {
            // Z（UTC）表記の last_modified でもオフセットを保持してJST換算で判定すること。
            // 2026-09-19T22:08:34Z は JSTで2026-09-20T07:08 と同 instant のため更新済みになる
            var checker = CreateChecker("{\"last_modified\":\"2026-09-19T22:08:34Z\"}", NowJst);

            var result = checker.Check();

            Assert.AreEqual(SnapShotVersionStatus.Updated, result.Status);
            // Z は UTC(+00:00)として保持される（実行環境の +09:00 に寄せられていないこと）。
            // オフセットが落ちず instant 比較になっている証拠
            Assert.AreEqual(TimeSpan.Zero, result.LastModified.Value.Offset);
        }

        [TestMethod]
        public void ToStatusLogLine_ContainsRawAndJudgment()
        {
            var checker = CreateChecker("{\"last_modified\":\"2026-09-20T07:08:34+09:00\"}", NowJst);

            string line = SnapShotVersionChecker.ToStatusLogLine(checker.Check());

            Assert.IsTrue(line.Contains("2026-09-20T07:08:34+09:00"), line);
            Assert.IsTrue(line.Contains("更新済み"), line);
        }

        [TestMethod]
        public void ToStatusLogLine_Failure_ContainsUnknownMark()
        {
            var checker = new SnapShotVersionChecker(
                (string url, out string text) => { text = null; return false; },
                () => NowJst);

            string line = SnapShotVersionChecker.ToStatusLogLine(checker.Check());

            Assert.IsTrue(line.Contains("確認不能"), line);
        }
    }
}
