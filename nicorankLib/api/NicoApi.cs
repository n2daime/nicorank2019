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

        /// <summary>
        /// 並列取得のスレッド数指定。設定時は Config（nicorank.xml）より優先する。
        /// oldlogはconfig.json側の設定をここへ渡すことで、nicorank.xmlへの依存をなくす（Issue #40）。
        /// </summary>
        public int? ThreadMaxOverride { get; set; }

        /// <summary>
        /// 並列数の決定。指定値→Config→既定値4の順に解決する。Config不在でも例外にしない。
        /// 指定値がある場合はConfigを読まないため、nicorank.xml不在のエラーログも出ない。
        /// </summary>
        /// <returns></returns>
        protected virtual int ResolveThreadMax()
        {
            if (ThreadMaxOverride.HasValue && ThreadMaxOverride.Value > 0)
            {
                return ThreadMaxOverride.Value;
            }
            //nicorank.xml がない場所（oldlog等）でも動くよう、設定取得に失敗したら既定値を使う
            try
            {
                int configValue = Config.GetInstance().ThreadMax;
                if (configValue > 0)
                {
                    return configValue;
                }
            }
            catch { }
            return 4;
        }

        public virtual bool OpenDB()
        {
            return OpenDB(DATA_SROURCE);
        }

        /// <summary>
        /// 指定パスのDBを開く。oldlogが週刊フォルダへ ApiXML.db を作る場合など、既定以外の場所を使うときに利用する。
        /// テーブルがなければ呼び出し側で ApiXmlCacheImporter.EnsureNicovideoThumbTable を呼んで確保する。
        /// </summary>
        /// <param name="dataSource">DBファイルパス</param>
        /// <returns></returns>
        public virtual bool OpenDB(string dataSource)
        {
            dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl();
            return dbCtrl.Open(dataSource);
        }

        public virtual void CloseDB()
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
        public virtual bool UpdateTumbInfo(IReadOnlyList<Ranking> rankingList, DateTime? targetDate)
        {
            if (dbCtrl?.IsOpen != true)
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
                        int threadMax = ResolveThreadMax();
                        try
                        {

                            aCmd.Transaction = (SqliteTransaction)aCmd.Connection.BeginTransaction();
                            StatusLog.WriteLine($"未取得・古いデータをNicoAPIから取得・更新します。 取得対象{updateList.Count}件");
                            var lockObject = new object();
                            var thumbinfoList = new List<ThumbinfoBase>();
                            int GetCounter = 0;

                            //進捗表示の方針：生きたコンソールでは同じ行を上書きし、リダイレクト時（ログファイル等）は一定間隔で1行ずつ出す。
                            //\b方式はファイル側に制御文字のゴミを残し桁管理も要るため、行頭復帰\r＋空白埋めに統一する（Issue #40）。
                            //カーソル表示の直接操作はやめる。ライブラリはWinFormからも呼ばれるため、表示方法は呼び出し側に任せる。
                            bool redirectOutput = false;
                            try { redirectOutput = System.Console.IsOutputRedirected; } catch { }
                            int progressStep = redirectOutput ? 500 : 5;
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
                                    if (thmbInfo != null)
                                    {
                                        thumbinfoList.Add(thmbInfo);
                                    }
                                    GetCounter++;
                                    if (GetCounter % progressStep == 0 || GetCounter == updateList.Count)
                                    {
                                        //\rで行頭に戻して書き直す。短くなった場合の消し残し防止に空白で埋める。
                                        //リダイレクト時は上書きが効かないため、間引きした件数行だけ出す（全件ログにしない）。
                                        string progress = $"取得中 {GetCounter}/{updateList.Count}件";
                                        if (redirectOutput)
                                        {
                                            StatusLog.WriteLine(progress);
                                        }
                                        else
                                        {
                                            StatusLog.Write("\r" + progress.PadRight(40));
                                        }
                                    }
                                }

                             });

                            // DBに登録する
                            // 一度古いデータを削除する
                            aCmd.CommandText =
                                        @"DELETE From NicovideoThumb
                                            WHERE ID = @ID";
                            aCmd.Parameters.Clear();
                            foreach (var thmbInfo in thumbinfoList)
                            {
                                aCmd.Parameters.Clear();
                                aCmd.Parameters.AddWithValue("@ID", thmbInfo.Ranking.ID);
                                aCmd.ExecuteNonQuery();
                            }

                            aCmd.CommandText =
                                      @"INSERT INTO NicovideoThumb(取得日,ID,Status,XML)
                                            VALUES(@取得日,@ID,@Status,@XML)";
                            aCmd.Parameters.Clear();
                            var todayStr = DateConvert.Time2String(DateTime.Today, false);
                            foreach (var thmbInfo in thumbinfoList)
                            {
                                aCmd.Parameters.Clear();
                                aCmd.Parameters.AddWithValue("@取得日", todayStr);
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
        /// NicovideoThumb表がなければ作る（配布DB作成時用）。OpenDBの後に呼ぶ。
        /// </summary>
        /// <returns></returns>
        public virtual bool EnsureCacheTable()
        {
            if (dbCtrl?.IsOpen != true)
            {
                return false;
            }
            ApiXmlCacheImporter.EnsureNicovideoThumbTable(dbCtrl);
            return true;
        }

        /// <summary>
        /// タグロックされているタグ一覧を取得する（取得専責。ネットワークアクセスなし）
        /// FavoriteTagReader の補完用。最新取得日の行を参照し、lock="1" のタグを定義順に返す。
        /// 行なし・Status非ok・パース失敗・タグロックなしの場合は空リストを返す（補完不可）。
        /// </summary>
        /// <param name="id">動画ID</param>
        /// <returns>lock="1" のタグ（定義順）</returns>
        public virtual List<string> GetLockedTags(string id)
        {
            var lockedTags = new List<string>();
            try
            {
                if (dbCtrl?.IsOpen != true)
                {
                    return lockedTags;
                }
                using (var aCmd = dbCtrl.Connection.CreateCommand())
                {
                    // 同一IDが複数取得日で存在する場合は最新の行を使う
                    aCmd.CommandText =
                        @" SELECT XML FROM NicovideoThumb
                           Where ID = @ID ORDER BY 取得日 DESC LIMIT 1";
                    aCmd.Parameters.AddWithValue("@ID", id);
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return lockedTags;
                        }
                        ThumbinfoBase thumbinfo;
                        try
                        {
                            thumbinfo = XmlSerializerUtil.Deserialize<ThumbinfoBase>(reader["XML"].ToString());
                        }
                        catch
                        {
                            return lockedTags;
                        }
                        if (thumbinfo == null || thumbinfo.Status != "ok" || thumbinfo.Thumb?.Tags?.Tag == null)
                        {
                            return lockedTags;
                        }
                        foreach (var tag in thumbinfo.Thumb.Tags.Tag)
                        {
                            if (tag.Lock == "1" && !string.IsNullOrWhiteSpace(tag.Text))
                            {
                                lockedTags.Add(tag.Text.Trim());
                            }
                        }
                    }
                }
            }
            catch
            {
                return lockedTags;
            }
            return lockedTags;
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
        /// ユーザー情報と再生時間を補完する。
        /// 表示用の補完だけを行い、集計対象の存否は判断しない。
        /// 取得失敗・Status非ok・行なしの場合も isDelete を立てず、空欄・既定値のまま残して true で返す。
        /// なぜ除外しないか: ApiXML.db は表示用キャッシュであり、取得タイミング次第で結果が変わる削除判定を
        /// 順位・ポイントに影響させないため（Issue #40）。除外の判断はスナップショット差分・Sabun・Hidden側に任せる。
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
                            //同一IDが複数取得日で存在する場合は最新の行を使う（GetLockedTagsと同一。行選択の不定をなくすため）
                            aCmd.CommandText =
                                @" SELECT XML FROM NicovideoThumb
                                Where ID = @ID ORDER BY 取得日 DESC LIMIT 1";
                            aCmd.Parameters.AddWithValue("@ID", ranking.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {

                                    ThumbinfoBase thumbinfo = GetTumbInfo(ranking, ranking.ID, reader["XML"].ToString());
                                    if (thumbinfo == null || thumbinfo.Status != "ok")
                                    {
                                        //削除・非公開・取得失敗の場合も除外せず、再生時間だけ既定値にして残す
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
                                //行なしの場合も除外せず、そのまま残す（表示欠落は呼び出し側・出力側で扱う）
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
        /// 動画情報で補完できるものは補完する。
        /// 表示用の補完だけを行い、集計対象の存否は判断しない。
        /// 取得失敗・Status非ok・行なしの場合も isDelete を立てず、空欄・既定値のまま残して true で返す。
        /// なぜ除外しないかは GetUserInfo と同一（Issue #40）。
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
                            //同一IDが複数取得日で存在する場合は最新の行を使う（GetLockedTagsと同一。行選択の不定をなくすため）
                            aCmd.CommandText =
                                @" SELECT XML FROM NicovideoThumb
                                Where ID = @ID ORDER BY 取得日 DESC LIMIT 1";
                            aCmd.Parameters.AddWithValue("@ID", ranking.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {

                                    ThumbinfoBase thumbinfo = GetTumbInfo(ranking, ranking.ID, reader["XML"].ToString());
                                    if (thumbinfo == null || thumbinfo.Status != "ok")
                                    {
                                        //削除・非公開・取得失敗の場合も除外せず、再生時間だけ既定値にして残す
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
                                //行なしの場合も除外せず、そのまま残す（SP側は予備情報で補う。タグ検索は数字なし除外のみ残す）
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
