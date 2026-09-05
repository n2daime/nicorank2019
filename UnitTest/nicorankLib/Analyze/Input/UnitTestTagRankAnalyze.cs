using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.SnapShot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnitTest.nicorankLib.Analyze.Input
{
    [TestClass]
    public class UnitTestTagRankAnalyze
    {
        private class StubTagRankAnalyze : TagRankAnalyze
        {
            public string CountJson;
            public readonly Dictionary<long, string> Pages = new Dictionary<long, string>();

            public StubTagRankAnalyze(TagSearchQuery query)
                : base(new DateTime(2024, 1, 1), query)
            {
            }

            protected override bool DownloadText(string url, out string text)
            {
                if (url.Contains("_limit=0"))
                {
                    text = CountJson;
                    return CountJson != null;
                }
                var match = Regex.Match(url, "_offset=(\\d+)");
                long offset = match.Success ? long.Parse(match.Groups[1].Value) : 0;
                if (Pages.TryGetValue(offset, out text))
                {
                    return true;
                }
                text = null;
                return false;
            }
        }

        private static string CountJson(long total)
        {
            return "{\"meta\":{\"status\":200,\"totalCount\":" + total + "},\"data\":[]}";
        }

        private static string PageJson(long total, params string[] ids)
        {
            var data = new List<string>();
            foreach (var id in ids)
            {
                data.Add("{\"contentId\":\"" + id + "\",\"commentCounter\":1,\"viewCounter\":2,\"mylistCounter\":3,\"likeCounter\":4}");
            }
            return "{\"meta\":{\"status\":200,\"totalCount\":" + total + "},\"data\":[" + string.Join(",", data) + "]}";
        }

        private static TagSearchQuery Query(string condition)
        {
            return new TagSearchQuery() { TagCondition = condition };
        }

        [TestMethod]
        public void GetTotalCount_ReturnsCount()
        {
            var analyzer = new StubTagRankAnalyze(Query("A")) { CountJson = CountJson(250) };

            bool ok = analyzer.GetTotalCount(out long total);

            Assert.IsTrue(ok);
            Assert.AreEqual(250, total);
        }

        [TestMethod]
        public void AnalyzeRank_DedupesIdsAcrossPages()
        {
            var analyzer = new StubTagRankAnalyze(Query("A&B|C*")) { CountJson = CountJson(250) };
            analyzer.Pages[0] = PageJson(250, "sm2", "sm1");
            analyzer.Pages[100] = PageJson(250, "sm2", "sm3");
            analyzer.Pages[200] = PageJson(250, "sm3");

            bool ok = analyzer.AnalyzeRank(out List<Ranking> list);

            Assert.IsTrue(ok);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("sm1", list[0].ID);
            Assert.AreEqual("sm2", list[1].ID);
            Assert.AreEqual("sm3", list[2].ID);
        }

        [TestMethod]
        public void AnalyzeRank_OverLimit_ReturnsFalse()
        {
            var analyzer = new StubTagRankAnalyze(Query("A")) { CountJson = CountJson(50001) };

            bool ok = analyzer.AnalyzeRank(out List<Ranking> list);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void AnalyzeRank_CountFails_ReturnsFalse()
        {
            var analyzer = new StubTagRankAnalyze(Query("A")) { CountJson = null };

            bool ok = analyzer.AnalyzeRank(out List<Ranking> list);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void AnalyzeRank_InvalidCondition_ReturnsFalse()
        {
            var analyzer = new StubTagRankAnalyze(Query("")) { CountJson = CountJson(10) };

            bool ok = analyzer.AnalyzeRank(out List<Ranking> list);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void AnalyzeRank_PageFails_ReturnsFalse()
        {
            var analyzer = new StubTagRankAnalyze(Query("A")) { CountJson = CountJson(100) };
            // ページ応答なし → リトライ上限で失敗

            bool ok = analyzer.AnalyzeRank(out List<Ranking> list);

            Assert.IsFalse(ok);
        }
    }
}
