using nicorankLib.Analyze.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace nicorankLib.api.model
{

    [XmlRoot(ElementName = "nicovideo_thumb_response")]
    public class ThumbinfoBase
    {
        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlElement(ElementName = "thumb")]
        public ThumbBase Thumb { get; set; }

        public string XML;
        public Ranking Ranking;
    }

    [XmlRoot(ElementName = "tag")]
    public class Tag
    {
        [XmlAttribute(AttributeName = "category")]
        public string Category { get; set; }
        [XmlAttribute(AttributeName = "lock")]
        public string Lock { get; set; }
        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "tags")]
    public class TagsElement
    {
        [XmlElement(ElementName = "tag")]
        public List<Tag> Tag { get; set; }
        [XmlAttribute(AttributeName = "domain")]
        public string Domain { get; set; }
    }

    [XmlRoot(ElementName = "thumb")]
    public class ThumbBase
    {
        [XmlElement(ElementName = "video_id")]
        public string Video_id { get; set; }
        [XmlElement(ElementName = "title")]
        public string Title { get; set; }
        [XmlElement(ElementName = "description")]
        public string Description { get; set; }
        [XmlElement(ElementName = "thumbnail_url")]
        public string Thumbnail_url { get; set; }
        [XmlElement(ElementName = "first_retrieve")]
        public string First_retrieve { get; set; }
        [XmlElement(ElementName = "length")]
        public string Length { get; set; }
        [XmlElement(ElementName = "movie_type")]
        public string Movie_type { get; set; }
        [XmlElement(ElementName = "size_high")]
        public string Size_high { get; set; }
        [XmlElement(ElementName = "size_low")]
        public string Size_low { get; set; }
        [XmlElement(ElementName = "view_counter")]
        public string View_counter { get; set; }
        [XmlElement(ElementName = "comment_num")]
        public string Comment_num { get; set; }
        [XmlElement(ElementName = "mylist_counter")]
        public string Mylist_counter { get; set; }
        [XmlElement(ElementName = "last_res_body")]
        public string Last_res_body { get; set; }
        [XmlElement(ElementName = "watch_url")]
        public string Watch_url { get; set; }
        [XmlElement(ElementName = "thumb_type")]
        public string Thumb_type { get; set; }
        [XmlElement(ElementName = "embeddable")]
        public string Embeddable { get; set; }
        [XmlElement(ElementName = "no_live_play")]
        public string No_live_play { get; set; }
        [XmlElement(ElementName = "tags")]
        public TagsElement Tags { get; set; }
        [XmlElement(ElementName = "genre")]
        public string Genre { get; set; }

        [XmlElement(ElementName = "user_id")]
        public string user_id { get; set; }
        [XmlElement(ElementName = "user_nickname")]
        public string user_nickname { get; set; }
        [XmlElement(ElementName = "user_icon_url")]
        public string user_icon_url { get; set; }

        [XmlElement(ElementName = "ch_id")]
        public string Ch_id { get; set; }
        [XmlElement(ElementName = "ch_name")]
        public string Ch_name { get; set; }
        [XmlElement(ElementName = "ch_icon_url")]
        public string Ch_icon_url { get; set; }

        public string GetUserID()
        {
            return user_id != null ? user_id : Ch_id;
        }

        public string GetUserName()
        {
            return user_id != null ? user_nickname : Ch_name;
        }

        public string GetUserIconUrl()
        {
            return user_id != null ? user_icon_url : Ch_icon_url;
        }
    }
}
