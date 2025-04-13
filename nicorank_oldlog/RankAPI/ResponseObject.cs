#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace nicorank_oldlog.RankAPI
{
    namespace ResGenres
    {
        public class Rootobject
        {
            /// <summary>
            /// メタデータ
            /// </summary>
            public Meta meta { get; set; }

            /// <summary>
            /// データ
            /// </summary>
            public Data data { get; set; }
        }

        public class Meta
        {
            /// <summary>
            /// ステータスコード
            /// </summary>
            public int status { get; set; }
        }

        public class Data
        {
            /// <summary>
            /// ジャンルの配列
            /// </summary>
            public Genre[] genres { get; set; }
        }

        public class Genre
        {
            /// <summary>
            /// ジャンルのキー
            /// </summary>
            public string key { get; set; }

            /// <summary>
            /// ジャンルのラベル
            /// </summary>
            public string label { get; set; }
        }
    }

    namespace ResTeibanGenres
    {
        public class Rootobject
        {
            /// <summary>
            /// メタデータ
            /// </summary>
            public Meta meta { get; set; }

            /// <summary>
            /// データ
            /// </summary>
            public Data data { get; set; }
        }

        public class Meta
        {
            /// <summary>
            /// ステータスコード
            /// </summary>
            public int status { get; set; }
        }

        public class Data
        {
            /// <summary>
            /// アイテムの配列
            /// </summary>
            public Item[] items { get; set; }

            /// <summary>
            /// 定義
            /// </summary>
            public Definition definition { get; set; }
        }

        public class Definition
        {
            /// <summary>
            /// 最大アイテム数
            /// </summary>
            public Maxitemcount maxItemCount { get; set; }
        }

        public class Maxitemcount
        {
            /// <summary>
            /// 定番の最大アイテム数
            /// </summary>
            public int teiban { get; set; }

            /// <summary>
            /// トレンドタグの最大アイテム数
            /// </summary>
            public int trendTag { get; set; }

            /// <summary>
            /// あなたへのおすすめの最大アイテム数
            /// </summary>
            public int forYou { get; set; }
        }

        public class Item
        {
            /// <summary>
            /// 特徴キー
            /// </summary>
            public string featuredKey { get; set; }

            /// <summary>
            /// ラベル
            /// </summary>
            public string label { get; set; }

            /// <summary>
            /// トレンドタグが有効かどうか
            /// </summary>
            public bool isEnabledTrendTag { get; set; }

            /// <summary>
            /// 主要な特徴かどうか
            /// </summary>
            public bool isMajorFeatured { get; set; }

            /// <summary>
            /// トップレベルかどうか
            /// </summary>
            public bool isTopLevel { get; set; }

            /// <summary>
            /// 不道徳かどうか
            /// </summary>
            public bool isImmoral { get; set; }

            /// <summary>
            /// 有効かどうか
            /// </summary>
            public bool isEnabled { get; set; }
        }
    }

    namespace ResGenreRanking
    {
        public class Rootobject
        {
            /// <summary>
            /// メタデータ
            /// </summary>
            public Meta meta { get; set; }

            /// <summary>
            /// データ
            /// </summary>
            public Data data { get; set; }
        }

        public class Meta
        {
            /// <summary>
            /// ステータスコード
            /// </summary>
            public int status { get; set; }
        }

        public class Data
        {
            /// <summary>
            /// アイテムの配列
            /// </summary>
            public Item[] items { get; set; }

            /// <summary>
            /// 次のページがあるかどうか
            /// </summary>
            public bool hasNext { get; set; }
        }

        public class Item
        {
            /// <summary>
            /// タイプ
            /// </summary>
            public string type { get; set; }

            /// <summary>
            /// ID
            /// </summary>
            public string id { get; set; }

            /// <summary>
            /// タイトル
            /// </summary>
            public string title { get; set; }

            /// <summary>
            /// 登録日時
            /// </summary>
            public DateTime registeredAt { get; set; }

            /// <summary>
            /// カウント情報
            /// </summary>
            public Count count { get; set; }

            /// <summary>
            /// サムネイル情報
            /// </summary>
            public Thumbnail thumbnail { get; set; }

            /// <summary>
            /// 再生時間
            /// </summary>
            public int duration { get; set; }

            /// <summary>
            /// 短い説明
            /// </summary>
            public string shortDescription { get; set; }

            /// <summary>
            /// 最新のコメント概要
            /// </summary>
            public string latestCommentSummary { get; set; }

            /// <summary>
            /// チャンネル動画かどうか
            /// </summary>
            public bool isChannelVideo { get; set; }

            /// <summary>
            /// 支払いが必要かどうか
            /// </summary>
            public bool isPaymentRequired { get; set; }

            /// <summary>
            /// 再生位置
            /// </summary>
            public double? playbackPosition { get; set; }

            /// <summary>
            /// オーナー情報
            /// </summary>
            public Owner owner { get; set; }

            /// <summary>
            /// センシティブなマスキングが必要かどうか
            /// </summary>
            public bool requireSensitiveMasking { get; set; }

            /// <summary>
            /// ライブビデオ情報
            /// </summary>
            public object videoLive { get; set; }

            /// <summary>
            /// ミュートされているかどうか
            /// </summary>
            public bool isMuted { get; set; }

            /// <summary>
            /// 不明なプロパティ
            /// </summary>
            public bool _9d091f87 { get; set; }

            /// <summary>
            /// 不明なプロパティ
            /// </summary>
            public bool acf68865 { get; set; }
        }

        public class Count
        {
            /// <summary>
            /// 再生数
            /// </summary>
            public int view { get; set; }

            /// <summary>
            /// コメント数
            /// </summary>
            public int comment { get; set; }

            /// <summary>
            /// マイリスト数
            /// </summary>
            public int mylist { get; set; }

            /// <summary>
            /// いいね数
            /// </summary>
            public int like { get; set; }
        }

        public class Thumbnail
        {
            /// <summary>
            /// URL
            /// </summary>
            public string url { get; set; }

            /// <summary>
            /// 中サイズのURL
            /// </summary>
            public string middleUrl { get; set; }

            /// <summary>
            /// 大サイズのURL
            /// </summary>
            public string largeUrl { get; set; }

            /// <summary>
            /// リスティングURL
            /// </summary>
            public string listingUrl { get; set; }

            /// <summary>
            /// 高解像度URL
            /// </summary>
            public string nHdUrl { get; set; }
        }

        public class Owner
        {
            /// <summary>
            /// オーナーのタイプ
            /// </summary>
            public string ownerType { get; set; }

            /// <summary>
            /// タイプ
            /// </summary>
            public string type { get; set; }

            /// <summary>
            /// 可視性
            /// </summary>
            public string visibility { get; set; }

            /// <summary>
            /// ID
            /// </summary>
            public string id { get; set; }

            /// <summary>
            /// 名前
            /// </summary>
            public string name { get; set; }

            /// <summary>
            /// アイコンURL
            /// </summary>
            public string iconUrl { get; set; }
        }
    }

    namespace ResTeibanRanking
    {
        public class Rootobject
        {
            /// <summary>
            /// メタデータ
            /// </summary>
            public Meta meta { get; set; }

            /// <summary>
            /// データ
            /// </summary>
            public Data data { get; set; }
        }

        public class Meta
        {
            /// <summary>
            /// ステータスコード
            /// </summary>
            public int status { get; set; }
        }

        public class Data
        {
            /// <summary>
            /// 特徴キー
            /// </summary>
            public string featuredKey { get; set; }

            /// <summary>
            /// ラベル
            /// </summary>
            public string label { get; set; }

            /// <summary>
            /// タグ
            /// </summary>
            public object tag { get; set; }

            /// <summary>
            /// 最大アイテム数
            /// </summary>
            public int maxItemCount { get; set; }

            /// <summary>
            /// アイテムの配列
            /// </summary>
            public Item[] items { get; set; }

            /// <summary>
            /// 次のページがあるかどうか
            /// </summary>
            public bool hasNext { get; set; }
        }

        public class Item
        {
            /// <summary>
            /// タイプ
            /// </summary>
            public string type { get; set; }

            /// <summary>
            /// ID
            /// </summary>
            public string id { get; set; }

            /// <summary>
            /// タイトル
            /// </summary>
            public string title { get; set; }

            /// <summary>
            /// 登録日時
            /// </summary>
            public DateTime registeredAt { get; set; }

            /// <summary>
            /// カウント情報
            /// </summary>
            public Count count { get; set; }

            /// <summary>
            /// サムネイル情報
            /// </summary>
            public Thumbnail thumbnail { get; set; }

            /// <summary>
            /// 再生時間
            /// </summary>
            public int duration { get; set; }

            /// <summary>
            /// 短い説明
            /// </summary>
            public string shortDescription { get; set; }

            /// <summary>
            /// 最新のコメント概要
            /// </summary>
            public string latestCommentSummary { get; set; }

            /// <summary>
            /// チャンネル動画かどうか
            /// </summary>
            public bool isChannelVideo { get; set; }

            /// <summary>
            /// 支払いが必要かどうか
            /// </summary>
            public bool isPaymentRequired { get; set; }

            /// <summary>
            /// 再生位置
            /// </summary>
            public float? playbackPosition { get; set; }

            /// <summary>
            /// オーナー情報
            /// </summary>
            public Owner owner { get; set; }

            /// <summary>
            /// センシティブなマスキングが必要かどうか
            /// </summary>
            public bool requireSensitiveMasking { get; set; }

            /// <summary>
            /// ライブビデオ情報
            /// </summary>
            public object videoLive { get; set; }

            /// <summary>
            /// ミュートされているかどうか
            /// </summary>
            public bool isMuted { get; set; }

            /// <summary>
            /// 不明なプロパティ
            /// </summary>
            public bool _9d091f87 { get; set; }

            /// <summary>
            /// 不明なプロパティ
            /// </summary>
            public bool acf68865 { get; set; }
        }

        public class Count
        {
            /// <summary>
            /// 再生数
            /// </summary>
            public int view { get; set; }

            /// <summary>
            /// コメント数
            /// </summary>
            public int comment { get; set; }

            /// <summary>
            /// マイリスト数
            /// </summary>
            public int mylist { get; set; }

            /// <summary>
            /// いいね数
            /// </summary>
            public int like { get; set; }
        }

        public class Thumbnail
        {
            /// <summary>
            /// URL
            /// </summary>
            public string url { get; set; }

            /// <summary>
            /// 中サイズのURL
            /// </summary>
            public string middleUrl { get; set; }

            /// <summary>
            /// 大サイズのURL
            /// </summary>
            public string largeUrl { get; set; }

            /// <summary>
            /// リスティングURL
            /// </summary>
            public string listingUrl { get; set; }

            /// <summary>
            /// 高解像度URL
            /// </summary>
            public string nHdUrl { get; set; }
        }

        public class Owner
        {
            /// <summary>
            /// オーナーのタイプ
            /// </summary>
            public string ownerType { get; set; }

            /// <summary>
            /// タイプ
            /// </summary>
            public string type { get; set; }

            /// <summary>
            /// 可視性
            /// </summary>
            public string visibility { get; set; }

            /// <summary>
            /// ID
            /// </summary>
            public string id { get; set; }

            /// <summary>
            /// 名前
            /// </summary>
            public string name { get; set; }

            /// <summary>
            /// アイコンURL
            /// </summary>
            public string iconUrl { get; set; }
        }
    }

    namespace ResGetTrendTag
    {
        public class Rootobject
        {
            /// <summary>
            /// メタデータ
            /// </summary>
            public Meta meta { get; set; }

            /// <summary>
            /// データ
            /// </summary>
            public Data data { get; set; }
        }

        public class Meta
        {
            /// <summary>
            /// ステータスコード
            /// </summary>
            public int status { get; set; }
        }

        public class Data
        {
            /// <summary>
            /// 特徴キー
            /// </summary>
            public string featuredKey { get; set; }

            /// <summary>
            /// ラベル
            /// </summary>
            public string label { get; set; }

            /// <summary>
            /// トップレベルかどうか
            /// </summary>
            public bool isTopLevel { get; set; }

            /// <summary>
            /// 不道徳かどうか
            /// </summary>
            public bool isImmoral { get; set; }

            /// <summary>
            /// トレンドタグの配列
            /// </summary>
            public string[] trendTags { get; set; }
        }
    }
}
