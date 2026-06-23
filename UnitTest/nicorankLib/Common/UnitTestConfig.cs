using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Common;
using nicorankLib.Util.Text;
using System.IO;

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
    }
}
