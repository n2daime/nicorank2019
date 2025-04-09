using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using nicorankLib.Common;
using nicorankLib.Util;
using nicorankLib.Util.Text;

namespace nicorank_oldlog
{

    /// <summary>
    /// 
    /// </summary>
    public class ConvertConfig
    {
        /// <summary>
        /// デイリーランキング用設定
        /// </summary>
        public Ranking_Info ranking_daily { get; set; } = new Ranking_Info();
        public Ranking_Info ranking_weekly { get; set; } = new Ranking_Info();
        public Ranking_Info ranking_monthly { get; set; } = new Ranking_Info();
        public Ranking_Info ranking_total { get; set; } = new Ranking_Info();

        protected static ConvertConfig? Instance = null;

        public static ConvertConfig? GetInstance()
        {
            if (ConvertConfig.Instance == null)
            {
                ConvertConfig.Instance = ConvertConfig.Initilize();
            }
            return ConvertConfig.Instance;
        }

        protected static ConvertConfig? Initilize()
        {
            try
            {
                bool isOpened = TextUtil.ReadText("config.json", out string json);
                if (!isOpened)
                {
                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"config.jsonの読み取りでエラーが発生(ConvertConfig::Initilize)");
                    return null;
                }

                return JsonConvert.DeserializeObject<ConvertConfig>(json, ConvertConfig.Settings);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return null; 
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

    public class Ranking_Info
    {
        public string folder { get; set; } = "";
        public string term { get; set; } = "";
    }

    public class GenreInfo
    {
        public string genre { get; set; } = "";
        public string genrekey { get; set; } = "";
        public string featuredKey { get; set; } = "";
        public object? tag { get; set; } = "";
        public string file { get; set; } = "";

        public bool isEnabledTrendTag { get; set; } = false;

        // genreはジャンルランキングに存在するものかどうか
        public bool isGenre { get; set; } = false;

        // genreランキングから取得するかどうか
        public bool isGenreRank { get; set; } = false;

        // 定番ランキングから取得するかどうか
        public bool isTeibanRank { get; set; } = false;
    }


}
