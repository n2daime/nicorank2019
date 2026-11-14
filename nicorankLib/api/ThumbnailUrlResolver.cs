using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace nicorankLib.api
{
    /// <summary>
    /// 視聴ページの server-response から取り出したサムネイルURL群（Issue #56）。
    /// player / ogp / short はキー付きURLで、サイズ部分だけを書き換えると 400 になる。
    /// そのため必要なサイズのURLをそのまま保持し、後段は候補として順番に試すだけにする。
    /// </summary>
    public class ThumbnailPageInfo
    {
        public string Normal { get; set; }
        public string Middle { get; set; }
        public string Large { get; set; }
        public string Player { get; set; }
        public string Ogp { get; set; }
        public string Short { get; set; }
    }

    /// <summary>
    /// ニコニコ動画のサムネイルURLを解決する（Issue #56）。
    /// 外部仕様の実測値は docs/knowledge/thumbnail.md にまとめてある。
    /// なぜ HTML を読むか: 1280x720 の ogp・960x540 の player・ショート用の short は
    /// getthumbinfo では返らず、視聴ページの server-response にのみ含まれるため。
    /// v3_guest は nicosid Cookie が必須なので採用していない（同ドキュメント参照）。
    /// このクラスはネットワークを使わない。HTMLの取得は呼び出し側（ImageDownloader）が行い、
    /// ここでは文字列の解析とURL候補の組み立てだけを行う（単体テストで検証できるようにするため）。
    /// </summary>
    public static class ThumbnailUrlResolver
    {
        private const string WatchPageUrlFormat = "https://www.nicovideo.jp/watch/{0}";

        // server-response の meta タグ。属性の順序が変わっても拾えるよう2パターン試す。
        private static readonly Regex ServerResponseNameFirst =
            new Regex("<meta[^>]*name=\"server-response\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase);

        private static readonly Regex ServerResponseContentFirst =
            new Regex("<meta[^>]*content=\"([^\"]*)\"[^>]*name=\"server-response\"", RegexOptions.IgnoreCase);

        // server-response が無い場合の保険。og:image は goptim の 1280x720 URL のみ。
        private static readonly Regex OgImagePropertyFirst =
            new Regex("<meta[^>]*property=\"og:image\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase);

        private static readonly Regex OgImageContentFirst =
            new Regex("<meta[^>]*content=\"([^\"]*)\"[^>]*property=\"og:image\"", RegexOptions.IgnoreCase);

        /// <summary>視聴ページのURL。Cookieは不要（docs/knowledge/thumbnail.md §4）。</summary>
        public static string GetWatchPageUrl(string videoId)
        {
            return string.Format(WatchPageUrlFormat, videoId);
        }

        /// <summary>
        /// 末尾のサイズ指定（.L / .M）とクエリを落として base URL に戻す。
        /// なぜ必要か: 週刊集計の ThumbnailURL は公式JSONの largeUrl（既に .L）であり、
        /// SP・タグは getthumbinfo の base である。無条件に .L を付けると「...L.L」になるため、
        /// 必ず base に正規化してから候補を組み立てる。
        /// </summary>
        public static string NormalizeToBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            string value = url.Trim();

            int queryIndex = value.IndexOf('?');
            if (queryIndex >= 0)
            {
                value = value.Substring(0, queryIndex);
            }

            if (value.EndsWith(".L", StringComparison.Ordinal) || value.EndsWith(".M", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 2);
            }

            return value;
        }

        /// <summary>
        /// 視聴ページHTMLから server-response を読み、サムネイルURL群を返す。
        /// 見つからない・壊れている場合は null（呼び出し側は .L へフォールバックする）。
        /// </summary>
        public static ThumbnailPageInfo ParseServerResponse(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return null;
            }

            Match match = ServerResponseNameFirst.Match(html);
            if (!match.Success)
            {
                match = ServerResponseContentFirst.Match(html);
            }

            if (match.Success)
            {
                ThumbnailPageInfo parsed = ParseResponseJson(match.Groups[1].Value);
                if (parsed != null)
                {
                    return parsed;
                }
            }

            // 最後の保険として og:image だけを使う。goptim の 1280x720 が入る。
            Match ogImage = OgImagePropertyFirst.Match(html);
            if (!ogImage.Success)
            {
                ogImage = OgImageContentFirst.Match(html);
            }
            if (ogImage.Success)
            {
                string url = DecodeHtml(ogImage.Groups[1].Value);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return new ThumbnailPageInfo() { Ogp = url };
                }
            }

            return null;
        }

        /// <summary>
        /// ダウンロード候補URLを優先順に返す。
        /// 既定: base.L（360x270）→ base（130x100）。
        /// 高画質: short（ショートのみ）→ ogp（1280x720）→ player（960x540）→ base.L → base。
        /// short はショート動画以外では null なので、動画IDの接頭辞判定は不要である。
        /// </summary>
        public static IReadOnlyList<string> BuildCandidateUrls(string thumbnailUrl, ThumbnailPageInfo page, bool highQuality)
        {
            var candidates = new List<string>();
            string baseUrl = NormalizeToBaseUrl(thumbnailUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return candidates;
            }

            if (highQuality && page != null)
            {
                AddIfNew(candidates, page.Short);
                AddIfNew(candidates, page.Ogp);
                AddIfNew(candidates, page.Player);
            }

            AddIfNew(candidates, baseUrl + ".L");
            AddIfNew(candidates, baseUrl);

            return candidates;
        }

        /// <summary>ユーザーアイコンは getthumbinfo のアイコンURLをそのまま使う（高画質の概念がない）。</summary>
        public static IReadOnlyList<string> BuildUserIconCandidateUrls(string userIconUrl)
        {
            var candidates = new List<string>();
            AddIfNew(candidates, userIconUrl);
            return candidates;
        }

        private static ThumbnailPageInfo ParseResponseJson(string escapedJson)
        {
            string json = DecodeHtml(escapedJson);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                JObject root = JObject.Parse(json);
                JToken thumbnail = root["data"]?["response"]?["$watchV4"]?["data"]?["video"]?["thumbnail"];
                if (thumbnail == null)
                {
                    return null;
                }

                return new ThumbnailPageInfo()
                {
                    Normal = (string)thumbnail["normal"],
                    Middle = (string)thumbnail["middle"],
                    Large = (string)thumbnail["large"],
                    Player = (string)thumbnail["player"],
                    Ogp = (string)thumbnail["ogp"],
                    Short = (string)thumbnail["short"],
                };
            }
            catch (Exception)
            {
                // JSON の形が変わった場合は高画質を諦めて .L にフォールバックする。
                // ここで例外を投げると画像DL全体が止まってしまうため、握りつぶして null を返す。
                return null;
            }
        }

        private static string DecodeHtml(string value)
        {
            try
            {
                return WebUtility.HtmlDecode(value);
            }
            catch (Exception)
            {
                return value;
            }
        }

        private static void AddIfNew(List<string> candidates, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            string trimmed = url.Trim();
            foreach (string existing in candidates)
            {
                if (string.Equals(existing, trimmed, StringComparison.Ordinal))
                {
                    return;
                }
            }
            candidates.Add(trimmed);
        }
    }
}
