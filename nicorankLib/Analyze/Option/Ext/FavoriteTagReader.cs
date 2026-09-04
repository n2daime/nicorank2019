using Newtonsoft.Json;
using nicorankLib.Analyze.model;
using nicorankLib.api;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
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

        protected ISQLiteCtrl _dbCtrlOverride;

        protected NicoApi _apiOverride;

        /// <summary>
        /// ローカルのみ（trueでApiXML.dbの確保＝外部取得を行わず、キャッシュ参照のみで補完する）
        /// </summary>
        protected bool IsLocalOnly;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="BaseTime">集計開始日</param>
        /// <param name="EndTime">集計終了日</param>
        /// <param name="otherOption"></param>
        public FavoriteTagReader(int userEnd, DateTime BaseTime, DateTime EndTime, ISQLiteCtrl dbCtrl = null, NicoApi api = null, bool isLocalOnly = false)
        {
            UserEnd = userEnd;
            this.BaseTime = BaseTime;
            this.EndTime = EndTime;
            _dbCtrlOverride = dbCtrl;
            _apiOverride = api;
            IsLocalOnly = isLocalOnly;
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

                // 注入された接続は呼び出し側の所有物のため破棄しない。自前生成分のみ破棄する
                bool ownsDbCtrl = _dbCtrlOverride == null;
                ISQLiteCtrl dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl();
                try
                {
                    // 注入済みでオープン中の接続（テスト用インメモリDB等）はそのまま使う
                    if (!dbCtrl.IsOpen && !dbCtrl.Open(DB.LOG_OFFICEIAL))
                    {
                        StatusLog.WriteLine($"{ DB.LOG_OFFICEIAL}を開けませんでした。");
                        return false;
                    }

                    using (var aCmd = dbCtrl.Connection.CreateCommand())
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
                            // Microsoft.Data.Sqlite は同名パラメータの重複追加を許さないため、ループ内でクリアして再設定する
                            aCmd.Parameters.Clear();
                            aCmd.Parameters.AddWithValue("@集計開始日", DateConvert.Time2String(this.BaseTime, false));
                            aCmd.Parameters.AddWithValue("@集計終了日", DateConvert.Time2String(this.EndTime, false));
                            aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string jsonString = reader["人気のタグ"].ToString();
                                    var hashObj = JsonConvert.DeserializeObject<List<string>>(jsonString);
                                    hashObj.ForEach(tag =>
                                    {
                                        if (!wRank.FavoriteTags.Contains(tag))
                                        {
                                            wRank.FavoriteTags.Add(tag);
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (ownsDbCtrl)
                    {
                        dbCtrl.Dispose();
                    }
                }

                // 人気タグをApiXML.dbのタグロックで補完する
                if (!ComplementLockedTags(targetList))
                {
                    return false;
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
        /// 人気タグをApiXML.dbのタグロックで補完する（件数上限なし）。
        /// 不足分は自前で確保（UpdateTumbInfo）してから取得するため、他オプションの処理順に依存しない。
        /// タグロックなし・行なし・取得失敗の動画は補完不可として素通りする。
        /// </summary>
        /// <param name="targetList">取得対象リスト</param>
        /// <returns></returns>
        private bool ComplementLockedTags(List<Ranking> targetList)
        {
            if (targetList.Count == 0)
            {
                return true;
            }

            // 注入されたインスタンスは呼び出し側の所有物のため開閉・破棄しない。自前生成分のみ破棄する
            bool ownsApi = _apiOverride == null;
            var api = _apiOverride ?? new NicoApi();
            try
            {
                if (!api.OpenDB())
                {
                    StatusLog.WriteLine("DB/ApiXML.dbを開けませんでした。");
                    return false;
                }
                // 不足分を確保する（無ければ取得。UserInfoReaderと重複してもキャッシュヒットのためAPI実打撃なし）
                // isLocalOnly時は外部取得を行わず、キャッシュ参照のみで補完する（中間集計用）
                if (!IsLocalOnly && !api.UpdateTumbInfo(targetList, EndTime))
                {
                    StatusLog.WriteLine("タグロックの補完中にエラーが発生しました:UpdateTumbInfo");
                    return false;
                }
                foreach (var wRank in targetList)
                {
                    try
                    {
                        foreach (var tag in api.GetLockedTags(wRank.ID))
                        {
                            if (!wRank.FavoriteTags.Contains(tag))
                            {
                                wRank.FavoriteTags.Add(tag);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrLog.GetInstance().Write(ex);
                    }
                }
            }
            finally
            {
                if (ownsApi)
                {
                    api.Dispose();
                }
            }
            return true;
        }

    }
}
