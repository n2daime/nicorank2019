using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.output;
using System;
using System.Collections.Generic;
using System.IO;

namespace UnitTest.nicorankLib.output
{
    [TestClass]
    public class UnitTestOutput
    {
        private List<Ranking> CreateTestRankingList()
        {
            return new List<Ranking>
            {
                new Ranking
                {
                    ID = "sm11111111",
                    Title = "テスト動画A",
                    Date = new System.DateTime(2023, 1, 1),
                    CountPlay = 100000,
                    CountComment = 500,
                    CountMyList = 2000,
                    CountLike = 3000,
                    RankTotal = 1,
                    RankCategory = 1,
                    RankPlay = 1,
                    RankComment = 1,
                    RankMyList = 1,
                    RankLike = 1,
                    Category = "音楽",
                    UserID = "user1",
                    UserName = "テストユーザーA",
                    PlayTime = "5分00秒"
                },
                new Ranking
                {
                    ID = "sm22222222",
                    Title = "テスト動画B",
                    Date = new System.DateTime(2023, 1, 2),
                    CountPlay = 50000,
                    CountComment = 1000,
                    CountMyList = 1000,
                    CountLike = 2000,
                    RankTotal = 2,
                    RankCategory = 2,
                    RankPlay = 2,
                    RankComment = 2,
                    RankMyList = 2,
                    RankLike = 2,
                    Category = "ゲーム",
                    UserID = "user2",
                    UserName = "テストユーザーB",
                    PlayTime = "3分30秒"
                }
            };
        }

        [TestMethod]
        public void TestResultCsv_Output()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_ResultCsv_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var csv = new ResultCsv();
                csv.SetOutput(tempDir, new List<ResultCsv.CsvConfig>
                {
                    new ResultCsv.CsvConfig { csvName = "test_result.csv", isUnicode = true, isOverwrite = true }
                });

                var rankingList = CreateTestRankingList();
                var result = csv.Execute(rankingList);

                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "test_result.csv")));

                var content = File.ReadAllText(Path.Combine(tempDir, "test_result.csv"));
                Assert.IsTrue(content.Contains("sm11111111"));
                Assert.IsTrue(content.Contains("sm22222222"));
                Assert.IsTrue(content.Contains("テスト動画A"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestNrmOutput_Output()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_NrmOutput_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var nrm = new NrmOutput();
                nrm.Set(tempDir, "test_rank.txt", 0, 2);

                var rankingList = CreateTestRankingList();
                var result = nrm.Execute(rankingList);

                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, "test_rank.txt")));

                var content = File.ReadAllText(Path.Combine(tempDir, "test_rank.txt"));
                Assert.IsTrue(content.Contains("sm11111111"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
