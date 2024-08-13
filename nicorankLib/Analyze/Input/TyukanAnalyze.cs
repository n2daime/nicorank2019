using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using nicorankLib.Analyze.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Official;
using nicorankLib.Analyze.Option;
using nicorankLib.Util;

namespace nicorankLib.Analyze.Input
{
    /// <summary>
    /// 中間集計を行う
    /// </summary>
    public class TyukanAnalyze : InputBase
    {
        const string DATASOURCE = "DB/Dailylog.db";


        protected DateTime TargetStartDay;

        public TyukanAnalyze(DateTime targetStartDay)
        {
            this.TargetStartDay = targetStartDay;
        }

        /// <summary>
        /// 集計するデイリーのリスト
        /// </summary>
        List<DateTime> targetDateList;

        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            calcTargetDateList(analyzeDay);
        }


        public override DateTime getAnalyzeDay()
        {
            if (this.targetDateList.Count < 1)
            {
                return DateTime.Today;
            }
            else
            {
                return this.targetDateList.Last();
            }
        }

        public DateTime GetBaseDay()
        {
            if (this.targetDateList.Count < 1)
            {
                return DateTime.Today.AddDays(-7);
            }
            else
            {
                return this.targetDateList.First().AddDays(-1).Date;
            }            
        }

        /// <summary>
        /// 中間集計を行う
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(out List<Ranking> rakingList)
        {
            rakingList = null;
            if (targetDateList.Count < 1)
            {
                StatusLog.WriteLine("\n集計期間に問題があります。メンテナンス以外の有効な日数が０日間です。集計できません");

                return false;
            }

            foreach (var targetDate in targetDateList)
            {
                StatusLog.WriteLine($"{targetDate.ToShortDateString()}のデイリーランキングを集計します...");
                if (!calcDailyRank(targetDate))
                {
                    StatusLog.WriteLine("\nデイリーランキング集計に失敗しました");
                    return false;
                }
                StatusLog.WriteLine("");
            }

            StatusLog.WriteLine("\n中間ランキングを集計しています...");
            if (!calcTyukanRank(out rakingList))
            {
                StatusLog.WriteLine("\n中間ランキング集計に失敗しました");
                return false;
            }


            return true;
        }

