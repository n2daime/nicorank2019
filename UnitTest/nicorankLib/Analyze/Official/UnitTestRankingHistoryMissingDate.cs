using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Official;

namespace UnitTest.nicorankLib.Analyze.Official
{
    /// <summary>
    /// Issue #53: 404 の日を「次回再取得」にするかの判定。
    /// 当日をメンテナンス日として登録すると、後から公開されたデータを永久に取り込めなくなるため、
    /// 当日以降は必ずスキップ（登録しない）にする。
    /// </summary>
    [TestClass]
    public class UnitTestRankingHistoryMissingDate
    {
        [TestMethod]
        public void ShouldDeferMissingDate_TodayOrFuture_ReturnsTrue()
        {
            var today = new DateTime(2026, 10, 22);

            Assert.IsTrue(RankingHistory.ShouldDeferMissingDate(today, today));
            Assert.IsTrue(RankingHistory.ShouldDeferMissingDate(today.AddDays(1), today));
            // 時刻が付いていても日付だけで判定する
            Assert.IsTrue(RankingHistory.ShouldDeferMissingDate(new DateTime(2026, 10, 22, 23, 59, 59), today));
        }

        [TestMethod]
        public void ShouldDeferMissingDate_PastDate_ReturnsFalse()
        {
            var today = new DateTime(2026, 10, 22);

            Assert.IsFalse(RankingHistory.ShouldDeferMissingDate(today.AddDays(-1), today));
        }
    }
}
