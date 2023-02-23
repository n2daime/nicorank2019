using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Common;

namespace UnitTest.nicorankLib.Common
{
    [TestClass]
    public class UnitTestConfig
    {
        [TestMethod]
        public void TestMethodConfig()
        {
            var config = Config.GetInstance();
        }
    }
}
