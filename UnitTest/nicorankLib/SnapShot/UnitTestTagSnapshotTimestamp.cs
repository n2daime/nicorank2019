using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;
using System;

namespace UnitTest.nicorankLib.SnapShot
{
    /// <summary>
    /// TagSnapshotTimestamp のテスト（Issue #39）。
    /// ラベル文言の仕様値（05:00固定・JST日付）を縛る。通信は行わず純粋処理だけを検証する。
    /// </summary>
    [TestClass]
    public class UnitTestTagSnapshotTimestamp
    {
        [TestMethod]
        public void Format_UsesJstDate_WithFixed0500()
        {
            // last_modified の時刻（07:08）は反映完了時刻であり、仕様上のデータ時点は5:00のため時刻は固定表示になる
            var lastModified = new DateTimeOffset(2026, 9, 26, 7, 8, 34, TimeSpan.FromHours(9));

            string label = TagSnapshotTimestamp.Format(lastModified);

            Assert.AreEqual("09/26 05:00 時点のスナップショットで集計", label);
        }

        [TestMethod]
        public void Format_ZuluInput_ConvertsToJstDate()
        {
            // Z（UTC）表記でもJST換算の日付を使う。2026-09-25T22:08:34Z はJSTで09/26 07:08のため09/26表示になる
            var lastModified = new DateTimeOffset(2026, 9, 25, 22, 8, 34, TimeSpan.Zero);

            string label = TagSnapshotTimestamp.Format(lastModified);

            Assert.AreEqual("09/26 05:00 時点のスナップショットで集計", label);
        }

        [TestMethod]
        public void Format_MidnightBoundary_UsesJstDate()
        {
            // 日付境界の確認。JSTで09/26 00:30は09/26表示になる
            var lastModified = new DateTimeOffset(2026, 9, 26, 0, 30, 0, TimeSpan.FromHours(9));

            string label = TagSnapshotTimestamp.Format(lastModified);

            Assert.AreEqual("09/26 05:00 時点のスナップショットで集計", label);
        }

        [TestMethod]
        public void DataHourAndMinute_AreFixed0500()
        {
            // 仕様値（5:00）が変わると表示の意味が変わるため、変更時はIssue見直しと判断できるようテストで縛る
            Assert.AreEqual(5, TagSnapshotTimestamp.DataHour);
            Assert.AreEqual(0, TagSnapshotTimestamp.DataMinute);
        }

        [TestMethod]
        public void TryFormat_UpdatedResult_ReturnsTrueWithLabel()
        {
            var result = new SnapShotVersionResult
            {
                Status = SnapShotVersionStatus.Updated,
                LastModifiedRaw = "2026-09-26T07:08:34+09:00",
                LastModified = new DateTimeOffset(2026, 9, 26, 7, 8, 34, TimeSpan.FromHours(9))
            };

            bool ok = TagSnapshotTimestamp.TryFormat(result, out string label);

            Assert.IsTrue(ok);
            Assert.AreEqual("09/26 05:00 時点のスナップショットで集計", label);
        }

        [TestMethod]
        public void TryFormat_UnknownResult_ReturnsFalse()
        {
            // 確認不能時は呼び出し側がエラー中断するため、ラベルは作らない
            var result = new SnapShotVersionResult { Status = SnapShotVersionStatus.Unknown };

            bool ok = TagSnapshotTimestamp.TryFormat(result, out string label);

            Assert.IsFalse(ok);
            Assert.IsNull(label);
        }

        [TestMethod]
        public void TryFormat_NullResult_ReturnsFalse()
        {
            bool ok = TagSnapshotTimestamp.TryFormat(null, out string label);

            Assert.IsFalse(ok);
            Assert.IsNull(label);
        }
    }
}
