/* 
 Licensed under the Apache License, Version 2.0

 http://www.apache.org/licenses/LICENSE-2.0
 */
using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using nicorankLib.Analyze.model;

namespace nicorankLib.api.model
{

    [XmlRoot(ElementName = "nicovideo_video_response")]
    public class VideoResponse
    {
        public Ranking Ranking;

        [XmlRoot(ElementName = "genre")]
        public class Genre
        {
            [XmlElement(ElementName = "key")]
            public string Key { get; set; }
            [XmlElement(ElementName = "label")]
            public string Label { get; set; }
        }

        [XmlRoot(ElementName = "options")]
        public class Options
        {
            [XmlAttribute(AttributeName = "mobile")]
            public string Mobile { get; set; }
            [XmlAttribute(AttributeName = "sun")]
            public string Sun { get; set; }
            [XmlAttribute(AttributeName = "large_thumbnail")]
            public string Large_thumbnail { get; set; }
            [XmlAttribute(AttributeName = "adult")]
            public string Adult { get; set; }
        }

        [XmlRoot(ElementName = "video")]
        public class VideoElement
        {
            [XmlElement(ElementName = "id")]
            public string Id { get; set; }
            [XmlElement(ElementName = "user_id")]
            public string User_id { get; set; }
            [XmlElement(ElementName = "deleted")]
            public string Deleted { get; set; }
            [XmlElement(ElementName = "title")]
            public string Title { get; set; }
            [XmlElement(ElementName = "description")]
            public string Description { get; set; }
            [XmlElement(ElementName = "length_in_seconds")]
            public string Length_in_seconds { get; set; }
            [XmlElement(ElementName = "thumbnail_url")]
            public string Thumbnail_url { get; set; }
            [XmlElement(ElementName = "upload_time")]
            public string Upload_time { get; set; }
            [XmlElement(ElementName = "first_retrieve")]
            public string First_retrieve { get; set; }
            [XmlElement(ElementName = "default_thread")]
            public string Default_thread { get; set; }
            [XmlElement(ElementName = "view_counter")]
            public string View_counter { get; set; }
            [XmlElement(ElementName = "mylist_counter")]
            public string Mylist_counter { get; set; }
            [XmlElement(ElementName = "genre")]
            public Genre Genre { get; set; }
            [XmlElement(ElementName = "option_flag_community")]
            public string Option_flag_community { get; set; }
            [XmlElement(ElementName = "option_flag_nicowari")]
            public string Option_flag_nicowari { get; set; }
            [XmlElement(ElementName = "option_flag_middle_thumbnail")]
            public string Option_flag_middle_thumbnail { get; set; }
            [XmlElement(ElementName = "option_flag_dmc_play")]
            public string Option_flag_dmc_play { get; set; }
            [XmlElement(ElementName = "community_id")]
            public string Community_id { get; set; }
            [XmlElement(ElementName = "vita_playable")]
            public string Vita_playable { get; set; }
            [XmlElement(ElementName = "ppv_video")]
            public string Ppv_video { get; set; }
            [XmlElement(ElementName = "permission")]
            public string Permission { get; set; }
            [XmlElement(ElementName = "provider_type")]
            public string Provider_type { get; set; }
            [XmlElement(ElementName = "options")]
            public Options Options { get; set; }
        }

        [XmlRoot(ElementName = "thread")]
        public class ThreadElement
        {
            [XmlElement(ElementName = "id")]
            public string Id { get; set; }
            [XmlElement(ElementName = "num_res")]
            public string Num_res { get; set; }
            [XmlElement(ElementName = "summary")]
            public string Summary { get; set; }
            [XmlElement(ElementName = "community_id")]
            public string Community_id { get; set; }
            [XmlElement(ElementName = "group_type")]
            public string Group_type { get; set; }
        }

        [XmlRoot(ElementName = "tag_info")]
        public class Tag_info
        {
            [XmlElement(ElementName = "tag")]
            public string Tag { get; set; }
            [XmlElement(ElementName = "area")]
            public string Area { get; set; }
        }

        [XmlRoot(ElementName = "tags")]
        public class TagsElement
        {
            [XmlElement(ElementName = "tag_info")]
            public List<Tag_info> Tag_info { get; set; }
        }

        [XmlElement(ElementName = "video")]
        public VideoElement Video { get; set; }
        [XmlElement(ElementName = "thread")]
        public ThreadElement Thread { get; set; }
        [XmlElement(ElementName = "tags")]
        public TagsElement Tags { get; set; }
        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }
    }
}
