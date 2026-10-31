using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;

namespace UnitTest.nicorankLib.Analyze.Input
{
    /// <summary>
    /// Issue #54: 仮集計で作ったID列を直接受け取る経路の検証。
    /// ファイル経路と同じ正規化（前後空白除去・空ID除外）を通すことを担保する。
    /// </summary>
    [TestClass]
    public class UnitTestSPAnalyze
    {
        [TestMethod]
        public void ID列を受け取るとファイルを読まずにRankingを作る()
        {
            // ファイルパスは存在しないものを渡し、ID列経路では読まれないことを示す
            var analyzer = new SPAnalyze(
                new DateTime(2026, 12, 26),
                new List<string>() { "sm2", " sm1 ", "", null, "sm3" });

            Assert.IsTrue(analyzer.AnalyzeRank(out List<Ranking> list));
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("sm2", list[0].ID);
            Assert.AreEqual("sm1", list[1].ID);
            Assert.AreEqual("sm3", list[2].ID);
        }

        [TestMethod]
        public void ID列が空でも成功して空のRankingになる()
        {
            var analyzer = new SPAnalyze(new DateTime(2026, 12, 26), new List<string>());

            Assert.IsTrue(analyzer.AnalyzeRank(out List<Ranking> list));
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void ID列がnullでも例外にしない()
        {
            var analyzer = new SPAnalyze(new DateTime(2026, 12, 26), (IEnumerable<string>)null);

            Assert.IsTrue(analyzer.AnalyzeRank(out List<Ranking> list));
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void ファイルがなければ失敗する従来経路は変わらない()
        {
            // null と空リストの区別が崩れていないこと（null はファイル経路のまま）
            var analyzer = new SPAnalyze(new DateTime(2026, 12, 26), (string)null);

            Assert.IsFalse(analyzer.AnalyzeRank(out List<Ranking> list));
            Assert.AreEqual(0, list.Count);
        }
    }
}
