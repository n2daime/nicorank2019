using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Official;

namespace UnitTest.nicorankLib.Analyze.Official
{
    [TestClass]
    public class UnitTestRankingHistory
    {
        [TestMethod]
        public void TestMethodUpdateOfficialRankingDB()
        {
            var testObj = new RankingHistory();
            testObj.Open();

            testObj.UpdateOfficialRankingDB();

            testObj.Close();
        }
        [TestMethod]
        public void TestMethodGetRankingDataLogNicoChart()
        {
            var testObj = new RankingHistory();
            testObj.Open();

            testObj.GetRankingDataLogNicoChart("sm35241833",20190617);

            testObj.Close();
        }
        [TestMethod]
        public void TestCheckSoMovieNeedSabun()
        {
            var testObj = new RankingHistory();
            testObj.Open();

            testObj.CheckSoMovieNeedSabun("so35121777", 20190728,out var rank);

            testObj.Close();
        }

    }
}
