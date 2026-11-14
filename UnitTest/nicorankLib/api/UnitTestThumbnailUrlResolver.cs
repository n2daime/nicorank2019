using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.api;
using System.Collections.Generic;
using System.Net;

namespace UnitTest.nicorankLib.api
{
    /// <summary>
    /// ThumbnailUrlResolver のテスト（Issue #56）。
    /// 視聴ページHTMLの解析とURL候補の優先順位は外部仕様の解釈が中心なので、
    /// ネットワークを使わず文字列だけで検証できるようにしてある。
    /// 実測値は docs/knowledge/thumbnail.md を参照。
    /// </summary>
    [TestClass]
    public class UnitTestThumbnailUrlResolver
    {
        private const string WatchJson =
            "{\"data\":{\"response\":{\"$watchV4\":{\"data\":{\"video\":{\"thumbnail\":{"
            + "\"normal\":\"https://example/normal\","
            + "\"middle\":\"https://example/middle.M\","
            + "\"large\":\"https://example/large.L\","
            + "\"player\":\"https://example/player?key=1\","
            + "\"ogp\":\"https://example/ogp?key=2\","
            + "\"short\":\"https://example/short?key=3\""
            + "}}}}}}}";

        private static string BuildServerResponseHtml(string json)
        {
            // 実際のHTMLは content="" の中に JSON が HTML エスケープされて入る。
            return "<html><head><meta name=\"server-response\" content=\""
                + WebUtility.HtmlEncode(json)
                + "\" /></head><body></body></html>";
        }

        [TestMethod]
        public void ParseServerResponse_ExtractsThumbnailUrls()
        {
            ThumbnailPageInfo info = ThumbnailUrlResolver.ParseServerResponse(BuildServerResponseHtml(WatchJson));

            Assert.IsNotNull(info);
            Assert.AreEqual("https://example/large.L", info.Large);
            Assert.AreEqual("https://example/player?key=1", info.Player);
            Assert.AreEqual("https://example/ogp?key=2", info.Ogp);
            Assert.AreEqual("https://example/short?key=3", info.Short);
        }

        [TestMethod]
        public void ParseServerResponse_AttributeOrderReversed_StillExtracts()
        {
            // 属性の順序が変わっても拾えること（name が後ろ）
            string html = "<html><head><meta content=\""
                + WebUtility.HtmlEncode(WatchJson)
                + "\" name=\"server-response\" /></head></html>";

            ThumbnailPageInfo info = ThumbnailUrlResolver.ParseServerResponse(html);

            Assert.IsNotNull(info);
            Assert.AreEqual("https://example/ogp?key=2", info.Ogp);
        }

        [TestMethod]
        public void ParseServerResponse_MissingMeta_ReturnsNull()
        {
            Assert.IsNull(ThumbnailUrlResolver.ParseServerResponse("<html><body>no meta</body></html>"));
        }

        [TestMethod]
        public void ParseServerResponse_BrokenJson_ReturnsNull()
        {
            string html = "<meta name=\"server-response\" content=\"not-json\" />";
            Assert.IsNull(ThumbnailUrlResolver.ParseServerResponse(html));
        }

        [TestMethod]
        public void ParseServerResponse_FallsBackToOgImage()
        {
            // server-response が無い場合の保険。goptim の 1280x720 が og:image に入る。
            string html = "<html><head><meta property=\"og:image\" content=\"https://example/og.jpg?a=1&amp;b=2\" /></head></html>";

            ThumbnailPageInfo info = ThumbnailUrlResolver.ParseServerResponse(html);

            Assert.IsNotNull(info);
            Assert.AreEqual("https://example/og.jpg?a=1&b=2", info.Ogp);
        }

        [TestMethod]
        public void NormalizeToBaseUrl_StripsSizeSuffix()
        {
            Assert.AreEqual("https://example/12345", ThumbnailUrlResolver.NormalizeToBaseUrl("https://example/12345.L"));
            Assert.AreEqual("https://example/12345", ThumbnailUrlResolver.NormalizeToBaseUrl("https://example/12345.M"));
        }

