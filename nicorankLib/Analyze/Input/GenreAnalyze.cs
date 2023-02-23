using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace nicorankLib.Analyze.Input
{
    public class GenreAnalyze : InputBase
    {
        public DateTime AnalyzeTime { get; protected set; }
        public DateTime BaseTime { get; protected set; }


        public string GenreSQL = @"人気のタグ LIKE '%""演奏してみた""%'";

        public GenreAnalyze(DateTime analyzeTime, DateTime baseTime)
        {
            AnalyzeTime = analyzeTime;
            BaseTime = baseTime;
        }


        public override bool AnalyzeRank(out List<Ranking> rakingList)
        {
            rakingList = new List<Ranking>();

            using (var dbCtrl = new SQLiteCtrl())
            {
                if(!dbCtrl.Open(DB.LOG_OFFICEIAL))
                {
                    StatusLog.WriteLine($"{DB.LOG_OFFICEIAL}が開けません");
                    return false;
                }
                using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                {
                    aCmd.CommandText =
                        $@"SELECT Movie.ID as ID , Movie.タイトル , Movie.投稿日 FROM Movie
                          JOIN (
                          SELECT ID FROM Ranking
                          WHERE 集計日 BETWEEN @開始日 AND @集計日 AND {GenreSQL}
                          GROUP BY ID ) AS IDLIST
                          ON Movie.ID = IDLIST.ID";

                    aCmd.Parameters.AddWithValue("@開始日", DateConvert.Time2String( this.BaseTime.AddDays(1) , false)) ;
                    aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String( this.AnalyzeTime , false));

                    using (var reader = aCmd.ExecuteReader())
                    {
                        while(reader.Read())
                        {
                            var wRank = new Ranking();
                            wRank.ID = reader["ID"].ToString();
                            wRank.Title = reader["タイトル"].ToString();
                            wRank.Date = DateConvert.String2Time( reader["投稿日"].ToString() , true);
                            rakingList.Add(wRank);
                        }
                    }
                }
            }

            return true;
        }

        public override DateTime getAnalyzeDay()
        {
            return AnalyzeTime;
        }

        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            AnalyzeTime = analyzeDay;
        }
    }
}
