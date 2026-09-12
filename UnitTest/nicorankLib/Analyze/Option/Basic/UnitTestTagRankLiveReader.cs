using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option.Basic;
using nicorankLib.Factory;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Option.Basic
{
    [TestClass]
    public class UnitTestTagRankLiveReader
    {
        private static TagRankAnalyze LiveInput()
        {
            var input = new TagRankAnalyze(new DateTime(2024, 1, 1), new TagSearchQuery() { TagCondition = "A", UseLiveCounter = true });
            input.LiveCounters["sm1"] = new SnapShotJson.SnapShotJsonData() { ID = "sm1", CountPlay = 100, CountComment = 10, CountMylist = 5, CountLike = 2 };
            input.LiveCounters["sm2"] = new SnapShotJson.SnapShotJsonData() { ID = "sm2", CountPlay = 200, CountComment = 20, CountMylist = 6, CountLike = 3 };
            return input;
        }

        private static List<Ranking> RankingList(params string[] ids)
        {
            var list = new List<Ranking>();
            foreach (var id in ids)
            {
                list.Add(new Ranking() { ID = id });
            }
            return list;
        }

        private static string CreateBaseDbFile(int syuukeiBi)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
            File.Create(path).Dispose();
            var dbCtrl = new SQLiteCtrl();
            Assert.IsTrue(dbCtrl.Open(path), "temp db open");
            TestDbHelper.CreateSnapshotRankingTable(dbCtrl);
            TestDbHelper.CreateDBVersionTable(dbCtrl);
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO DBVersion(集計日, Ver) VALUES(@日, '1.0')";
                cmd.Parameters.AddWithValue("@日", syuukeiBi);
                cmd.ExecuteNonQuery();
            }
            dbCtrl.Close();
            return path;
        }

        [TestMethod]
        public void ApplyLiveTotals_MapsCountersAndMarksMissingDeleted()
        {
            var list = RankingList("sm1", "sm9");

            TagRankLiveTotalReader.ApplyLiveTotals(LiveInput(), list);

            Assert.AreEqual(100, list[0].CountPlayTotal);
            Assert.AreEqual(10, list[0].CountCommentTotal);
            Assert.AreEqual(5, list[0].CountMyListTotal);
            Assert.AreEqual(2, list[0].CountLikeTotal);
            Assert.IsFalse(list[0].isDelete);
            Assert.IsTrue(list[1].isDelete);
        }

        [TestMethod]
        public void LiveTotalReader_Open_NullInput_ReturnsFalse()
        {
            var reader = new TagRankLiveTotalReader(null, DateTime.Today);

            Assert.IsFalse(reader.Open());
        }

        [TestMethod]
        public void LiveTotalReader_Open_WithQuery_ReturnsTrue()
        {
            var reader = new TagRankLiveTotalReader(LiveInput(), DateTime.Today);

            Assert.IsTrue(reader.Open());
            Assert.AreEqual(DateTime.Today, reader.AnalyzeTime);
        }

        [TestMethod]
        public void LiveSabunReader_Open_MissingBaseDB_ReturnsFalse()
        {
            var reader = new TagRankLiveSabunReader(
                LiveInput(), DateTime.Today,
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"));

            Assert.IsFalse(reader.Open());
        }

        [TestMethod]
        public void LiveSabunReader_Open_WithBaseDB_ReturnsTrueAndBaseTime()
        {
            string path = CreateBaseDbFile(20240101);
            try
            {
                using (var reader = new TagRankLiveSabunReader(LiveInput(), DateTime.Today, path))
                {
                    Assert.IsTrue(reader.Open());
                    Assert.AreEqual(new DateTime(2024, 1, 1), reader.BaseTime);
                    Assert.AreEqual(DateTime.Today, reader.AnalyzeTime);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ModeFactoryTagRank_LiveWithoutBaseDB_ReturnsTrueAndToday()
        {
            var factory = new ModeFactoryTagRank();
            factory.SetInputFile(
                "",
                "",
                new TagSearchQuery() { TagCondition = "A", UseLiveCounter = true },
                "");

            // DBを開かないため存在確認なしで成功すること（集計日は実行日）
            Assert.IsTrue(factory.CreateAnalyzer());
            Assert.AreEqual(DateTime.Today, factory.TargetDay);
            Assert.AreEqual(DateTime.Today, factory.BaseDay);
        }

        [TestMethod]
        public void ModeFactoryTagRank_LiveWithMissingBaseDB_ReturnsFalse()
        {
            var factory = new ModeFactoryTagRank();
            factory.SetInputFile(
                "",
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                new TagSearchQuery() { TagCondition = "A", UseLiveCounter = true },
                "");

            // 例外なく false を返すこと（v2最新値の差分あり経路）
            Assert.IsFalse(factory.CreateAnalyzer());
        }

        // 工場の基準DBあり成功系は、工場がReaderを保持し続ける設計上テスト後の実DB削除がロック競合するため、
        // 成功系の検証はReader単体のOpen_WithBaseDB（using破棄あり）に集約し、工場側は失敗系のみとする（既存の前例と同一方針）
    }
}
