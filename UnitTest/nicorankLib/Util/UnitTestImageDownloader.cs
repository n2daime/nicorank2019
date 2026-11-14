using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// ImageDownloader のテスト（Issue #56）。
    /// ネットワークは IHttpDownloader のフェイクで置き換え、以下を検証する:
    /// - 404 は次の候補へ、403/429 は再試行せず見送り、5xx は規定回数だけ再試行
    /// - 既存ファイルのスキップ／上書き、一時ファイルの後始末
    /// - ユーザーIDの重複排除とチャンネル接頭辞
    /// - HTTPリクエスト間の 0.5 秒間隔（実時間では待たず、待機値だけ記録する）
    /// </summary>
    [TestClass]
    public class UnitTestImageDownloader
    {
        private string _dir;

        [TestInitialize]
        public void Initialize()
        {
            _dir = Path.Combine(Path.GetTempPath(), "nicorank_t56_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        /// <summary>URLごとの応答を差し替えられるフェイク。成功時は実ファイルも作る。</summary>
        private sealed class FakeHttpDownloader : IHttpDownloader
        {
            public readonly List<string> Requests = new List<string>();
            public Func<string, HttpResult> TextHandler = _ => HttpResult.FromStatus(HttpDownloadStatus.NotFound, 404);
            public Func<string, string, HttpResult> FileHandler = (_, __) => HttpResult.FromStatus(HttpDownloadStatus.NotFound, 404);

            public HttpResult GetText(string url, CancellationToken cancellationToken)
            {
                Requests.Add("TEXT " + url);
                return TextHandler(url);
            }

            public HttpResult DownloadFile(string url, string destinationPath, CancellationToken cancellationToken)
            {
                Requests.Add("FILE " + url);
                HttpResult result = FileHandler(url, destinationPath);
                if (result.IsSuccess)
                {
                    File.WriteAllBytes(destinationPath, new byte[] { 1, 2, 3 });
                }
                return result;
            }
        }

        private sealed class CapturingProgress : IProgress<ImageDownloadProgress>
        {
            public readonly List<ImageDownloadProgress> Items = new List<ImageDownloadProgress>();

            public void Report(ImageDownloadProgress value)
            {
                Items.Add(value);
            }
        }

        private static HttpResult Success()
        {
            return HttpResult.FromStatus(HttpDownloadStatus.Success, 200);
        }

        private static HttpResult NotFound()
        {
            return HttpResult.FromStatus(HttpDownloadStatus.NotFound, 404);
        }

        private static Ranking Movie(long rankTotal, string id, string thumbnailUrl)
        {
            return new Ranking()
            {
                ID = id,
                RankTotal = rankTotal,
                ThumbnailURL = thumbnailUrl,
                UserID = "u" + rankTotal,
                UserImageURL = "https://example/icon" + rankTotal + ".jpg",
            };
        }

        private static ImageDownloader CreateDownloader(FakeHttpDownloader fake, List<int> waits = null, int intervalMs = 0)
        {
            Action<int> wait = ms =>
            {
                if (waits != null)
                {
                    waits.Add(ms);
                }
            };
            return new ImageDownloader(fake, wait, intervalMs);
        }

        [TestMethod]
        public void Movie_NotFoundOnL_FallsBackToBase()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => url.EndsWith(".L") ? NotFound() : Success();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Success);
            Assert.AreEqual(0, summary.Failed);
            Assert.AreEqual(2, fake.Requests.Count);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "1.jpg")));
        }

        [TestMethod]
        public void Movie_RateLimited_SkipsWithoutRetry()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => HttpResult.FromStatus(HttpDownloadStatus.RateLimited, 429);
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            // 429 は再試行もサイズ違いの再挑戦もしない（負荷を重ねないため）。
            Assert.AreEqual(0, summary.Success);
            Assert.AreEqual(1, summary.Failed);
            Assert.AreEqual(1, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_Forbidden_SkipsWithoutRetry()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => HttpResult.FromStatus(HttpDownloadStatus.Forbidden, 403);
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Failed);
            Assert.AreEqual(1, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_ServerError_RetriesConfiguredTimesThenFails()
        {
            var fake = new FakeHttpDownloader();
            var waits = new List<int>();
            fake.FileHandler = (url, dest) => HttpResult.FromStatus(HttpDownloadStatus.ServerError, 500);
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake, waits)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Failed);
            // 初回 + RetryCount(2) = 3回。5xx はサイズ違いでも直らないので別候補は試さない。
            Assert.AreEqual(3, fake.Requests.Count);
            Assert.AreEqual(2, waits.Count(ms => ms == ImageDownloader.RetryDelayMs));
        }

        [TestMethod]
        public void Movie_NotFoundOnAllCandidates_Fails()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => NotFound();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Failed);
            // .L と base の2候補だけ。
            Assert.AreEqual(2, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_SkipExisting_DoesNotRequest()
        {
            string path = Path.Combine(_dir, "1.jpg");
            File.WriteAllBytes(path, new byte[] { 9 });

            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions() { OverwriteExisting = false }, null);

            Assert.AreEqual(1, summary.Skipped);
            Assert.AreEqual(0, fake.Requests.Count);
            CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(path));
        }

        [TestMethod]
        public void Movie_Overwrite_ReplacesExistingFile()
        {
            string path = Path.Combine(_dir, "1.jpg");
            File.WriteAllBytes(path, new byte[] { 9 });

            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions() { OverwriteExisting = true }, null);

            Assert.AreEqual(1, summary.Success);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [TestMethod]
        public void Movie_DeletedUrl_SkippedWithoutRequest()
        {
            var fake = new FakeHttpDownloader();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/thumbnails/video_deleted") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Skipped);
            Assert.AreEqual(0, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_EmptyUrl_SkippedWithoutRequest()
        {
            var fake = new FakeHttpDownloader();
            var list = new List<Ranking>() { Movie(1, "sm1", "") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Skipped);
            Assert.AreEqual(0, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_EndRankClampsToRankingCount()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>()
            {
                Movie(1, "sm1", "https://example/1"),
                Movie(2, "sm2", "https://example/2"),
                Movie(3, "sm3", "https://example/3"),
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(3, summary.Total);
            Assert.AreEqual(3, summary.Success);
        }

        [TestMethod]
        public void Movie_EndRankLimitsTargets()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>()
            {
                Movie(1, "sm1", "https://example/1"),
                Movie(2, "sm2", "https://example/2"),
                Movie(3, "sm3", "https://example/3"),
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 2, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(2, summary.Total);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "1.jpg")));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "2.jpg")));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "3.jpg")));
        }

        [TestMethod]
        public void Movie_ProgressReportsCumulativeCounts()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var progress = new CapturingProgress();
            var list = new List<Ranking>()
            {
                Movie(1, "sm1", "https://example/1"),
                Movie(2, "sm2", "https://example/2"),
            };

            CreateDownloader(fake).DownloadMovieThumbnails(
                list, 200, _dir, new ImageDownloadOptions(), progress);

            Assert.AreEqual(2, progress.Items.Count);
            Assert.AreEqual(1, progress.Items[0].Index);
            Assert.AreEqual(2, progress.Items[1].Index);
            Assert.AreEqual(2, progress.Items[1].Total);
            Assert.AreEqual(2, progress.Items[1].SuccessCount);
            Assert.AreEqual("2.jpg", progress.Items[1].FileName);
        }

        [TestMethod]
        public void Movie_Interval_AppliedBetweenRequests()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var waits = new List<int>();
            var list = new List<Ranking>()
            {
                Movie(1, "sm1", "https://example/1"),
                Movie(2, "sm2", "https://example/2"),
            };

            CreateDownloader(fake, waits, ImageDownloader.DownloadIntervalMs)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            // リクエスト2回に対して待機は1回（最初の前は待たない）。
            Assert.AreEqual(2, fake.Requests.Count);
            CollectionAssert.AreEqual(new[] { ImageDownloader.DownloadIntervalMs }, waits);
        }

        [TestMethod]
        public void Movie_HighQuality_UsesOgpFromWatchPage()
        {
            var fake = new FakeHttpDownloader();
            fake.TextHandler = url => HttpResult.FromStatus(HttpDownloadStatus.Success, 200,
                "<meta name=\"server-response\" content=\""
                + WebUtility.HtmlEncode("{\"data\":{\"response\":{\"$watchV4\":{\"data\":{\"video\":{\"thumbnail\":{\"ogp\":\"https://example/ogp?key=1\",\"player\":\"https://example/player?key=1\"}}}}}}}")
                + "\" />");
            fake.FileHandler = (url, dest) => url == "https://example/ogp?key=1" ? Success() : NotFound();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions() { HighQuality = true }, null);

            Assert.AreEqual(1, summary.Success);
            // 最初に視聴ページ、次に ogp の画像を取得する。
            Assert.AreEqual("TEXT https://www.nicovideo.jp/watch/sm1", fake.Requests[0]);
            Assert.AreEqual("FILE https://example/ogp?key=1", fake.Requests[1]);
        }

        [TestMethod]
        public void Movie_HighQuality_PageRateLimited_FallsBackToL()
        {
            var fake = new FakeHttpDownloader();
            fake.TextHandler = url => HttpResult.FromStatus(HttpDownloadStatus.RateLimited, 429);
            fake.FileHandler = (url, dest) => url.EndsWith(".L") ? Success() : NotFound();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions() { HighQuality = true }, null);

            // 高画質は諦めるが、360x270 で保存は続ける（ユーザー決定）。
            Assert.AreEqual(1, summary.Success);
            Assert.AreEqual("FILE https://example/1.L", fake.Requests[1]);
        }

        [TestMethod]
        public void Movie_TempFile_RemovedOnFailure()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) =>
            {
                File.WriteAllBytes(dest, new byte[] { 9 }); // 書きかけを模擬
                return HttpResult.FromStatus(HttpDownloadStatus.RateLimited, 429);
            };
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            CreateDownloader(fake).DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.IsFalse(File.Exists(Path.Combine(_dir, "1.jpg.tmp")));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "1.jpg")));
        }

        [TestMethod]
        public void UserIcons_DuplicateUserId_DownloadedOnce()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>()
            {
                new Ranking() { ID = "sm1", RankTotal = 1, UserID = "u1", UserImageURL = "https://example/i1" },
                new Ranking() { ID = "sm2", RankTotal = 2, UserID = "u1", UserImageURL = "https://example/i1" },
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadUserIcons(list, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Total);
            Assert.AreEqual(1, fake.Requests.Count);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "u1.jpg")));
        }

        [TestMethod]
        public void UserIcons_ChannelAndUser_SavedAsSeparateFiles()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>()
            {
                new Ranking() { ID = "so1", RankTotal = 1, UserID = "u1", UserImageURL = "https://example/i1" },
                new Ranking() { ID = "sm2", RankTotal = 2, UserID = "u1", UserImageURL = "https://example/i2" },
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadUserIcons(list, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(2, summary.Total);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "chu1.jpg")));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "u1.jpg")));
        }

        [TestMethod]
        public void UserIcons_EmptyUserId_NotCounted()
        {
            var fake = new FakeHttpDownloader();
            var list = new List<Ranking>()
            {
                new Ranking() { ID = "sm1", RankTotal = 1, UserID = "", UserImageURL = "https://example/i1" },
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadUserIcons(list, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(0, summary.Total);
            Assert.AreEqual(0, fake.Requests.Count);
        }

        [TestMethod]
        public void UserIcons_EmptyUrl_Skipped()
        {
            var fake = new FakeHttpDownloader();
            var list = new List<Ranking>()
            {
                new Ranking() { ID = "sm1", RankTotal = 1, UserID = "u1", UserImageURL = "" },
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadUserIcons(list, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Skipped);
            Assert.AreEqual(0, fake.Requests.Count);
        }

        [TestMethod]
        public void UserIcons_SkipExisting_DoesNotRequest()
        {
            File.WriteAllBytes(Path.Combine(_dir, "u1.jpg"), new byte[] { 9 });
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>()
            {
                new Ranking() { ID = "sm1", RankTotal = 1, UserID = "u1", UserImageURL = "https://example/i1" },
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadUserIcons(list, _dir, new ImageDownloadOptions() { OverwriteExisting = false }, null);

            Assert.AreEqual(1, summary.Skipped);
            Assert.AreEqual(0, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_KeyedUrl_Forbidden_FallsBackToCdn()
        {
            // 週刊のキー付きURLが403（キー失効・制限）でも、動画IDから作る通常CDNを試す。
            const string keyed = "https://img.cdn.nimg.jp/s/nicovideo/thumbnails/8134546/8134546.original/r640x360l?key=abc";
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => url == keyed
                ? HttpResult.FromStatus(HttpDownloadStatus.Forbidden, 403)
                : Success();
            var list = new List<Ranking>() { Movie(1, "sm8134546", keyed) };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Success);
            Assert.AreEqual(2, fake.Requests.Count);
            Assert.AreEqual("FILE https://nicovideo.cdn.nimg.jp/thumbnails/8134546/8134546.L", fake.Requests[1]);
        }

        [TestMethod]
        public void Movie_KeyedUrl_RateLimited_FallsBackToCdn()
        {
            const string keyed = "https://img.cdn.nimg.jp/s/nicovideo/thumbnails/8134546/8134546.original/r640x360l?key=abc";
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => url == keyed
                ? HttpResult.FromStatus(HttpDownloadStatus.RateLimited, 429)
                : Success();
            var list = new List<Ranking>() { Movie(1, "sm8134546", keyed) };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Success);
            Assert.AreEqual(2, fake.Requests.Count);
            Assert.AreEqual("FILE https://nicovideo.cdn.nimg.jp/thumbnails/8134546/8134546.L", fake.Requests[1]);
        }

        [TestMethod]
        public void Movie_KeyedUrl_ServerError_FallsBackToCdn()
        {
            const string keyed = "https://img.cdn.nimg.jp/s/nicovideo/thumbnails/8134546/8134546.original/r640x360l?key=abc";
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => url == keyed
                ? HttpResult.FromStatus(HttpDownloadStatus.ServerError, 500)
                : Success();
            var list = new List<Ranking>() { Movie(1, "sm8134546", keyed) };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null);

            Assert.AreEqual(1, summary.Success);
            // キー付きURLは初回+2回リトライ、その後CDNへ1回。
            Assert.AreEqual(4, fake.Requests.Count);
        }

        [TestMethod]
        public void UserIcons_UserInfoRange_FiltersByRankAndCategory()
        {
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) => Success();
            var list = new List<Ranking>()
            {
                new Ranking() { ID = "sm1", RankTotal = 1, RankCategory = 2, UserID = "u1", UserImageURL = "https://example/i1" },
                new Ranking() { ID = "sm2", RankTotal = 2000, RankCategory = 2, UserID = "u2", UserImageURL = "https://example/i2" },
                new Ranking() { ID = "sm3", RankTotal = 2000, RankCategory = 1, UserID = "u3", UserImageURL = "https://example/i3" },
                new Ranking() { ID = "sm4", RankTotal = 5000, RankCategory = 0, UserID = "u4", UserImageURL = "https://example/i4" },
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadUserIcons(list, _dir, new ImageDownloadOptions(), null, 1000);

            Assert.AreEqual(2, summary.Total);
            Assert.AreEqual(2, summary.Success);
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "u1.jpg")));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "u3.jpg")));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "u2.jpg")));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "u4.jpg")));
        }

        [TestMethod]
        public void IsUsableThumbnailUrl_RejectsEmptyAndDeleted()
        {
            Assert.IsFalse(ImageDownloader.IsUsableThumbnailUrl(null));
            Assert.IsFalse(ImageDownloader.IsUsableThumbnailUrl(""));
            Assert.IsFalse(ImageDownloader.IsUsableThumbnailUrl("https://example/video_deleted"));
            Assert.IsTrue(ImageDownloader.IsUsableThumbnailUrl("https://example/thumbnails/1"));
        }

        [TestMethod]
        public void Movie_CancelledAfterFirstItem_StopsBeforeNextItem()
        {
            var cts = new CancellationTokenSource();
            var fake = new FakeHttpDownloader();
            fake.FileHandler = (url, dest) =>
            {
                cts.Cancel();
                return Success();
            };
            var list = new List<Ranking>()
            {
                Movie(1, "sm1", "https://example/1"),
                Movie(2, "sm2", "https://example/2"),
                Movie(3, "sm3", "https://example/3"),
            };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null, cts.Token);

            Assert.IsTrue(summary.Cancelled);
            Assert.AreEqual(1, summary.Success);
            Assert.AreEqual(1, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_CancelledBeforeStart_DoesNotRequest()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var fake = new FakeHttpDownloader();
            var list = new List<Ranking>() { Movie(1, "sm1", "https://example/1") };

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), null, cts.Token);

            Assert.IsTrue(summary.Cancelled);
            Assert.AreEqual(1, summary.Total);
            Assert.AreEqual(0, fake.Requests.Count);
        }

        [TestMethod]
        public void Movie_FailureKind_Reports403AndServerError()
        {
            var progress = new CapturingProgress();
            var list = new List<Ranking>()
            {
                Movie(1, "sm1", "https://example/1"),
                Movie(2, "sm2", "https://example/2"),
            };
            var fake = new FakeHttpDownloader();
            int count = 0;
            fake.FileHandler = (url, dest) =>
                ++count == 1
                    ? HttpResult.FromStatus(HttpDownloadStatus.Forbidden, 403)
                    : HttpResult.FromStatus(HttpDownloadStatus.ServerError, 500);

            ImageDownloadSummary summary = CreateDownloader(fake)
                .DownloadMovieThumbnails(list, 200, _dir, new ImageDownloadOptions(), progress);

            Assert.AreEqual(2, summary.Failed);
            Assert.AreEqual(ImageDownloadFailureKind.Forbidden, progress.Items[0].Kind);
            Assert.AreEqual(ImageDownloadFailureKind.ServerError, progress.Items[1].Kind);
        }
    }
}
