using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace nicorankLib.Analyze.model
{

    public class RankLogJson
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string title { get; set; }

        [JsonProperty("registeredAt")]
        public DateTimeOffset RegisteredAt { get; set; }

        [JsonProperty("count")]
        public Count Count { get; set; }

        [JsonProperty("playtime")]
        public string PlayTime { get; set; }


        [JsonProperty("thumbnail")]
        public Thumbnail Thumbnail { get; set; }


        public static RankLogJson[] FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RankLogJson[]>(json, RankLogJson.Settings);
        }

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    public class Count
    {
        [JsonProperty("view")]
        public long View { get; set; }

        [JsonProperty("comment")]
        public long Comment { get; set; }

        [JsonProperty("mylist")]
        public long Mylist { get; set; }

        [JsonProperty("like")]
        public long Like { get; set; }
    }

    public class Thumbnail
    {
        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("middleUrl")]
        public Uri MiddleUrl { get; set; }

        [JsonProperty("largeUrl")]
        public Uri LargeUrl { get; set; }

        /// <summary>
        /// 一番が質の良いサムネイルを取得する
        /// </summary>
        /// <returns></returns>
        public string GetBestUrl()
        {
            if(this.LargeUrl != null )
            {
                return this.LargeUrl.ToString();
            }
            if (this.MiddleUrl != null)
            {
                return this.MiddleUrl.ToString();
            }
            return this.Url.ToString();
        }
    }

}