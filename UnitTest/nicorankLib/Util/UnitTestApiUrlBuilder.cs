using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using System;
using System.Collections.Generic;

namespace UnitTest.nicorankLib.Util
{
    [TestClass]
    public class UnitTestApiUrlBuilder
    {
        [TestMethod]
        public void Build_EncodesJapaneseValue()
        {
            // nvapi の日本語 tag と同条件。値がエンコードされクエリを破壊しないこと（Issue #19 横展開の中核）
            var query = new Dictionary<string, string>
            {
                { "term", "24h" },
                { "pageSize", "100" },
                { "page", "1" },
                { "tag", "ゆっくり解説&100%" }
            };

            string url = ApiUrlBuilder.Build("https://nvapi.nicovideo.jp/v1/ranking/teiban/v6wdx6p5", query);

            Assert.IsTrue(url.StartsWith("https://nvapi.nicovideo.jp/v1/ranking/teiban/v6wdx6p5?"), url);
            Assert.IsTrue(url.Contains("tag=" + Uri.EscapeDataString("ゆっくり解説&100%")), url);
            Assert.IsFalse(url.Contains("ゆっくり解説&100%"), url);
        }

        [TestMethod]
        public void Build_OmitsAbsentTagCleanly()
        {
            // tag省略分岐（term=24h/hour以外）のdict形状でも正しいURLになること
            var query = new Dictionary<string, string>
            {
                { "term", "week" },
                { "pageSize", "100" },
                { "page", "2" }
            };

            string url = ApiUrlBuilder.Build("https://nvapi.nicovideo.jp/v1/ranking/teiban/v6wdx6p5", query);

            Assert.IsFalse(url.Contains("tag="), url);
            Assert.IsTrue(url.Contains("term=week"), url);
            Assert.IsTrue(url.Contains("page=2"), url);
        }

        [TestMethod]
        public void Build_NullQuery_ReturnsBaseUrl()
        {
            string url = ApiUrlBuilder.Build("https://nvapi.nicovideo.jp/v2/genres", null);

            Assert.AreEqual("https://nvapi.nicovideo.jp/v2/genres", url);
        }

        [TestMethod]
        public void Build_EmptyQuery_ReturnsBaseUrl()
        {
            string url = ApiUrlBuilder.Build("https://nvapi.nicovideo.jp/v2/genres", new Dictionary<string, string>());

            Assert.AreEqual("https://nvapi.nicovideo.jp/v2/genres", url);
        }

        [TestMethod]
        public void Build_NullValue_BecomesEmpty()
        {
            var query = new Dictionary<string, string>
            {
                { "tag", null }
            };

            string url = ApiUrlBuilder.Build("https://example.com/api", query);

            Assert.AreEqual("https://example.com/api?tag=", url);
        }

        [TestMethod]
        public void Build_AppendsWithAmpersand_WhenBaseUrlHasQuery()
        {
            var query = new Dictionary<string, string>
            {
                { "page", "1" }
            };

            string url = ApiUrlBuilder.Build("https://example.com/api?_frontendId=6", query);

            Assert.AreEqual("https://example.com/api?_frontendId=6&page=1", url);
        }

        [TestMethod]
        public void Build_NullBaseUrl_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => ApiUrlBuilder.Build(null, new Dictionary<string, string>()));
        }
    }
}
