using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Common;
using nicorankLib.Util.Text;
using System.IO;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Common
{
    [TestClass]
    public class UnitTestConfig
    {
        [TestMethod]
        public void TestConfigGetInstance()
        {
            var config = Config.GetInstance();
            Assert.IsNotNull(config);
            Assert.IsTrue(config.Rank > 0);
        }

        [TestMethod]
        public void TestConfigDefaultValues()
        {
            var config = Config.GetInstance();
            Assert.IsTrue(config.ThreadMax > 0);
            Assert.IsTrue(config.RetryNicoAPI > 0);
        }

        [TestMethod]
        public void TestConfigSPMode()
        {
            var config = Config.GetInstance();
            config.IsSP = true;

            Assert.IsTrue(config.RankED > 0);
            Assert.IsTrue(config.Rank > 0);

            config.IsSP = false;
        }

        [TestMethod]
        public void TestConfigHasXml()
        {
            var config = Config.GetInstance();
            var xmlString = config.GetXMLString();

            Assert.IsFalse(string.IsNullOrEmpty(xmlString));
            Assert.IsTrue(xmlString.Contains("nicorank"));
        }

        private const string XmlBase =
            "<nicorank>" +
            "<RANK Num=\"20\" Tyouki=\"1\"/>" +
            "<RANKED Num=\"200\"/>" +
            "<UserInfo Num=\"1000\"/>" +
            "<POINT><CALC_MYLIST>40</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>" +
            "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>10</CALC_LIKE></POINT>" +
            "<SP><POINT><CALC_MYLIST>20</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>" +
            "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>20</CALC_LIKE></POINT>" +
            "<RANK Num=\"100\" Tyouki=\"0\"/><RANKED Num=\"400\"/>" +
            "<UserInfo Num=\"1000\"/><CheckDateOver>20170701</CheckDateOver></SP>{0}</nicorank>";

        private const string TagRankSection =
            "<TAGRANK><POINT><CALC_MYLIST>5</CALC_MYLIST><CALC_PLAY>6</CALC_PLAY>" +
            "<CALC_COMMENT>7</CALC_COMMENT><CALC_LIKE>8</CALC_LIKE></POINT>" +
            "<RANK Num=\"30\" Tyouki=\"0\"/><RANKED Num=\"300\"/>" +
            "<UserInfo Num=\"500\"/><CheckDateOver>20240101</CheckDateOver></TAGRANK>";

        [TestMethod]
        public void TestConfigTagRank_WithSection_UsesTagRankValues()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBase, TagRankSection));
                var config = Config.GetInstance();
                config.IsSP = false;
                config.IsTagRank = true;

                Assert.AreEqual(30, config.Rank);
                Assert.AreEqual(300, config.RankED);
                Assert.AreEqual(500, config.UserNum);
                Assert.AreEqual(5, config.CalcMyList);
                Assert.AreEqual(8, config.CalcLike);
                Assert.IsFalse(config.IsTyouki);
                Assert.AreEqual("20240101", config.CheckDateOver);
            }
            finally
            {
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void TestConfigTagRank_WithoutSection_FallsBackToWeekly()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBase, ""));
                var config = Config.GetInstance();
                config.IsSP = false;
                config.IsTagRank = true;

                Assert.AreEqual(20, config.Rank);
                Assert.AreEqual(200, config.RankED);
                Assert.AreEqual(40, config.CalcMyList);
                Assert.IsTrue(config.IsTyouki);
            }
            finally
            {
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void TestConfigTagRank_FlagOff_UsesWeeklyOrSP()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBase, TagRankSection));
                var config = Config.GetInstance();
                config.IsSP = false;
                config.IsTagRank = false;

                Assert.AreEqual(20, config.Rank);

                config.IsSP = true;
                Assert.AreEqual(100, config.Rank);
                config.IsSP = false;
            }
            finally
            {
                TestConfigBuilder.ResetInstance();
            }
        }
    }
}
