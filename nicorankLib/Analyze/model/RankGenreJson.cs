
using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace nicorankLib.Analyze.model
{


    public class RankGenreJson
    {
        [JsonProperty("genre")]
        public string Genre { get; set; }

        [JsonProperty("tag")]
        public object Tag { get; set; }

        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("isGenre")]
        public bool isGenre { get; set; } = true;

        public static RankGenreJson[] FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RankGenreJson[]>(json, RankGenreJson.Settings);
        }

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            }
        };
    
    }
}
