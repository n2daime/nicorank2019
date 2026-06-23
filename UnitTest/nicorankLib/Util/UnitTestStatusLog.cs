using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestStatusLog
    {
        [TestMethod]
        public void TestStatusLog_Write()
        {
            var writer = new MockStatusLogWriter();
            StatusLog.SetLogWriter(writer);

            StatusLog.Write("テストメッセージ");
            Assert.AreEqual("テストメッセージ", writer.LastMessage);
        }

        [TestMethod]
        public void TestStatusLog_WriteLine()
        {
            var writer = new MockStatusLogWriter();
            StatusLog.SetLogWriter(writer);

            StatusLog.WriteLine("テスト行");
            Assert.AreEqual("テスト行\n", writer.LastMessage);
        }

        [TestMethod]
        public void TestStatusLog_NullWriter()
        {
            StatusLog.SetLogWriter(null);
            StatusLog.Write("このメッセージは例外を発生させない");
        }

        private class MockStatusLogWriter : IStatusLogWriter
        {
            public string LastMessage { get; private set; }

            public void Write(string log)
            {
                LastMessage = log;
            }
        }
    }
}
