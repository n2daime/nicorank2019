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

            //T.B.D 単体テスト
            //現時点ではデバッグテスト実行して、
            // XMLが正常に読み込まれるかを目視確認している
        }
    }
}
