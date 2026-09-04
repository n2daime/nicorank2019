using System;
using System.Collections.Generic;
using System.Text;

namespace nicorankLib.Util
{
    /// <summary>
    /// GETクエリパラメータ付きURLの組み立て（Issue #19）
    /// キーはそのまま、値は Uri.EscapeDataString でエンコードする。ベースURLにクエリがあれば &amp; で連結する。
    /// </summary>
    public static class ApiUrlBuilder
    {
        /// <summary>
        /// クエリパラメータ付きURLを組み立てる
        /// </summary>
        /// <param name="baseUrl">クエリなし（またはクエリ付き）のベースURL</param>
        /// <param name="query">追加のクエリパラメータ。null・空ならベースURLをそのまま返す</param>
        /// <returns>組み立てたURL</returns>
        public static string Build(string baseUrl, IDictionary<string, string> query)
        {
            var url = new StringBuilder(baseUrl);
            string separator = baseUrl.Contains("?") ? "&" : "?";
            if (query != null)
            {
                foreach (var param in query)
                {
                    url.Append(separator).Append(param.Key).Append('=').Append(Uri.EscapeDataString(param.Value ?? ""));
                    separator = "&";
                }
            }
            return url.ToString();
        }
    }
}
