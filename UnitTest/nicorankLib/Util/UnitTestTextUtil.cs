using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestTextUtil
    {
        [TestMethod]
        public void TestReadCsv_List()
        {
            var fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "test_ranking.csv");
            var result = TextUtil.ReadCsv(fixturePath, out List<Ranking> rankingList);

            Assert.IsTrue(result);
            Assert.IsNotNull(rankingList);
            Assert.IsTrue(rankingList.Count > 0);
        }

        [TestMethod]
        public void TestReadCsv_Dictionary()
        {
            var fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "test_ranking.csv");
            var result = TextUtil.ReadCsv(fixturePath, out Dictionary<string, Ranking> rankingMap);

            Assert.IsTrue(result);
            Assert.IsNotNull(rankingMap);
            Assert.IsTrue(rankingMap.ContainsKey("sm40422969"));
        }

        [TestMethod]
        public void TestReadCsv_FileNotFound()
        {
            var result = TextUtil.ReadCsv("nonexistent_file.csv", out List<Ranking> rankingList);

            Assert.IsFalse(result);
            Assert.IsNotNull(rankingList);
            Assert.AreEqual(0, rankingList.Count);
        }

        [TestMethod]
        public void TestReadCsv_FixtureValues()
        {
            var fixturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "test_ranking.csv");
            var result = TextUtil.ReadCsv(fixturePath, out Dictionary<string, Ranking> rankingMap);

            Assert.IsTrue(result);
            var rank = rankingMap["sm40422969"];
            Assert.AreEqual(1, rank.RankTotal);
            Assert.AreEqual(10000, rank.PointTotal);
            Assert.AreEqual(80000, rank.PointMyList);
            Assert.AreEqual(100000, rank.CountPlay);
            Assert.AreEqual(500, rank.CountComment);
            Assert.AreEqual(2000, rank.CountMyList);
            Assert.AreEqual(1, rank.RankLike);
            Assert.AreEqual(5000, rank.CountLike);
            Assert.AreEqual("音楽", rank.Category);
            Assert.AreEqual("user1", rank.UserID);
            Assert.AreEqual("テストユーザー1", rank.UserName);
            Assert.AreEqual("user1.jpg", rank.UserImageURL);
            Assert.AreEqual(0, rank.LastRank);
            Assert.AreEqual(0, rank.LastPoint);
            Assert.AreEqual(new DateTime(2023, 1, 1), rank.Date.Date);
            Assert.IsNotNull(rank.FavoriteTags);
            Assert.AreEqual(0, rank.FavoriteTags.Count);
        }

        [TestMethod]
        public void TestReadCsv_ColumnReorder_And_MissingDefaults()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "UnitTest_ReadCsvReorder_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                var rows = new List<List<object>>
                {
                    new List<object> { "ポイント", "人気のタグ", "ID", "総合ランク", "タイトル" },
                    new List<object> { 777, "タグA,タグB", "sm99999999", 5, "順序入替動画" }
                };
                Assert.IsTrue(CsvUtil.Write(tempPath, rows, true));

                var result = TextUtil.ReadCsv(tempPath, out List<Ranking> rankingList);

                Assert.IsTrue(result);
                Assert.AreEqual(1, rankingList.Count);
                var rank = rankingList[0];
                Assert.AreEqual("sm99999999", rank.ID);
                Assert.AreEqual(777, rank.PointTotal);
                Assert.AreEqual(5, rank.RankTotal);
                Assert.AreEqual("順序入替動画", rank.Title);
                CollectionAssert.AreEqual(new List<string> { "タグA", "タグB" }, rank.FavoriteTags);
                //欠落列は既定値
                Assert.AreEqual(0, rank.CountPlay);
                Assert.AreEqual(string.Empty, rank.Category);
                Assert.AreEqual(string.Empty, rank.UserID);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [TestMethod]
        public void TestReadCsv_UserIconAlias()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "UnitTest_ReadCsvAlias_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                var rows = new List<List<object>>
                {
                    new List<object> { "ID", "ユーザーアイコンファイル" },
                    new List<object> { "sm88888888", "alias.jpg" }
                };
                Assert.IsTrue(CsvUtil.Write(tempPath, rows, true));

                var result = TextUtil.ReadCsv(tempPath, out List<Ranking> rankingList);

                Assert.IsTrue(result);
                Assert.AreEqual(1, rankingList.Count);
                Assert.AreEqual("alias.jpg", rankingList[0].UserImageURL);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
