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

        [TestMethod]
        public void TestConfigTagRank_PartialSection_FallsBackToWeekly()
        {
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBase, "<TAGRANK><RANK Num=\"30\" Tyouki=\"0\"/></TAGRANK>"));
                var config = Config.GetInstance();
                config.IsSP = false;
                config.IsTagRank = true;

                Assert.AreEqual(20, config.Rank);
                Assert.AreEqual(40, config.CalcMyList);
            }
            finally
            {
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }

        // 共通OFFSET（Mode値）を持つ最小XML。節内OFFSETの有無によるフォールバック検証の土台にする
        private const string XmlBaseWithOffsets =
            "<nicorank>" +
            "<RANK Num=\"20\" Tyouki=\"1\"/>" +
            "<RANKED Num=\"200\"/>" +
            "<UserInfo Num=\"1000\"/>" +
            "<POINT><CALC_MYLIST>40</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>" +
            "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>10</CALC_LIKE></POINT>" +
            "<COMMENT_OFFSET Mode=\"2\" UnderLimit=\"0.01\"/>" +
            "<MYLIST_OFFSET Mode=\"1\"/>" +
            "<PLAY_OFFSET Mode=\"2\"/>" +
            "<POINTALL_OFFSET Mode=\"0\"/>" +
            "{0}{1}</nicorank>";

        private const string SpSectionWithoutOffsets =
            "<SP><POINT><CALC_MYLIST>20</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>" +
            "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>20</CALC_LIKE></POINT>" +
            "<RANK Num=\"100\" Tyouki=\"0\"/><RANKED Num=\"400\"/>" +
            "<UserInfo Num=\"1000\"/><CheckDateOver>20170701</CheckDateOver></SP>";

        private const string SpSectionWithOffsets =
            "<SP><POINT><CALC_MYLIST>20</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>" +
            "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>20</CALC_LIKE></POINT>" +
            "<RANK Num=\"100\" Tyouki=\"0\"/><RANKED Num=\"400\"/>" +
            "<UserInfo Num=\"1000\"/><CheckDateOver>20170701</CheckDateOver>" +
            "<COMMENT_OFFSET Mode=\"0\" UnderLimit=\"0.05\"/>" +
            "<MYLIST_OFFSET Mode=\"2\"/>" +
            "<PLAY_OFFSET Mode=\"1\"/>" +
            "<POINTALL_OFFSET Mode=\"1\"/></SP>";

        private const string TagRankSectionWithOffsets =
            "<TAGRANK><POINT><CALC_MYLIST>5</CALC_MYLIST><CALC_PLAY>6</CALC_PLAY>" +
            "<CALC_COMMENT>7</CALC_COMMENT><CALC_LIKE>8</CALC_LIKE></POINT>" +
            "<RANK Num=\"30\" Tyouki=\"0\"/><RANKED Num=\"300\"/>" +
            "<UserInfo Num=\"500\"/><CheckDateOver>20240101</CheckDateOver>" +
            "<COMMENT_OFFSET Mode=\"1\" UnderLimit=\"0.02\"/>" +
            "<MYLIST_OFFSET Mode=\"0\"/>" +
            "<PLAY_OFFSET Mode=\"0\"/>" +
            "<POINTALL_OFFSET Mode=\"1\"/></TAGRANK>";

        [TestMethod]
        public void TestConfigOffset_WithoutSectionOffset_FallsBackToCommon()
        {
            // 節内OFFSETなしの既存XMLでは、SP・タグ検索いずれも共通値を使う（互換条件）
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBaseWithOffsets, SpSectionWithoutOffsets, ""));
                var config = Config.GetInstance();

                config.IsSP = false;
                config.IsTagRank = false;
                Assert.AreEqual(1, config.CalcMyListKind);
                Assert.AreEqual(2, config.CalcCommentKind);
                Assert.AreEqual(0.01, config.CalcCommentUnderLimit);
                Assert.AreEqual(2, config.CalcPlayKind);
                Assert.AreEqual(0, config.CalcPointAllKind);

                config.IsSP = true;
                config.IsTagRank = false;
                Assert.AreEqual(1, config.CalcMyListKind);
                Assert.AreEqual(2, config.CalcCommentKind);

                config.IsSP = false;
                config.IsTagRank = true;
                Assert.AreEqual(1, config.CalcMyListKind);
                Assert.AreEqual(2, config.CalcCommentKind);
            }
            finally
            {
                Config.GetInstance().IsSP = false;
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void TestConfigOffset_WithSectionOffset_UsesSectionValues()
        {
            // 節内OFFSETありでは、モードごとに節内値を使う
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBaseWithOffsets, SpSectionWithOffsets, TagRankSectionWithOffsets));
                var config = Config.GetInstance();

                config.IsSP = false;
                config.IsTagRank = false;
                Assert.AreEqual(1, config.CalcMyListKind);

                config.IsSP = true;
                config.IsTagRank = false;
                Assert.AreEqual(2, config.CalcMyListKind);
                Assert.AreEqual(0, config.CalcCommentKind);
                Assert.AreEqual(0.05, config.CalcCommentUnderLimit);
                Assert.AreEqual(1, config.CalcPlayKind);
                Assert.AreEqual(1, config.CalcPointAllKind);

                config.IsSP = false;
                config.IsTagRank = true;
                Assert.AreEqual(0, config.CalcMyListKind);
                Assert.AreEqual(1, config.CalcCommentKind);
                Assert.AreEqual(0.02, config.CalcCommentUnderLimit);
                Assert.AreEqual(0, config.CalcPlayKind);
                Assert.AreEqual(1, config.CalcPointAllKind);
            }
            finally
            {
                Config.GetInstance().IsSP = false;
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void TestConfigOffset_PartialSectionOffset_FallsBackPerItem()
        {
            // 項目単位フォールバックの検証。MYLISTだけ節内にあればMYLISTは節内値、他は共通値になる
            // なぜ節単位にしないか：1項目だけ変えたいときに4項目全部書かせるのは手間であり、コメントの要求にそぐわないため
            try
            {
                string partialTagRank = "<TAGRANK><POINT><CALC_MYLIST>5</CALC_MYLIST><CALC_PLAY>6</CALC_PLAY>" +
                    "<CALC_COMMENT>7</CALC_COMMENT><CALC_LIKE>8</CALC_LIKE></POINT>" +
                    "<RANK Num=\"30\" Tyouki=\"0\"/><RANKED Num=\"300\"/>" +
                    "<UserInfo Num=\"500\"/><CheckDateOver>20240101</CheckDateOver>" +
                    "<MYLIST_OFFSET Mode=\"0\"/></TAGRANK>";
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBaseWithOffsets, SpSectionWithoutOffsets, partialTagRank));
                var config = Config.GetInstance();
                config.IsSP = false;
                config.IsTagRank = true;

                Assert.AreEqual(0, config.CalcMyListKind);
                Assert.AreEqual(2, config.CalcCommentKind);
                Assert.AreEqual(2, config.CalcPlayKind);
                Assert.AreEqual(0, config.CalcPointAllKind);
            }
            finally
            {
                Config.GetInstance().IsSP = false;
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }

        [TestMethod]
        public void TestConfigOffset_Setter_WritesToEffectiveSection()
        {
            // パネル書戻し（SavePointCalcPanel経由のsetter）がモード別の節に書かれ、他モードに波及しないことの検証
            try
            {
                TestConfigBuilder.LoadFromXmlString(string.Format(XmlBaseWithOffsets, SpSectionWithOffsets, TagRankSectionWithOffsets));
                var config = Config.GetInstance();

                config.IsSP = false;
                config.IsTagRank = true;
                config.CalcMyListKind = 1;
                Assert.AreEqual(1, config.CalcMyListKind);

                config.IsSP = false;
                config.IsTagRank = false;
                Assert.AreEqual(1, config.CalcMyListKind);

                config.IsSP = true;
                config.IsTagRank = false;
                Assert.AreEqual(2, config.CalcMyListKind);
            }
            finally
            {
                Config.GetInstance().IsSP = false;
                Config.GetInstance().IsTagRank = false;
                TestConfigBuilder.ResetInstance();
            }
        }
    }
}
