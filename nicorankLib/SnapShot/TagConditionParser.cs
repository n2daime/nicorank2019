using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// タグ検索条件式（例: タグ1 タグ2|タグ3*）をスナップショット検索API v2 の jsonFilter に変換する。
    /// 空白は AND（優先）・| は OR・語尾の * は tags 部分一致・それ以外は tagsExact 完全一致。括弧は使えない。
    /// &amp; はタグの文字として扱う。ニコニコのタグ名には &amp; を含むもの（例: Akira,Jeans&amp;Chinpo）があり、
    /// これを AND 演算子にすると検索できないため、AND は公式のタグ検索と同じ空白で表す。
    /// </summary>
    public static class TagConditionParser
    {
        /// <summary>部分一致の対象フィールド</summary>
        public const string FieldTags = "tags";
        /// <summary>完全一致の対象フィールド</summary>
        public const string FieldTagsExact = "tagsExact";
        /// <summary>部分一致を表す記号。語尾に1つだけ置ける</summary>
        public const char PartialMatchSuffix = '*';
        /// <summary>全角スペース。AND区切りとして半角スペースへ正規化する</summary>
        public const char IdeographicSpace = '\u3000';

        /// <summary>
        /// 条件式を jsonFilter の JSON 文字列に変換する
        /// </summary>
        /// <param name="condition">タグ条件式</param>
        /// <param name="jsonFilter">変換結果の JSON（失敗時は null）</param>
        /// <param name="error">失敗時の理由（成功時は null）</param>
        /// <returns>変換に成功したら true</returns>
        public static bool TryParse(string condition, out string jsonFilter, out string error)
        {
            if (!TryParseNode(condition, out JObject node, out error))
            {
                jsonFilter = null;
                return false;
            }
            jsonFilter = node.ToString(Formatting.None);
            return true;
        }

        /// <summary>
        /// 条件式を jsonFilter の JObject に変換する（他の条件と組み合わせる場合用）
        /// </summary>
        public static bool TryParseNode(string condition, out JObject node, out string error)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(condition))
            {
                error = "タグ条件を入力してください";
                return false;
            }
            // 全角スペースはIMEで混入しやすい。半角へ寄せてから区切る（ニコ動タグに全角スペースを含むものは確認できていない）
            string normalized = condition.Replace(IdeographicSpace, ' ');
            var orFilters = new JArray();
            foreach (var group in normalized.Split('|'))
            {
                // 空白区切り。RemoveEmptyEntries で連続空白を1区切りにし、語の前後の空白も落とす。
                // ただし空配列をそのまま通すと「空のANDフィルタ」を生成してしまうため、件数を明示的に確認する。
                var terms = group.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (terms.Length == 0)
                {
                    error = "「|」の前後にタグがありません";
                    return false;
                }

                var andFilters = new JArray();
                foreach (var rawTerm in terms)
                {
                    var term = rawTerm.Trim();
                    string field = FieldTagsExact;
                    bool partialMatch = term[term.Length - 1] == PartialMatchSuffix;
                    if (partialMatch)
                    {
                        // 末尾の * だけを部分一致マーカーとして外す。途中の * はタグの文字として残す（DECO*27 など）
                        field = FieldTags;
                        term = term.Substring(0, term.Length - 1).Trim();
                    }
                    if (term.Length == 0)
                    {
                        // * だけの入力は「空のタグ」ではなく、記号の使い方の誤りとして案内する
                        error = partialMatch ? "「*」の前後にタグがありません" : "空のタグがあります";
                        return false;
                    }
                    andFilters.Add(new JObject(
                        new JProperty("type", "equal"),
                        new JProperty("field", field),
                        new JProperty("value", term)));
                }
                if (andFilters.Count == 1)
                {
                    orFilters.Add(andFilters[0]);
                }
                else
                {
                    orFilters.Add(new JObject(
                        new JProperty("type", "and"),
                        new JProperty("filters", andFilters)));
                }
            }
            if (orFilters.Count == 1)
            {
                node = (JObject)orFilters[0];
            }
            else
            {
                node = new JObject(
                    new JProperty("type", "or"),
                    new JProperty("filters", orFilters));
            }
            error = null;
            return true;
        }
    }
}
