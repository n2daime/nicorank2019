using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Json;
using nicorankLib.Util;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Input
{
    /// <summary>
    /// Issue #53: 過去ランキング JSON の「データなし」判定。
    /// 404（未提供）・[]（明示マーカー）・200だが0件（取得失敗）を混同しないことを確認する。
    /// 実際の HTTP は使わず、TryDownloadText を差し替えて再現する。
    /// </summary>
    [TestClass]
    public class UnitTestJsonReaderMissingDate
    {
        private const string BaseUrl = "https://example.invalid/old-ranking/daily/2026-10-22/";
        private const string FileListUrl = BaseUrl + "file_name_list.json";
        private const string GenreUrl = BaseUrl + "all.json";

        private const string FileListJson =
            "[{\"genre\":\"全ジャンル\",\"tag\":\"\",\"file\":\"all.json\",\"isGenre\":false}]";

        private const string GenreJson =
            "[{\"id\":\"sm1\",\"title\":\"t\",\"registeredAt\":\"2024-01-01T00:00:00+09:00\","
            + "\"count\":{\"view\":1,\"comment\":2,\"mylist\":3,\"like\":4},"
            + "\"thumbnail\":{\"url\":\"https://example.invalid/a.jpg\"}}]";

        private class StubResponse
        {
            public bool Ok;
            public TxtDownloadStatus Status;
            public string Text;
        }

        private class StubJsonReader : JsonReaderBase
        {
            public readonly Dictionary<string, StubResponse> Responses = new Dictionary<string, StubResponse>();

            public StubJsonReader() : base(new DateTime(2026, 10, 22))
            {
            }

            protected override string calcJsonTarget()
            {
                return BaseUrl;
            }

            public override void setAnalyzeDay(DateTime analyzeDay)
            {
            }

            protected override bool TryDownloadText(string url, out string text, out TxtDownloadStatus status)
            {
                if (Responses.TryGetValue(url, out var response))
                {
                    text = response.Text;
                    status = response.Status;
                    return response.Ok;
                }

                // 未定義のURLは404扱い（ジャンルファイルの欠落を再現）
                text = null;
                status = TxtDownloadStatus.NotFound;
                return false;
            }
        }

        private const string ConfigXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
            + "<nicorank><RANK Num=\"20\" Tyouki=\"true\" /><RANKED Num=\"200\" />"
            + "<UserInfo Num=\"1000\" /><ICONDL_PATH>c:\\tmp</ICONDL_PATH>"
            + "<POINT><CALC_MYLIST>40</CALC_MYLIST><CALC_PLAY>1</CALC_PLAY>"
            + "<CALC_COMMENT>1</CALC_COMMENT><CALC_LIKE>10</CALC_LIKE></POINT>"
            + "<COMMENT_OFFSET Mode=\"2\" UnderLimit=\"0.01\" /><MYLIST_OFFSET Mode=\"1\" />"
            + "<PLAY_OFFSET Mode=\"2\" /><POINTALL_OFFSET Mode=\"0\" />"
            + "<SYSTEM><ResultCsv Code=\"0\" /><Thread Max=\"1\" />"
            + "<Download><NicoAPI Retry=\"20\" /><UserIcon Retry=\"1\" /></Download>"
            + "<URL_JSON_TARGET Url=\"https://example.invalid/{0}/{1}/\" />"
            + "</SYSTEM></nicorank>";

        [TestInitialize]
        public void Setup()
        {
            // ThreadMax を1にして、スタブの辞書を単一スレッドから読む
            TestConfigBuilder.LoadFromXmlString(ConfigXml);
        }

        [TestCleanup]
        public void Cleanup()
        {
            TestConfigBuilder.ResetInstance();
        }

        [TestMethod]
        public void FileList404_ReportsNotFound()
        {
            var reader = new StubJsonReader();
            reader.Responses[FileListUrl] = new StubResponse { Ok = false, Status = TxtDownloadStatus.NotFound };

            bool ok = reader.AnalyzeRank(out var rankings);

            Assert.IsFalse(ok);
            Assert.AreEqual(TxtDownloadStatus.NotFound, reader.LastFileListStatus);
            Assert.IsFalse(reader.LastFileListWasEmpty);
            Assert.AreEqual(0, rankings.Count);
        }

        [TestMethod]
        public void EmptyFileList_IsNoDataMarker()
        {
            var reader = new StubJsonReader();
            reader.Responses[FileListUrl] = new StubResponse
            {
                Ok = true,
                Status = TxtDownloadStatus.Success,
                Text = "[]",
            };

            bool ok = reader.AnalyzeRank(out var rankings);

            Assert.IsTrue(ok);
            Assert.AreEqual(TxtDownloadStatus.Success, reader.LastFileListStatus);
            Assert.IsTrue(reader.LastFileListWasEmpty);
            Assert.AreEqual(0, rankings.Count);
        }

        [TestMethod]
        public void EmptyText_IsFailureNotMarker()
        {
            var reader = new StubJsonReader();
            reader.Responses[FileListUrl] = new StubResponse
            {
                Ok = true,
                Status = TxtDownloadStatus.Success,
                Text = "",
            };

            bool ok = reader.AnalyzeRank(out _);

            Assert.IsFalse(ok);
            Assert.AreEqual(TxtDownloadStatus.Failed, reader.LastFileListStatus);
            Assert.IsFalse(reader.LastFileListWasEmpty);
        }

        [TestMethod]
        public void Genre404_ReturnsNoRankingsButNotEmptyMarker()
        {
            var reader = new StubJsonReader();
            reader.Responses[FileListUrl] = new StubResponse
            {
                Ok = true,
                Status = TxtDownloadStatus.Success,
                Text = FileListJson,
            };
            reader.Responses[GenreUrl] = new StubResponse { Ok = false, Status = TxtDownloadStatus.NotFound };

            bool ok = reader.AnalyzeRank(out var rankings);

            Assert.IsTrue(ok);
            Assert.AreEqual(TxtDownloadStatus.Success, reader.LastFileListStatus);
            // ジャンル側の404は「空配列マーカー」ではない。ここを true にすると
            // 公開途中の日をメンテナンス日と誤認する
            Assert.IsFalse(reader.LastFileListWasEmpty);
            Assert.AreEqual(0, rankings.Count);
        }

        [TestMethod]
        public void ValidGenre_ReturnsRanking()
        {
            var reader = new StubJsonReader();
            reader.Responses[FileListUrl] = new StubResponse
            {
                Ok = true,
                Status = TxtDownloadStatus.Success,
                Text = FileListJson,
            };
            reader.Responses[GenreUrl] = new StubResponse
            {
                Ok = true,
                Status = TxtDownloadStatus.Success,
                Text = GenreJson,
            };

            bool ok = reader.AnalyzeRank(out var rankings);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, rankings.Count);
            Assert.AreEqual("sm1", rankings[0].ID);
            Assert.IsFalse(reader.LastFileListWasEmpty);
        }
    }
}
