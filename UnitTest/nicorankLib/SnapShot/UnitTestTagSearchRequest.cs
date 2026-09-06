using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;
using System;

namespace UnitTest.nicorankLib.SnapShot
{
    [TestClass]
    public class UnitTestTagSearchRequest
    {
        private const string TagJson = "{\"type\":\"equal\",\"field\":\"tagsExact\",\"value\":\"A\"}";

        private static SnapShotRequest CreateDefault()
        {
            return SnapShotRequest.CreateTagSearch(TagJson, 0, 0, 0, 0,
                SnapShotRequest.NeutralStartGte, SnapShotRequest.NeutralStartLt, null, 0, 0);
        }

        [TestMethod]
        public void CreateTagSearch_EmptyQueryWithoutTargets()
        {
            string url = CreateDefault().ToUrl();

            Assert.IsTrue(url.Contains("q=&") || url.Contains("q="), url);
            Assert.IsFalse(url.Contains("targets="), url);
            Assert.IsTrue(url.Contains("_context=WeeklyNicoranProgram"), url);
        }

        [TestMethod]
        public void CreateTagSearch_ZeroMin_OmitsCounterFilters()
        {
            string url = CreateDefault().ToUrl();

            Assert.IsFalse(url.Contains("filters[viewCounter]"), url);
            Assert.IsFalse(url.Contains("filters[mylistCounter]"), url);
            Assert.IsFalse(url.Contains("filters[likeCounter]"), url);
            Assert.IsFalse(url.Contains("filters[commentCounter]"), url);
        }

        [TestMethod]
        public void CreateTagSearch_PositiveMin_EmitsCounterFilters()
        {
            var request = SnapShotRequest.CreateTagSearch(TagJson, 100, 200, 300, 400,
                SnapShotRequest.NeutralStartGte, SnapShotRequest.NeutralStartLt, null, 100, 0);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("filters[viewCounter][gte]=100"), url);
            Assert.IsTrue(url.Contains("filters[mylistCounter][gte]=200"), url);
            Assert.IsTrue(url.Contains("filters[likeCounter][gte]=300"), url);
            Assert.IsTrue(url.Contains("filters[commentCounter][gte]=400"), url);
        }

        [TestMethod]
        public void CreateTagSearch_ContentType_OnlyLongShort()
        {
            string longUrl = SnapShotRequest.CreateTagSearch(TagJson, 0, 0, 0, 0,
                SnapShotRequest.NeutralStartGte, SnapShotRequest.NeutralStartLt, "long", 0, 0).ToUrl();
            Assert.IsTrue(longUrl.Contains("filters[contentType][0]=long"), longUrl);

            string invalidUrl = SnapShotRequest.CreateTagSearch(TagJson, 0, 0, 0, 0,
                SnapShotRequest.NeutralStartGte, SnapShotRequest.NeutralStartLt, "movie", 0, 0).ToUrl();
            Assert.IsFalse(invalidUrl.Contains("filters[contentType]"), invalidUrl);

            string nullUrl = CreateDefault().ToUrl();
            Assert.IsFalse(nullUrl.Contains("filters[contentType]"), nullUrl);
        }

        [TestMethod]
        public void CreateTagSearch_EmitsJsonFilterEncoded()
        {
            string url = CreateDefault().ToUrl();

            Assert.IsTrue(url.Contains("jsonFilter=" + Uri.EscapeDataString(TagJson)), url);
        }

        [TestMethod]
        public void CreateTagSearch_NeutralDates_CoverAllVideos()
        {
            string url = CreateDefault().ToUrl();

            Assert.IsTrue(url.Contains("filters[startTime][gte]=2000-01-01T00%3A00%3A00%2B09%3A00"), url);
            Assert.IsTrue(url.Contains("filters[startTime][lt]=2100-01-01T00%3A00%3A00%2B09%3A00"), url);
        }

        [TestMethod]
        public void CreateTagSearch_CustomDates_AreUsed()
        {
            var request = SnapShotRequest.CreateTagSearch(TagJson, 0, 0, 0, 0,
                new DateTime(2024, 1, 2), new DateTime(2024, 1, 17), null, 0, 0);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("filters[startTime][gte]=2024-01-02T00%3A00%3A00%2B09%3A00"), url);
            Assert.IsTrue(url.Contains("filters[startTime][lt]=2024-01-17T00%3A00%3A00%2B09%3A00"), url);
        }
    }
}
