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
    public class InternetUtil
    {
        private class NicoranWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = 5 * 1000;
                return w;
            }
        }


        /// <summary>
        /// インターネット上のテキストを読み込む
        /// </summary>
        /// <param name="url"></param>
        /// <param name="readText"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static bool TxtDownLoad(string url, out string readText, string encode = "UTF-8")
        {
            readText = "";
            Stream stream = null;
            using (var web = new WebClient())
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

                for (int count = 0; count < 20; count++)
                {
                    try
                    {
                        using (stream = web.OpenRead(url))
                        {
                            var reader = new StreamReader(stream, Encoding.GetEncoding(encode));
                            readText = reader.ReadToEnd();
                        }
                    }
                    catch (WebException ex)
                    {
                        if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response.ContentType == "application/xml")
                        {
                            //真面目にやるのであれば中身をちゃんと解析するべきだが、AccessDeniedと見直して、再試行を抜ける
                            break;
                        }
                        //Console.WriteLine($"\nWebException:{ex.Message} retry:{count}");
                        int delayMax = calculateDelayMax(count);
                        int delay = random.Next(1000, delayMax);
                        Thread.Sleep(delay);
                        continue;
                    }
                    catch (System.IO.IOException)
                    {
                        int delayMax = calculateDelayMax(count);
                        int delay = random.Next(1000, delayMax);
                        Thread.Sleep(delay);
                        continue;
                    }
                    catch (Exception)
                    {
                        int delayMax = calculateDelayMax(count);
                        int delay = random.Next(1000, delayMax);
                        Thread.Sleep(delay);
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
