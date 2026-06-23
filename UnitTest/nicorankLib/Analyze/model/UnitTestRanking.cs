using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using System;

namespace UnitTest.nicorankLib.Analyze.model
{
    [TestClass]
    public class UnitTestRanking
    {
        [TestInitialize]
        public void Init()
        {
            var config = Config.GetInstance();
            config.IsSP = false;

            config.CalcPointAllKind = 0;
            config.CalcCommentKind = 1;
            config.CalcCommentUnderLimit = 0.01;
            config.CalcMyListKind = 1;
            config.CalcPlayKind = 2;

            config.CalcMyList = 40;
            config.CalcPlay = 1;
            config.CalcComment = 1;
            config.CalcLike = 10;
        }

        [TestMethod]
        public void TestPointCalc_POINTALL_VOCACOLE2023()
        {
            var config = Config.GetInstance();

            config.CalcPointAllKind = 1;
            config.CalcCommentKind = 2;
            config.CalcCommentUnderLimit = 0.01;
            config.CalcMyListKind = 1;
            config.CalcPlayKind = 1;

            config.IsSP = false;
            config.CalcMyList = 40;
            config.CalcPlay = 1;
            config.CalcComment = 1;
            config.CalcLike = 10;

            var rank = new Ranking() { ID = "sm40422969", CountPlay = 198735, CountComment = 236, CountMyList = 1362, CountLike = 3661 };
            var Point = rank.PointTotal;

            Assert.AreEqual<string>("0.29", $"{rank.HoseiAllPoint}");

            rank = new Ranking() { ID = "sm41716570", CountPlay = 130984, CountComment = 412, CountMyList = 264, CountLike = 1794 };
            Point = rank.PointTotal;
            Assert.AreEqual<string>("0.78", $"{rank.HoseiAllPoint}");

            rank = new Ranking() { ID = "so41698792", CountPlay = 317645, CountComment = 59666, CountMyList = 440, CountLike = 4063 };
            Point = rank.PointTotal;
            Assert.AreEqual<string>("1", $"{rank.HoseiAllPoint}");
        }

        [TestMethod]
        public void TestPointCalc_HOSEI_NASHI()
        {
            var config = Config.GetInstance();
            config.CalcCommentKind = 0;
            config.CalcPointAllKind = 0;

            var rank = new Ranking() { ID = "sm12345678", CountPlay = 100000, CountComment = 500, CountMyList = 2000, CountLike = 3000 };
            var point = rank.PointTotal;

            Assert.IsTrue(point > 0);
            Assert.AreEqual(1.0, rank.HoseiAllPoint);
        }

        [TestMethod]
        public void TestPointCalc_HOSEI_ARI_SQRT()
        {
            var config = Config.GetInstance();
            config.CalcCommentKind = 3;
            config.CalcPointAllKind = 0;

            var rank = new Ranking() { ID = "sm87654321", CountPlay = 50000, CountComment = 1000, CountMyList = 500, CountLike = 1000 };
            var point = rank.PointTotal;

            Assert.IsTrue(point > 0);
        }

        [TestMethod]
        public void TestPointCalc_DeletedMovie()
        {
            var rank = new Ranking() { ID = "sm00000000", isDelete = true };
            var point = rank.PointTotal;

            Assert.AreEqual(0, point);
        }

        [TestMethod]
        public void TestPointCalc_ZeroCounts()
        {
            var config = Config.GetInstance();
            config.CalcCommentKind = 0;
            config.CalcPointAllKind = 0;

            var rank = new Ranking() { ID = "sm00000001", CountPlay = 0, CountComment = 0, CountMyList = 0, CountLike = 0 };
            var point = rank.PointTotal;

            Assert.AreEqual(0, point);
        }

        [TestMethod]
        public void TestHoseiAllPoint_Boundary()
        {
            var config = Config.GetInstance();
            config.CalcPointAllKind = 1;
            config.CalcCommentKind = 0;

            var rank = new Ranking() { ID = "sm99999999", CountPlay = 1000, CountComment = 1, CountMyList = 0, CountLike = 0 };
            var point = rank.PointTotal;

            Assert.IsTrue(rank.HoseiAllPoint >= 0.25);
            Assert.IsTrue(rank.HoseiAllPoint <= 1.0);
        }
    }
}
