using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;

namespace UnitTest.nicorankLib.Analyze.model
{
    [TestClass]
    public class UnitTestRankingDisplayTags
    {
        private static Ranking CreateRank(string category, params string[] tags)
        {
            var rank = new Ranking() { Category = category };
            rank.FavoriteTags.AddRange(tags);
            return rank;
        }

        [TestMethod]
        public void カテゴリ同名タグを除外して挿入順に返す()
        {
            var rank = CreateRank("音楽", "音楽", "演奏してみた", "流田P");

            CollectionAssert.AreEqual(
                new List<string> { "演奏してみた", "流田P" }, rank.GetDisplayTags());
        }

        [TestMethod]
        public void カテゴリ空は除外しない()
        {
            var rank = CreateRank("", "音楽", "演奏してみた");

            CollectionAssert.AreEqual(
                new List<string> { "音楽", "演奏してみた" }, rank.GetDisplayTags());
        }

        [TestMethod]
        public void 前後空白はTrimして判定する()
        {
            var rank = CreateRank("音楽", " 音楽 ", " 演奏してみた ", "", "   ");

            CollectionAssert.AreEqual(
                new List<string> { "演奏してみた" }, rank.GetDisplayTags());
        }

        [TestMethod]
        public void 重複は除外する()
        {
            var rank = CreateRank("ゲーム", "実況", "実況", "実況プレイ");

            CollectionAssert.AreEqual(
                new List<string> { "実況", "実況プレイ" }, rank.GetDisplayTags());
        }

        [TestMethod]
        public void 元リストは変更しない()
        {
            var rank = CreateRank("音楽", "音楽", "演奏してみた");

            rank.GetDisplayTags();

            CollectionAssert.AreEqual(
                new List<string> { "音楽", "演奏してみた" }, new List<string>(rank.FavoriteTags));
        }

        [TestMethod]
        public void FavoriteTagsがnullでも空リストを返す()
        {
            var rank = new Ranking() { Category = "音楽", FavoriteTags = null };

            Assert.AreEqual(0, rank.GetDisplayTags().Count);
        }
    }
}
