using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option.Basic;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// Issue #40 案B: SPで動画情報が取れない場合の予備補完の検証。
    /// LastResultのタイトル・LogOfficialの期間内初見日で埋め、それでもなければ残す（除外しない）。
    /// </summary>
    [TestClass]
    public class UnitTestSpMovieInfoFallback
    {
        private static List<Ranking> RankingList(params string[] ids)
        {
            var list = new List<Ranking>();
            foreach (var id in ids)
            {
                list.Add(new Ranking() { ID = id });
            }
            return list;
        }

        [TestMethod]
        public void タイトル空欄はLastResultの最新タイトルで埋まる()
        {
            using (var history = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(history);
                TestDbHelper.InsertLastResultData(history, "Weekly", 20240101, "sm1", 1, 100, "{}");
                TestDbHelper.CreateRankingTable(official);

                var fallback = new SpMovieInfoFallback(history, official);
                var list = RankingList("sm1");

                Assert.IsTrue(fallback.ComplementMovieInfo(list, new DateTime(2024, 1, 8), new DateTime(2024, 1, 15)));
                Assert.AreEqual("タイトル_sm1", list[0].Title);
            }
        }

        [TestMethod]
        public void 初見日は期間内の最小集計日になる()
        {
            using (var history = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(history);
                TestDbHelper.CreateRankingTable(official);
                TestDbHelper.InsertRankingData(official, "sm2", 20240620, 100, 10, 5, 1);
                TestDbHelper.InsertRankingData(official, "sm2", 20240615, 90, 9, 4, 1);

                var fallback = new SpMovieInfoFallback(history, official);
                var list = RankingList("sm2");

                Assert.IsTrue(fallback.ComplementMovieInfo(list, new DateTime(2024, 6, 20), new DateTime(2024, 6, 30)));
                Assert.AreEqual(new DateTime(2024, 6, 15), list[0].Date);
            }
        }

        [TestMethod]
        public void 予備がなければ空のまま残し中断しない()
        {
            using (var history = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(history);
                TestDbHelper.CreateRankingTable(official);

                var fallback = new SpMovieInfoFallback(history, official);
                var list = RankingList("sm9");

                Assert.IsTrue(fallback.ComplementMovieInfo(list, new DateTime(2024, 6, 20), new DateTime(2024, 6, 30)));
                Assert.IsTrue(string.IsNullOrWhiteSpace(list[0].Title));
                Assert.IsFalse(list[0].isDelete);
            }
        }

        [TestMethod]
        public void タイトル取得済みは上書きしない()
        {
            using (var history = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(history);
                TestDbHelper.InsertLastResultData(history, "Weekly", 20240101, "sm1", 1, 100, "{}");
                TestDbHelper.CreateRankingTable(official);

                var fallback = new SpMovieInfoFallback(history, official);
                var list = RankingList("sm1");
                list[0].Title = "既存";

                Assert.IsTrue(fallback.ComplementMovieInfo(list, new DateTime(2024, 1, 8), new DateTime(2024, 1, 15)));
                Assert.AreEqual("既存", list[0].Title);
            }
        }
    }
}
