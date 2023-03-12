using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using System;

namespace UnitTest.nicorankLib.Analyze.model
{
    [TestClass]
    public class UnitTestRanking
    {
        [TestMethod]
        public void TestPointCalc_POINTALL_VOCACOLE2023()
        {
            var config = Config.GetInstance();

            // コメント全体補正あり
            config.CalcPointAllKind = 1;
            config.CalcCommentKind = 2;
            config.CalcCommentUnderLimit = 0.01;
            config.CalcMyListKind = 1;
            config.CalcPlayKind = 2;

            config.IsSP = false;
            config.CalcMyList = 40;
            config.CalcPlay = 1;
            config.CalcComment = 1;
            config.CalcLike = 10;

            var rank = new Ranking() { ID = "sm40422969", CountPlay = 198735, CountComment = 236, CountMyList = 1362, CountLike = 3661 };
            var Point = rank.PointTotal;

            //doubleの比較なので、文字列変換したときにただしく表示されるかで確認する
            Assert.AreEqual<String>($"{ rank.HoseiAllPoint}","0.29");

            rank = new Ranking() { ID = "sm41716570", CountPlay = 130984, CountComment = 412, CountMyList = 264, CountLike = 1794 };
            Point = rank.PointTotal;
            Assert.AreEqual<String>($"{ rank.HoseiAllPoint}", "0.78");

            rank = new Ranking() { ID = "so41698792", CountPlay = 317645, CountComment = 59666, CountMyList = 440, CountLike = 4063 };
            Point = rank.PointTotal;
            Assert.AreEqual<String>($"{ rank.HoseiAllPoint}", "1");

        }
    }
}
