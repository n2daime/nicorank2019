using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option;
using nicorankLib.api;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Option.Ext
{
    [TestClass]
    public class UnitTestFavoriteTagReader
    {
        private static readonly DateTime BaseTime = new DateTime(2020, 1, 1);
        private static readonly DateTime EndTime = new DateTime(2020, 1, 10);

        private class FakeNicoApi : NicoApi
        {
            public Dictionary<string, List<string>> LockedTags = new Dictionary<string, List<string>>();
            public List<Ranking> UpdatedTargets;
            public bool UpdateResult = true;

            public override bool OpenDB()
            {
                return true;
            }

            public override void CloseDB()
            {
            }

            public override bool UpdateTumbInfo(IReadOnlyList<Ranking> rankingList, DateTime? targetDate)
            {
                UpdatedTargets = new List<Ranking>(rankingList);
                return UpdateResult;
            }

            public override List<string> GetLockedTags(string id)
            {
                return LockedTags.TryGetValue(id, out var tags) ? tags : new List<string>();
            }
        }

        private static Ranking CreateRank(string id, long rankTotal, long rankCategory = 5)
        {
            return new Ranking() { ID = id, RankTotal = rankTotal, RankCategory = rankCategory };
        }

        [TestMethod]
        public void 人気タグが多い場合もロックタグを全件追加する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\",\"B\",\"C\"]");

                var api = new FakeNicoApi();
                api.LockedTags["sm1"] = new List<string> { "X", "Y" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(
                    new List<string> { "A", "B", "C", "X", "Y" }, new List<string>(list[0].FavoriteTags));
            }
        }

        [TestMethod]
        public void 重複タグは追加しない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\",\"B\"]");

                var api = new FakeNicoApi();
                api.LockedTags["sm1"] = new List<string> { "B", "X" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(
                    new List<string> { "A", "B", "X" }, new List<string>(list[0].FavoriteTags));
            }
        }

        [TestMethod]
        public void 人気タグなしはロックタグを全件追加する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[]");

                var api = new FakeNicoApi();
                api.LockedTags["sm1"] = new List<string> { "X", "Y", "Z", "W" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(
                    new List<string> { "X", "Y", "Z", "W" }, new List<string>(list[0].FavoriteTags));
            }
        }

        [TestMethod]
        public void タグロックなしは人気タグのみ残る()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\"]");

                var api = new FakeNicoApi();
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(new List<string> { "A" }, new List<string>(list[0].FavoriteTags));
                Assert.AreEqual(1, api.UpdatedTargets.Count);
            }
        }

        [TestMethod]
        public void 取得対象外は取得も補完もしない()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\"]");

                var api = new FakeNicoApi();
                api.LockedTags["sm1"] = new List<string> { "X", "Y" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 100, 5) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                Assert.AreEqual(0, list[0].FavoriteTags.Count);
                Assert.IsNull(api.UpdatedTargets);
            }
        }

        [TestMethod]
        public void 人気タグがnull値の行は読み飛ばして補完する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "null");

                var api = new FakeNicoApi();
                api.LockedTags["sm1"] = new List<string> { "X" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(new List<string> { "X" }, new List<string>(list[0].FavoriteTags));
            }
        }

        [TestMethod]
        public void 確保失敗時はfalseを返す()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\"]");

                var api = new FakeNicoApi();
                api.UpdateResult = false;
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsFalse(reader.AnalyzeRank(list));
            }
        }

        [TestMethod]
        public void isLocalOnly時は確保せずキャッシュのみ補完する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\"]");

                var api = new FakeNicoApi();
                api.LockedTags["sm1"] = new List<string> { "X", "Y" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api, true);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(
                    new List<string> { "A", "X", "Y" }, new List<string>(list[0].FavoriteTags));
                Assert.IsNull(api.UpdatedTargets);
            }
        }

        [TestMethod]
        public void isLocalOnly時は確保失敗でも成功する()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingTable(db);
                TestDbHelper.InsertRankingData(db, "sm1", 20200105, 100, 10, 5, 2, "[\"A\"]");

                var api = new FakeNicoApi();
                api.UpdateResult = false;
                api.LockedTags["sm1"] = new List<string> { "X" };
                var reader = new FavoriteTagReader(10, BaseTime, EndTime, db, api, true);

                var list = new List<Ranking> { CreateRank("sm1", 1) };

                Assert.IsTrue(reader.AnalyzeRank(list));
                CollectionAssert.AreEqual(
                    new List<string> { "A", "X" }, new List<string>(list[0].FavoriteTags));
                Assert.IsNull(api.UpdatedTargets);
            }
        }
    }
}
