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

        protected string CheckString;

        public bool isRetry { get { return CheckString.Length > 0 ; } }

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
        /// コンストラクタ
        /// </summary>
        /// <param name="rankInfo"></param>
        public Rss2Json(in Ranking_Info rankInfo, in string checkstring = "")
        {
            this.RankInfo = rankInfo;
            this.GenreResultList = new List<GenreRankResult>();
            this.CheckString = checkstring;
        }

        /// <summary>
        /// 集計対象の指定
        /// </summary>
        /// <param name="genreList"></param>
        public virtual bool Initilize(in List<GenreInfo> genreList, in DateTime today,in string optionFolderAppend)
        {
            // ランキング日付フォルダ名の確定
            string rankfolderDate = Path.Combine($"{today.ToString("yyyy-MM-dd")}{optionFolderAppend}");

            var workDir = Path.Combine("old-ranking", this.RankInfo.folder);
            this.TargetSaveDir = Path.Combine(workDir, rankfolderDate);

            this.Path_file_name_list = Path.Combine(this.TargetSaveDir, "file_name_list.json");

            foreach (var genreinfo in genreList)
            {
                this.GenreResultList.Add(new GenreRankResult(genreinfo, this.TargetSaveDir));
            }
            return true;
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
            };

            if (this.CheckString.Length > 0)
            {
                // 更新チェックを行う
                if (File.Exists(CheckString ) && TextUtil.ReadText(CheckString, out string strLastJson))
                {
                    var lastResult = JsonConvert.DeserializeObject<RankLogJson[]>(strLastJson);

                    bool isUpdate = false;
                    while (!isUpdate)
                    {
                        var allJson = this.GenreResultList.Where(x => x.genreInfo.file == "all.json").ToList();
                        if (allJson.Count > 0)
                        {
                            analyze(allJson[0]);

                            if (allJson[0].rankLogJsonList.Count() != lastResult?.Length)
                            {
                                isUpdate = true;
                            }
                            else
                            {
                                for (int i = 0; i < lastResult.Length ;i++)
                                {
                                    if (allJson[0].rankLogJsonList[i].Id != lastResult[i].Id)
                                    {//異なるID→動画が更新された
                                        isUpdate = true; 
                                        break;
                                    }
                                }

                            }

                            allJson[0].rankLogJsonList.Clear();

                            if (isUpdate)
                            {
                                Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} 更新を検出しました　---- ");
                                break;
                            }
                            Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} 更新がありませんでした。5分後にリトライします　---- ");
                            // 5分間隔でチェックする
                            Task.Delay(60000 * 5).Wait();
                        }
                        else
                        {
                            isUpdate = true;
                            break;
                        }
                    }

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
            return SaveOldRankingData();
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ragetRankingList"></param>
        /// <returns></returns>
        public bool SaveOldRankingData()
        {
            //まずデータが存在するかチェックする
            var CountData = (this.GenreResultList.Sum( genre => genre.rankLogJsonList.Count )  );
            if (CountData <= 0)
            {// じゃあ１件取れていればいいのか？という問題がある...運用で検討
                //データ取得なし
                return false;
            }
            

            // ランキング日付フォルダ名の確定

                // old-ranking/{取得するランキングの種類}/{取得したい日付}


                if (this.GenreResultList.Sum(genre => genre.rankLogJsonList.Count) > 0)
                {
                    //データがあるのでフォルダを作成する
                    Directory.CreateDirectory(this.TargetSaveDir);

                    {
                        // ++++++++++++++++++++++++++++++ file_name_list.jsonの作成 ++++++++++++++++++++++++++++++++++++++++++++
                        var genreList = new List<RankGenreJson>(this.GenreResultList.Count);
                        foreach (var genreInfo in this.GenreResultList)
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
                            if (textUtil.WriteOpen(this.Path_file_name_list, true, true))
                            {
                                textUtil.WriteText(str_file_name_list);
                            }
                            else
                            {
                                var errLog = ErrLog.GetInstance();
                                errLog.Write($"{this.Path_file_name_list}の書き込みでエラー発生。(Rss2Json::SaveOldRankingData)");
                            }
                        }
                        // ----------------------------------- file_name_list.jsonの作成 -----------------------------------------

                    }

                    // 各ジャンル、タグのjsonの作成
                    {
                        foreach (var genreInfo in this.GenreResultList)
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
                                if (this.CheckString.Length > 0)
                                {
                                    if (genreInfo.genreInfo.file == "all.json")
                                    {
                                        using (TextUtil textUtil = new TextUtil())
                                        {
                                            if (textUtil.WriteOpen(this.CheckString, true, true))
                                            {
                                                textUtil.WriteText(str_genre_jon);
                                            }
                                            else
                                            {
                                                var errLog = ErrLog.GetInstance();
                                                errLog.Write($"{this.CheckString}の書き込みでエラー発生。(Rss2Json::SaveOldRankingData)");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} の結果を保存しました　----");
                    }
            }
            return true;
        }
    }
}
