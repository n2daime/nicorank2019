using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze;
using nicorankLib.Analyze.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option;
using nicorankLib.api;
using nicorankLib.Common;
using nicorankLib.Factory;
using nicorankLib.output;
using nicorankLib.SnapShot;
using nicorankLib.Util;

namespace UnitTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethodSnapShot()
        {
            //var testObj = new SnapShotAnalyze();

            //DateTime dateTime = DateConvert.String2Time("20070306", false);
            //while (dateTime < DateTime.Now)
            //{
            //    testObj.AnalyzeRank(dateTime);
            //    dateTime = dateTime.AddMonths(1);
            //}
        }

        [TestMethod]
        public void TestMethodAAAA()
        {
            var config = Config.GetInstance();

            Console.WriteLine("Test");
            StatusLog.SetLogWriter(new ConsolWriter());



            var modeFactory = new ModeFactoryTyukan();

            var testObj = modeFactory.CreateAnalyzer();

            //var testObj = new RankingAnalyze(
            //    new JsonReaderDaily(DateTime.ParseExact("20190625", "yyyyMMdd", null))
            //    , DateTime.ParseExact("20190624", "yyyyMMdd", null));

            //var testObj = new RankingAnalyze(
            //    new JsonReaderWeekly()
            //    , DateTime.ParseExact("20190624", "yyyyMMdd", null));

            //var testObj = new RankingAnalyze(
            //    new TotyuAnalyze()
            //    , null);

            if (!modeFactory.AnalyzeRank())
            {
                Assert.Fail() ;
            }



            var outputList = new List<OutputBase>()
            {
                modeFactory.CreateHistory(),
                modeFactory.TyokiHantei,
                modeFactory.CreateNRMRank(),
                modeFactory.CreateNRMRank1000(),
                modeFactory.CreateNRMRankED(),
                modeFactory.CreateOutputCSV(),
                modeFactory.CreateOutputCSV_rankDB(),
                modeFactory.CreateOutputHTML(),
                modeFactory.CreateOutputMovieIconGet(),
                modeFactory.CreateOutputUserIconGet(),
                modeFactory.CreateOutputWORK()
            };

            foreach (var output in outputList)
            {
                output?.Execute(modeFactory.RankingList);
            }

            //var tyokiHantei = new TyokiHantei();
            //tyokiHantei.Set("Output", modeFactory.TargetDay);
            //tyokiHantei.Execute(modeFactory.RankingList);

            //var output1 = new ResultCsv();
            //output1.SetOutput("Output", new List<ResultCsv.CsvConfig>() {
            //    new ResultCsv.CsvConfig() { csvName = "result.csv", isUnicode = true, isOverwrite = true }
            //});
            //output1.Execute(modeFactory.RankingList);

            //int rank = config.Rank + tyokiHantei.tyokiRankList.Count;

            //var output2 = new NrmOutput();
            //output2.Set("Output", "rank.txt", 0, rank);
            //output2.Execute(modeFactory.RankingList);

            //output2.Set("Output", "rankED.txt", rank, rank + config.RankED);
            //output2.Execute(modeFactory.RankingList);

            //output2.Set("Output", "rank1000.txt", 0, 1000);
            //output2.Execute(modeFactory.RankingList);

            //output2.Set("Output", "rankALL.txt", 0, modeFactory.RankingList.Count);
            //output2.Execute(modeFactory.RankingList);

            //var output3 = new ResultImagegetMovieIcon("Output", "queue.irv", rank + config.RankED);
            //output3.Execute(modeFactory.RankingList);

            //var output4 = new ResultImagegetUserIcon("Output", "queue_UserIcon.irv");
            //output4.Execute(modeFactory.RankingList);

            //var output5 = new ResultCsvRankDB();
            //output5.SetOutput("Output", new List<ResultCsv.CsvConfig>() {
            //    new ResultCsv.CsvConfig() { csvName = "result_DB登録用(UTF8).csv", isUnicode = true, isOverwrite = true },
            //    new ResultCsv.CsvConfig() { csvName = "result_DB登録用(SJIS).csv", isUnicode = false, isOverwrite = true }
            //});
            //output5.Execute(modeFactory.RankingList);

        }

        public class ConsolWriter : IStatusLogWriter
        {
            void IStatusLogWriter.Write(string log)
            {
                Console.Write(log);
            }
        }
    }
}
