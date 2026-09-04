using Newtonsoft.Json;
using nicorankLib.Util.Text;
using nicorankLib.Util;


namespace nicorank_oldlog.RankAPI
{
    public class NicoRankiApi
    {
        protected static NicoRankiApi? Instance = null;
        protected string _cookie = "";

        /// <summary>
        /// 共通クエリパラメータ _frontendId（公式仕様で 6 が必須）
        /// </summary>
        public const string FrontendId = "6";

        /// <summary>
        /// リクエストヘッダー User-Agent（公式仕様で識別可能なツール名が必須）
        /// </summary>
        public const string UserAgent = "WeeklyNicoranProgram/2025.04";

        /// <summary>
        /// NicoRankiApiのインスタンスを取得します。
        /// </summary>
        /// <returns>NicoRankiApiのインスタンス</returns>
        public static NicoRankiApi? GetInstance()
        {
            if (NicoRankiApi.Instance == null)
            {
                NicoRankiApi.Instance = NicoRankiApi.Initilize();
            }
            return NicoRankiApi.Instance;
        }

        /// <summary>
        /// NicoRankiApiのインスタンスを初期化します。
        /// </summary>
        /// <returns>初期化されたNicoRankiApiのインスタンス</returns>
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

        /// <summary>
        /// クエリパラメータ付きのAPI URLを組み立てる（Issue #19。文字列連結の代替）
        /// 共通クエリ _frontendId を先頭に付与し、組み立ては ApiUrlBuilder（値のエンコード付き）に委譲する。
        /// </summary>
        /// <param name="apiurl">APIのURL（クエリなし）</param>
        /// <param name="query">追加のクエリパラメータ</param>
        /// <returns>組み立てたURL</returns>
        public static string BuildUrl(string apiurl, IDictionary<string, string>? query = null)
        {
            var merged = new Dictionary<string, string>
            {
                { "_frontendId", FrontendId }
            };
            if (query != null)
            {
                foreach (var param in query)
                {
                    merged.Add(param.Key, param.Value);
                }
            }
            return ApiUrlBuilder.Build(apiurl, merged);
        }

        /// <summary>
        /// 指定されたAPI URLに対してリクエストを送信し、レスポンスを取得します。
        /// </summary>
        /// <typeparam name="ResObjType">レスポンスオブジェクトの型</typeparam>
        /// <param name="apiurl">APIのURL</param>
        /// <param name="query">追加のクエリパラメータ</param>
        /// <returns>レスポンスオブジェクト</returns>
        public async Task<ResObjType?> requestAPI<ResObjType>(string apiurl, IDictionary<string, string>? query = null)
        {
            using (HttpClient client = new HttpClient())
            {
                string workApi = BuildUrl(apiurl, query);
                try
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                    client.DefaultRequestHeaders.Add("Cookie", $"user_session={this._cookie}");

                    // GETリクエストを送信
                    HttpResponseMessage response = await client.GetAsync(workApi);

                    // レスポンスが成功かチェック
                    var statusCode = response.EnsureSuccessStatusCode();
                    var resultStr = await response.Content.ReadAsStringAsync();
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

        /// <summary>
        /// ジャンルリストを取得します。
        /// </summary>
        /// <param name="genreList">取得したジャンルリスト</param>
        /// <returns>取得成功かどうか</returns>
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

        /// <summary>
        /// 定番ジャンルリストを取得します。
        /// </summary>
        /// <param name="genreList">取得した定番ジャンルリスト</param>
        /// <returns>取得成功かどうか</returns>
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
        /// 定番ランキング区分のトレンドタグを取得します。
        /// </summary>
        /// <param name="featuredKey">定番ランキング区分のキー</param>
        /// <param name="trendTagList">取得したトレンドタグリスト</param>
        /// <returns>取得成功かどうか</returns>
        public bool GetTrendTagList(in string featuredKey, out List<string> trendTagList)
        {
            trendTagList = new List<string>();

            // APIのURL
            string apiUrl = $"https://nvapi.nicovideo.jp/v1/ranking/teiban/featured-keys/{Uri.EscapeDataString(featuredKey)}/trend-tags";

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
        /// ジャンルランキングを取得します。
        /// </summary>
        /// <param name="term">期間</param>
        /// <param name="genre">ジャンル</param>
        /// <param name="rankingItemList">取得したランキングアイテムリスト</param>
        /// <param name="pageSize">ページサイズ</param>
        /// <param name="maxpage">最大ページ数</param>
        /// <returns>取得成功かどうか</returns>
        public bool GetGenreRanking(in string term, in string genre,
                                    out List<ResGenreRanking.Item> rankingItemList,
                                    in uint pageSize = 100, in uint maxpage = 20)
        {
            rankingItemList = new List<ResGenreRanking.Item>();

            // APIのURL
            string apiUrl = $"https://nvapi.nicovideo.jp/v1/ranking/genre/{Uri.EscapeDataString(genre)}";

            bool getResult = true;
            try
            {
                var queryBase = new Dictionary<string, string>
                {
                    { "term", term },
                    { "pageSize", pageSize.ToString() }
                };

                for (uint page = 1; page <= maxpage; page++)
                {

                    var query = new Dictionary<string, string>(queryBase)
                    {
                        { "page", page.ToString() }
                    };

                    // JSONデータを取得
                    var result = requestAPI<ResGenreRanking.Rootobject>(apiUrl, query);
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

                    if (!resObj.data.hasNext)
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
        /// 定番ランキングを取得します。
        /// </summary>
        /// <param name="term">期間</param>
        /// <param name="featuredKey">定番ランキング区分のキー</param>
        /// <param name="tagName">タグ名</param>
        /// <param name="rankingItemList">取得したランキングアイテムリスト</param>
        /// <param name="pageSize">ページサイズ</param>
        /// <param name="maxpage">最大ページ数</param>
        /// <returns>取得成功かどうか</returns>
        public bool GetTeibanRanking(in string term, in string featuredKey, in string tagName,
                        out List<ResTeibanRanking.Item> rankingItemList,
                        in uint pageSize = 100, in uint maxpage = 20)
        {
            rankingItemList = new List<ResTeibanRanking.Item>();

            // APIのURL
            string apiUrl = $"https://nvapi.nicovideo.jp/v1/ranking/teiban/{Uri.EscapeDataString(featuredKey)}";

            try
            {
                var queryBase = new Dictionary<string, string>
                {
                    { "term", term },
                    { "pageSize", pageSize.ToString() }
                };

                if (!string.IsNullOrEmpty(tagName))
                {
                    if (term == "24h" || term == "hour")
                    {
                        queryBase.Add("tag", tagName);
                    }
                    else
                    {
                        // tag指定は term=24h/hour の場合のみ有効（公式仕様）のため省略する
                        Console.WriteLine($"{apiUrl} : tag指定は term=24h/hour の場合のみ有効のため省略します (term={term})");
                    }
                }

                for (uint page = 1; page <= maxpage; page++)
                {

                    var query = new Dictionary<string, string>(queryBase)
                    {
                        { "page", page.ToString() }
                    };

                    // JSONデータを取得
                    var result = requestAPI<ResTeibanRanking.Rootobject>(apiUrl, query);
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

