using nicorankLib.Analyze.model;
using nicorankLib.api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace nicorankLib.Util
{
    /// <summary>直接ダウンロードの実行オプション（Issue #56）。UIのチェック状態をそのまま渡す。</summary>
    public class ImageDownloadOptions
    {
        /// <summary>可能であれば 1280x720（ogp）などの高画質を試す。既定は false。</summary>
        public bool HighQuality { get; set; }

        /// <summary>既存ファイルを上書きする。false なら存在確認だけでスキップする。</summary>
        public bool OverwriteExisting { get; set; }
    }

    /// <summary>1件ごとの結果区分。</summary>
    public enum ImageDownloadOutcome
    {
        Success,
        Skipped,
        Failed,
    }

    /// <summary>
    /// 進捗通知。件数は「その時点までの累計」を持たせる。
    /// なぜ累計か: 表示側で数え直さなくても、そのまま [n/N] 成功/失敗/スキップ を出せるようにするため。
    /// </summary>
    public class ImageDownloadProgress
    {
        public int Index { get; set; }
        public int Total { get; set; }
        public string FileName { get; set; }
        public ImageDownloadOutcome Outcome { get; set; }
        public string Message { get; set; }
        public int SuccessCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }

    /// <summary>実行結果の集計。最後にUIへ件数を表示するために使う。</summary>
    public class ImageDownloadSummary
    {
        public int Total { get; set; }
        public int Success { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
    }

    /// <summary>
    /// 動画サムネイル・ユーザーアイコンをアプリ内で直接ダウンロードする（Issue #56）。
    ///
    /// 設計上の要点:
    /// - 順次処理にして HTTP リクエスト間に DownloadIntervalMs（0.5秒）を空ける。並列にすると
    ///   ニコ動への負荷が読みにくく、403/429 を誘発しやすいため。ED枠200件なら約100秒、
    ///   ユーザーアイコン最大1000件なら約8分が目安。
    /// - 404 は「そのサイズが無い」だけなので次の候補へ進む。403/429 は再試行せず見送る。
    ///   タイムアウト・5xx は RetryCount 回だけ再試行し、だめなら見送る。1件の失敗で全体は止めない。
    /// - 上書き時は一時ファイルへ保存してから置き換える。途中で失敗しても既存ファイルを壊さないため。
    /// - 高画質URLは保存しない。視聴ページから都度取得する（キー付きURLの期限や、
    ///   Ranking をシリアライズする成果物への列追加を避けるため）。
    /// </summary>
    public class ImageDownloader
    {
        /// <summary>HTTPリクエスト間の待機時間。負荷を抑えるための調整値。</summary>
        public const int DownloadIntervalMs = 500;

        /// <summary>タイムアウト・5xx の再試行回数（初回を除く）。</summary>
        public const int RetryCount = 2;

        /// <summary>再試行前の待機時間。</summary>
        public const int RetryDelayMs = 1000;

        private const string TempFileExtension = ".tmp";

        private readonly IHttpDownloader _http;
        private readonly Action<int> _wait;
        private readonly int _intervalMs;
        private bool _hasRequested;

        public ImageDownloader(IHttpDownloader http)
            : this(http, null, DownloadIntervalMs)
        {
        }

        /// <summary>
        /// テスト用に待機処理と間隔を差し替えられるコンストラクタ。
        /// なぜ差し替えるか: 0.5秒×件数の実時間待ちをテストで発生させないため。
        /// </summary>
        public ImageDownloader(IHttpDownloader http, Action<int> wait, int intervalMs)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _wait = wait ?? Thread.Sleep;
            _intervalMs = Math.Max(0, intervalMs);
        }

        /// <summary>
        /// ED枠までの動画サムネイルをダウンロードする。endRank は queue.irv と同じ「紹介枠＋ED枠」。
        /// </summary>
        public ImageDownloadSummary DownloadMovieThumbnails(
            IReadOnlyList<Ranking> rankings,
            int endRank,
            string outputDir,
            ImageDownloadOptions options,
            IProgress<ImageDownloadProgress> progress)
        {
            var summary = new ImageDownloadSummary();
            if (rankings == null || rankings.Count == 0 || string.IsNullOrWhiteSpace(outputDir))
            {
                return summary;
            }

            options = options ?? new ImageDownloadOptions();
            Directory.CreateDirectory(outputDir);

            int total = Math.Min(Math.Max(endRank, 0), rankings.Count);
            summary.Total = total;

            for (int i = 0; i < total; i++)
            {
                Ranking rank = rankings[i];
                string fileName = ImageFileNaming.GetMovieFileName(rank);
                ImageDownloadOutcome outcome;
                string message;

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    outcome = ImageDownloadOutcome.Skipped;
                    message = "ファイル名を作れません";
                }
                else if (!IsUsableThumbnailUrl(rank.ThumbnailURL))
                {
                    // 削除済み動画は ThumbnailURL が /video_deleted になる（HiddenMovieDelete と同じ判定）。
                    outcome = ImageDownloadOutcome.Skipped;
                    message = "サムネイルURLがありません";
                }
                else
                {
                    outcome = DownloadMovieItem(rank, Path.Combine(outputDir, fileName), options, out message);
                }

                ApplyResult(summary, i, fileName, outcome, message, progress);
            }

            return summary;
        }

        /// <summary>
        /// ユーザーアイコンを全件ダウンロードする。UserID（チャンネルは ch 付き）で重複を除く。
        /// 既存 queue_UserIcon.irv と同じく、UserID が空の動画は対象外。
        /// </summary>
        public ImageDownloadSummary DownloadUserIcons(
            IReadOnlyList<Ranking> rankings,
            string outputDir,
            ImageDownloadOptions options,
            IProgress<ImageDownloadProgress> progress)
        {
            var summary = new ImageDownloadSummary();
            if (rankings == null || rankings.Count == 0 || string.IsNullOrWhiteSpace(outputDir))
            {
                return summary;
            }

            options = options ?? new ImageDownloadOptions();
            Directory.CreateDirectory(outputDir);

            // 同じユーザーが複数動画に入っていても、保存名が同じなので1回だけ取得する。
            var items = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Ranking rank in rankings)
            {
                string fileName = ImageFileNaming.GetUserIconFileName(rank);
                if (string.IsNullOrWhiteSpace(fileName) || !seen.Add(fileName))
                {
                    continue;
                }
                items.Add(new KeyValuePair<string, string>(fileName, rank.UserImageURL));
            }

            summary.Total = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                string fileName = items[i].Key;
                string url = items[i].Value;
                string destinationPath = Path.Combine(outputDir, fileName);
                ImageDownloadOutcome outcome;
                string message;

                if (!options.OverwriteExisting && File.Exists(destinationPath))
                {
                    outcome = ImageDownloadOutcome.Skipped;
                    message = "既存ファイルのためスキップ";
                }
                else if (string.IsNullOrWhiteSpace(url))
                {
                    outcome = ImageDownloadOutcome.Skipped;
                    message = "アイコンURLがありません";
                }
                else
                {
                    outcome = DownloadOne(
                        ThumbnailUrlResolver.BuildUserIconCandidateUrls(url),
                        destinationPath,
                        out message);
                }

                ApplyResult(summary, i, fileName, outcome, message, progress);
            }

            return summary;
        }

        /// <summary>
        /// サムネイルとして使えるURLか。空と削除済みパスを弾く。
        /// 削除判定は RankingHistory / HiddenMovieDelete と同じ /video_deleted を見る。
        /// </summary>
        public static bool IsUsableThumbnailUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }
            return url.IndexOf("/video_deleted", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private ImageDownloadOutcome DownloadMovieItem(
            Ranking rank,
            string destinationPath,
            ImageDownloadOptions options,
            out string message)
        {
            if (!options.OverwriteExisting && File.Exists(destinationPath))
            {
                message = "既存ファイルのためスキップ";
                return ImageDownloadOutcome.Skipped;
            }

            // 高画質ONのときだけ視聴ページを読む。HTML取得は1件あたり1リクエスト増えるが、
            // これはユーザーが明示的に選んだときだけなので許容する。
            ThumbnailPageInfo page = null;
            if (options.HighQuality)
            {
                page = FetchPageInfo(rank.ID);
            }

            IReadOnlyList<string> candidates = ThumbnailUrlResolver.BuildCandidateUrls(
                rank.ThumbnailURL, page, options.HighQuality);

            return DownloadOne(candidates, destinationPath, out message);
        }

        /// <summary>
        /// 視聴ページHTMLからサムネイルURL群を取り出す。取得できなければ null を返し、
        /// 呼び出し側は .L にフォールバックする。
        /// 403/429 でも .L での保存は続ける（高画質だけを諦める）方針。
        /// HTML取得は1回だけ試し、再試行はしない。再試行するとレート制限時に負荷を重ねるため。
        /// </summary>
        private ThumbnailPageInfo FetchPageInfo(string videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return null;
            }

            WaitBeforeRequest();
            HttpResult result = _http.GetText(ThumbnailUrlResolver.GetWatchPageUrl(videoId));
            if (!result.IsSuccess)
            {
                return null;
            }
            return ThumbnailUrlResolver.ParseServerResponse(result.Text);
        }

        private ImageDownloadOutcome DownloadOne(
            IReadOnlyList<string> candidates,
            string destinationPath,
            out string message)
        {
            if (candidates == null || candidates.Count == 0)
            {
                message = "候補URLがありません";
                return ImageDownloadOutcome.Failed;
            }

            string tempPath = destinationPath + TempFileExtension;
            string lastMessage = null;

            foreach (string url in candidates)
            {
                bool nextCandidate = false;
                for (int attempt = 0; attempt <= RetryCount && !nextCandidate; attempt++)
                {
                    WaitBeforeRequest();
                    HttpResult result = _http.DownloadFile(url, tempPath);

                    if (result.IsSuccess)
                    {
                        try
                        {
                            ReplaceTempFile(tempPath, destinationPath);
                            message = null;
                            return ImageDownloadOutcome.Success;
                        }
                        catch (Exception ex)
                        {
                            TryDelete(tempPath);
                            message = "保存に失敗: " + ex.Message;
                            return ImageDownloadOutcome.Failed;
                        }
                    }

                    TryDelete(tempPath);

                    if (result.Status == HttpDownloadStatus.NotFound)
                    {
                        // このサイズは存在しない。次の候補（base など）を試す。
                        nextCandidate = true;
                        lastMessage = "404";
                        continue;
                    }

                    if (result.Status == HttpDownloadStatus.Forbidden
                        || result.Status == HttpDownloadStatus.RateLimited)
                    {
                        // レート制限・アクセス拒否は再試行すると悪化するため、その画像だけ見送る。
                        message = $"HTTP {result.StatusCode} のため見送り";
                        return ImageDownloadOutcome.Failed;
                    }

                    lastMessage = result.StatusCode > 0
                        ? $"HTTP {result.StatusCode}"
                        : "通信エラー";

                    if (attempt < RetryCount)
                    {
                        _wait(RetryDelayMs);
                        continue;
                    }

                    // 5xx・通信エラーはサイズ違いの別候補でも直る可能性が低く、
                    // 追加リクエストは負荷になるだけなので、その画像はここで諦める。
                    message = lastMessage;
                    return ImageDownloadOutcome.Failed;
                }
            }

            message = string.IsNullOrEmpty(lastMessage) ? "取得できませんでした" : lastMessage;
            return ImageDownloadOutcome.Failed;
        }

        /// <summary>
        /// リクエスト間隔を空ける。最初のリクエストの前は待たない。
        /// リトライも1リクエストとして数えるため、間隔が二重に効くことはない。
        /// </summary>
        private void WaitBeforeRequest()
        {
            if (_hasRequested && _intervalMs > 0)
            {
                _wait(_intervalMs);
            }
            _hasRequested = true;
        }

        /// <summary>
        /// 一時ファイルを本番名へ置き換える。
        /// File.Replace を使うのは、上書き時に「削除→移動」の隙間で既存ファイルを失わないため。
        /// </summary>
        private static void ReplaceTempFile(string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(tempPath, destinationPath, null);
            }
            else
            {
                File.Move(tempPath, destinationPath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // 一時ファイルの後始末失敗は本処理の成否に影響させない。
            }
        }

        private static void ApplyResult(
            ImageDownloadSummary summary,
            int index,
            string fileName,
            ImageDownloadOutcome outcome,
            string message,
            IProgress<ImageDownloadProgress> progress)
        {
            switch (outcome)
            {
                case ImageDownloadOutcome.Success:
                    summary.Success++;
                    break;
                case ImageDownloadOutcome.Skipped:
                    summary.Skipped++;
                    break;
                default:
                    summary.Failed++;
                    break;
            }

            if (progress == null)
            {
                return;
            }

            progress.Report(new ImageDownloadProgress()
            {
                Index = index + 1,
                Total = summary.Total,
                FileName = fileName,
                Outcome = outcome,
                Message = message,
                SuccessCount = summary.Success,
                SkippedCount = summary.Skipped,
                FailedCount = summary.Failed,
            });
        }
    }
}
