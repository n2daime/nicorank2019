using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Json;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using System.Threading;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace nicorankLib.Analyze.Official
{
    public class RankingHistory:IDisposable
    {
        protected const string DBFILE_NAME = DB.LOG_OFFICEIAL;

        protected SQLiteCtrl dbCtrlOfficial = null;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RankingHistory()
        {
        }

        /// <summary>
        /// LogOfficial.dbを開く
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            Close();
            dbCtrlOfficial = new SQLiteCtrl();

            var dbFile = Path.Combine(Directory.GetCurrentDirectory(), DBFILE_NAME);

            if (!dbCtrlOfficial.Open(dbFile))
            {
                return false;
            }

            using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
            {
                try
                {
                    var strSQL = @"attach 'DB/LogNicoChart.db' as NicoChart;";
                    aCmd.CommandText = strSQL;
                    aCmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"LogNicoChart.dbアタッチでエラー発生。(RankingHistory::Open)");
                    errLog.Write(ex);
                    return false;
                }
            }
            return true;

        }

        /// <summary>
        /// DBを閉じる
        /// </summary>
        public void Close()
        {
            if (dbCtrlOfficial != null && dbCtrlOfficial.IsOpen)
            {
                try
                {
                    using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                    {
                        var strSQL = @"detach NicoChart;";
                        aCmd.CommandText = strSQL;
                        aCmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    dbCtrlOfficial.Close();
                }
            }
            dbCtrlOfficial = null;
        }

        /// <summary>
        /// 公式チャンネルの動画が本当に新着なのか確認する
        /// </summary>
        /// <param name="id"></param>
        /// <param name="baseTime"></param>
        /// <param name="ranking"></param>
        /// <returns></returns>
        public bool CheckSoMovieNeedSabun(string id, long baseTime, out Ranking ranking)
        {
            ranking = null;
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                {
                    aCmd.CommandText =
                        @"select * from Ranking 
                        Where ID = @ID and 集計日 <= @Date 
                        order by 集計日 desc 
                        Limit 1 ";

                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@Date", baseTime);

                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {                    
                            //過去のランキングに記載されている＝新着ではない
                            ranking = new Ranking()
                            {
                                ID = id,
                                CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                CountLike = System.Convert.ToInt64(reader["いいね数"])
                            };
                        }
                    }
                    if (ranking == null)
                    {//取得できなかった場合、ニコチャートの方で確認する
                        for (int reTryCnt = 0; reTryCnt < 2; reTryCnt++)
                        {
                            aCmd.CommandText =
                                @"Select min(集計日) as 集計日 from NicoChart.Ranking 
                              Where ID = @ID";

                            aCmd.Parameters.AddWithValue("@ID", id);

                            //実行結果の取得
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    object syuukeiBi = reader["集計日"];
                                    if (syuukeiBi == DBNull.Value)
                                    {// ニコチャートから取得したことがない
                                        if (this.GetRankingDataLogNicoChart(id,baseTime))
                                        {
                                            //DB更新してもう一回
                                            continue;
                                        }
                                    }
                                    else
                                    {//
                                        var syukeibi = System.Convert.ToInt64(reader["集計日"]);
                                        if (syukeibi > baseTime)
                                        {
                                            ranking = null;
                                            return true;
                                        }
                                    }
                                }
                            }
                            break;
                        }

                        aCmd.CommandText =
                            @"select * from NicoChart.Ranking 
                        Where ID = @ID and 集計日 <= @Date 
                        order by 集計日 desc 
                        Limit 1 ";

                        aCmd.Parameters.AddWithValue("@ID", id);
                        aCmd.Parameters.AddWithValue("@Date", baseTime);

                        //実行結果の取得
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ranking = new Ranking()
                                {
                                    ID = id,
                                    CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                    CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                    CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                    CountLike = 0 //ニコチャートからは取得できない
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。ID={id}(RankingHistory::GetRankingSabunData)");
                errLog.Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        ///  公式の過去ログから差分を取得する
        /// </summary>
        /// <param name="id"></param>
        /// <param name="baseTime"></param>
        /// <param name="ranking"></param>
        /// <returns></returns>
        public bool GetRankingSabunDataLogOfficial(string id, long baseTime, long baseTime2, out Ranking ranking)
        {
            ranking = null;
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                {
                    aCmd.CommandText =
                        @"select * from Ranking 
                        Where ID = @ID and 集計日 BETWEEN @Date2 AND @Date1
                        order by 集計日 desc 
                        Limit 1 ";

 
                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@Date1", baseTime);

                    aCmd.Parameters.AddWithValue("@Date2", baseTime2);

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ranking = new Ranking()
                            {
                                ID = id,
                                CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                CountLike = System.Convert.ToInt64(reader["いいね数"])
                            };
                        }
                    }

                    if (ranking == null)
                    {//取得できなかった場合
                        aCmd.CommandText =
                        @"select * from Ranking 
                        Where ID = @ID and 集計日 >= @Date
                        order by 集計日 
                        Limit 1 ";

                        aCmd.Parameters.AddWithValue("@ID", id);
                        aCmd.Parameters.AddWithValue("@Date", baseTime);

                        //実行結果の取得
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ranking = new Ranking()
                                {
                                    ID = id,
                                    CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                    CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                    CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                    CountLike = System.Convert.ToInt64(reader["いいね数"])
                                };
                            }
                        }
                    }
                    if (ranking == null)
                    {//取得できなかった場合
                        aCmd.CommandText =
                            @"select * from NicoChart.Ranking 
                        Where ID = @ID and BETWEEN @Date2 AND @Date1 
                        order by 集計日 desc 
                        Limit 1 ";

                        aCmd.Parameters.AddWithValue("@ID", id);
                        aCmd.Parameters.AddWithValue("@Date1", baseTime);
                        aCmd.Parameters.AddWithValue("@Date2", baseTime2);

                        //実行結果の取得
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ranking = new Ranking()
                                {
                                    ID = id,
                                    CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                    CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                    CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                    CountLike = 0
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。ID={id}(RankingHistory::GetRankingSabunData)");
                errLog.Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        ///  ニコチャートの過去ログから差分を取得する
        /// </summary>
        /// <param name="id"></param>
        /// <param name="baseTime"></param>
        /// <param name="ranking"></param>
        /// <returns></returns>
        public bool GetRankingSabunDataLogNicoChart(string id, long baseTime, out Ranking ranking)
        {
            ranking = null;
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                {
                    //集計日より新しいデータが無い場合はNicoChartに取りに行く
                    aCmd.CommandText =
                        @"select * from NicoChart.Ranking 
                        Where ID = @ID
                        Limit 1 ";

                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@Date", baseTime);

                    //ニコチャートからデータを取得する必要があるか
                    bool isGetNicoChart = true;

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            //すでにデータがある場合
                            isGetNicoChart = false; //ニコチャートからデータを取得しない
                        }
                    }
                    if (isGetNicoChart)
                    { //ニコチャートからデータを取得する
                        if (!this.GetRankingDataLogNicoChart(id , baseTime))
                        {
                            return false;
                        }
                    }
                    aCmd.CommandText =
                        @"select * from NicoChart.Ranking 
                        Where ID = @ID and 集計日 <= @Date 
                        order by 集計日 desc 
                        Limit 1 ";

                    aCmd.Parameters.Clear();
                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@Date", baseTime);

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ranking = new Ranking()
                            {
                                ID = id,
                                CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                CountMyList = System.Convert.ToInt64(reader["マイリスト数"])
                            };
                        }
                        else if(!isGetNicoChart)
                        {
                            ranking = new Ranking()
                            {
                                ID = id,
                                CountPlay = 0,
                                CountComment = 0,
                                CountMyList = 0
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。ID={id}(RankingHistory::GetRankingSabunData)");
                errLog.Write(ex);
                return false;
            }

            return true;
        }


        /// <summary>
        /// NicoChartTsv一時保管用
        /// </summary>
        class NicoChartTsv
        {
            public DateTime Date;
            public long? CountPlay = null;
            public long? CountComment = null;
            public long? CountMyList = null;
        };

        /// <summary>
        /// NicoChartが公開しているTSVファイルを取得してDB更新する
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool GetRankingDataLogNicoChart(string id , long baseTime)
        {
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            //const int COL_KIND = 0;
            const int COL_DATE = 1;
            const int COL_PLAY = 7;
            const int COL_COMMENT = 8;
            const int COL_MYLIST = 9;
            try
            {
                //http://www.nicochart.jp/point/sm10555564.tsv
                var tsvUrl = $"http://www.nicochart.jp/point/{id}.tsv";
                if (!InternetUtil.TxtDownLoad(tsvUrl, out string tsvText))
                {
                    //失敗
                    return false;
                }

                // 改行毎に分割する
                string[] lines = tsvText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                var tsvMap = new Dictionary<DateTime, NicoChartTsv>(lines.Length);
                foreach (var strLine in lines)
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(strLine)))
                    {
                        var parser = new TextFieldParser(stream, Encoding.UTF8);
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters("\t");
                        // カラム毎に分割する
                        string[] cols = parser.ReadFields();
                        if (cols.Length > COL_MYLIST)
                        {//有効なデイリー
                            var wRank = new NicoChartTsv()
                            {
                                Date = DateTime.ParseExact(cols[COL_DATE], "yyyy-MM-dd", null)
                            };
                            if (!tsvMap.ContainsKey(wRank.Date))
                            {
                                if (long.TryParse(cols[COL_PLAY], out long parseWork))
                                {
                                    wRank.CountPlay = parseWork;
                                }
                                if (long.TryParse(cols[COL_COMMENT], out parseWork))
                                {
                                    wRank.CountComment = parseWork;
                                }
                                if (long.TryParse(cols[COL_MYLIST], out parseWork))
                                {
                                    wRank.CountMyList = parseWork;
                                }

                                if (wRank.CountPlay == null && wRank.CountComment == null && wRank.CountMyList == null)
                                {//有効なデータが1個も無い
                                    continue;
                                }
                                tsvMap[wRank.Date] = wRank;
                            }
                        }
                    }
                }
                //Listに変換
                //集計日が古い→新しい順にソートする( 空白のデータを埋めるため )
                List<NicoChartTsv> tsvList = tsvMap.Values.OrderBy(rank => rank.Date).ToList();
                long CountPlay = 0;
                long CountComment = 0;
                long CountMyList = 0;
                foreach (var wRank in tsvList)
                {
                    //空白のデータは古い日付のデータを採用し、データがあれば更新する
                    if (wRank.CountPlay != null)
                    {
                        CountPlay = (long)wRank.CountPlay;
                    }
                    if (wRank.CountComment != null)
                    {
                        CountComment = (long)wRank.CountComment;
                    }
                    if (wRank.CountMyList != null)
                    {
                        CountMyList = (long)wRank.CountMyList;
                    }
                    wRank.CountPlay = CountPlay;
                    wRank.CountComment = CountComment;
                    wRank.CountMyList = CountMyList;
                }

                //データの節約のため、2019-01-01未満のデータは1つあれば良い
                if (tsvList.Any(rank => rank.Date.Year < 2019))
                {
                    //新→古いリストに変換
                    var workList = tsvList.OrderByDescending(rank => rank.Date);
                    var newList = new List<NicoChartTsv>(tsvList.Count);
                    foreach (var wRank in workList)
                    {
                        newList.Add(wRank);
                        if (wRank.Date.Year < 2019)
                        {
                            break;
                        }
                    }
                    //集計日が古い→新しい順にソートする
                    tsvList = newList.OrderBy(rank => rank.Date).ToList();
                }


                using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                {
                    try
                    {
                        //トランザクションの開始
                        aCmd.Transaction = dbCtrlOfficial.Connection.BeginTransaction();


                        //動画情報が無いときだけ追加する
                        var strSQL = @"INSERT INTO NicoChart.Ranking( ID, '集計日','再生数','コメント数','マイリスト数')
                               SELECT @ID,@Date,@Play,@Comment,@MyList
                               WHERE NOT EXISTS (SELECT * FROM NicoChart.Ranking WHERE ID=@ID AND 集計日 = @Date);";

                        aCmd.CommandText = strSQL;

                        if (tsvList.Count < 1)
                        {
                            tsvList.Add(new NicoChartTsv()
                            {
                                Date = DateConvert.String2Time(baseTime.ToString(), false),
                                CountPlay = 0,
                                CountComment = 0,
                                CountMyList = 0
                            });
                        }

                        foreach (var wRank in tsvList)
                        {

                            aCmd.Parameters.Clear();
                            aCmd.Parameters.AddWithValue("@ID", id);
                            aCmd.Parameters.AddWithValue("@Date", DateConvert.Time2String( wRank.Date , false) );
                            aCmd.Parameters.AddWithValue("@Play", (long)wRank.CountPlay);
                            aCmd.Parameters.AddWithValue("@Comment", (long)wRank.CountComment);
                            aCmd.Parameters.AddWithValue("@MyList", (long)wRank.CountMyList);
                            aCmd.ExecuteNonQuery();
                        }
                        aCmd.Transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        aCmd.Transaction.Rollback();
                        var errLog = ErrLog.GetInstance();
                        errLog.Write($"NicoChartTSV登録でエラー。(RankingHistory::getRankingDataLogNicoChart)");
                        errLog.Write(ex);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write("NicoChartTSV取得でエラー RankingHistory::getRankingDataLogNicoChart");
                errLog.Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// LogOfficial.dbの過去ログを更新する
        /// </summary>
        /// <returns></returns>
        public bool UpdateOfficialRankingDB()
        {
            try
            {
                if (!this.dbCtrlOfficial.IsOpen)
                {
                    return false;
                }
                //RankingDateテーブルが存在しなければ作成する
                if (!createRankingDateTable())
                {
                    return false;
                }

                ////更新の必要性をチェック
                if (!checkNeedUpdateOfficialRankingDB(out var needDailyList))
                {//エラー
                    return false;
                }
                if (needDailyList.Count < 1)
                {
                    StatusLog.WriteLine($"過去ランキングデータは最新です。");
                    //更新の必要性はない
                    return true;
                }
                bool isMaintenanceOK = false;
                foreach (var targetDate in needDailyList)
                {

                    StatusLog.WriteLine($"{targetDate.ToShortDateString()}の過去ランキングデータを取得しています...");
                    if (!analyzeDailyRanking(targetDate, out var rankings))
                    {
                        StatusLog.WriteLine($"\n{ targetDate.ToShortDateString() } のランキングデータ取得でエラー発生");
                        break;
                    }
                    if (rankings.Count < 1)
                    {
                        if (!isMaintenanceOK)
                        {
                            //1件も取得できなかった場合、対象の日にニコ動がメンテナンスしていた可能性がある
                            var askResult = MessageBox.Show(
    $@"{targetDate.ToShortDateString()}のランキングデータが取得できませんでした。
メンテナンス日として登録しますか？

OK: この後の取得不可日は全てメンテナンス日として登録します
キャンセル : 現在の処理を中断します"
                                , "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);

                            if (askResult == DialogResult.OK)
                            {
                                isMaintenanceOK = true;
                            }
                        }

                        if (isMaintenanceOK)
                        {
                            //このまま登録処理を続ける
                            if (!updateOfficialRankingDB_Daily(targetDate, rankings, true))
                            {
                                StatusLog.WriteLine($"\n{targetDate.ToShortDateString()} のランキングデータ登録でエラー発生");
                                break;
                            }
                        }
                        else
                        {
                            StatusLog.WriteLine($"\n{targetDate.ToShortDateString()} のランキングデータ取得でエラー発生。集計を中断します");
                            return false;
                        }

                    }
                    else
                    {
                        StatusLog.Write($"DB登録開始..");
                        if (!updateOfficialRankingDB_Daily(targetDate, rankings))
                        {
                            StatusLog.WriteLine($"\n{targetDate.ToShortDateString()} のランキングデータ登録でエラー発生");
                            break;
                        }
                    }
                    StatusLog.WriteLine($"DB登録終了。");

                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::UpdateOfficialRankingDB)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 指定日付のランキング情報を取得する
        /// </summary>
        /// <param name="targetDate"></param>
        /// <returns></returns>
        protected bool analyzeDailyRanking(DateTime targetDate, out List<Ranking> rankings)
        {
            rankings = new List<Ranking>();
            var readerList = new List<JsonReaderBase>()
            { 
                //デイリーと総合ランキングは集計する
                new JsonReaderDaily(targetDate),
                new JsonReaderTotal(targetDate)
            };
            if (targetDate.DayOfWeek == DayOfWeek.Monday)
            {// 月曜日の場合は週刊ランキングも集計する
                readerList.Add(new JsonReaderWeekly(targetDate));
            }
            if (targetDate.Day == 1)
            {// 1日の場合は月間ランキングも集計する
                readerList.Add(new JsonReaderMonthly(targetDate));
            }

            var rankListList = new List<List<Ranking>>();
            foreach (var reader in readerList)
            {
                if (reader.AnalyzeRank(out List<Ranking> workList))
                {
                    rankListList.Add(workList);
                }
            }
            if (rankListList.Count == 1)
            {
                rankings = rankListList[0];
            }
            else
            {
                rankings = Ranking.MergeRankingList(rankListList);
            }
            return true;
        }

        /// <summary>
        /// 更新の必要性をチェック
        /// </summary>
        /// <param name="needDailyList"></param>
        /// <returns></returns>
        protected bool checkNeedUpdateOfficialRankingDB(out List<DateTime> needDailyList)
        {
            needDailyList = new List<DateTime>();
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                {
                    //どこまで集計されているか取得する
                    //NULL=集計されていない場合、20190611から集計できるように設定しておく
                    aCmd.CommandText =
                        @"SELECT IFNULL(Max(集計日), 20190610) as '集計日' 
                          FROM RankingDate;";

                    DateTime today = DateTime.Now.Date;
                    DateTime baseDateTime = today;

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object result = reader["集計日"];

                            // 最終集計した次の日から取得する
                            baseDateTime = DateConvert.String2Time(result.ToString(), false);
                            baseDateTime = baseDateTime.AddDays(1);
                            break;
                        }

                        //集計が必要な開始日～最新のデイリーまでログを取得する
                        while (baseDateTime <= today)
                        {
                            if (!JsonReaderBase.CheckAnalyzeTime(DateTime.Now))
                            {//当日の0:30前の場合は、まだ集計されていない可能性があるので、集計しない
                                break;
                            }

                            needDailyList.Add(baseDateTime);
                            baseDateTime = baseDateTime.AddDays(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::checkNeedUpdateOfficialRankingDB)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }


        /// <summary>
        /// LogOfficial.dbの過去ログを更新する(1日分）
        /// </summary>
        /// <param name="analyzeTime"></param>
        /// <param name="rankings"></param>
        /// <returns></returns>
        protected bool updateOfficialRankingDB_Daily(DateTime analyzeTime, List<Ranking> rankings,bool isMaintenance = false)
        {
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }

            using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
            {
                try
                {
                    //トランザクションの開始
                    aCmd.Transaction = dbCtrlOfficial.Connection.BeginTransaction();

                    long analyzeDate = long.Parse(DateConvert.Time2String(analyzeTime, false));

                    if (!isMaintenance)
                    {//←メンテナンス日以外の時実行

      

                        var strSQL = @"INSERT INTO Ranking
                                  ( ID,     集計日,再生数,コメント数,マイリスト数,いいね数,人気のタグ )
                                    VALUES
                                  ( @ID,    @Date,  @Play, @Comment, @MyList ,@いいね数 ,@人気のタグ)";

                        aCmd.CommandText = strSQL;

                        
                        Regex regDelete = new Regex(@"/video_deleted");

                        foreach (var wRank in rankings)
                        {
                            if (regDelete.IsMatch(wRank.ThumbnailURL))
                            {// 削除 or 非表示動画は登録しない
                            }
                            else
                            {
                                aCmd.Parameters.Clear();
                                aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                                aCmd.Parameters.AddWithValue("@Date", analyzeDate);
                                aCmd.Parameters.AddWithValue("@Play", wRank.CountPlay);
                                aCmd.Parameters.AddWithValue("@Comment", wRank.CountComment);
                                aCmd.Parameters.AddWithValue("@MyList", wRank.CountMyList);
                                aCmd.Parameters.AddWithValue("@いいね数", wRank.CountLike);
                                aCmd.Parameters.AddWithValue("@人気のタグ", JsonConvert.SerializeObject(wRank.FavoriteTags));
                                //更新の実行
                                aCmd.ExecuteNonQuery();
                            }
                        }

                        //動画情報が無いときだけ追加する
                        strSQL = @"INSERT INTO Movie( ID, '投稿日','タイトル')
                               SELECT @ID,@Date,@Title
                               WHERE NOT EXISTS (SELECT * FROM Movie WHERE ID=@ID);";

                        aCmd.CommandText = strSQL;

                        foreach (var wRank in rankings)
                        {
                            if (!regDelete.IsMatch(wRank.ThumbnailURL))
                            {// 削除 or 非表示動画は登録しない
                            }
                            else
                            {
                                aCmd.Parameters.Clear();
                                aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                                aCmd.Parameters.AddWithValue("@Date", DateConvert.Time2String(wRank.Date, true));
                                aCmd.Parameters.AddWithValue("@Title", wRank.Title);
                                //更新の実行
                                aCmd.ExecuteNonQuery();
                            }
                        }
                    }//←メンテナンス日以外の時実行


                    {//RankingDateテーブルの更新

                        var strSQL = @"INSERT INTO RankingDate
                                  ( '集計日','メンテナンス' )
                                    VALUES
                                  ( @Date,  @メンテナンス)";

                        aCmd.CommandText = strSQL;
                        aCmd.Parameters.Clear();
                        aCmd.Parameters.AddWithValue("@Date", analyzeDate);
                        aCmd.Parameters.AddWithValue("@メンテナンス", isMaintenance? 1:0 );

                        //更新の実行
                        aCmd.ExecuteNonQuery();
                    }


                    aCmd.Transaction.Commit();
                }
                catch (Exception ex)
                {
                    aCmd.Transaction.Rollback();

                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::updateOfficialRankingDB_Daily)");
                    errLog.Write(ex);
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// RankingDateテーブルが存在しなければ追加する
        /// </summary>
        /// <param name="needDailyList"></param>
        /// <returns></returns>
        protected bool createRankingDateTable()
        {
            // メンテナンス日かどうかを判定するための、RankingDateテーブルが存在しなければ追加する
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = new SQLiteCommand(dbCtrlOfficial.Connection))
                {
                    //どこまで集計されているか取得する
                    //NULL=集計されていない場合、20190611から集計できるように設定しておく
                    aCmd.CommandText =
                        @"SELECT * FROM sqlite_master WHERE TYPE='table' AND name='RankingDate';";


                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            //存在しているので作成する必要なし
                            return true;
                        }

                    }

                    StatusLog.WriteLine("メンテナンス日を管理するテーブルを作成しています(数分程度かかります。気長にお待ちください)...");


                    //存在しないので作成する
                    //トランザクションの開始
                    aCmd.Transaction = dbCtrlOfficial.Connection.BeginTransaction();

                    aCmd.CommandText =
                        @"CREATE TABLE RankingDate (
                            '集計日'	    INTEGER             ,
                            'メンテナンス'	INTEGER DEFAULT 0   ,
                            PRIMARY KEY('集計日')
                            );";

                    //DEB作成の実行
                    aCmd.ExecuteNonQuery();

                    //テーブルの中身を、現時点のデータを元に作成する
                    aCmd.CommandText =
                        @"INSERT INTO RankingDate ('集計日', 'メンテナンス')
                            SELECT 集計日,
                              CASE
                                WHEN COUNT(集計日) > 0 THEN 0
                                ELSE 1
                              END AS 'メンテナンス'
                          FROM Ranking
                          Group by 集計日;";

                    //テーブル更新の実行
                    aCmd.ExecuteNonQuery();

                    aCmd.Transaction.Commit();
                }

            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{ DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::createRankingDateTable)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }

        ///メンテナンス日かどうかをチェックする
        public bool CheckMaintananceDay(DateTime chechDay)
        {
            try
            {
                if (!this.dbCtrlOfficial.IsOpen)
                {
                    return false;
                }

                using (var aCmd = new SQLiteCommand(this.dbCtrlOfficial.Connection))
                {
                    //すでに集計済みか確認する
                    aCmd.CommandText =
                        @"SELECT メンテナンス FROM RankingDate
                                Where 集計日 = @集計日
                                LIMIT 1";
                    aCmd.Parameters.Clear();
                    aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(chechDay, false));

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (Convert.ToInt64(reader["メンテナンス"].ToString()) > 0)
                            {
                                //メンテナンス中
                                return true;
                            }
                            else
                            {
                                return false;

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return false;
        }
       
        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    Close();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        ~RankingHistory()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
