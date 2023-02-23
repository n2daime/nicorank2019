using nicorankLib.Analyze.Official;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorank_aggregation
{
    class Program
    {
        static void Main(string[] args)
        {
            StatusLog.SetLogWriter(new ConsolWriter());
            //var testObj = new RankingHistory();
            //testObj.Open();

            //testObj.UpdateOfficialRankingDB();

            //testObj.Close();

            var test = new SnapShotDB();
            test.GetJsonData(@"T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank2019\UnitTest\bin\Test\Snap20190701", out var dataList);

            test.RegistDB(dataList, DateConvert.String2Time("20190701", false));

            //var testObj = new SnapShotAnalyze();
            //DateTime dateTime = DateConvert.String2Time("20070301", false);
            //while (dateTime < DateTime.Now)
            //{
            //    StatusLog.WriteLine(DateConvert.Time2String(dateTime,false));
            //    testObj.AnalyzeRank(dateTime);
            //    dateTime = dateTime.AddDays(1);
            //}
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
