using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// タグ検索条件式（例: タグ1&amp;タグ2|タグ3*）をスナップショット検索API v2 の jsonFilter に変換する。
    /// &amp; は AND（優先）・| は OR・* 付きは tags部分一致・* なしは tagsExact完全一致。括弧は使えない。
    /// </summary>
    public static class TagConditionParser
    {
        /// <summary>部分一致の対象フィールド</summary>
        public const string FieldTags = "tags";
        /// <summary>完全一致の対象フィールド</summary>
        public const string FieldTagsExact = "tagsExact";

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
            var orFilters = new JArray();
            foreach (var group in condition.Split('|'))
            {
                var andFilters = new JArray();
                foreach (var rawTerm in group.Split('&'))
                {
                    var term = rawTerm.Trim();
                    if (term.Length == 0)
                    {
                        error = "空のタグがあります";
                        return false;
                    }
                    string field = FieldTagsExact;
                    if (term.Contains("*"))
                    {
                        field = FieldTags;
                        term = term.Replace("*", "").Trim();
                    }
                    if (term.Length == 0)
                    {
                        error = "空のタグがあります";
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
