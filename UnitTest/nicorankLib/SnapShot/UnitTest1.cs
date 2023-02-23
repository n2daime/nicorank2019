using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.SnapShot
{
    [TestClass]
    public class UnitTestSnapShot
    {
        [TestMethod]
        public void TestMethodSnapShot()
        {
            var test = new SnapShotDB();
            test.GetJsonData(@"T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank2019\UnitTest\bin\Test\Snap20190701\200703\06", out var dataList);

            test.RegistDB(dataList, DateConvert.String2Time("20190701",false));
        }
    }
}
