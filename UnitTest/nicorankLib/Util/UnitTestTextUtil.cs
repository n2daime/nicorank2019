using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestTextUtil
    {
        [TestMethod]
        public void TestMethodRead()
        {
            {
                bool result = TextUtil.ReadCsv(@"T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank_x64\nicorank_x64\nicorank_x64\OutPut\result_DB登録用(SJIS).csv",
                    out List<Ranking> rankingList);
            }
            {
                bool result = TextUtil.ReadCsv(@"T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank_x64\nicorank_x64\nicorank_x64\OutPut\result_DB登録用(UTF8).csv",
                    out List<Ranking> rankingList);
            }
            {
                bool result = TextUtil.ReadCsv(@"T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank_x64\nicorank_x64\nicorank_x64\OutPut\result_DB登録用(UTF8).csv",
                    out Dictionary<string , Ranking> rankingMap);
            }
        }
        [TestMethod]
        public void TestMethodWrite()
        {
            var err = ErrLog.GetInstance();
            err.Write("aaaaa");

        }
    }
}
