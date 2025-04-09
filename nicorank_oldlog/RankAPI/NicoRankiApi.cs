using AngleSharp.Dom;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using nicorankLib.Util.Text;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace nicorank_oldlog.RankAPI
{
    public class NicoRankiApi
    {
        protected static NicoRankiApi? Instance = null;
        protected string _cookie = "";
        public static NicoRankiApi? GetInstance()
        {
            if (NicoRankiApi.Instance == null)
            {
                NicoRankiApi.Instance = NicoRankiApi.Initilize();
            }
            return NicoRankiApi.Instance;
        }
        protected static NicoRankiApi? Initilize()
        {
            try
            {
                bool isOpened = TextUtil.ReadText("cookie.txt", out string strCookie);
                if (!isOpened)
                {
                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"cookie.txtの読み取りでエラーが発生(NicoRankiApi::Initilize)");
                    return null;
                }
                var workApi = new NicoRankiApi();
                workApi._cookie = strCookie.Trim();
                return workApi;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return null;
        }

        ///// <summary>
        ///// ニコニコ動画のログインCookieを取得する
        ///// </summary>
        ///// <returns></returns>
        //public async Task<string> GetNicoNicoLoginCookie()
        //{
        //    // APIのURL
        //    string url = "https://www.nicovideo.jp";

        //    HttpClientHandler handler = new HttpClientHandler();
        //    handler.UseCookies = true; // Cookieを有効にする
        //    CookieContainer cookieContainer = new CookieContainer();
        //    handler.CookieContainer = cookieContainer;

        //    using (HttpClient client = new HttpClient(handler))
        //    {
        //        try
        //        {
        //            HttpResponseMessage response = await client.GetAsync(url);

        //            // user_session　Cookieの取得
        //            var cookies = cookieContainer.GetCookies(new Uri(url));



        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Cookie取得でエラーが発生しました: {ex.Message}");
        //        }
        //    }
        //    return "";
        //}

        public async Task<ResObjType?> requestAPI<ResObjType>(string apiurl,string appendURL = "")
        {
            using (HttpClient client = new HttpClient())
            {
                string workApi = $"{apiurl}?_frontendId=6";
                try
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WeeklyNicoranProgram/2025.04");
                    client.DefaultRequestHeaders.Add("Cookie", $"user_session={this._cookie}");

                    // GETリクエストを送信
                    HttpResponseMessage response = await client.GetAsync(workApi + appendURL);

                    // レスポンスが成功かチェック
                    var statusCode = response.EnsureSuccessStatusCode();
                    var resultStr  = await response.Content.ReadAsStringAsync();
                    ResObjType? res = JsonConvert.DeserializeObject<ResObjType>(resultStr, ConvertConfig.Settings);
                    if (res == null)
                    {
                        return default;
                    }
                    return res;

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{apiurl} でエラーが発生しました: {ex.Message}");
                }
            }
            return default;
        }


        public bool GetGenreList(out List<ResGenres.Genre> genreList)
        {
            genreList = new List<ResGenres.Genre>();

            // APIのURL
            string apiUrl = "https://nvapi.nicovideo.jp/v2/genres";

            try
            {
                // JSONデータを取得
                var result = requestAPI<ResGenres.Rootobject>(apiUrl);
                var resObj = result.Result;

                if (resObj == null)
                {
                    Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます null");
                    return false;
                }
                else if (resObj.meta.status != 200 )
                {
                    // resObj.meta.status毎にエラー処理を分岐
                    switch (resObj.meta.status)
                    {
                        case 400:
                            Console.WriteLine($"{apiUrl} :ログインセッションが無効" );
                            break;
                        default:
                            Console.WriteLine($"{apiUrl} :エラーが返されました: {resObj.meta.status}");
                            break;
                    }
                }
                else if (resObj.data == null || resObj.data.genres == null)
                {
                    Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます 構造エラー");
                    return false;
                }

                // resObj.data.genres を戻り値用にListに変換
                genreList = resObj.data.genres.ToList(); 
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{apiUrl} :の取得でエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        public bool GetTeibanGenreList(out List<ResTeibanGenres.Item> genreList)
        {
            genreList = new List<ResTeibanGenres.Item>();

            // APIのURL
            string apiUrl = "https://nvapi.nicovideo.jp/v1/ranking/teiban/featured-keys";

            try
            {
                // JSONデータを取得
                var result = requestAPI<ResTeibanGenres.Rootobject>(apiUrl);
                var resObj = result.Result;

                if (resObj == null)
                {
                    Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます null");
                    return false;
                }
                else if (resObj.meta.status != 200)
                {
                    // resObj.meta.status毎にエラー処理を分岐
                    switch (resObj.meta.status)
                    {
                        case 400:
                            Console.WriteLine($"{apiUrl} :ログインセッションが無効");
                            break;
                        default:
                            Console.WriteLine($"{apiUrl} :エラーが返されました: {resObj.meta.status}");
                            break;
                    }
                }
                else if (resObj.data == null || resObj.data.items == null)
                {
                    Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます 構造エラー");
                    return false;
                }

                // resObj.data.genres を戻り値用にListに変換
                genreList = resObj.data.items.ToList();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{apiUrl} :の取得でエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 定番ランキング区分のトレンドタグ取得API
        /// </summary>
        /// <param name="genreList"></param>
        /// <returns></returns>
        public bool GetTrendTagList(in string featuredKey , out List<string> trendTagList)
        {
            trendTagList = new List<string>();

            // APIのURL
            string apiUrl = $"https://nvapi.nicovideo.jp/v1/ranking/teiban/featured-keys/{featuredKey}/trend-tags";

            try
            {
                // JSONデータを取得
                var result = requestAPI<ResGetTrendTag.Rootobject>(apiUrl);
                var resObj = result.Result;

                if (resObj == null)
                {
                    Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます null");
                    return false;
                }
                else if (resObj.meta.status != 200)
                {
                    // resObj.meta.status毎にエラー処理を分岐
                    switch (resObj.meta.status)
                    {
                        case 400:
                            Console.WriteLine($"{apiUrl} :ログインセッションが無効");
                            break;
                        default:
                            Console.WriteLine($"{apiUrl} :エラーが返されました: {resObj.meta.status}");
                            break;
                    }
                }
                else if (resObj.data == null || resObj.data.trendTags == null)
                {
                    Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます 構造エラー");
                    return false;
                }

                // resObj.data.genres を戻り値用にListに変換
                trendTagList = resObj.data.trendTags.ToList();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{apiUrl} :の取得でエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ジャンルランキングを取得する
        /// </summary>
        /// <param name="term"></param>
        /// <param name="genre"></param>
        /// <param name="rankingItemList"></param>
        /// <returns></returns>
        public bool GetGenreRanking(in string term, in string genre , 
                                    out List<ResGenreRanking.Item> rankingItemList,
                                    in uint pageSize = 100,in uint maxpage = 20)
        {
            rankingItemList = new List<ResGenreRanking.Item>();
            
            // APIのURL
            string apiUrl = $"https://nvapi.nicovideo.jp/v1/ranking/genre/{genre}";

            bool getResult = true;
            try
            {
                var appendURL_base = $"&term={term}&pageSize={pageSize}";

                for (uint page = 1; page <= maxpage; page++)
                {

                    var appendURL = appendURL_base + $"&page={page}";

                    // JSONデータを取得
                    var result = requestAPI<ResGenreRanking.Rootobject>(apiUrl, appendURL);
                    var resObj = result.Result;

                    if (resObj == null)
                    {
                        getResult = false;
                        Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます null");
                        break;
                    }
                    else if (resObj.meta.status != 200)
                    {
                        // resObj.meta.status毎にエラー処理を分岐
                        switch (resObj.meta.status)
                        {
                            case 400:
                                Console.WriteLine($"{apiUrl} :ログインセッションが無効");
                                break;
                            default:
                                Console.WriteLine($"{apiUrl} :エラーが返されました: {resObj.meta.status}");
                                break;
                        }
                    }
                    else if (resObj.data == null)
                    {
                        getResult = false;
                        Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます 構造エラー");
                        break;
                    }

                    // rankingItemList に resObj.data.items を追加

                    if (resObj.data.items != null)
                    {
                        rankingItemList.AddRange(resObj.data.items.ToList());
                    }

                    if (!resObj.data.hasNext )
                    {
                        break;
                    }
                    
                }
                return getResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{apiUrl} :の取得でエラーが発生しました: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 定番ランキングを取得する
        /// </summary>
        /// <param name="term"></param>
        /// <param name="featuredKey"></param>
        /// <param name="rankingItemList"></param>
        /// <returns></returns>
        public bool GetTeibanRanking(in string term, in string featuredKey,in string tagName ,
                        out List<ResTeibanRanking.Item> rankingItemList,
                        in uint pageSize = 100, in uint maxpage = 20)
        {
            rankingItemList = new List<ResTeibanRanking.Item>();
            
            // APIのURL
            string apiUrl = $"https://nvapi.nicovideo.jp/v1/ranking/teiban/{featuredKey}";

            try
            {
                var appendURL_base = $"&term={term}&pageSize={pageSize}";
                
                if (!string.IsNullOrEmpty(tagName))
                {
                    appendURL_base += $"&tag={tagName}";
                }

                for (uint page = 1; page <= maxpage; page++)
                {

                    var appendURL = appendURL_base + $"&page={page}";

                    // JSONデータを取得
                    var result = requestAPI<ResTeibanRanking.Rootobject>(apiUrl, appendURL);
                    var resObj = result.Result;

                    if (resObj == null)
                    {
                        Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます null");
                        break;
                    }
                    else if (resObj.meta.status != 200)
                    {
                        // resObj.meta.status毎にエラー処理を分岐
                        switch (resObj.meta.status)
                        {
                            case 400:
                                Console.WriteLine($"{apiUrl} :ログインセッションが無効");
                                break;
                            default:
                                Console.WriteLine($"{apiUrl} :エラーが返されました: {resObj.meta.status}");
                                break;
                        }
                    }
                    else if (resObj.data == null)
                    {
                        Console.WriteLine($"{apiUrl} : 知らないデータが戻ってきてます 構造エラー");
                        break;
                    }

                    // rankingItemList に resObj.data.items を追加

                    if (resObj.data.items != null)
                    {
                        rankingItemList.AddRange(resObj.data.items);
                    }

                    if (!resObj.data.hasNext)
                    {
                        break;
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{apiUrl} :の取得でエラーが発生しました: {ex.Message}");
                return false;
            }
        }
    }
}

