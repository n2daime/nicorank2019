using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace nicorank_oldlog.RankAPI
{
    namespace ResGenres
    {
        public class Rootobject
        {
            public Meta meta { get; set; }
            public Data data { get; set; }
        }

        public class Meta
        {
            public int status { get; set; }
        }

        public class Data
        {
            public Genre[] genres { get; set; }
        }

        public class Genre
        {
            public string key { get; set; }
            public string label { get; set; }
        }
    }
    namespace ResTeibanGenres
    {

        public class Rootobject
        {
            public Meta meta { get; set; }
            public Data data { get; set; }
        }

        public class Meta
        {
            public int status { get; set; }
        }

        public class Data
        {
            public Item[] items { get; set; }
            public Definition definition { get; set; }
        }

        public class Definition
        {
            public Maxitemcount maxItemCount { get; set; }
        }

        public class Maxitemcount
        {
            public int teiban { get; set; }
            public int trendTag { get; set; }
            public int forYou { get; set; }
        }

        public class Item
        {
            public string featuredKey { get; set; }
            public string label { get; set; }
            public bool isEnabledTrendTag { get; set; }
            public bool isMajorFeatured { get; set; }
            public bool isTopLevel { get; set; }
            public bool isImmoral { get; set; }
            public bool isEnabled { get; set; }
        }

    }
    namespace ResGenreRanking
    {

        public class Rootobject
        {
            public Meta meta { get; set; }
            public Data data { get; set; }
        }

        public class Meta
        {
            public int status { get; set; }
        }

        public class Data
        {
            public Item[] items { get; set; }
            public bool hasNext { get; set; }
        }

        public class Item
        {
            public string type { get; set; }
            public string id { get; set; }
            public string title { get; set; }
            public DateTime registeredAt { get; set; }
            public Count count { get; set; }
            public Thumbnail thumbnail { get; set; }
            public int duration { get; set; }
            public string shortDescription { get; set; }
            public string latestCommentSummary { get; set; }
            public bool isChannelVideo { get; set; }
            public bool isPaymentRequired { get; set; }
            public double? playbackPosition { get; set; }
            public Owner owner { get; set; }
            public bool requireSensitiveMasking { get; set; }
            public object videoLive { get; set; }
            public bool isMuted { get; set; }
            public bool _9d091f87 { get; set; }
            public bool acf68865 { get; set; }
        }

        public class Count
        {
            public int view { get; set; }
            public int comment { get; set; }
            public int mylist { get; set; }
            public int like { get; set; }
        }

        public class Thumbnail
        {
            public string url { get; set; }
            public string middleUrl { get; set; }
            public string largeUrl { get; set; }
            public string listingUrl { get; set; }
            public string nHdUrl { get; set; }
        }

        public class Owner
        {
            public string ownerType { get; set; }
            public string type { get; set; }
            public string visibility { get; set; }
            public string id { get; set; }
            public string name { get; set; }
            public string iconUrl { get; set; }
        }

    }
    namespace ResTeibanRanking
    {

        public class Rootobject
        {
            public Meta meta { get; set; }
            public Data data { get; set; }
        }

        public class Meta
        {
            public int status { get; set; }
        }

        public class Data
        {
            public string featuredKey { get; set; }
            public string label { get; set; }
            public object tag { get; set; }
            public int maxItemCount { get; set; }
            public Item[] items { get; set; }
            public bool hasNext { get; set; }
        }

        public class Item
        {
            public string type { get; set; }
            public string id { get; set; }
            public string title { get; set; }
            public DateTime registeredAt { get; set; }
            public Count count { get; set; }
            public Thumbnail thumbnail { get; set; }
            public int duration { get; set; }
            public string shortDescription { get; set; }
            public string latestCommentSummary { get; set; }
            public bool isChannelVideo { get; set; }
            public bool isPaymentRequired { get; set; }
            public float? playbackPosition { get; set; }
            public Owner owner { get; set; }
            public bool requireSensitiveMasking { get; set; }
            public object videoLive { get; set; }
            public bool isMuted { get; set; }
            public bool _9d091f87 { get; set; }
            public bool acf68865 { get; set; }
        }

        public class Count
        {
            public int view { get; set; }
            public int comment { get; set; }
            public int mylist { get; set; }
            public int like { get; set; }
        }

        public class Thumbnail
        {
            public string url { get; set; }
            public string middleUrl { get; set; }
            public string largeUrl { get; set; }
            public string listingUrl { get; set; }
            public string nHdUrl { get; set; }
        }

        public class Owner
        {
            public string ownerType { get; set; }
            public string type { get; set; }
            public string visibility { get; set; }
            public string id { get; set; }
            public string name { get; set; }
            public string iconUrl { get; set; }
        }
    }
    namespace ResGetTrendTag
    {

        public class Rootobject
        {
            public Meta meta { get; set; }
            public Data data { get; set; }
        }

        public class Meta
        {
            public int status { get; set; }
        }

        public class Data
        {
            public string featuredKey { get; set; }
            public string label { get; set; }
            public bool isTopLevel { get; set; }
            public bool isImmoral { get; set; }
            public string[] trendTags { get; set; }
        }
    }
}

