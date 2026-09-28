using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;

namespace UnitTest.nicorankLib.Analyze.model
{
    /// <summary>
    /// Issue #40: 情報欠落時のタイトル目印【集計後削除】の検証。表示だけの変更で順位・ポイントに触らない。
    /// </summary>
    [TestClass]
    public class UnitTestRankingDeletedMarker
    {
        [TestMethod]
        public void タイトル空欄に目印が付く()
        {
            var rank = new Ranking() { ID = "sm1" };
            rank.Title = string.Empty;

            rank.ApplyDeletedTitleMarker();

            Assert.AreEqual(Ranking.DeletedTitlePrefix, rank.Title);
            Assert.IsFalse(rank.isDelete);
        }

        [TestMethod]
        public void タイトル取得済みは変わらない()
        {
            var rank = new Ranking() { ID = "sm1" };
            rank.Title = "件名";

            rank.ApplyDeletedTitleMarker();

            Assert.AreEqual("件名", rank.Title);
        }
    }
}
