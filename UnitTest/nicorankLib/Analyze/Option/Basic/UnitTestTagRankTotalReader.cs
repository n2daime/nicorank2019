using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Option.Basic;
using nicorankLib.Factory;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.IO;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Option.Basic
{
    [TestClass]
    public class UnitTestTagRankTotalReader
    {
        private static string CreateSnapshotDbFile(int syuukeiBi)
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
            using (var cmd = dbCtrl.Connection.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO Ranking(ID, 再生数, コメント数, マイリスト数, いいね数) VALUES('sm1', 100, 10, 5, 2)";
                cmd.ExecuteNonQuery();
            }
            dbCtrl.Close();
            return path;
        }

        [TestMethod]
        public void Open_MissingFile_ReturnsFalse()
        {
            var reader = new TagRankTotalReader(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"));

            Assert.IsFalse(reader.Open());
        }

        [TestMethod]
        public void Open_WithDBVersion_ReturnsTrueAndAnalyzeTime()
        {
            string path = CreateSnapshotDbFile(20240101);
            try
            {
                using (var reader = new TagRankTotalReader(path))
                {
                    Assert.IsTrue(reader.Open());
                    Assert.AreEqual(new DateTime(2024, 1, 1), reader.AnalyzeTime);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ModeFactoryTagRank_WithoutBaseDB_MissingAnalyzeDB_ReturnsFalse()
        {
            var factory = new ModeFactoryTagRank();
            factory.SetInputFile(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                "",
                new TagSearchQuery() { TagCondition = "A" },
                "");

            // 例外なく false を返すこと（差分なし経路）
            Assert.IsFalse(factory.CreateAnalyzer());
        }

        [TestMethod]
        public void ModeFactoryTagRank_WithBaseDB_MissingFiles_ReturnsFalse()
        {
            var factory = new ModeFactoryTagRank();
            factory.SetInputFile(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db"),
                new TagSearchQuery() { TagCondition = "A" },
                "");

            // 例外なく false を返すこと（差分あり経路）
            Assert.IsFalse(factory.CreateAnalyzer());
        }
    }
}
