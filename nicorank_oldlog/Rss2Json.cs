using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using nicorankLib.Common;
using System;
using System.Collections.Generic;

/// <remarks/>
using System.Linq;

namespace nicorank_oldlog
{
    public class Rss2Json
    {

        /// <summary>
        /// どのランキングを集計するか(daily/weekly/monthly/total)
        /// </summary>
        protected Ranking_Info RankInfo;

        /// <summary>
        /// ジャンル毎の集計結果
        /// </summary>
        protected List<GenreRankResult> GenreResultList;


        /// <summary>
        /// 保存ディレクトリパス
        /// </summary>
        public string TargetSaveDir = "";

        public string Path_file_name_list = "";

        /// <summary>
        ///  ジャンルごとにランキング結果
        /// </summary>
        public class GenreRankResult
        {
            public GenreInfo genreInfo;
            public List<RankLogJson> rankLogJsonList;
            public string TargetSavePath = "";

            public GenreRankResult(in GenreInfo genreInfo,in string TargetSaveDir)
            {

                this.genreInfo = genreInfo;
                this.TargetSavePath = Path.Combine(TargetSaveDir, genreInfo.file);
                rankLogJsonList = new List<RankLogJson>();
            }
        }

        /// <summary>
        /// コンストラクタ（集計対象の指定)
        /// </summary>
        /// <param name="rankInfo"></param>
        /// <param name="genreList"></param>
        public Rss2Json(Ranking_Info rankInfo, in List<GenreInfo> genreList, in DateTime today)
        {
            // ランキング日付フォルダ名の確定
            string rankfolderDate = Path.Combine(today.ToString("yyyy-MM-dd"));

            this.TargetSaveDir = Path.Combine("old-ranking", rankInfo.folder);


            this.TargetSaveDir = Path.Combine(this.TargetSaveDir, rankfolderDate);

            this.Path_file_name_list = Path.Combine(this.TargetSaveDir, "file_name_list.json");


            var workgenreList = new List<GenreInfo>(genreList);
            this.RankInfo = rankInfo;
            this.GenreResultList = new List<GenreRankResult>();
            if (rankInfo.folder == "daily")
            {
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
                this.GenreResultList.Add(new GenreRankResult(genreinfo, this.TargetSaveDir));

                if (genreinfo.file == "all.json")
                {
                    //全てはジャンルタグなし
                    continue;
                }
                Console.WriteLine($@"{rankInfo.folder}:{genreinfo.genre} のジャンルタグを取得しています...");

                //ジャンルタグの取得
                if (InternetUtil.TxtDownLoad($@"https://www.nicovideo.jp/{genreinfo.rss}?term={rankInfo.rss}", out string strRankingPage))
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

        }

        public bool AnalyzeRank()
        {
            //if (File.Exists(this.Path_file_name_list))
            //{
            //    //既に取得済
            //    return true;
            //}


            var analyze = (GenreRankResult genreResult) =>
            {
                // https://www.nicovideo.jp/ranking/genre/{ジャンル名}?tag={タグ名}&term={集計期間}&rss=2.0&lang=ja-jp




            };

            foreach (var genreResult in this.GenreResultList)
            {
                string targetURL;

                try
                {
                    // RSS読み込み
                    if (genreResult.genreInfo.tag == null)
                    {
                        Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}のランキングを取得中...");

                        targetURL = $@"https://www.nicovideo.jp/{genreResult.genreInfo.rss}?term={this.RankInfo.rss}&rss=2.0&lang=ja-jp";
                    }
                    else
                    {
                        Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}:[{genreResult.genreInfo.tag}]のランキングを取得中...");
                        targetURL = $@"https://www.nicovideo.jp/{genreResult.genreInfo.rss}&term={this.RankInfo.rss}&rss=2.0&lang=ja-jp";
                    }
                    for (uint pageNo = 1; pageNo <= genreResult.genreInfo.Page; pageNo++)
                    {
                        if (!InternetUtil.TxtDownLoad($@"{targetURL}&page={pageNo}", out string strRSS))
                        {
                            break;
                        }

                        var encoding = Encoding.GetEncoding("UTF-8");
                        var data = encoding.GetBytes(strRSS);
                        var stream = new System.IO.MemoryStream(data);

                        XElement element;

                        try
                        {
                            element = XElement.Load(stream);
                        }
                        catch (Exception)
                        {
                            break;
                        }

                        //XElement element = XElement.Load("TestRSS.xml");

                        // channelの取得
                        XElement? channelElement = element.Element("channel");
                        if (channelElement == null)
                        {
                            break;
                        }
                        //itemの取得
                        IEnumerable<XElement> elementItems = channelElement.Elements("item");

                        if (elementItems.Count() <= 0)
                        {
                            break;
                        }

                        for (int i = 0; i < elementItems.Count(); i++)
                        {
                            RankLogJson workRank = new RankLogJson();

                            XElement item = elementItems.ElementAt(i);

                            //タイトル
                            var title = item.Element("title");
                            if (title == null)
                            {// 無効なデータ 
                             // T.B.D エラー処理

                                continue;
                            }
                            workRank.title = RegLib.RegExpRep(title.Value, @"^第\d+位[：:]", "");

                            //動画ID
                            var link = item.Element("link");
                            if (link == null)
                            {// 無効なデータ 
                             // T.B.D エラー処理
                                continue;
                            }

                            workRank.Id = RegLib.RegExpRep(link.Value, @"(^.+/)|\?.+", "");

                            //データの更新時間
                            //DateTime updateTime = DateTime.Parse(item.Element("pubDate").Value);


                            //各種数字取得に必要なHTML取得
                            var description = item.Element("description");

                            if (description != null)
                            {
                                //インスタンス作成
                                HtmlParser parser = new HtmlParser();

                                //HTMLの文字列を分解します。
                                IHtmlDocument doc = parser.ParseDocument(description.Value);

                                //サムネイルURL
                                var nico_thumbnail = doc.GetElementsByClassName("nico-thumbnail");
                                var img = nico_thumbnail[0].QuerySelectorAll("img");
                                workRank.Thumbnail = new Thumbnail();
                                if (img != null)
                                {
                                    var ThumbnailURI = img[0].GetAttribute("src");
                                    if (ThumbnailURI != null)
                                    {
                                        workRank.Thumbnail.Url = new System.Uri(ThumbnailURI);
                                        if (ThumbnailURI.IndexOf(@"https://nicovideo.cdn.nimg.jp/thumbnails") > -1)
                                        {
                                            workRank.Thumbnail.MiddleUrl = new System.Uri($"{ThumbnailURI}.M");
                                            workRank.Thumbnail.LargeUrl = new System.Uri($"{ThumbnailURI}.L");
                                        }
                                        else
                                        {
                                            workRank.Thumbnail.MiddleUrl = workRank.Thumbnail.Url; // T.B.D 大きなサイズのサムネイル取得方法
                                            workRank.Thumbnail.LargeUrl = workRank.Thumbnail.Url;// T.B.D 〃

                                        }
                                    }
                                }

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

                                genreResult.rankLogJsonList.Add(workRank);
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    //throw e;
                    var errlog = ErrLog.GetInstance();
                    errlog.Write(e);
                }
            }

            // 並列処理オプションの設定
            ParallelOptions parallelOptions = new ParallelOptions();
            parallelOptions.MaxDegreeOfParallelism = 5;

            if (false)
            {//デバッグ
                foreach (var genreResult in this.GenreResultList)
                {
                    analyze(genreResult);
                }
            }
            else
            {

                Parallel.ForEach(this.GenreResultList, parallelOptions, genreResult =>
                {
                    analyze(genreResult);
                }
                );
            }

            return true;
        }

        /// <summary>
        /// 取得するランキングの種類を決定する(daily/weekly/monthly/total)
        /// </summary>
        /// <param name="today"></param>
        /// <returns></returns>
        public static List<Ranking_Info> GetRankingInfo(in DateTime today)
        {
            var convConfig = ConvertConfig.GetInstance();
            
            var getRankingList = new List<Ranking_Info>(); // 取得するランキングの種類

            if (convConfig != null)
            {
                // daily / totalは毎日更新
                getRankingList.Add(convConfig.ranking_daily);
                getRankingList.Add(convConfig.ranking_total);

                // 今日の日付を取得
                // 有効な集計日になるまでループ
                
                if (today.DayOfWeek == DayOfWeek.Monday)
                {
                    //月曜日はweekly
                    getRankingList.Add(convConfig.ranking_weekly);
                }
                if (today.Day == 1)
                {
                    //毎月１日はmonthly
                    getRankingList.Add(convConfig.ranking_monthly);
                }
            }
            return getRankingList;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ragetRankingList"></param>
        /// <returns></returns>
        public static bool SaveOldRankingData(in List<Rss2Json> rss2jsonList)
        {
            //まずデータが存在するかチェックする
            var CountData = rss2jsonList.Sum(rank => rank.GenreResultList.Sum( genre => genre.rankLogJsonList.Count )  );
            if (CountData <= 0)
            {// じゃあ１件取れていればいいのか？という問題がある...運用で検討
                //データ取得なし
                return false;
            }
            

            // ランキング日付フォルダ名の確定

            foreach (var rss2json in rss2jsonList)
            {
                // old-ranking/{取得するランキングの種類}/{取得したい日付}


                if (rss2json.GenreResultList.Sum(genre => genre.rankLogJsonList.Count) > 0)
                {
                    //データがあるのでフォルダを作成する
                    Directory.CreateDirectory(rss2json.TargetSaveDir);

                    {
                        // ++++++++++++++++++++++++++++++ file_name_list.jsonの作成 ++++++++++++++++++++++++++++++++++++++++++++
                        var genreList = new List<RankGenreJson>(rss2json.GenreResultList.Count);
                        foreach (var genreInfo in rss2json.GenreResultList)
                        {
                            if (genreInfo.rankLogJsonList.Count > 0)
                            {//値を取得できた
                                var wjsongenre = new RankGenreJson();
                                wjsongenre.Genre = genreInfo.genreInfo.genre;
                                wjsongenre.Tag = genreInfo.genreInfo.tag;
                                wjsongenre.File = genreInfo.genreInfo.file;
                                genreList.Add(wjsongenre);
                            }
                        }
                        // file_name_list.jsonの文字列
                        string str_file_name_list = JsonConvert.SerializeObject(genreList, Newtonsoft.Json.Formatting.None);

                        // ファイル名の計算
                        using (TextUtil textUtil = new TextUtil())
                        {
                            if (textUtil.WriteOpen(rss2json.Path_file_name_list, true, true))
                            {
                                textUtil.WriteText(str_file_name_list);
                            }
                            else
                            {
                                var errLog = ErrLog.GetInstance();
                                errLog.Write($"{rss2json.Path_file_name_list}の書き込みでエラー発生。(Rss2Json::SaveOldRankingData)");
                            }
                        }
                        // ----------------------------------- file_name_list.jsonの作成 -----------------------------------------

                    }

                    // 各ジャンル、タグのjsonの作成
                    {
                        foreach (var genreInfo in rss2json.GenreResultList)
                        {
                            if (genreInfo.rankLogJsonList.Count > 0)
                            {//値を取得できた
                               
                                string str_genre_jon = JsonConvert.SerializeObject(genreInfo.rankLogJsonList, Newtonsoft.Json.Formatting.None);

                                using (TextUtil textUtil = new TextUtil())
                                {
                                    if (textUtil.WriteOpen(genreInfo.TargetSavePath, true, true))
                                    {
                                        textUtil.WriteText(str_genre_jon);
                                    }
                                    else
                                    {
                                        var errLog = ErrLog.GetInstance();
                                        errLog.Write($"{genreInfo.TargetSavePath}の書き込みでエラー発生。(Rss2Json::SaveOldRankingData)");
                                    }
                                }

                            }
                        }
                    }
                }

            }
            return true;
        }
    }
}
