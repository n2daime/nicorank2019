using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitTest.nicorankLib.Analyze
{
    [TestClass]
    public class UnitTestRankingAnalyze
    {
        // なぜフェイクInputを使うか：calcRankingはprotectedのため直接呼べない。
        // 公開経路のAnalyzeRankに固定リストを流し、入力順を入れ替えても結果が同一になることで
        // タイブレークの決定的順序を検証する（tasks.md 1.2の方針と同一手法）。
        private class StubInput : InputBase
        {
            private readonly List<Ranking> data;
            private DateTime day = new DateTime(2026, 9, 8);

            public StubInput(List<Ranking> data)
            {
                this.data = data;
            }

            public override bool AnalyzeRank(out List<Ranking> rakingList)
            {
                rakingList = data;
                return true;
            }

            public override DateTime getAnalyzeDay()
            {
                return day;
            }

            public override void setAnalyzeDay(DateTime analyzeDay)
            {
                day = analyzeDay;
            }
        }

        [TestInitialize]
        public void Init()
        {
            // ポイント計算を決定的にするためUnitTestRankingと同一の設定にする
            var config = Config.GetInstance();
            config.IsSP = false;
            config.CalcPointAllKind = 0;
            config.CalcCommentKind = 1;
            config.CalcCommentUnderLimit = 0.01;
            config.CalcMyListKind = 1;
            config.CalcPlayKind = 2;
            config.CalcMyList = 40;
            config.CalcPlay = 1;
            config.CalcComment = 1;
            config.CalcLike = 10;
        }

        private static Ranking NewRank(string id)
        {
            // 全件同一の数値にし、ポイント・4数値のすべてで同点にする
            return new Ranking()
            {
                ID = id,
                Category = "テスト",
                CountPlay = 10000,
                CountComment = 100,
                CountMyList = 200,
                CountLike = 300,
            };
        }

        private static Dictionary<string, Ranking> RunAnalyze(params string[] idsInInputOrder)
        {
            var list = idsInInputOrder.Select(id => NewRank(id)).ToList();
            var analyzer = new RankingAnalyze(new StubInput(list));
            bool ok = analyzer.AnalyzeRank(out List<Ranking> result);
            Assert.IsTrue(ok);
            return result.ToDictionary(r => r.ID);
        }

        [TestMethod]
        public void IdComparer_NumericOrder_Sm20BeforeSm199()
        {
            // 辞書式だと sm199 < sm20 になるが、数値認識では 20 < 199 でなければならない
            Assert.IsTrue(RankingIdComparer.Instance.Compare("sm20", "sm199") < 0);
            Assert.IsTrue(RankingIdComparer.Instance.Compare("sm199", "sm20") > 0);
        }

        [TestMethod]
        public void IdComparer_NumericOrder_DigitLengthBoundary()
        {
            // 桁数境界（999/1000）でも数値順になること
            Assert.IsTrue(RankingIdComparer.Instance.Compare("sm999", "sm1000") < 0);
            Assert.IsTrue(RankingIdComparer.Instance.Compare("sm1000", "sm999") > 0);
        }

        [TestMethod]
        public void IdComparer_PrefixOrder_SmBeforeSo()
        {
            // 種別が違えば種別の辞書式で決まる（sm < so）。数字の大小より種別が優先される
            Assert.IsTrue(RankingIdComparer.Instance.Compare("sm1", "so1") < 0);
            Assert.IsTrue(RankingIdComparer.Instance.Compare("so1", "sm1") > 0);
        }

        [TestMethod]
        public void IdComparer_Fallback_NonNumeric()
        {
            // 数字化できないIDは例外にせず辞書式フォールバックで決定的になること
            Assert.AreEqual(0, RankingIdComparer.Instance.Compare("smABC", "smABC"));
            Assert.IsTrue(RankingIdComparer.Instance.Compare("smABC", "smABD") < 0);
            // 片方だけ数字化できなくても決定的に比べられること（例外なし）
            Assert.IsNotNull(RankingIdComparer.Instance.Compare("sm10", "smABC").ToString());
        }

        [TestMethod]
        public void IdComparer_NullAndIdentical()
        {
            Assert.AreEqual(0, RankingIdComparer.Instance.Compare(null, null));
            Assert.IsTrue(RankingIdComparer.Instance.Compare(null, "sm1") < 0);
            Assert.IsTrue(RankingIdComparer.Instance.Compare("sm1", null) > 0);
            Assert.AreEqual(0, RankingIdComparer.Instance.Compare("sm1", "sm1"));
        }

        [TestMethod]
        public void AnalyzeRank_TotalRankFollowsNumericIdOrder()
        {
            // 同点3件の総合順位が数値順（sm3 < sm20 < sm199）になること
            var byId = RunAnalyze("sm199", "sm20", "sm3");

            Assert.AreEqual(1, byId["sm3"].RankTotal);
            Assert.AreEqual(2, byId["sm20"].RankTotal);
            Assert.AreEqual(3, byId["sm199"].RankTotal);
        }

        [TestMethod]
        public void AnalyzeRank_TieIsDeterministicRegardlessOfInputOrder()
        {
            // 入力順を反転しても6種すべての順位が同一になること（実行ごとの不定順序の解消）
            var first = RunAnalyze("sm199", "sm20", "sm3");
            var second = RunAnalyze("sm3", "sm20", "sm199");

            foreach (var id in new[] { "sm3", "sm20", "sm199" })
            {
                Assert.AreEqual(first[id].RankTotal, second[id].RankTotal, $"RankTotal {id}");
                Assert.AreEqual(first[id].RankPlay, second[id].RankPlay, $"RankPlay {id}");
                Assert.AreEqual(first[id].RankComment, second[id].RankComment, $"RankComment {id}");
                Assert.AreEqual(first[id].RankMyList, second[id].RankMyList, $"RankMyList {id}");
                Assert.AreEqual(first[id].RankLike, second[id].RankLike, $"RankLike {id}");
                Assert.AreEqual(first[id].RankCategory, second[id].RankCategory, $"RankCategory {id}");
            }
        }

        [TestMethod]
        public void AnalyzeRank_SubRanksFollowNumericIdOrder()
        {
            // 4数値も同点のため、再生・コメント・マイリスト・いいね・カテゴリも数値順になること
            var byId = RunAnalyze("sm199", "sm20", "sm3");

            Assert.AreEqual(1, byId["sm3"].RankPlay);
            Assert.AreEqual(2, byId["sm20"].RankPlay);
            Assert.AreEqual(3, byId["sm199"].RankPlay);

            Assert.AreEqual(1, byId["sm3"].RankComment);
            Assert.AreEqual(1, byId["sm3"].RankMyList);
            Assert.AreEqual(1, byId["sm3"].RankLike);
            Assert.AreEqual(1, byId["sm3"].RankCategory);
        }
    }
}
