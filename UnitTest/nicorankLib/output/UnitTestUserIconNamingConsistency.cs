using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.output;
using System;
using System.Collections.Generic;
using System.IO;

namespace UnitTest.nicorankLib.output
{
    /// <summary>
    /// ユーザーアイコンのファイル名が、一覧出力（rank.txt 等）・CSV・直接DLで一致することを保証する（Issue #56）。
    /// なぜ必要か: 一覧やCSVが指すファイル名と、実際に保存されるファイル名がずれると、
    /// 後工程でアイコンがリンクされない。以前は NrmOutput だけが UserID 3文字未満を
    /// 「NoImage.jpg」にしていたため、同じユーザーでも出力ごとに名前が違い得た。
    /// 現在は3箇所とも ImageFileNaming.GetUserIconFileName を呼ぶ。このテストはその契約を出力結果で縛る。
    /// </summary>
    [TestClass]
    public class UnitTestUserIconNamingConsistency
    {
        private static Ranking CreateRanking(string id, string userId)
        {
            return new Ranking()
            {
                ID = id,
                Title = "テスト動画",
                Date = new DateTime(2023, 1, 1),
                CountPlay = 100,
                CountComment = 10,
                CountMyList = 5,
                CountLike = 3,
                RankTotal = 1,
                RankCategory = 1,
                RankPlay = 1,
                RankComment = 1,
                RankMyList = 1,
                RankLike = 1,
                Category = "音楽",
                UserID = userId,
                UserName = "テストユーザー",
                PlayTime = "1分00秒",
                FavoriteTags = new List<string>(),
            };
        }

        private static List<Ranking> CreateRankingList()
        {
            return new List<Ranking>()
            {
                CreateRanking("sm1", "12"),    // 旧 NrmOutput では NoImage.jpg になっていた短いID
                CreateRanking("so1", "345"),   // チャンネル動画
                CreateRanking("sm3", ""),      // ユーザーIDなし
            };
        }

        [TestMethod]
        public void NrmOutput_UsesSameIconNameAsDownloader()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_IconName_Nrm_" + Guid.NewGuid().ToString("N"));
            try
            {
                var nrm = new NrmOutput();
                nrm.Set(tempDir, "test_rank.txt", 0, 3);

                Assert.IsTrue(nrm.Execute(CreateRankingList()));

                string content = File.ReadAllText(Path.Combine(tempDir, "test_rank.txt"));
                Assert.IsTrue(content.Contains("12.jpg"), "短いUserIDでも {UserID}.jpg になること");
                Assert.IsTrue(content.Contains("ch345.jpg"), "チャンネルは ch 接頭辞になること");
                Assert.IsFalse(content.Contains("NoImage.jpg"), "旧プレースホルダ名が残っていないこと");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void ResultCsv_UsesSameIconNameAsDownloader()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "UnitTest_IconName_Csv_" + Guid.NewGuid().ToString("N"));
            try
            {
                var csv = new ResultCsv();
                csv.SetOutput(tempDir, new List<ResultCsv.CsvConfig>()
                {
                    new ResultCsv.CsvConfig() { csvName = "test_result.csv", isUnicode = true, isOverwrite = true }
                });

                Assert.IsTrue(csv.Execute(CreateRankingList()));

                string content = File.ReadAllText(Path.Combine(tempDir, "test_result.csv"));
                Assert.IsTrue(content.Contains("12.jpg"));
                Assert.IsTrue(content.Contains("ch345.jpg"));
                Assert.IsFalse(content.Contains("NoImage.jpg"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
