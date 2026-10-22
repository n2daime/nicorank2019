using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// Issue #53: 404 を「リトライしない」で返せること。
    /// 実際のHTTPサーバの代わりにローカルのTCPリスナーで 404 を返し、
    /// 接続回数でリトライの有無を判定する（待ち時間に依存しない）。
    /// </summary>
    [TestClass]
    public class UnitTestInternetUtilNotFound
    {
        private static void ServeNotFound(TcpListener listener, Action onConnection)
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch
                {
                    // listener.Stop() で Accept が例外になる。サーバ終了の合図
                    break;
                }

                onConnection();
                using (client)
                using (var stream = client.GetStream())
                {
                    try
                    {
                        var buffer = new byte[1024];
                        stream.Read(buffer, 0, buffer.Length);
                    }
                    catch
                    {
                        // リクエスト読み取りの失敗は無視して404を返す
                    }

                    var response = "HTTP/1.1 404 Not Found\r\nContent-Type: text/html\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    var bytes = Encoding.ASCII.GetBytes(response);
                    try
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                    catch
                    {
                        // 切断済みなら無視
                    }
                }
            }
        }

        private static TcpListener StartServer(out int port, out Func<int> connectionCount)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;

            int count = 0;
            Task.Run(() => ServeNotFound(listener, () => Interlocked.Increment(ref count)));
            connectionCount = () => Volatile.Read(ref count);
            return listener;
        }

        [TestMethod]
        public void TxtDownLoad_404_RetryOnNotFoundFalse_StopsAfterFirstRequest()
        {
            var listener = StartServer(out int port, out Func<int> connectionCount);
            try
            {
                bool ok = InternetUtil.TxtDownLoad(
                    $"http://127.0.0.1:{port}/missing.json",
                    out string text,
                    out TxtDownloadStatus status,
                    retryOnNotFound: false,
                    maxAttempts: 2);

                Assert.IsFalse(ok);
                Assert.AreEqual(TxtDownloadStatus.NotFound, status);
                Assert.AreEqual("", text);
                // リトライしていれば2回以上接続する。1回で止まることを確認する
                Assert.AreEqual(1, connectionCount());
            }
            finally
            {
                listener.Stop();
            }
        }

        [TestMethod]
        public void TxtDownLoad_404_RetryOnNotFoundTrue_RetriesUpToMaxAttempts()
        {
            var listener = StartServer(out int port, out Func<int> connectionCount);
            try
            {
                // 既定（true）は従来どおりリトライする。maxAttempts=2 にして待ち時間を抑える
                bool ok = InternetUtil.TxtDownLoad(
                    $"http://127.0.0.1:{port}/missing.json",
                    out _,
                    out TxtDownloadStatus status,
                    maxAttempts: 2);

                Assert.IsFalse(ok);
                Assert.AreEqual(TxtDownloadStatus.NotFound, status);
                Assert.AreEqual(2, connectionCount());
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
