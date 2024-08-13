using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorank_oldlog
{
    public class Rss2JsonDaily : Rss2Json
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="rankInfo"></param>
        public Rss2JsonDaily(Ranking_Info rankInfo) : base(rankInfo,"")
        {

        }

        /// <summary>
        /// 集計対象の指定
        /// </summary>
        /// <param name="genreList"></param>
        public override bool Initilize(in List<GenreInfo> genreList, in DateTime today,in string optionFolderAppend)
        {
            if (!base.Initilize(genreList, today, optionFolderAppend))
            {
                return false;
            }

            var workgenreList = new List<GenreInfo>(genreList);

            {// デイリーのみ話題を対象にする
                var workGenre = new GenreInfo();
                workGenre.genre = "話題";
                workGenre.tag = null;
                workGenre.file = "hot-topic.json";
                workGenre.rss = "ranking/hot-topic";
                workGenre.Page = 10;
                workgenreList.Add(workGenre);
            }

            foreach (var genreinfo in workgenreList)
            {
                if (genreinfo.file == "all.json")
                {
                    //全てはジャンルタグなし
                    continue;
                }
                Console.WriteLine($@"{this.RankInfo.folder}:{genreinfo.genre} のジャンルタグを取得しています...");
                //ジャンルタグの取得
                if (InternetUtil.TxtDownLoad($@"https://www.nicovideo.jp/{genreinfo.rss}?term={this.RankInfo.rss}", out string strRankingPage))
                {
                    //インスタンス作成
                    HtmlParser parser = new HtmlParser();

                    //HTMLの文字列を分解します。
                    IHtmlDocument doc = parser.ParseDocument(strRankingPage);

                    //サムネイルURL
                    var RankingFilterTag = doc.GetElementsByClassName("RankingFilterTag");
                    if (RankingFilterTag == null || RankingFilterTag.Length < 1)
                    {
                        //ジャンルタグがない
                        continue;
                    }
                    uint tagCount = 1;

                    foreach (var elememnt in RankingFilterTag)
                    {
                        var tagName = elememnt.TextContent.Trim();
                        if (tagName == "すべて" || tagName == genreinfo.genre)
                        {
                            //すべてや、ジャンル名と同じタグは２重取得になるので対象外
                            continue;
                        }

                        var workGenre = new GenreInfo();
                        workGenre.genre = genreinfo.genre;
                        workGenre.tag = tagName;
                        var strhref = elememnt.GetAttribute("href");
                        if (strhref == null)
                        {
                            continue;
                        }
                        workGenre.rss = strhref;

                        //ファイル名を取り出す
                        var fileInfo = genreinfo.file.Split(".json");
                        if (fileInfo.Length < 1)
                        {
                            continue;
                        }
                        workGenre.file = $@"{fileInfo[0]}_{tagCount:00}.json";
                        tagCount++;
                        workGenre.Page = 10;

                        this.GenreResultList.Add(new GenreRankResult(workGenre, this.TargetSaveDir));
                        Console.WriteLine($@"{workGenre.file}:{workGenre.tag}");

                    }
                }
            }

            return true;
        }
    }
}
