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
    /// コンフィグ設定を管理するクラス
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

        /// <summary>
        /// ConvertConfigのインスタンスを取得します。
        /// </summary>
        /// <returns>ConvertConfigのインスタンス</returns>
        public static ConvertConfig? GetInstance()
        {
            if (ConvertConfig.Instance == null)
            {
                ConvertConfig.Instance = ConvertConfig.Initilize();
            }
            return ConvertConfig.Instance;
        }

        /// <summary>
        /// ConvertConfigのインスタンスを初期化します。
        /// </summary>
        /// <returns>初期化されたConvertConfigのインスタンス</returns>
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

        /// <summary>
        /// JSONシリアライザーの設定
        /// </summary>
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

    /// <summary>
    /// ランキング情報を管理するクラス
    /// </summary>
    public class Ranking_Info
    {
        /// <summary>
        /// フォルダ名
        /// </summary>
        public string folder { get; set; } = "";

        /// <summary>
        /// 期間
        /// </summary>
        public string term { get; set; } = "";
    }

    /// <summary>
    /// ジャンル情報を管理するクラス
    /// </summary>
    public class GenreInfo
    {
        /// <summary>
        /// ジャンル名
        /// </summary>
        public string genre { get; set; } = "";

        /// <summary>
        /// ジャンルキー
        /// </summary>
        public string genrekey { get; set; } = "";

        /// <summary>
        /// 特徴キー
        /// </summary>
        public string featuredKey { get; set; } = "";

        /// <summary>
        /// タグ
        /// </summary>
        public object? tag { get; set; } = "";

        /// <summary>
        /// ファイル名
        /// </summary>
        public string file { get; set; } = "";

        /// <summary>
        /// トレンドタグが有効かどうか
        /// </summary>
        public bool isEnabledTrendTag { get; set; } = false;

        /// <summary>
        /// ジャンルランキングに存在するものかどうか
        /// </summary>
        public bool isGenre { get; set; } = false;

        /// <summary>
        /// ジャンルランキングから取得するかどうか
        /// </summary>
        public bool isGenreRank { get; set; } = false;

        /// <summary>
        /// 定番ランキングから取得するかどうか
        /// </summary>
        public bool isTeibanRank { get; set; } = false;
    }
}
