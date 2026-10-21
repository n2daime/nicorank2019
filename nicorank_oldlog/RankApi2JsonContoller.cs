using nicorank_oldlog.RankAPI;
using nicorankLib.Util;


namespace nicorank_oldlog
{
    /// <summary>
    /// term（daily/weekly/monthly/total）ごとの実行結果（Issue #51）。
    /// 「全滅時だけ異常終了」の判断に使う。部分失敗は exit 0 のままログで警告する
    /// （ユーザー判断。全 term が失敗したときだけ NAS のエラーメールを出す）。
    /// </summary>
    public class RankingRunResult
    {
        /// <summary>実行対象になった term 数</summary>
        public int TotalCount => Succeeded.Count + Failed.Count;

        /// <summary>データを保存できた term のフォルダ名（ログ用）</summary>
        public List<string> Succeeded { get; } = new List<string>();

        /// <summary>データを保存できなかった term のフォルダ名（ログ用）</summary>
        public List<string> Failed { get; } = new List<string>();
    }

    public class RankApi2JsonContoller
    {
        DateTime today;
        string optionFolderAppend = "";

        NicoRankiApi nicoApi;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RankApi2JsonContoller()
        {
            // 日付は一回の実行で共通のものを使用する（途中で日付が変わることを考慮）
            this.today = DateTime.Now;

            nicoApi = NicoRankiApi.GetInstance() ?? throw new InvalidOperationException("NicoRankiApi instance is null");
        }

        /// <summary>
        /// 取得するジャンルランキングの一覧を取得する
        /// </summary>
        /// <param name="genreInfoList">取得したジャンル情報リスト</param>
        /// <returns>取得成功かどうか</returns>
        public bool GetGenreInfoList(out List<GenreInfo> genreInfoList)
        {
            var genreInfoMap = new Dictionary<string, GenreInfo>();

            // allを追加
            {
                var workGenre = new GenreInfo();
                workGenre.genre = "全ジャンル";
                workGenre.genrekey = "all";
                workGenre.file = "all.json";
                workGenre.isGenre = false; // ジャンルではない
                workGenre.isGenreRank = true;
                genreInfoMap.Add(workGenre.genre, workGenre);
            }

            // ジャンルランキングの一覧を取得する
            bool isSuccess = nicoApi.GetGenreList(out var genreList);
            if (!isSuccess)
            {
                genreInfoList = new List<GenreInfo>();
                return false;
            }

            foreach (var genreInfo in genreList)
            {
                var workGenre = new GenreInfo();
                workGenre.genre = genreInfo.label;
                workGenre.genrekey = genreInfo.key;
                workGenre.file = genreInfo.key + ".json";
                workGenre.isGenre = true;
                workGenre.isGenreRank = true;
                genreInfoMap.Add(workGenre.genre, workGenre);
            }

            // 定番ジャンルの一覧を取得する
            isSuccess = nicoApi.GetTeibanGenreList(out var teibangenreList);
            if (!isSuccess)
            {
                genreInfoList = genreInfoMap.Values.ToList();
                return false;
            }

            foreach (var genreInfo in teibangenreList)
            {
                if (!genreInfo.isEnabled)
                {
                    // 無効なジャンルは登録しない
                    continue;
                }

                var genreKey = genreInfo.label == "総合" ? "全ジャンル" : genreInfo.label;
                if (genreInfoMap.TryGetValue(genreKey, out var workGenre))
                {
                    // 同じ名前のジャンルがある
                    workGenre.featuredKey = genreInfo.featuredKey;
                    workGenre.isTeibanRank = true;
                    workGenre.isEnabledTrendTag = genreInfo.isEnabledTrendTag;
                }
                else
                {
                    // 未登録の定番区分
                    workGenre = new GenreInfo
                    {
                        genre = genreInfo.label,
                        featuredKey = genreInfo.featuredKey,
                        file = genreInfo.featuredKey + ".json",
                        isTeibanRank = true,
                        isEnabledTrendTag = genreInfo.isEnabledTrendTag
                    };
                }
                genreInfoMap[genreKey] = workGenre;
            }
            genreInfoList = genreInfoMap.Values.ToList();
            return true;
        }

