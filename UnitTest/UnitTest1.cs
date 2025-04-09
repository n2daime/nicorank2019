using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
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
using System.Linq;
using nicorankLib.Util.Text;

namespace UnitTest
{
    public class RankGenreJsonOld
    {
        [JsonProperty("genre")]
        public string Genre { get; set; }

        [JsonProperty("tag")]
        public object Tag { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("isGenreRank")]
        public bool isGenreRank { get; set; } = true;

        [JsonProperty("isTeibanRank")]
        public bool isTeibanRank { get; set; } = false;


        public static RankGenreJsonOld[] FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RankGenreJsonOld[]>(json, RankGenreJsonOld.Settings);
        }

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            }
        };

    }

    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestFixFileJson()
        {
            // file_name_list.jsonを読み取る
            var filePath = @"\\ds224\Temp\nicorankOld2025\old-ranking\total\2025-04-10_05\file_name_list.json";
            var json = System.IO.File.ReadAllText(filePath);

            // RankGenreJsonOld[]に変換
            var list = UnitTest.RankGenreJsonOld.FromJson(json);

            // RankGenreJsonOld[]をRankGenreJson[]に変換する
            var newList = new List<RankGenreJson>();
            foreach (var item in list)
            {
                var newItem = new RankGenreJson()
                {
                    Genre = item.Genre,
                    Tag = item.Tag,
                    File = item.File,
                    isGenre = item.isGenreRank,
                };
                if (item.Genre == "全ジャンル" )
                {
                    newItem.isGenre = false;
                }
                else if(item.isGenreRank)
                {
                    newItem.isGenre = true;
                }
                else
                {
                    if (item.Tag == null)
                    {
                        // タグがない場合は、ジャンルでないことが確定する
                        newItem.isGenre = false;
                    }
                    else
                    {
                        //親のジャンルを確認する
                        var work = list.Where(x => x.Tag != null && x.Genre == item.Genre).ToList()[0];
                        newItem.isGenre = work.isGenreRank;
                    }
                }
                newList.Add(newItem);
            }

            //file_name_list.jsonをnewListで上書きする
            string str_genre_jon = JsonConvert.SerializeObject(newList, Newtonsoft.Json.Formatting.None);

            using (TextUtil textUtil = new TextUtil())
            {
                if (textUtil.WriteOpen(filePath, true, true))
                {
                    textUtil.WriteText(str_genre_jon);
                }
                else
                {
                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"{filePath}の書き込みでエラー発生。(RankApi2Json::SaveOldRankingData)");
                }
            }



            //T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank2019\nicorank2019\bin\Debug\old-ranking\daily\2025-04-10
            //
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
