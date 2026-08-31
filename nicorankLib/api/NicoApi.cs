using Microsoft.VisualBasic;
using nicorankLib.Analyze.model;
using nicorankLib.api.model;
using nicorankLib.Common;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nicorankLib.api
{
    public class NicoApi : IDisposable
    {
        protected bool LocalXml;
        protected ISQLiteCtrl dbCtrl;

        const string APIURL = @"https://ext.nicovideo.jp/api/getthumbinfo/";
        const string CONVERTID_API_URL = @"https://api.ce.nicovideo.jp/nicoapi/v1/video.info?v=";

        const string DATA_SROURCE = @"DB/ApiXML.db";

        public NicoApi(ISQLiteCtrl dbCtrl = null)
        {
            LocalXml = UIConfig.GetInstance().LocalXml;
            _dbCtrlOverride = dbCtrl;
            this.dbCtrl = null;
        }

        protected ISQLiteCtrl _dbCtrlOverride;

        public bool OpenDB()
        {
            dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl();
            return dbCtrl.Open(DATA_SROURCE);
        }

        public void CloseDB()
        {
            dbCtrl?.Close();
            dbCtrl = null;
        }

        /// <summary>
        /// ニコAPIを取得する。
        /// ローカルになし→必ず更新する
        /// ローカルにあり→指定日より新しければそのまま使う。古い場合は更新する。NULLは更新しない
        /// </summary>
        /// <param name="rankingList"></param>
        /// <param name="targetDate">データがなし→更新、古いデータであれば更新する</param>
        /// <returns></returns>
        public bool UpdateTumbInfo(IReadOnlyList<Ranking> rankingList, DateTime? targetDate)
        {
            if (!dbCtrl.IsOpen)
            {
                return false;
            }
            using (var aCmd = dbCtrl.Connection.CreateCommand())
            {
                try
                {
                    // 取得日は0:00相当になるので、更新チェックDateも0:00にする
                    var targetDateBase = ((DateTime)targetDate).Date;

                    //更新するべきデータのリスト
                    var updateList = new List<Ranking>(rankingList.Count);
                    foreach (var wRank in rankingList)
                    {
                        //ローカルにあるかどうかチェックする
                        aCmd.CommandText =
                            @" SELECT MAX(取得日) as 取得日 FROM NicovideoThumb
                               Where ID = @ID";

                        aCmd.Parameters.Clear();
                        aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                object strGetDate = reader["取得日"];
                                if (strGetDate == DBNull.Value)
                                {// データなし
                                    updateList.Add(wRank);
                                }
                                else
                                {// データあり
                                    if (targetDate != null)
                                    {// 指定日あり
                                        {
                                            var getDate = DateConvert.String2Time(strGetDate.ToString(), false);
                                            // 取得日が指定日より古ければ更新する
                                            if (getDate < targetDateBase)
                                            {
                                                updateList.Add(wRank);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (updateList.Count > 0)
                    {//マルチスレッドで取得する
                        int threadMax = Config.GetInstance().ThreadMax;
                        try
                        {

                            aCmd.Transaction = (SqliteTransaction)aCmd.Connection.BeginTransaction();
                            StatusLog.WriteLine($"未取得・古いデータをNicoAPIから取得・更新します。 取得対象{updateList.Count}件");
                            var lockObject = new object();
                            var thumbinfoList = new List<ThumbinfoBase>();
                            int GetCounter = 0;
                            int beforeLen = 1;

                            System.Console.CursorVisible = false;
                            Random rnd = new Random();
                            ManualResetEventSlim resumeEvent = new ManualResetEventSlim(true);

                            Func<int, int> calculateDelayMax = (int retryCount) =>
                            {
                                // 1 → 1.5 → 2.25 → 3.38 → 5.06 → 7.59...とリトライ間隔が変更
                                return (int)( Math.Pow(1.5, retryCount) * 1000.0 );
                            };
                            Random random = new Random();

                            Parallel.ForEach(updateList, new ParallelOptions() { MaxDegreeOfParallelism = threadMax }, (wRank) =>
                            {
                                System.Threading.Thread.Sleep(rnd.Next(50,200));
                                var thmbInfo = GetTumbInfo(wRank, wRank.ID);
                                if (thmbInfo == null)
                                {
                                    //取得失敗
                                    lock (lockObject)
                                    {// ここからシングルスレッド
                                        do
                                        {
                                            thmbInfo = GetTumbInfo(wRank, wRank.ID);
                                            if (thmbInfo != null)
                                            {
                                                //他スレッドで回復済みなら↓回復待ちをやらずにそのまま止める
                                                break;
                                            }
                                            else
                                            {
                                                resumeEvent.Reset(); // 全スレッドを止める

                                                StatusLog.WriteLine($"\nNicoAPIに連続アクセスでエラー発生しているため回復を待ちます");
                                                const int MaxRetryCount = 100;
                                                for (int retry = 0; retry < MaxRetryCount; retry++)
                                                {
                                                    int delay = calculateDelayMax(retry);
                                                    System.Threading.Thread.Sleep(delay);
                                                    thmbInfo = GetTumbInfo(wRank, wRank.ID);
                                                    if (thmbInfo == null)
                                                    {
                                                        StatusLog.WriteLine($"接続失敗。 {delay / 1000.0:F1} 秒後にRetry..");
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        StatusLog.WriteLine($"接続成功。再開します");
                                                        break;
                                                    }
                                                }
                                                resumeEvent.Set(); // 全スレッド再開
                                            }
                                        } while (false) ;
                                    }
                                }
                                lock (lockObject)
                                {
                                    //StatusLog.Write(".");
                                    if (GetCounter % 5 == 0 && GetCounter != 0)
                                    {
                                        var outNum = $"{GetCounter}";
                                        StatusLog.Write(new string('\b', beforeLen));
                                        StatusLog.Write(outNum);
                                        beforeLen = outNum.Length;

                                    }
                                    if (thmbInfo != null)
                                    {
                                        thumbinfoList.Add(thmbInfo);
                                    }
                                    GetCounter++;
                                }
                                
                             });
                            if(GetCounter > 0)
                            {
                                var outNum = $"{GetCounter}";
                                StatusLog.Write(new string('\b', beforeLen));
                                StatusLog.Write(outNum);
                            }

                            // DBに登録する
                            // 一度古いデータを削除する
                            aCmd.CommandText =
                                        @"DELETE From NicovideoThumb
                                            WHERE ID = @ID";
                            aCmd.Parameters.Clear();
                            foreach (var thmbInfo in thumbinfoList)
                            {
                                aCmd.Parameters.AddWithValue("@ID", thmbInfo.Ranking.ID);
                                aCmd.ExecuteNonQuery();
                            }

                            aCmd.CommandText =
                                      @"INSERT INTO NicovideoThumb(取得日,ID,Status,XML)
                                            VALUES(@取得日,@ID,@Status,@XML)";
                            aCmd.Parameters.Clear();
                            aCmd.Parameters.AddWithValue("@取得日", DateConvert.Time2String(DateTime.Today, false));
                            foreach (var thmbInfo in thumbinfoList)
                            {
                                aCmd.Parameters.AddWithValue("@ID", thmbInfo.Ranking.ID);
                                aCmd.Parameters.AddWithValue("@Status", thmbInfo.Status == "ok" ? 1 : 0);
                                aCmd.Parameters.AddWithValue("@XML", thmbInfo.XML);
                                aCmd.ExecuteNonQuery();
                            }

                            StatusLog.WriteLine("NicoAPIから情報を取得終了");
                            aCmd.Transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            aCmd.Transaction.Rollback();
                            ErrLog.GetInstance().Write(ex);
                            return false;
                        }
                        System.Console.CursorVisible = true;

                    }
                }
                catch (Exception ex)
                {
                    ErrLog.GetInstance().Write(ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// TumbInfoAPIから情報を取得する
        /// </summary>
        /// <param name="ranking"></param>
        /// <returns></returns>
        protected ThumbinfoBase GetTumbInfo(Ranking ranking, string id, string strXml = "")
        {
            try
            {
                if (string.IsNullOrEmpty(strXml))
                {
                    string url = $"{APIURL}{id}";
                    if (!InternetUtil.TxtDownLoad(url, out strXml))
                    {
                        return null;
                    }
                }
                var returnObj = XmlSerializerUtil.Deserialize<ThumbinfoBase>(strXml);
                returnObj.XML = strXml;
                returnObj.Ranking = ranking;
                return returnObj;
            }
            catch (Exception)
            {
                return null;
            }
        }


        /// <summary>
        /// VideoResponseAPIから情報を取得する
        /// </summary>
        /// <param name="ranking"></param>
        /// <returns></returns>
        protected VideoResponse GetVideoResponse(Ranking ranking)
        {
            try
            {
                string url = $"{CONVERTID_API_URL}{ranking.ID}";
                if (!InternetUtil.TxtDownLoad(url, out string strXml))
                {
                    return null;
                }
                var returnObj = XmlSerializerUtil.Deserialize<VideoResponse>(strXml);
                returnObj.Ranking = ranking;
                return returnObj;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return null;
            }
        }


        /// <summary>
        /// ユーザー情報と再生時間を補完する
        /// </summary>
        /// <param name="ranking"></param>
        /// <returns></returns>
        public bool GetUserInfo(IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                foreach (var ranking in rankingList)
                {
                    if (string.IsNullOrWhiteSpace(ranking.UserID) || string.IsNullOrWhiteSpace(ranking.PlayTime))
                    {
                        if (!dbCtrl.IsOpen)
                        {
                            return false;
                        }
                        using (var aCmd = dbCtrl.Connection.CreateCommand())
                        {
                            //ローカルにあるかどうかチェックする
                            aCmd.CommandText =
                                @" SELECT XML FROM NicovideoThumb
                               Where ID = @ID";
                            aCmd.Parameters.AddWithValue("@ID", ranking.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {

                                    ThumbinfoBase thumbinfo = GetTumbInfo(ranking, ranking.ID, reader["XML"].ToString());
                                    if (thumbinfo == null || thumbinfo.Status != "ok")
                                    {
                                        ranking.isDelete = true;
                                        ranking.SetPlayTime("??:??");
                                    }
                                    else
                                    {
                                        ranking.UserID = thumbinfo.Thumb.GetUserID();
                                        ranking.UserName = thumbinfo.Thumb.GetUserName();
                                        ranking.UserImageURL = thumbinfo.Thumb.GetUserIconUrl();
                                        ranking.SetPlayTime(thumbinfo.Thumb.Length);

                                        if (string.IsNullOrEmpty(ranking.Category))
                                        {
                                            ranking.Category = thumbinfo.Thumb.Genre;
                                        }
                                    }
                                }
                                else
                                {
                                    ranking.isDelete = true;
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

        /// <summary>
        /// 動画情報で補完できるものは補完する
        /// </summary>
        /// <param name="ranking"></param>
        /// <returns></returns>
        public bool GetMovieInfo(IReadOnlyList<Ranking> rankingList,bool isCountGet,bool isUpdateDate = false)
        {
            try
            {
                foreach (var ranking in rankingList)
                {
                    if (string.IsNullOrWhiteSpace(ranking.UserID) || string.IsNullOrWhiteSpace(ranking.PlayTime))
                    {
                        if (!dbCtrl.IsOpen)
                        {
                            return false;
                        }
                        using (var aCmd = dbCtrl.Connection.CreateCommand())
                        {
                            //ローカルにあるかどうかチェックする
                            aCmd.CommandText =
                                @" SELECT XML FROM NicovideoThumb
                               Where ID = @ID";
                            aCmd.Parameters.AddWithValue("@ID", ranking.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {

                                    ThumbinfoBase thumbinfo = GetTumbInfo(ranking, ranking.ID, reader["XML"].ToString());
                                    if (thumbinfo == null || thumbinfo.Status != "ok")
                                    {
                                        ranking.isDelete = true;
                                        ranking.SetPlayTime("??:??");
                                    }
                                    else
                                    {
                                        ranking.Title = thumbinfo.Thumb.Title;
                                        ranking.ThumbnailURL = thumbinfo.Thumb.Thumbnail_url;
                                        ranking.SetPlayTime(thumbinfo.Thumb.Length);
                                        ranking.Category = thumbinfo.Thumb.Genre;
                                        ranking.UserID = thumbinfo.Thumb.GetUserID();
                                        ranking.UserName = thumbinfo.Thumb.GetUserName();
                                        ranking.UserImageURL = thumbinfo.Thumb.GetUserIconUrl();

                                        if(isCountGet)
                                        {
                                            ranking.CountPlayTotal = long.Parse( thumbinfo.Thumb.View_counter);
                                            ranking.CountCommentTotal = long.Parse( thumbinfo.Thumb.Comment_num);
                                            ranking.CountMyListTotal = long.Parse( thumbinfo.Thumb.Mylist_counter);
                                        }
                                        if(isUpdateDate)
                                        {
                                            ranking.Date = DateTime.ParseExact(thumbinfo.Thumb.First_retrieve, "yyyy-MM-ddTHH:mm:sszzz", null);
                                        }
                                    }
                                }
                                else
                                {
                                    ranking.isDelete = true;
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

        //protected bool convertMovieID( string srcID , out string dstID)
        //{
        //    if (!srcID.StartsWith("so"))
        //    {//チャンネル動画以外の場合、変換は不要
        //        dstID = srcID;
        //        return true;
        //    }
        //    //DBにデータが存在するかチェックする
        //    using (var aCmd = dbCtrl.Connection.CreateCommand())
        //    {
        //        aCmd.CommandText =
        //            @"SELECT ThreadID FROM IDConvert
        //              WHERE ID = @ID";
        //        aCmd.Parameters.AddWithValue("@ID", srcID);

        //        using (var reader = aCmd.ExecuteReader())
        //        {
        //            if( reader.Read() )
        //            {// 変換データあり
        //                dstID = reader["ThreadID"].ToString();
        //                if( dstID == "DELETE")
        //                {
        //                    return false;
        //                }
        //                return true;
        //            }
        //        }
        //        //変換データ無し
        //        string url = $"{CONVERTID_API_URL}{srcID}";
        //        if (!InternetUtil.TxtDownLoad(url, out string strXml))
        //        {
        //            dstID = srcID;
        //            return false;
        //        }
        //        var responseObj = XmlSerializerUtil.Deserialize<VideoResponse>(strXml);
        //        if(responseObj.Status != "ok")
        //        {
        //            // 不正なXMLファイル or 情報がもうない(NOT_FOUND)
        //        }
        //        dstID = responseObj.Video_info.Thread.Id;

        //        aCmd.CommandText =
        //        @"INSERT INTO IDConvert(ID,ThreadID)
        //          VALUES(@ID,@ThreadID)";

        //        aCmd.Parameters.AddWithValue("@ID", srcID);
        //        aCmd.Parameters.AddWithValue("@ThreadID", dstID);
        //        aCmd.ExecuteNonQuery();

        //        aCmd.Transaction.Commit();

        //    }
        //}
        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    CloseDB();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。
                dbCtrl = null;
                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        ~NicoApi()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