        [TestMethod]
        public void NormalizeToBaseUrl_KeepsRandomSuffixAndStripsQuery()
        {
            // サムネイル変更済みの動画は {n}.{rand} が base。クエリは落とす。
            Assert.AreEqual(
                "https://example/12345.35949673",
                ThumbnailUrlResolver.NormalizeToBaseUrl("https://example/12345.35949673.L?key=x"));
        }

        [TestMethod]
        public void NormalizeToBaseUrl_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ThumbnailUrlResolver.NormalizeToBaseUrl(null));
            Assert.AreEqual(string.Empty, ThumbnailUrlResolver.NormalizeToBaseUrl("  "));
        }

        [TestMethod]
        public void BuildCandidateUrls_Default_PrefersLThenBase()
        {
            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildCandidateUrls(
                "https://example/12345", null, false);

            CollectionAssert.AreEqual(
                new[] { "https://example/12345.L", "https://example/12345" },
                new List<string>(urls));
        }

        [TestMethod]
        public void BuildCandidateUrls_Default_DoesNotDoubleSuffix()
        {
            // 週刊の ThumbnailURL は既に .L のことがある。...L.L にしてはいけない。
            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildCandidateUrls(
                "https://example/12345.L", null, false);

            CollectionAssert.AreEqual(
                new[] { "https://example/12345.L", "https://example/12345" },
                new List<string>(urls));
        }

        [TestMethod]
        public void BuildCandidateUrls_HighQuality_PrefersOgpThenPlayerThenLThenBase()
        {
            var page = new ThumbnailPageInfo()
            {
                Ogp = "https://example/ogp",
                Player = "https://example/player",
            };

            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildCandidateUrls(
                "https://example/12345", page, true);

            CollectionAssert.AreEqual(
                new[]
                {
                    "https://example/ogp",
                    "https://example/player",
                    "https://example/12345.L",
                    "https://example/12345",
                },
                new List<string>(urls));
        }

        [TestMethod]
        public void BuildCandidateUrls_HighQualityWithShort_PrefersShortFirst()
        {
            var page = new ThumbnailPageInfo()
            {
                Short = "https://example/short",
                Ogp = "https://example/ogp",
                Player = "https://example/player",
            };

            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildCandidateUrls(
                "https://example/12345", page, true);

            // short はショート動画だけに入る。入っていれば最優先（405x720）。
            Assert.AreEqual("https://example/short", urls[0]);
            Assert.AreEqual("https://example/ogp", urls[1]);
        }

        [TestMethod]
        public void BuildCandidateUrls_HighQualityWithoutPage_FallsBackToDefault()
        {
            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildCandidateUrls(
                "https://example/12345", null, true);

            CollectionAssert.AreEqual(
                new[] { "https://example/12345.L", "https://example/12345" },
                new List<string>(urls));
        }

        [TestMethod]
        public void BuildCandidateUrls_DuplicateCandidate_AddedOnce()
        {
            // ogp が .L と同じURLを指す場合でも二重にリクエストしない。
            var page = new ThumbnailPageInfo() { Ogp = "https://example/12345.L" };

            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildCandidateUrls(
                "https://example/12345", page, true);

            Assert.AreEqual(2, urls.Count);
            Assert.AreEqual("https://example/12345.L", urls[0]);
            Assert.AreEqual("https://example/12345", urls[1]);
        }

        [TestMethod]
        public void BuildCandidateUrls_EmptyUrl_ReturnsEmpty()
        {
            Assert.AreEqual(0, ThumbnailUrlResolver.BuildCandidateUrls(null, null, false).Count);
        }

        [TestMethod]
        public void BuildUserIconCandidateUrls_ReturnsSingleUrl()
        {
            IReadOnlyList<string> urls = ThumbnailUrlResolver.BuildUserIconCandidateUrls("https://example/icon.jpg");

            CollectionAssert.AreEqual(new[] { "https://example/icon.jpg" }, new List<string>(urls));
        }

        [TestMethod]
        public void GetWatchPageUrl_UsesVideoId()
        {
            Assert.AreEqual(
                "https://www.nicovideo.jp/watch/sm47042567",
                ThumbnailUrlResolver.GetWatchPageUrl("sm47042567"));
        }
    }
}
