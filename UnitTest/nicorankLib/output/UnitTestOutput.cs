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

        private List<Ranking> CreateTagRankingList()
        {
            return new List<Ranking>
            {
                new Ranking
                {
                    ID = "sm33333333",
                    Title = "テスト動画C",
                    Date = new System.DateTime(2023, 1, 3),
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
                    Category = "カテゴリX",
                    UserID = "user3",
                    UserName = "テストユーザーC",
                    PlayTime = "5分00秒",
                    FavoriteTags = new List<string> { "タグ1", "タグ2", "タグ3", "タグ4", "タグ5" }
                }
            };
        }

        [TestMethod]
        public void TestNrmOutput_タグ上限3で打ち切る()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_NrmOutput3_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var nrm = new NrmOutput();
                nrm.Set(tempDir, "test_rank.txt", 0, 1, false, 3);

                Assert.IsTrue(nrm.Execute(CreateTagRankingList()));

                var content = File.ReadAllText(Path.Combine(tempDir, "test_rank.txt"));
                Assert.IsTrue(content.Contains("タグ1,タグ2,タグ3"));
                Assert.IsFalse(content.Contains("タグ4"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestNrmOutput_上限なしは全件出力する()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_NrmOutputAll_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var nrm = new NrmOutput();
                nrm.Set(tempDir, "test_rank.txt", 0, 1);

                Assert.IsTrue(nrm.Execute(CreateTagRankingList()));

                var content = File.ReadAllText(Path.Combine(tempDir, "test_rank.txt"));
                Assert.IsTrue(content.Contains("タグ1,タグ2,タグ3,タグ4,タグ5"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestNrmOutput_カテゴリ同名タグを除外する()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_NrmOutputCat_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var nrm = new NrmOutput();
                nrm.Set(tempDir, "test_rank.txt", 0, 1);

                var rankingList = CreateTagRankingList();
                rankingList[0].Category = "タグ1";
                rankingList[0].FavoriteTags = new List<string> { "タグ1", "タグA" };

                Assert.IsTrue(nrm.Execute(rankingList));

                var content = File.ReadAllText(Path.Combine(tempDir, "test_rank.txt"));
                Assert.IsTrue(content.Contains("タグA"));
                Assert.IsFalse(content.Contains("タグ1,タグA"));
                Assert.IsFalse(content.Contains("タグA,タグ1"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestResultCsv_人気のタグ列を出力する()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_ResultCsvTag_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var csv = new ResultCsv();
                csv.SetOutput(tempDir, new List<ResultCsv.CsvConfig>
                {
                    new ResultCsv.CsvConfig { csvName = "test_result.csv", isUnicode = true, isOverwrite = true }
                });

                var rankingList = CreateTagRankingList();
                rankingList[0].Category = "音楽";
                rankingList[0].FavoriteTags = new List<string> { "音楽", "演奏してみた" };

                Assert.IsTrue(csv.Execute(rankingList));

                var content = File.ReadAllText(Path.Combine(tempDir, "test_result.csv"));
                Assert.IsTrue(content.Contains("人気のタグ"));
                Assert.IsTrue(content.Contains("\"演奏してみた\""));
                Assert.IsFalse(content.Contains("\"音楽,演奏してみた\""));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
