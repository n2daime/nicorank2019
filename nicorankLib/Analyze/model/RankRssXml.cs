// メモ: 生成されたコードは、少なくとも .NET Framework 4.5または .NET Core/Standard 2.0 が必要な可能性があります。
/// <remarks/>

using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using nicorankLib.Common;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;

/// <remarks/>
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

/// <remarks/>
namespace nicorankLib.Analyze.model
{

    public class RankRssXml
    {
        static public List<RankGenreJson> CreateFileNameList()
        {
            string defaultJson = @"
                    [{""genre"":""全ジャンル"",""tag"":null,""file"":""all.json""},{""genre"":""エンターテイメント"",""tag"":null,""file"":""entertainment.json""},{""genre"":""ラジオ"",""tag"":null,""file"":""radio.json""},{""genre"":""音楽・サウンド"",""tag"":null,""file"":""music_sound.json""},{""genre"":""ダンス"",""tag"":null,""file"":""dance.json""},{""genre"":""動物"",""tag"":null,""file"":""animal.json""},{""genre"":""自然"",""tag"":null,""file"":""nature.json""},{""genre"":""料理"",""tag"":null,""file"":""cooking.json""},{""genre"":""旅行・アウトドア"",""tag"":null,""file"":""traveling_outdoor.json""},{""genre"":""乗り物"",""tag"":null,""file"":""vehicle.json""},{""genre"":""スポーツ"",""tag"":null,""file"":""sports.json""},{""genre"":""社会・政治・時事"",""tag"":null,""file"":""society_politics_news.json""},{""genre"":""技術・工作"",""tag"":null,""file"":""technology_craft.json""},{""genre"":""解説・講座"",""tag"":null,""file"":""commentary_lecture.json""},{""genre"":""アニメ"",""tag"":null,""file"":""anime.json""},{""genre"":""ゲーム"",""tag"":null,""file"":""game.json""},{""genre"":""その他"",""tag"":null,""file"":""other.json""},{""genre"":""R-18"",""tag"":null,""file"":""r18.json""}]";

            var gunreList = JsonConvert.DeserializeObject<List<RankGenreJson>>(defaultJson);

            return gunreList;
        }

        static public List<RankLogJson> CreateRankLogJson(string strXml)
        {
            // RSS読み込み
            XElement element = XElement.Load("TestRSS.xml");
            // channelの取得
            XElement channelElement = element.Element("channel");

            //itemの取得
            IEnumerable<XElement> elementItems = channelElement.Elements("item");

            var RankLogList = new List<RankLogJson>();

            for (int i = 0; i < elementItems.Count(); i++)
            {
                RankLogJson workRank = new RankLogJson();

                XElement item = elementItems.ElementAt(i);

                //タイトル
                workRank.title = item.Element("title").Value;

                //動画ID
                workRank.Id = RegLib.RegExpRep(item.Element("link").Value, @"^.+/", "");

                //データの更新時間
                //DateTime updateTime = DateTime.Parse(item.Element("pubDate").Value);


                //各種数字取得に必要なHTML取得
                var description = item.Element("description").Value;

                //インスタンス作成
                HtmlParser parser = new HtmlParser();

                //HTMLの文字列を分解します。
                IHtmlDocument doc = parser.ParseDocument(description);

                //サムネイルURL
                var nico_thumbnail = doc.GetElementsByClassName("nico-thumbnail");
                var img = nico_thumbnail[0].QuerySelectorAll("img");
                workRank.Thumbnail = new Thumbnail();
                workRank.Thumbnail.Url = new System.Uri( img[0].GetAttribute("src") );
                workRank.Thumbnail.MiddleUrl = new System.Uri(img[0].GetAttribute("src"));
                workRank.Thumbnail.LargeUrl = new System.Uri(img[0].GetAttribute("src"));

                //再生時間
                var nico_info_length = doc.GetElementsByClassName("nico-info-length");
                workRank.PlayTime = nico_info_length[0].TextContent;

                //投稿時間
                var nico_info_date = doc.GetElementsByClassName("nico-info-date");
                workRank.RegisteredAt = DateTime.ParseExact(nico_info_date[0].TextContent, @"yyyy年MM月dd日 HH：mm：ss", null);

                workRank.Count = new Count();

                var nico_info_total_view = doc.GetElementsByClassName("nico-info-total-view");
                workRank.Count.View = long.Parse(nico_info_total_view[0].TextContent, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);

                var nico_info_total_res = doc.GetElementsByClassName("nico-info-total-res");
                workRank.Count.Comment = long.Parse(nico_info_total_res[0].TextContent, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);

                var nico_info_total_like = doc.GetElementsByClassName("nico-info-total-like");
                workRank.Count.Like = long.Parse(nico_info_total_like[0].TextContent, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);

                var nico_info_total_mylist = doc.GetElementsByClassName("nico-info-total-mylist");
                workRank.Count.Mylist = long.Parse(nico_info_total_mylist[0].TextContent, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign);

                RankLogList.Add(workRank);
                
            }
            //string jsonstr = JsonConvert.SerializeObject(RankLogList, Newtonsoft.Json.Formatting.None);
            return RankLogList;
        }
    }
}