        /// <summary>
        /// 取得するランキングの種類を決定する(daily/weekly/monthly/total)
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        /// <returns>取得するランキング情報のリスト</returns>
        public List<Ranking_Info> GetRankingInfo(in string[] args)
        {
            var convConfig = ConvertConfig.GetInstance();

            var getRankingList = new List<Ranking_Info>(); // 取得するランキングの種類

            if (convConfig != null)
            {
                if (args.Length < 1)
                {
                    // daily / totalは毎日更新
                    getRankingList.Add(convConfig.ranking_daily);
                    getRankingList.Add(convConfig.ranking_total);

                    // 今日の日付を取得
                    // 有効な集計日になるまでループ

                    if (today.DayOfWeek == DayOfWeek.Monday)
                    {
                        // 月曜日はweekly
                        getRankingList.Add(convConfig.ranking_weekly);
                    }
                    if (today.Day == 1)
                    {
                        // 毎月１日はmonthly
                        getRankingList.Add(convConfig.ranking_monthly);
                    }
                }
                else
                {
                    Ranking_Info? temp = null;

                    foreach (var arg in args)
                    {
                        if (arg.StartsWith("/term:"))
                        {
                            var workOptiopn = arg.Substring("/term:".Length);

                            switch (workOptiopn)
                            {
                                case "daily":
                                    temp = convConfig.ranking_daily;
                                    break;
                                case "weekly":
                                    temp = convConfig.ranking_weekly;
                                    break;
                                case "monthly":
                                    temp = convConfig.ranking_monthly;
                                    break;
                                case "total":
                                    temp = convConfig.ranking_total;
                                    break;
                            }
                        }
                        if (arg.StartsWith("/folderappend:"))
                        {
                            optionFolderAppend = arg.Substring("/folderappend:".Length);
                        }
                    }
                    if (temp != null)
                    {
                        getRankingList.Add(temp);
                    }
                }
            }
            return getRankingList;
        }

        /// <summary>
        /// ランキングの分析を非同期で実行する（Issue #51）。
        /// term ごとに失敗した分だけを RetryPoller で再試行する。成功済みの term をやり直さないのは、
        /// 週刊の ApiXML.db（約26000件の動画情報）再取得のような無駄なアクセスを避けるためである。
        /// 再試行のたびに RankApi2Json を作り直すのは、RankApi2JsonDaily の Initilize が
        /// トレンドタグのジャンルを GenreResultList へ追記するため、使い回すと重複するからである。
        /// </summary>
        /// <param name="apigetRankingList">取得するランキング情報のリスト</param>
        /// <param name="genreList">ジャンル情報のリスト</param>
        /// <param name="retryPoller">再試行の予算を共有するポーラー</param>
        /// <returns>term ごとの成否</returns>
        public async Task<RankingRunResult> AsyncExecuteAnalyzeRank(
            List<Ranking_Info> apigetRankingList, List<GenreInfo> genreList, RetryPoller retryPoller)
        {
            var runResult = new RankingRunResult();

            var taskList = new List<Task>();

            foreach (var rankingInfo in apigetRankingList)
            {
                var workRankingInfo = rankingInfo;
                taskList.Add(Task.Run(() =>
                {
                    bool ok = retryPoller.RunUntilSuccess(
                        () => ExecuteRankOnce(workRankingInfo, genreList),
                        message => StatusLog.WriteLine($"{workRankingInfo.folder}: {message}"));

                    // term は並行に走るため、結果リストへの追加は lock で保護する
                    lock (runResult)
                    {
                        if (ok)
                        {
                            runResult.Succeeded.Add(workRankingInfo.folder);
                        }
                        else
                        {
                            runResult.Failed.Add(workRankingInfo.folder);
                        }
                    }
                }));
            }

            // 各タスクが終わるまで待つ
            await Task.WhenAll(taskList);

            return runResult;
        }

        /// <summary>
        /// 1回分の取得を実行する。成功判定は「取得できて、保存まで完了したか」。
        /// 部分的なカテゴリ失敗は RankApi2Json 側が警告として記録する（Issue #51）。
        /// </summary>
        private bool ExecuteRankOnce(Ranking_Info rankingInfo, List<GenreInfo> genreList)
        {
            RankApi2Json rss2jsonWork;
            if (rankingInfo.term == "24h")
            {
                rss2jsonWork = new RankApi2JsonDaily(rankingInfo, "");
            }
            else if (rankingInfo.term == "week")
            {
                rss2jsonWork = new RankApi2Json(rankingInfo, "lastweekly_all.json");
            }
            else if (rankingInfo.term == "month")
            {
                rss2jsonWork = new RankApi2Json(rankingInfo, "lastmonthly_all.json");
            }
            else
            {
                rss2jsonWork = new RankApi2Json(rankingInfo);
            }

            if (!rss2jsonWork.Initilize(genreList, today, optionFolderAppend))
            {
                // 現状の Initilize は false を返さないが、契約として成否を見る（Issue #51）
                StatusLog.WriteLine($"{rankingInfo.folder}: 初期化に失敗しました");
                return false;
            }

            return rss2jsonWork.AnalyzeRank();
        }
    }
}
