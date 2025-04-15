using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using Newtonsoft.Json;
using nicorank_oldlog.RankAPI;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace nicorank_oldlog
{
    public class RankApi2Json
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
        /// 更新チェック用に残す情報
        /// </summary>
        LastRankResult lastRankResult;

        /// <summary>
        /// 保存ディレクトリパス
        /// </summary>
        public string TargetSaveDir = "";

        public string Path_file_name_list = "";

        protected string CheckString;

        public bool isRetry { get { return CheckString.Length > 0; } }

        /// <summary>
        /// ジャンルごとにランキング結果を保持するクラス
        /// </summary>
        public class GenreRankResult
        {
            public GenreInfo genreInfo;
            public List<RankLogJson> rankLogJsonList;
            public string TargetSavePath = "";

            public GenreRankResult(in GenreInfo genreInfo, in string TargetSaveDir)
            {
                this.genreInfo = genreInfo;
                this.TargetSavePath = Path.Combine(TargetSaveDir, genreInfo.file);
                rankLogJsonList = new List<RankLogJson>();
            }
        }

        /// <summary>
        /// 前回のランキング結果を保持するクラス
        /// </summary>
        public class LastRankResult
        {
            public Result[] results { get; set; }

            public class Result
            {
                public string label { get; set; }
                public RankLogJson[] genreItems { get; set; }
                public RankLogJson[] teibanItems { get; set; }
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="rankInfo">ランキング情報</param>
        /// <param name="checkstring">チェック用文字列</param>
        public RankApi2Json(in Ranking_Info rankInfo, in string checkstring = "")
        {
            this.RankInfo = rankInfo;
            this.GenreResultList = new List<GenreRankResult>();
            this.CheckString = checkstring;
            this.lastRankResult = new LastRankResult();
        }

        /// <summary>
        /// 集計対象の指定
        /// </summary>
        /// <param name="genreList">ジャンルリスト</param>
        /// <param name="today">今日の日付</param>
        /// <param name="optionFolderAppend">フォルダ名のオプション</param>
        /// <returns>初期化成功かどうか</returns>
        public virtual bool Initilize(in List<GenreInfo> genreList, in DateTime today, in string optionFolderAppend)
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

        /// <summary>
        /// ランキングを分析する
        /// </summary>
        /// <returns>分析成功かどうか</returns>
        public bool AnalyzeRank()
        {
            this.lastRankResult = new LastRankResult();

            // 更新チェック用は全てのみで行う（数が多すぎて大変なので）
            {
                this.lastRankResult.results = new LastRankResult.Result[1];
                this.lastRankResult.results[0] = new LastRankResult.Result();
                this.lastRankResult.results[0].label = "全ジャンル";
            }

            var analyze = (GenreRankResult genreResult) =>
            {
                var nicoApi = NicoRankiApi.GetInstance() ?? throw new InvalidOperationException("NicoRankiApi instance is null");

                // マージ用のMap
                var workAllResultMap = new Dictionary<string, RankLogJson>(2000);

                try
                {
                    // ジャンルランキングを取得する
                    if (genreResult.genreInfo.isGenreRank)
                    {
                        Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}のジャンルランキングを取得中...");

                        for (uint retry = 0; retry <= 3; retry++)
                        {
                            bool result = nicoApi.GetGenreRanking(this.RankInfo.term, genreResult.genreInfo.genrekey, out var genreRankingItemsList);
                            if (!result || genreRankingItemsList.Count == 0)
                            {
                                Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}のジャンルランキング取得エラー。リトライします...{retry}n回目");
                            }
                            else
                            {
                                var ConvertRankObj = (RankAPI.ResGenreRanking.Item item) =>
                                {
                                    var workRank = new RankLogJson
                                    {
                                        title = item.title,
                                        Id = item.id,
                                        PlayTime = TimeSpan.FromSeconds(item.duration).ToString(@"m\:ss"),
                                        RegisteredAt = item.registeredAt,
                                        Count = new Count
                                        {
                                            View = item.count.view,
                                            Comment = item.count.comment,
                                            Like = item.count.like,
                                            Mylist = item.count.mylist
                                        },
                                        Thumbnail = item.thumbnail != null ? new Thumbnail
                                        {
                                            Url = !string.IsNullOrEmpty(item.thumbnail.nHdUrl) ? new Uri(item.thumbnail.nHdUrl) : null,
                                            MiddleUrl = !string.IsNullOrEmpty(item.thumbnail.middleUrl) ? new Uri(item.thumbnail.middleUrl) : null,
                                            LargeUrl = !string.IsNullOrEmpty(item.thumbnail.largeUrl) ? new Uri(item.thumbnail.largeUrl) : null
                                        } : null
                                    };
                                    return workRank;
                                };

                                var genreLogJsonList = new List<RankLogJson>();
                                foreach (var item in genreRankingItemsList)
                                {
                                    var workRank = ConvertRankObj(item);
                                    genreLogJsonList.Add(workRank);
                                }

                                // 更新チェック用の結果を保存する
                                var targetResult = this.lastRankResult.results.Where(x => x.label == genreResult.genreInfo.genre).FirstOrDefault();
                                if (targetResult != null)
                                {
                                    targetResult.genreItems = genreLogJsonList.ToArray();
                                }

                                foreach (var item in genreLogJsonList)
                                {
                                    if (workAllResultMap.ContainsKey(item.Id))
                                    {
                                        // すでに存在するのでスキップ
                                        continue;
                                    }
                                    workAllResultMap.Add(item.Id, item);
                                }
                            }
                        }
                    }
                    // 定番ランキングを取得する
                    if (genreResult.genreInfo.isTeibanRank)
                    {
                        string tagName = (string?)genreResult.genreInfo.tag ?? "";
                        if (string.IsNullOrEmpty(tagName))
                        {
                            Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}の定番ランキングを取得中...");
                        }
                        else
                        {
                            Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}:{tagName}の定番ランキングを取得中...");
                        }
                        for (uint retry = 0; retry <= 3; retry++)
                        {
                            bool result = nicoApi.GetTeibanRanking(this.RankInfo.term, genreResult.genreInfo.featuredKey, tagName, out var teibanRankingItemsList);
                            if (!result || teibanRankingItemsList.Count == 0)
                            {
                                Console.WriteLine($@"{this.RankInfo.folder}:{genreResult.genreInfo.genre}:{tagName}の定番ランキング取得エラー。リトライします...{retry}n回目");
                            }
                            else
                            {
                                var ConvertRankObj = (RankAPI.ResTeibanRanking.Item item) =>
                                {
                                    var workRank = new RankLogJson
                                    {
                                        title = item.title,
                                        Id = item.id,
                                        PlayTime = TimeSpan.FromSeconds(item.duration).ToString(@"m\:ss"),
                                        RegisteredAt = item.registeredAt,
                                        Count = new Count
                                        {
                                            View = item.count.view,
                                            Comment = item.count.comment,
                                            Like = item.count.like,
                                            Mylist = item.count.mylist
                                        },
                                        Thumbnail = item.thumbnail != null ? new Thumbnail
                                        {
                                            Url = !string.IsNullOrEmpty(item.thumbnail.nHdUrl) ? new Uri(item.thumbnail.nHdUrl) : null,
                                            MiddleUrl = !string.IsNullOrEmpty(item.thumbnail.middleUrl) ? new Uri(item.thumbnail.middleUrl) : null,
                                            LargeUrl = !string.IsNullOrEmpty(item.thumbnail.largeUrl) ? new Uri(item.thumbnail.largeUrl) : null
                                        } : null
                                    };
                                    return workRank;
                                };

                                var teibanLogJsonList = new List<RankLogJson>();
                                foreach (var item in teibanRankingItemsList)
                                {
                                    var workRank = ConvertRankObj(item);
                                    teibanLogJsonList.Add(workRank);
                                }

                                // 更新チェック用の結果を保存する
                                var targetResult = this.lastRankResult.results.Where(x => x.label == genreResult.genreInfo.genre).FirstOrDefault();
                                if (targetResult != null)
                                {
                                    targetResult.teibanItems = teibanLogJsonList.ToArray();
                                }

                                // マージ
                                var checkMap = genreResult.rankLogJsonList.ToDictionary(x => x.Id);
                                foreach (var item in teibanLogJsonList)
                                {
                                    if (workAllResultMap.ContainsKey(item.Id))
                                    {
                                        // すでに存在するのでスキップ
                                        continue;
                                    }
                                    workAllResultMap.Add(item.Id, item);
                                }
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    var errlog = ErrLog.GetInstance();
                    errlog.Write(e);
                }
                genreResult.rankLogJsonList = workAllResultMap.Values.ToList();
            };

            if (this.CheckString.Length > 0)
            {
                // 更新チェックを行う
                if (File.Exists(CheckString) && TextUtil.ReadText(CheckString, out string strLastJson))
                {
                    var lastResult = JsonConvert.DeserializeObject<LastRankResult>(strLastJson);
                    if (lastResult == null)
                    {
                        Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} lastResultの書式が不正です　---- ");
                        return false;
                    }

                    // 全ジャンルでチェックする
                    var workGenreList = this.GenreResultList.Where(x => x.genreInfo.genre == "全ジャンル");
                    if (workGenreList.Count() != 1)
                    {
                        Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} 全ジャンルの情報取得でエラー　---- ");
                        return false;
                    }

                    // 全ジャンルの集計情報
                    var allGenreInfo = workGenreList.ToList()[0].genreInfo;

                    // 前回の全ジャンルの集計結果
                    var lastAllGenre = lastResult.results?.ToList()[0];

                    var nicoApi = NicoRankiApi.GetInstance() ?? throw new InvalidOperationException("NicoRankiApi instance is null");

                    {
                        bool isUpdate = false;

                        // ジャンルの更新チェック
                        var lastCheckMap = lastAllGenre?.genreItems.ToDictionary(x => x.Id);
                        if (lastCheckMap == null)
                        {
                            // 前回の結果がない→更新あり
                            isUpdate = true;
                        }
                        else
                        {
                            while (true)
                            {
                                bool result = false;
                                // 実際にジャンルランキングを取得する
                                for (uint retry = 0; retry <= 3; retry++)
                                {
                                    result = nicoApi.GetGenreRanking(this.RankInfo.term, allGenreInfo.genrekey, out var genreRankingItemsList);
                                    if (result)
                                    {
                                        if (genreRankingItemsList.Count() != lastCheckMap.Count)
                                        {
                                            isUpdate = true;
                                        }
                                        else
                                        {
                                            foreach (var rankItem in genreRankingItemsList)
                                            {
                                                if (!lastCheckMap.ContainsKey(rankItem.id))
                                                {
                                                    // 前回の結果にない動画が増えている
                                                    // →更新あり
                                                    isUpdate = true;
                                                    break;
                                                }
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (!result)
                                {
                                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} ジャンル更新チェックでエラーが発生しました　---- ");
                                    return false;
                                }
                                if (isUpdate)
                                {
                                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} ジャンルランキング更新を検出しました　---- ");
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} ジャンルランキング更新がありませんでした。5分後にリトライします　---- ");
                                    // 5分間隔でチェックする
                                    Task.Delay(60000 * 5).Wait();
                                }
                            }
                        }
                    }
                    {
                        // 定番の更新チェック
                        var lastCheckMap = lastAllGenre?.teibanItems.ToDictionary(x => x.Id);
                        bool isUpdate = false;
                        if (lastCheckMap == null)
                        {
                            // 前回の結果がない→更新あり
                            isUpdate = true;
                        }
                        else
                        {
                            while (true)
                            {
                                bool result = false;
                                // 実際にジャンルランキングを取得する
                                for (uint retry = 0; retry <= 3; retry++)
                                {
                                    result = nicoApi.GetTeibanRanking(this.RankInfo.term, allGenreInfo.featuredKey, "", out var teibanRankingItemsList);
                                    if (result)
                                    {
                                        if (teibanRankingItemsList.Count() != lastCheckMap.Count)
                                        {
                                            isUpdate = true;
                                        }
                                        else
                                        {
                                            foreach (var rankItem in teibanRankingItemsList)
                                            {
                                                if (!lastCheckMap.ContainsKey(rankItem.id))
                                                {
                                                    // 前回の結果にない動画が増えている
                                                    // →更新あり
                                                    isUpdate = true;
                                                    break;
                                                }
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (!result)
                                {
                                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} 定番更新チェックでエラーが発生しました　---- ");
                                    return false;
                                }
                                if (isUpdate)
                                {
                                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} 定番ランキング更新を検出しました　---- ");
                                    break;
                                }
                                else
                                {
                                    Console.WriteLine($"---- {this.RankInfo.folder}:{DateTime.Now.ToString()} 定番ランキング更新がありませんでした。5分後にリトライします　---- ");
                                    // 5分間隔でチェックする
                                    Task.Delay(60000 * 5).Wait();
                                }
                            }
                        }
                    }
                }
            }

            // 並列処理オプションの設定
            ParallelOptions parallelOptions = new ParallelOptions();
            parallelOptions.MaxDegreeOfParallelism = 5;

            if (false)
            {// デバッグ
                var debugTarget = this.GenreResultList.Where(x => (string?)x.genreInfo.genre == "全ジャンル").ToList();
                foreach (var genreResult in debugTarget)
                {
                    analyze(genreResult);
                }
            }
            else
            {
                Parallel.ForEach(this.GenreResultList, parallelOptions, genreResult =>
                {
                    analyze(genreResult);
                });
            }
            return SaveOldRankingData();
        }

        /// <summary>
        /// ランキングデータを保存する
        /// </summary>
        /// <returns>保存成功かどうか</returns>
        public bool SaveOldRankingData()
        {
            // まずデータが存在するかチェックする
            var CountData = (this.GenreResultList.Sum(genre => genre.rankLogJsonList.Count));
            if (CountData <= 0)
            {
                // データ取得なし
                return false;
            }

            // ランキング日付フォルダ名の確定
            if (this.GenreResultList.Sum(genre => genre.rankLogJsonList.Count) > 0)
            {
                // データがあるのでフォルダを作成する
                Directory.CreateDirectory(this.TargetSaveDir);

                {
                    // ++++++++++++++++++++++++++++++ file_name_list.jsonの作成 ++++++++++++++++++++++++++++++++++++++++++++
                    var genreList = new List<RankGenreJson>(this.GenreResultList.Count);
                    foreach (var genreInfo in this.GenreResultList)
                    {
                        if (genreInfo.rankLogJsonList.Count > 0)
                        {
                            // 値を取得できた
                            var wjsongenre = new RankGenreJson();
                            wjsongenre.Genre = genreInfo.genreInfo.genre;
                            wjsongenre.Tag = genreInfo.genreInfo.tag;
                            wjsongenre.File = genreInfo.genreInfo.file;
                            wjsongenre.isGenre = genreInfo.genreInfo.isGenre;
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
                            errLog.Write($"{this.Path_file_name_list}の書き込みでエラー発生。(RankApi2Json::SaveOldRankingData)");
                        }
                    }
                    // ----------------------------------- file_name_list.json

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
                                    errLog.Write($"{genreInfo.TargetSavePath}の書き込みでエラー発生。(RankApi2Json::SaveOldRankingData)");
                                }
                            }

                        }
                    }
                    if (this.CheckString.Length > 0)
                    {
                        string str_genre_jon = JsonConvert.SerializeObject(this.lastRankResult, Newtonsoft.Json.Formatting.None);

                        using (TextUtil textUtil = new TextUtil())
                        {
                            if (textUtil.WriteOpen(this.CheckString, true, true))
                            {
                                textUtil.WriteText(str_genre_jon);
                            }
                            else
                            {
                                var errLog = ErrLog.GetInstance();
                                errLog.Write($"{this.CheckString}の書き込みでエラー発生。(RankApi2Json::SaveOldRankingData)");
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
