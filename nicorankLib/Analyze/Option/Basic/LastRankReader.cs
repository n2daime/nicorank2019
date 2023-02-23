using Newtonsoft.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace nicorankLib.Analyze.Option
{
    /// <summary>
    /// 先週(前回）のランキングを付与する
    /// </summary>
    public class LastRankReader : BasicOptionBase
    {
        /// <summary>
        /// 集計モード
        /// </summary>
        protected EAnalyzeMode analyzeMode;

        /// <summary>
        /// 前回集計日
        /// </summary>
        DateTime lastRankDay;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="analyzeMode">集計モード</param>
        /// <param name="lastDay">前回集計日</param>
        /// <param name="otherOption"></param>
        public LastRankReader(EAnalyzeMode analyzeMode, DateTime lastDay)
        {
            this.analyzeMode = analyzeMode;
            this.lastRankDay = lastDay;
        }

        /// <summary>
        /// 先週(前回）のランキングを付与する
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(ref List<Ranking> rakingList)
        {
            try
            {
                //先週のランキングを集計する
                StatusLog.WriteLine($"{ analyzeMode.ToString() }:{this.lastRankDay.ToShortDateString()}の集計データから前回順位を取得しています...");

                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (!dbCtrl.Open(DB.NiCORAN_HISTORY))
                    {
                        StatusLog.WriteLine($"{ DB.NiCORAN_HISTORY }が参照できません。");
                        return false;
                    }
                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        // 前回のランキングが存在するかチェックする
                        aCmd.CommandText =
                            @"SELECT * FROM Lastresult
                                      Where 種別=@種別 and 集計日=@集計日
                                      Limit 1;";

                        aCmd.Parameters.AddWithValue("@種別", analyzeMode.ToString());
                        aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(lastRankDay, false));
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {//データが存在しない
                                StatusLog.WriteLine($"{ analyzeMode.ToString() }:{ lastRankDay.ToShortDateString() }の集計データが存在しません");
                                StatusLog.WriteLine($"前回順位は取得できません");
                                return true;
                            }
                        }

                        // 前回のランキングの情報を取得する
                        aCmd.CommandText =
                            @"SELECT 総合ランク,ポイント FROM Lastresult
                                      Where 種別=@種別 and 集計日=@集計日 and ID = @ID";

                        foreach (var wRank in rakingList)
                        {
                            aCmd.Parameters.AddWithValue("@ID", wRank.ID);

                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {//先週のデータが存在する
                                    wRank.LastRank = Convert.ToInt64(reader["総合ランク"]);
                                    wRank.LastPoint = Convert.ToInt64(reader["ポイント"]);
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
