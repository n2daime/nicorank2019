using Newtonsoft.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
namespace nicorankLib.Analyze.Option
{
    public class FavoriteTagReader : IExtOptionBase
    {
        /// <summary>
        /// 集計開始日
        /// </summary>
        protected DateTime BaseTime;

        /// <summary>
        /// 集計終了日
        /// </summary>
        protected DateTime EndTime;
        public int UserEnd = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="BaseTime">集計開始日</param>
        /// <param name="EndTime">集計終了日</param>
        /// <param name="otherOption"></param>
        public FavoriteTagReader(int userEnd, DateTime BaseTime, DateTime EndTime)
        {
            UserEnd = userEnd;
            this.BaseTime = BaseTime;
            this.EndTime = EndTime;
        }


        /// <summary>
        /// 人気のタグを取得する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public bool AnalyzeRank(List<Ranking> rankingList)
        {
            try
            {
                StatusLog.WriteLine("過去ランキングデータから人気のタグを取得しています...");

                //取得対象の抽出
                // 指定順位内か、カテゴリ一位の場合は取得する
                List<Ranking> targetList = rankingList;
                if (UserEnd > 0)
                {
                    targetList =
                       rankingList.Where(wRank => wRank.RankTotal <= this.UserEnd || wRank.RankCategory == 1)
                       .ToList();
                }

                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (!dbCtrl.Open(DB.LOG_OFFICEIAL))
                    {
                        StatusLog.WriteLine($"{ DB.LOG_OFFICEIAL}を開けませんでした。");
                        return false;
                    }

                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        // 過去ランキングデータから人気のタグを取得する
                        aCmd.CommandText =
                            @"SELECT Ranking.ID,Ranking.人気のタグ
                              FROM Ranking
                              JOIN 
                                ( 
                                SELECT ID,MAX(集計日) as 集計日 ,人気のタグ FROM Ranking
                                Where 集計日 BETWEEN @集計開始日 and @集計終了日 AND 人気のタグ != '[]'
                                GROUP BY ID
                                ) AS 人気タグデータ
                                ON Ranking.ID = 人気タグデータ.ID AND Ranking.集計日 = 人気タグデータ.集計日
                            WHERE Ranking.ID = @ID";
                        aCmd.Parameters.AddWithValue("@集計開始日", DateConvert.Time2String(this.BaseTime, false));
                        aCmd.Parameters.AddWithValue("@集計終了日", DateConvert.Time2String(this.EndTime, false));

                        foreach (var wRank in targetList)
                        {
                            aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string jsonString = reader["人気のタグ"].ToString();
                                    var hashObj = JsonConvert.DeserializeObject<List<string>>(jsonString);
                                    hashObj.ForEach(tag => wRank.FavoriteTags.Add(tag));
                                }
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

    }
}
