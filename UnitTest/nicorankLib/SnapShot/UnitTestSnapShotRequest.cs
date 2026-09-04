using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;
using System;
using System.Collections.Generic;

namespace UnitTest.nicorankLib.SnapShot
{
    [TestClass]
    public class UnitTestSnapShotRequest
    {
        private static readonly DateTime StartDay = new DateTime(2024, 1, 2);
        private static readonly DateTime EndDay = new DateTime(2024, 1, 17);

        [TestMethod]
        public void CreateRange_WithLimit1000_ContainsViewCounterFilter()
        {
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 100, 0, true);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("filters[viewCounter][gte]=1000"), url);
        }

        [TestMethod]
        public void CreateRange_WithoutLimit1000_OmitsViewCounterFilter()
        {
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 100, 0, false);

            string url = request.ToUrl();

            Assert.IsFalse(url.Contains("filters[viewCounter]"), url);
        }

        [TestMethod]
        public void ToUrl_AlwaysContainsContext()
        {
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 0, 0, true);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("_context=WeeklyNicoranProgram"), url);
        }

        [TestMethod]
        public void ToUrl_EncodesTimezonePlus()
        {
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 0, 0, true);

            string url = request.ToUrl();

            // +09:00 の + は %2B にエンコードされ、生の +09:00 は残らないこと
            Assert.IsTrue(url.Contains("%2B09"), url);
            Assert.IsFalse(url.Contains("+09:00"), url);
        }

        [TestMethod]
        public void ToUrl_MatchesLegacyParameters()
        {
            // 旧実装（string.Format 直書き）の再現。移行前後で送信パラメータが変わらないことの検証用
            const string legacyUrl =
                "https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search" +
                "?q=&_sort=-viewCounter&fields=contentId,commentCounter,viewCounter,mylistCounter,likeCounter" +
                "&filters[startTime][gte]=2024-01-02T00:00:00%2B09:00&filters[startTime][lt]=2024-01-17T00:00:00%2B09:00" +
                "&_limit=0&_offset=0&filters[viewCounter][gte]=1000";

            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 0, 0, true);
            string url = request.ToUrl();

            var legacyParams = ParseQuery(legacyUrl);
            var newParams = ParseQuery(url);

            // _context は新規追加のため比較対象外
            Assert.IsTrue(newParams.ContainsKey("_context"), url);
            newParams.Remove("_context");

            Assert.AreEqual(legacyParams.Count, newParams.Count, url);
            foreach (var param in legacyParams)
            {
                Assert.IsTrue(newParams.ContainsKey(param.Key), $"欠落キー: {param.Key} ({url})");
                Assert.AreEqual(param.Value, newParams[param.Key], $"キー {param.Key} ({url})");
            }
        }

        [TestMethod]
        public void ToUrl_EncodesJapaneseQuery()
        {
            // 将来のCLI等による外部検索条件指定の下地。日本語・&・% を含む値がクエリを破壊しないこと
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 3, 0, false);
            request.Q = "初音ミク&100%";
            request.Targets = "title";

            string url = request.ToUrl();

            string query = url.Substring(url.IndexOf('?') + 1);
            Assert.IsFalse(query.Contains("初音ミク"), url);
            Assert.IsTrue(query.Contains("q=" + Uri.EscapeDataString("初音ミク&100%")), url);
        }

        [TestMethod]
        public void ToUrl_ClampsLimitAndOffset()
        {
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 1000, -5, true);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("_limit=100"), url);
            Assert.IsTrue(url.Contains("_offset=0"), url);
        }

        [TestMethod]
        public void ToUrl_KeepsZeroLimitForCountQuery()
        {
            // 件数取得用（_limit=0）は 0 のまま保つこと
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 0, 0, true);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("_limit=0"), url);
        }

        [TestMethod]
        public void ToUrl_ClampsOffsetUpperBound()
        {
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 100, 100001, true);

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("_offset=100000"), url);
        }

        [TestMethod]
        public void ToUrl_OmitsTargets_WhenKeywordLessSearch()
        {
            // キーワード無し検索では targets を省略すること
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 0, 0, true);

            string url = request.ToUrl();

            Assert.IsFalse(url.Contains("targets="), url);
        }

        [TestMethod]
        public void ToUrl_EncodesJsonFilter_WhenSpecified()
        {
            // 将来拡張口 jsonFilter はエンコードして載ること
            var request = SnapShotRequest.CreateRange(StartDay, EndDay, 0, 0, true);
            request.JsonFilterJson = "{\"type\":\"equal\",\"field\":\"genre\",\"value\":\"ゲーム\"}";

            string url = request.ToUrl();

            Assert.IsTrue(url.Contains("jsonFilter=" + Uri.EscapeDataString("{\"type\":\"equal\",\"field\":\"genre\",\"value\":\"ゲーム\"}")), url);
        }

        [TestMethod]
        public void FromJson_NullCounters_RequireNullToZeroReplacement()
        {
            // レスポンスの null カウンタは long 直結のため FromJson 単体では読めない。
            // 本番コード（SnapShotAnalyze）の Replace(":null", ":0") が必須であることの検証
            const string json =
                "{\"meta\":{\"status\":200,\"totalCount\":1,\"id\":\"594513df-85ea-4122-9859-f4ec2701cacf\"}," +
                "\"data\":[{\"contentId\":\"sm9\",\"commentCounter\":null,\"viewCounter\":1,\"mylistCounter\":null,\"likeCounter\":null}]}";

            Assert.ThrowsException<Newtonsoft.Json.JsonSerializationException>(() => SnapShotJson.FromJson(json));

            var snapshot = SnapShotJson.FromJson(json.Replace(":null", ":0"));

            Assert.IsNotNull(snapshot);
            Assert.AreEqual(1, snapshot.Data.Count);
            Assert.AreEqual("sm9", snapshot.Data[0].ID);
            Assert.AreEqual(0, snapshot.Data[0].CountComment);
            Assert.AreEqual(1, snapshot.Data[0].CountPlay);
            Assert.AreEqual(0, snapshot.Data[0].CountMylist);
            Assert.AreEqual(0, snapshot.Data[0].CountLike);
        }

        private static Dictionary<string, string> ParseQuery(string url)
        {
            var result = new Dictionary<string, string>();
            string query = url.Substring(url.IndexOf('?') + 1);
            foreach (string pair in query.Split('&'))
            {
                int index = pair.IndexOf('=');
                string key = pair.Substring(0, index);
                string value = Uri.UnescapeDataString(pair.Substring(index + 1));
                result.Add(key, value);
            }
            return result;
        }
    }
}
