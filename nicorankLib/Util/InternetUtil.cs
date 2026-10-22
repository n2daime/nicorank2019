using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    /// <summary>
    /// テキスト取得の結果区分（Issue #53）。
    /// 「404（データが無い）」と「それ以外の失敗（通信断・5xx等）」を呼び出し側で区別するために使う。
    /// 過去ランキングJSONは 404 を「その日のデータが存在しない」の合図として扱うが、
    /// 通信エラーまで同じ扱いにすると誤ってメンテナンス日を登録してしまうため分ける。
    /// </summary>
    public enum TxtDownloadStatus
    {
        Success,
        NotFound,
        Failed,
    }

    public class InternetUtil
    {
        private class NicoranWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public NicoranWebClient(int timeoutMs)
            {
                _timeoutMs = timeoutMs;
            }

            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                // 応答ヘッダーを受け取るまでの上限。本文の読み込み時間は含まない。
                // 既定は従来と同じ100秒。過去ランキングJSONだけ呼び出し側が5秒を指定する（Issue #53）。
                w.Timeout = _timeoutMs;
                return w;
            }
        }


        /// <summary>
        /// インターネット上のテキストを読み込む（従来互換の入口）。
        /// </summary>
        /// <param name="url"></param>
        /// <param name="readText"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static bool TxtDownLoad(string url, out string readText, string encode = "UTF-8")
        {
            TxtDownloadStatus ignored;
            return TxtDownLoad(url, out readText, out ignored, encode);
        }

        /// <summary>
        /// インターネット上のテキストを読み込み、結果区分も返す（Issue #53）。
        /// </summary>
        /// <param name="url"></param>
        /// <param name="readText"></param>
        /// <param name="status">成功・404・その他失敗の区分</param>
        /// <param name="encode"></param>
        /// <param name="retryOnNotFound">
        /// false のとき、HTTP 404 は再試行せず即座に諦める。
        /// 過去ランキングJSONのように「404 = その日のデータが無い」が確定している用途で、
        /// 無駄な20回リトライ（指数バックオフで約6分）を避けるため。
        /// 既定の true は従来動作（スナップショット等の「まだ公開されていない」を待つ用途）を維持する。
        /// </param>
        /// <param name="maxAttempts">最大試行回数。テストで短縮できるように引数化している。</param>
        /// <param name="timeoutMs">
        /// 応答ヘッダーを受け取るまでの上限（ミリ秒）。既定は従来と同じ100秒。
        /// 過去ランキングJSONは静的ファイルなので5秒を指定する（Issue #53）。
        /// スナップショット検索APIは応答に時間がかかることがあるため、既定を短くしない。
        /// </param>
        /// <returns>取得できたか</returns>
        public static bool TxtDownLoad(
            string url,
            out string readText,
            out TxtDownloadStatus status,
            string encode = "UTF-8",
            bool retryOnNotFound = true,
            int maxAttempts = 20,
            int timeoutMs = 100 * 1000)
        {
            readText = "";
            status = TxtDownloadStatus.Failed;
            bool success = false;

            // タイムアウト付きのクライアントを使う。以前はこのクラスが定義されているのに
            // 使われておらず、タイムアウトを指定できなかった（Issue #53）。
            using (var web = new NicoranWebClient(timeoutMs))
            {
                web.Headers.Add("User-Agent", "WeeklyNicoranProgram");



                Func<int, int> calculateDelayMax = (int retryCount) =>
                {
                    // TxtDownLoad メソッド内の該当箇所
                    const int baseMin = 1000; // random.Next の第1引数に合わせる
                    const int capMs = 30_000; // 過度なスリープを防ぐ上限

                    // 2^retryCount * 100 を計算（負やオーバーフローを避ける）
                    double raw = Math.Pow(2.0, Math.Max(0, retryCount)) * 100.0;
                    if (double.IsInfinity(raw) || raw > capMs) raw = capMs;

                    // 必ず baseMin より大きくする（random.Next(min, max) は min < max が必要）
                    int delayMax = (int)Math.Max(raw, baseMin + 1);

                    return delayMax;
                };
                Random random = new Random();

                int attempts = Math.Max(1, maxAttempts);
                for (int count = 0; count < attempts; count++)
                {
                    // 前回試行の区分を引き継がない（404 の後に通信エラーで終わった場合に
                    // NotFound のままになると、メンテナンス日と誤認するため）
                    status = TxtDownloadStatus.Failed;
                    try
                    {
                        using (var stream = web.OpenRead(url))
                        {
                            var reader = new StreamReader(stream, Encoding.GetEncoding(encode));
                            readText = reader.ReadToEnd();
                        }
                        success = true;
                        status = TxtDownloadStatus.Success;
                    }
                    catch (WebException ex)
                    {
                        if (ex.Status == WebExceptionStatus.ProtocolError)
                        {
                            if (ex.Response is HttpWebResponse httpResponse
                                && httpResponse.StatusCode == HttpStatusCode.NotFound)
                            {
                                status = TxtDownloadStatus.NotFound;
                                if (!retryOnNotFound)
                                {
                                    // 404 は「データが無い」の確定。再試行しても結果は変わらない
                                    break;
                                }
                            }
                            else if (ex.Response != null && ex.Response.ContentType == "application/xml")
                            {
                                //真面目にやるのであれば中身をちゃんと解析するべきだが、AccessDeniedと見直して、再試行を抜ける
                                break;
                            }
                        }
                        // リトライ時は指数バックオフで待つ。進捗の直接Console出力は行わない（表示は呼び出し側のStatusLogに任せる。Issue #43）。
                        if (count + 1 >= attempts)
                        {
                            // 最後の試行の後に待っても無駄なので、待たずに抜ける
                            break;
                        }
                        Thread.Sleep(random.Next(1000, calculateDelayMax(count)));
                        continue;
                    }
                    catch (System.IO.IOException)
                    {
                        if (count + 1 >= attempts)
                        {
                            break;
                        }
                        Thread.Sleep(random.Next(1000, calculateDelayMax(count)));
                        continue;
                    }
                    catch (Exception)
                    {
                        if (count + 1 >= attempts)
                        {
                            break;
                        }
                        Thread.Sleep(random.Next(1000, calculateDelayMax(count)));
                        continue;
                    }
                    break;
                }
            }

            // success フラグで判定する。以前は stream の非nullで判定していたため、
            // 読み込み途中で例外が出た場合に成功扱いになる可能性があった（Issue #53）。
            if (!success)
            {
                if (status != TxtDownloadStatus.NotFound)
                {
                    status = TxtDownloadStatus.Failed;
                }
                return false;
            }
            return true;
        }
        /// <summary>
        /// インターネット上のファイルを保存する
        /// </summary>
        /// <param name="url"></param>
        /// <param name="readText"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static bool FileDownLoad(string url, string fileName)
        {
            Stream stream = null;
            using (var web = new WebClient())
            {
                web.Headers.Add("User-Agent", "WeeklyNicoranProgram");
                for (int count = 0; count < 20; count++)
                {
                    try
                    {
                        using (stream = web.OpenRead(url))
                        {
                            using (var file = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                int read;
                                byte[] buffer = new byte[1024];
                                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    file.Write(buffer, 0, read);
                                }
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response.ContentType == "application/xml")
                        {
                            //真面目にやるのであれば中身をちゃんと解析するべきだが、AccessDeniedと見直して、再試行を抜ける
                            break;
                        }

                        switch (ex.Status)
                        {
                            case WebExceptionStatus.ProtocolError:
                            case WebExceptionStatus.Timeout:

                                Thread.Sleep(1000);
                                continue;
                        }
                    }
                    catch (System.IO.IOException)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    break;
                }
            }
            if (stream == null)
            {
                return false;
            }
            return true;
        }

    }
}
