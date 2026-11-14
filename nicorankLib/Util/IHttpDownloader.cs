namespace nicorankLib.Util
{
    /// <summary>
    /// HTTP取得の結果区分（Issue #56）。
    /// なぜ 404 と 403/429 を分けるか: 404 は「そのサイズの画像が無い」だけなので次の候補を試せるが、
    /// 403/429 はレート制限・アクセス拒否であり、リトライを重ねると状況を悪化させるため即座に諦める。
    /// </summary>
    public enum HttpDownloadStatus
    {
        Success,
        NotFound,
        Forbidden,
        RateLimited,
        ServerError,
        Failed,
    }

    /// <summary>
    /// HTTP取得の結果。テキスト取得では Text に本文が入る。
    /// StatusCode はログ用（0 は「HTTP応答を受け取れなかった」ことを表す）。
    /// </summary>
    public class HttpResult
    {
        public HttpDownloadStatus Status { get; set; }
        public int StatusCode { get; set; }
        public string Text { get; set; }

        public bool IsSuccess
        {
            get { return Status == HttpDownloadStatus.Success; }
        }

        public static HttpResult FromStatus(HttpDownloadStatus status, int statusCode, string text = null)
        {
            return new HttpResult() { Status = status, StatusCode = statusCode, Text = text };
        }
    }

    /// <summary>
    /// 画像・HTML取得の薄い抽象（Issue #56）。
    /// なぜインターフェースにするか: ダウンロードの順序・フォールバック・リトライ・間隔といった判断を
    /// ネットワークなしで単体テストできるようにするため。既存の InternetUtil は static で差し替えできず、
    /// また画像DLには 20 回リトライ・無タイムアウトという今回と合わない挙動が埋まっているため流用しない。
    /// </summary>
    public interface IHttpDownloader
    {
        /// <summary>テキストを取得する（視聴ページHTML用）。</summary>
        HttpResult GetText(string url);

        /// <summary>
        /// ファイルを取得して destinationPath に保存する。
        /// 成功時のみファイルを残す。失敗時は呼び出し側が一時ファイルを後始末できるよう、
        /// 実装側でも書きかけファイルを削除する。
        /// </summary>
        HttpResult DownloadFile(string url, string destinationPath);
    }
}
