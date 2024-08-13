using AngleSharp.Html.Parser;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Option
{
    public class NocoChartReader : BasicOptionBase
    {
        protected readonly List<String> xmlURLs = new List<string>()
        {
            "http://now.nicochart.jp/today/mylist/feed/",
            "http://now.nicochart.jp/today/res/feed/",
            "http://now.nicochart.jp/today/view/feed/"
        };

        public NocoChartReader() 
        {
        }

        /// <summary>
        /// ランキングのMap
        /// </summary>
        public Dictionary<string, Ranking> RankMap { get; protected set; }

        /// <summary>
        /// マルチスレッド対応解析
        /// </summary>
        /// <param name="xmlURL"></param>
        /// <returns></returns>
        protected async Task<List<Ranking>> AnalyzeRankTaskAsync(string xmlURL)
        {
            List<Ranking> rankList = new List<Ranking>();
            await Task.Run(() =>
            {
                if (!InternetUtil.TxtDownLoad(xmlURL, out string readXml))
                {
                    //ニコチャートが落ちてるときは早々に諦める
                }
                else
                {
                    var parser = new HtmlParser();
                    var encoding = Encoding.GetEncoding("UTF-8");

                    NicoChartModel nicoChart = XmlSerializerUtil.Deserialize<NicoChartModel>(readXml);

                    foreach (var movieData in nicoChart.Entry)
                    {
                        Ranking rankInfo = new Ranking();

                        //タイトルの読み込み
                        // "第1位：プロパガンダ（本物）みたいな形式になっているので、「第xx位：」を削除する
                        rankInfo.Title = RegLib.RegExpRep(movieData.Title, @"^第\d+位：", "");

                        //動画idの読み込み
                        //"tag:nicovideo.jp,2019-06-09:/watch/sm35238003" みたいな形式になっているので、不要な部分を削除する
                        rankInfo.ID = RegLib.RegExpRep(movieData.Id, @"^.+/watch/", "");

                        //Contentの文字列を解析する
                        var doc = parser.ParseDocument(movieData.Content.Text);

                        {
                            var thumbnail = doc.QuerySelector("p.nico-thumbnail");

                            var imgTagItem = thumbnail.QuerySelector("img");
                            rankInfo.ThumbnailURL = imgTagItem.GetAttribute("src");
                        }
                        {
                            var nicoInfo = doc.QuerySelector("p.nico-info");

                            var dateItem = nicoInfo.QuerySelector("strong.nico-info-date");
                            rankInfo.Date = DateTime.ParseExact(dateItem.TextContent, "yyyy年MM月dd日 HH:mm:ss", null);

                            var lengthItem = nicoInfo.QuerySelector("strong.nico-info-length");
                            rankInfo.SetPlayTime( lengthItem.TextContent);

                            var viewItem = nicoInfo.QuerySelector("strong.nico-info-today-view");
                            rankInfo.CountPlay = ParseCommmaValue(viewItem.TextContent);

                            var resItem = nicoInfo.QuerySelector("strong.nico-info-today-res");
                            rankInfo.CountComment = ParseCommmaValue(resItem.TextContent);

                            var mylistItem = nicoInfo.QuerySelector("strong.nico-info-today-mylist");
                            rankInfo.CountMyList = ParseCommmaValue(mylistItem.TextContent);
                        }
                        rankList.Add(rankInfo);
                    }
                }
            });
            return rankList;
        }

        /// <summary>
        /// NicoChartランキングを解析します
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {

            this.RankMap = new Dictionary<string, Ranking>();

            List<Task<List<Ranking>>> taskResultList = new List<Task<List<Ranking>>>(); 

            //3スレッドで解析する
            foreach (var xmlURL in this.xmlURLs)
            {
                taskResultList.Add( AnalyzeRankTaskAsync(xmlURL) );
            }

            //マージ処理
            foreach (var taskResult in taskResultList)
            {
                var rankList = taskResult.Result;
                foreach(var rankInfo in rankList)
                {
                    if(!this.RankMap.ContainsKey(rankInfo.ID))
                    {
                        this.RankMap[rankInfo.ID] = rankInfo;
                    }
                }
            }
            rankingList = this.RankMap.Values.ToList();
            return true;
        }

        protected static long ParseCommmaValue(string str)
        {
            str = str.Replace(",", "");

            return long.Parse(str);
        }
    }
}
