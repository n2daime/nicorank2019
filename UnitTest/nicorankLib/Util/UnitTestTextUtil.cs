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
    }
}
