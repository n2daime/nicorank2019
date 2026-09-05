using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.output;
using nicorankLib.Util.Text;
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

        [TestMethod]
        public void TestResultCsv_Header30ColumnsExact()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_ResultCsvHeader_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var csv = new ResultCsv();
                csv.SetOutput(tempDir, new List<ResultCsv.CsvConfig>
                {
                    new ResultCsv.CsvConfig { csvName = "test_result.csv", isUnicode = true, isOverwrite = true }
                });

                Assert.IsTrue(csv.Execute(CreateTestRankingList()));
                Assert.IsTrue(CsvUtil.Read(Path.Combine(tempDir, "test_result.csv"), out List<string[]> rows));
                Assert.IsTrue(rows.Count >= 1);

                var expected = new[]
                {
                    "ID","投稿日","タイトル","人気のタグ","再生時間",
                    "総合ランク","ポイント","カテゴリランク","カテゴリ","再生ランク","再生数",
                    "コメントランク","コメント数","マイリストランク","登録数","いいねランク","いいね数","前回ランク","前回ポイント",
                    "ユーザーID","ユーザー名","ユーザーアイコン","マイリストポイント",
                    "コメント補正","コメントポイント","マイリスト補正",
                    "再生補正","再生ポイント","いいねポイント","ポイント全体補正"
                };
                CollectionAssert.AreEqual(expected, rows[0]);
                Assert.AreEqual(30, rows[0].Length);
                Assert.AreEqual("人気のタグ", rows[0][3]);
                Assert.IsFalse(new List<string>(rows[0]).Contains("運営ポイントランク"));
                Assert.IsFalse(new List<string>(rows[0]).Contains("運営ポイント"));
                if (rows.Count >= 2)
                {
                    Assert.AreEqual(30, rows[1].Length);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void TestResultCsv_Roundtrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_ResultCsvRoundtrip_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var csv = new ResultCsv();
                csv.SetOutput(tempDir, new List<ResultCsv.CsvConfig>
                {
                    new ResultCsv.CsvConfig { csvName = "test_result.csv", isUnicode = true, isOverwrite = true }
                });

                var rankingList = new List<Ranking>
                {
                    new Ranking
                    {
                        ID = "sm12345678",
                        Title = "ラウンドトリップ動画",
                        Date = new DateTime(2023, 5, 6, 12, 34, 56),
                        PlayTime = "10分00秒",
                        RankTotal = 7,
                        RankCategory = 3,
                        RankPlay = 4,
                        RankComment = 5,
                        RankMyList = 6,
                        RankLike = 8,
                        CountPlay = 20000,
                        CountComment = 300,
                        CountMyList = 500,
                        CountLike = 400,
                        Category = "ゲーム",
                        FavoriteTags = new List<string> { "実況", "攻略" },
                        LastRank = 8,
                        LastPoint = 12345,
                        UserID = "user9",
                        UserName = "ユーザー九"
                    }
                };

                Assert.IsTrue(csv.Execute(rankingList));

                var csvPath = Path.Combine(tempDir, "test_result.csv");
                Assert.IsTrue(TextUtil.ReadCsv(csvPath, out List<Ranking> readList));
                Assert.AreEqual(1, readList.Count);

                var original = rankingList[0];
                var read = readList[0];
                Assert.AreEqual(original.ID, read.ID);
                Assert.AreEqual(original.Title, read.Title);
                Assert.AreEqual(original.Date, read.Date);
                Assert.AreEqual(original.Category, read.Category);
                Assert.AreEqual(original.RankTotal, read.RankTotal);
                Assert.AreEqual(original.PointTotal, read.PointTotal);
                Assert.AreEqual(0, read.PointMyList); // マイリストポイントは読取対象外（再計算するため）のため既定値
                Assert.AreEqual(original.CountPlay, read.CountPlay);
                Assert.AreEqual(original.CountComment, read.CountComment);
                Assert.AreEqual(original.CountMyList, read.CountMyList);
                Assert.AreEqual(original.CountLike, read.CountLike);
                Assert.AreEqual(original.RankLike, read.RankLike);
                Assert.AreEqual(original.LastRank, read.LastRank);
                Assert.AreEqual(original.LastPoint, read.LastPoint);
                Assert.AreEqual(original.UserID, read.UserID);
                Assert.AreEqual(original.UserName, read.UserName);
                CollectionAssert.AreEqual(new List<string> { "実況", "攻略" }, read.FavoriteTags);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