        /// <summary>
        /// デイリーランキングを算出する
        /// </summary>
        /// <param name="targetDate"></param>
        /// <param name="rakingDailyList"></param>
        /// <returns></returns>
        protected bool calcTyukanRank(out List<Ranking> rakingList)
        {
            rakingList = new List<Ranking>();
            try
            {
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (!dbCtrl.Open(DATASOURCE))
                    {
                        //失敗
                        return false;
                    }
                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {


                        aCmd.CommandText =
                            @"SELECT ID,タイトル,投稿日,
                            SUM(再生数) as 再生数 , SUM(コメント数) as コメント数 , SUM(マイリスト数) as マイリスト数, SUM(いいね数) as いいね数,
                            人気のタグ,イメージパス,カテゴリ
                            FROM Dailylog
                            WHERE 集計日 BETWEEN @集計開始日 AND @集計終了日
                            GROUP BY ID";
                        aCmd.Parameters.Clear();

                        DateTime startDate = this.targetDateList.First();
                        DateTime endDate = this.targetDateList.Last();

                        aCmd.Parameters.AddWithValue("@集計開始日", DateConvert.Time2String(startDate, false));
                        aCmd.Parameters.AddWithValue("@集計終了日", DateConvert.Time2String(endDate, false));

                        //実行結果の取得
                        using (var reader = aCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var wRank = new Ranking();

                                wRank.ID = reader["ID"].ToString();
                                wRank.Title = reader["タイトル"].ToString();
                                wRank.Date = DateConvert.String2Time(reader["投稿日"].ToString(), true);
                                wRank.CountPlay = Convert.ToInt64(reader["再生数"].ToString());
                                wRank.CountComment = Convert.ToInt64(reader["コメント数"].ToString());
                                wRank.CountMyList = Convert.ToInt64(reader["マイリスト数"].ToString());
                                wRank.CountLike = Convert.ToInt64(reader["いいね数"].ToString());
                                wRank.Category = reader["カテゴリ"].ToString();
                                //wRank.ID = reader["人気のタグ"].ToString();
                                wRank.ThumbnailURL = reader["イメージパス"].ToString();
                                rakingList.Add(wRank);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }


        /// <summary>
        /// デイリーランキングを算出する
        /// </summary>
        /// <param name="targetDate"></param>
        /// <param name="rakingDailyList"></param>
        /// <returns></returns>
        protected bool calcDailyRank(DateTime targetDate)
        {
            try
            {
                using (var dbCtrl = new SQLiteCtrl())
                using( var rankOfficial = new RankingHistory())
                {
                    if (!rankOfficial.Open())
                    {
                        //失敗
                        return false; ;
                    }

                    if (!dbCtrl.Open(DATASOURCE))
                    {
                        //失敗
                        return false; ;
                    }
                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        //すでに集計済みか確認する
                        aCmd.CommandText =
                            @"SELECT * FROM Dailylog
                          Where 集計日 = @集計日
                          LIMIT 1";
                        aCmd.Parameters.Clear();
                        aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(targetDate, false));

                        //実行結果の取得
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                //すでに集計済
                                return true;
                            }
                        }

                        //集計していないので、集計する必要がある
                        //集計に必要なオプションを作成する
                        DateTime BaseDay = targetDate;
                        do {
                            BaseDay = BaseDay.AddDays(-1);

                        }//差分対象がメンテナンス日であればさらに一日さかのぼる 
                        while (rankOfficial.CheckMaintananceDay(BaseDay));

                        var options = new List<BasicOptionBase>()
                        {
                            new SabunReader(BaseDay) 
                        };

                        var dailyInput = new RankingAnalyze(new JsonReaderDaily(targetDate),options);
                        if (!dailyInput.AnalyzeRank(out var rakingDailyList))
                        {
                            return false;
                        }
                        try
                        {
                            //集計結果をDBに登録する
                            aCmd.Transaction = dbCtrl.Connection.BeginTransaction();

                            {//DBの更新確認
                                bool isLikeFieldExist = false;
                                aCmd.CommandText = "PRAGMA table_info('Dailylog');";
                                using (var reader = aCmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {

                                        if (reader["name"].ToString().Equals("いいね数"))
                                        {
                                            isLikeFieldExist = true;
                                            break;
                                        }

                                    }
                                    reader.Close();
                                }
                                if (isLikeFieldExist == false)
                                {
                                    //いいねフィールドがない→アップデートする
                                    aCmd.CommandText =
                                        "ALTER TABLE Dailylog ADD いいねランク INTEGER DEFAULT 0; " +
                                        "ALTER TABLE Dailylog ADD いいね数 INTEGER DEFAULT 0;" +
                                        "ALTER TABLE Dailylog ADD いいね補正 INTEGER DEFAULT 1;" +
                                        "ALTER TABLE Dailylog ADD いいねポイント INTEGER DEFAULT 0;";

                                    aCmd.ExecuteNonQuery();
                                }
                                aCmd.Parameters.Clear();
                            }

                            aCmd.CommandText =
                                @"INSERT INTO Dailylog( 
                                 '集計日' , 'ID', 'タイトル',
                                 '投稿日' , '再生時間', '総合順位' , 
                                 'ポイント数' , 'カテゴリランク' ,'カテゴリ' , '人気のタグ', 
                                 '再生ランク' , '再生数' , '再生補正' ,'再生ポイント' ,
                                 'コメントランク' ,'コメント数' , 'コメント補正' , 'コメントポイント' , 
                                 'マイリストランク' , 'マイリスト数' , 'マイリスト補正' , 'マイリストポイント' , 
                                 'いいねランク' , 'いいね数' , 'いいね補正' , 'いいねポイント' , 
                                 'イメージパス')
                                  VALUES( 
                                 @集計日 , @ID, @タイトル,
                                 @投稿日 , @再生時間, @総合順位 , 
                                 @ポイント数 , @カテゴリランク , @カテゴリ , @人気のタグ, 
                                 @再生ランク , @再生数 , @再生補正 ,@再生ポイント ,
                                 @コメントランク ,@コメント数 , @コメント補正 , @コメントポイント , 
                                 @マイリストランク , @マイリスト数 , @マイリスト補正 , @マイリストポイント , 
                                 @いいねランク , @いいね数 , @いいね補正 , @いいねポイント , 
                                 @イメージパス)";

                            aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(targetDate, false));

                            foreach (var rank in rakingDailyList)
                            {
                                aCmd.Parameters.AddWithValue("@ID", rank.ID);
                                aCmd.Parameters.AddWithValue("@タイトル", rank.Title);
                                aCmd.Parameters.AddWithValue("@投稿日", DateConvert.Time2String(rank.Date, true));
                                aCmd.Parameters.AddWithValue("@再生時間", rank.PlayTime);
                                aCmd.Parameters.AddWithValue("@総合順位", rank.RankTotal);
                                aCmd.Parameters.AddWithValue("@ポイント数", rank.PointTotal);
                                aCmd.Parameters.AddWithValue("@カテゴリランク", rank.RankCategory);
                                aCmd.Parameters.AddWithValue("@カテゴリ", rank.Category);
                                aCmd.Parameters.AddWithValue("@人気のタグ", JsonConvert.SerializeObject(rank.FavoriteTags));
                                aCmd.Parameters.AddWithValue("@再生ランク", rank.RankPlay);
                                aCmd.Parameters.AddWithValue("@再生数", rank.CountPlay);
                                aCmd.Parameters.AddWithValue("@再生補正", rank.HoseiPlay);
                                aCmd.Parameters.AddWithValue("@再生ポイント", rank.PointPlay);
                                aCmd.Parameters.AddWithValue("@コメントランク", rank.RankComment);
                                aCmd.Parameters.AddWithValue("@コメント数", rank.CountComment);
                                aCmd.Parameters.AddWithValue("@コメント補正", rank.HoseiComment);
                                aCmd.Parameters.AddWithValue("@コメントポイント", rank.PointComment);
                                aCmd.Parameters.AddWithValue("@マイリストランク", rank.RankMyList);
                                aCmd.Parameters.AddWithValue("@マイリスト数", rank.CountMyList);
                                aCmd.Parameters.AddWithValue("@マイリスト補正", rank.HoseiMylist);
                                aCmd.Parameters.AddWithValue("@マイリストポイント", rank.PointMyList);
                                aCmd.Parameters.AddWithValue("@いいねランク", rank.RankLike);
                                aCmd.Parameters.AddWithValue("@いいね数", rank.CountLike);
                                aCmd.Parameters.AddWithValue("@いいね補正", 1);
                                aCmd.Parameters.AddWithValue("@いいねポイント", rank.PointLike);
                                aCmd.Parameters.AddWithValue("@イメージパス", rank.ThumbnailURL);

                                aCmd.ExecuteNonQuery();
                            }

                            aCmd.Transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            ErrLog.GetInstance().Write(ex);
                            aCmd.Transaction.Rollback();
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 集計するデイリーのリストを作成する
        /// </summary>
        protected void calcTargetDateList(DateTime analyzeDay)
        {
            targetDateList = new List<DateTime>();

            using (RankingHistory rankOfficial = new RankingHistory())
            {
                rankOfficial.Open();

                //月曜日の0:00は実質日曜日のデータなので集計しない（ので+１日）
                DateTime startDate = this.TargetStartDay.AddDays(1).Date;

                // 火曜日を見つけるまでループ
                while (true)
                {
                    if (analyzeDay.Date <= startDate)
                    {
                        if (!JsonReaderBase.CheckAnalyzeTime(analyzeDay))
                        {
                            //当日の0:30 前＝まだ集計されていない可能性がある
                        }
                        else
                        {
                            //集計日確定
                            if (!rankOfficial.CheckMaintananceDay(analyzeDay))
                            {
                                //メンテナンス中以外であれば集計する
                                targetDateList.Add(analyzeDay.Date);
                            }
                            break;
                        }
                    }
                    //集計日確定
                    if (!rankOfficial.CheckMaintananceDay(analyzeDay))
                    {
                        //メンテナンス中以外であれば集計する
                        targetDateList.Add(analyzeDay.Date);
                    }
                    analyzeDay = analyzeDay.AddDays(-1);
                }
            }
            targetDateList = targetDateList.OrderBy(date => date.Date).ToList(); ;
        }
    }
}
