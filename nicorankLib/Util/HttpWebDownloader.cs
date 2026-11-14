using System;
using System.IO;
using System.Net;
using System.Text;

namespace nicorankLib.Util
{
    /// <summary>
    /// HttpWebRequest による IHttpDownloader の実装（Issue #56）。
    /// なぜ WebClient ではなく HttpWebRequest か: 403/429/404 を区別して扱うには応答ステータスを
    /// 直接見る必要があり、タイムアウトも明示したいからである。既存 InternetUtil の WebClient は
    /// テキスト取得用でリトライ回数が今回の仕様（403/429は即スキップ）と合わない。
    /// </summary>
    public class HttpWebDownloader : IHttpDownloader
    {
        /// <summary>応答ヘッダー待ち・読み書きの上限。画像1枚と49KB程度のHTMLには十分な余裕を取る。</summary>
        public const int DefaultTimeoutMs = 30 * 1000;

        /// <summary>
        /// 取得に使う User-Agent。既存の InternetUtil と同じアプリ名を使う。
        /// 実測（2026-11-14）ではこのUAでも視聴ページの server-response を取得できたため、
        /// ブラウザを装う必要はない。
        /// </summary>
        public const string UserAgent = "WeeklyNicoranProgram";

        private readonly int _timeoutMs;

        public HttpWebDownloader() : this(DefaultTimeoutMs)
        {
        }

        public HttpWebDownloader(int timeoutMs)
        {
            _timeoutMs = timeoutMs > 0 ? timeoutMs : DefaultTimeoutMs;
        }

        public HttpResult GetText(string url)
        {
            try
            {
                HttpWebRequest request = CreateRequest(url);
                request.Accept = "text/html,application/xhtml+xml";
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string text = reader.ReadToEnd();
                    return HttpResult.FromStatus(HttpDownloadStatus.Success, (int)response.StatusCode, text);
                }
            }
            catch (WebException ex)
            {
                return FromWebException(ex);
            }
            catch (Exception)
            {
                return HttpResult.FromStatus(HttpDownloadStatus.Failed, 0);
            }
        }

        public HttpResult DownloadFile(string url, string destinationPath)
        {
            try
            {
                HttpWebRequest request = CreateRequest(url);
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var input = response.GetResponseStream())
                using (var output = File.Create(destinationPath))
                {
                    input.CopyTo(output);
                    return HttpResult.FromStatus(HttpDownloadStatus.Success, (int)response.StatusCode);
                }
            }
            catch (WebException ex)
            {
                TryDelete(destinationPath);
                return FromWebException(ex);
            }
            catch (Exception)
            {
                TryDelete(destinationPath);
                return HttpResult.FromStatus(HttpDownloadStatus.Failed, 0);
            }
        }

        private HttpWebRequest CreateRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = UserAgent;
            request.Timeout = _timeoutMs;
            request.ReadWriteTimeout = _timeoutMs;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.AllowAutoRedirect = true;
            return request;
        }

        /// <summary>
        /// WebException を今回の区分へ変換する。403/429 は再試行しない方針なので区別して返す。
        /// 応答が取れない（タイムアウト・名前解決失敗など）場合は Failed。
        /// </summary>
        private static HttpResult FromWebException(WebException ex)
        {
            if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response is HttpWebResponse response)
            {
                int statusCode = (int)response.StatusCode;
                switch (statusCode)
                {
                    case 404:
                        return HttpResult.FromStatus(HttpDownloadStatus.NotFound, statusCode);
                    case 403:
                        return HttpResult.FromStatus(HttpDownloadStatus.Forbidden, statusCode);
                    case 429:
                        return HttpResult.FromStatus(HttpDownloadStatus.RateLimited, statusCode);
                }
                if (statusCode >= 500)
                {
                    return HttpResult.FromStatus(HttpDownloadStatus.ServerError, statusCode);
                }
                return HttpResult.FromStatus(HttpDownloadStatus.Failed, statusCode);
            }
            return HttpResult.FromStatus(HttpDownloadStatus.Failed, 0);
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
    }
}
