using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Linq;


namespace nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// スナップショットAPIで差分を計算するクラス
    /// </summary>
    public class SnapShotSabunReader : BasicOptionBase
    {
        public DateTime AnalyzeTime { get; protected set; }
        public DateTime BaseTime { get; protected set; }

        public String AnalyzeDB;
        public String BaseDB;

        ISQLiteCtrl dbCtrlAnalyze;
        ISQLiteCtrl dbCtrlBase;

        //注入された接続は呼び出し側の所有物のため破棄しない。自前生成分のみ破棄する（SpMovieInfoFallbackと同一の流儀）。
        //なぜ所有権を追うか: コンストラクタはテスト差し替えのために注入を受け付けるが、読み手の接続を
        //閉じてしまうと呼び出し側の所有権を壊すため。受け入れ条件「注入接続は閉じないこと」の根拠（Issue #44）。
        protected bool _ownsDbCtrl;

        //動画情報が取れなかった場合の予備補完（案B・Issue #40）。テストで差し替え可能にするため注入可。
        protected SpMovieInfoFallback _fallback;

        public SnapShotSabunReader(string analyzeDB, string baseDB, ISQLiteCtrl dbCtrl = null, SpMovieInfoFallback fallback = null)
        {
            AnalyzeDB = analyzeDB;
            BaseDB = baseDB;

            _ownsDbCtrl = dbCtrl == null;
            dbCtrlAnalyze = dbCtrl ?? new SQLiteCtrl();
            dbCtrlBase = dbCtrl ?? new SQLiteCtrl();
            _fallback = fallback ?? new SpMovieInfoFallback();
        }

        /// <summary>
        /// 開く
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            if (dbCtrlAnalyze.Open(AnalyzeDB) && dbCtrlBase.Open(BaseDB))
            {
                this.AnalyzeTime = GetTargetTime(dbCtrlAnalyze);
                this.BaseTime = GetTargetTime(dbCtrlBase);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 集計日を取得する
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <returns></returns>
        protected DateTime GetTargetTime(ISQLiteCtrl dbCtrl)
        {
            using (var aCmd = dbCtrl.Connection.CreateCommand())
            {
                aCmd.CommandText = @"SELECT 集計日 FROM DBVersion";
                using (var reader = aCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return DateConvert.String2Time(reader["集計日"].ToString(), false);
                    }
                }
            }
            //エラーは想定してない（手抜き）
            return DateTime.Now;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <returns></returns>
        protected bool GetMovieData(SqliteCommand aCmd, string ID, out long countPlay, out long countComment, out long countMylist,out long countLike)
        {
            // Microsoft.Data.Sqlite は同名パラメータの重複追加を許さないため、再利用前にクリアする
            aCmd.Parameters.Clear();

            aCmd.CommandText = @"SELECT ID,再生数,コメント数,マイリスト数,いいね数 FROM Ranking Where ID = @ID";
            aCmd.Parameters.AddWithValue("@ID", ID);
            using (var reader = aCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    countPlay = Convert.ToInt64(reader["再生数"]);
                    countComment = Convert.ToInt64(reader["コメント数"]);
                    countMylist = Convert.ToInt64(reader["マイリスト数"]);
                    countLike = Convert.ToInt64(reader["いいね数"]);
                    return true;
                }
                else
                {
                    countPlay = 0;
                    countComment = 0;
                    countMylist = 0;
                    countLike = 0;
                    return false;
                }
            }
        }


        /// <summary>
        /// 差分値を集計する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            try
            {

                //基準値のデータを取得する
                using (var aCmd = this.dbCtrlAnalyze.Connection.CreateCommand())
                {
                    foreach (var wRank in rankingList)
                    {
                        if (!GetMovieData(aCmd, wRank.ID, out wRank.CountPlayTotal, out wRank.CountCommentTotal, out wRank.CountMyListTotal, out wRank.CountLikeTotal))
                        {// データが存在しなかった
                            wRank.isDelete = true;
                        }
                    }
                }
                //データが取得できたものだけ抽出
                rankingList = rankingList.Where(wRank => !wRank.isDelete).ToList();

                // 動画情報を取得できていないので、取得する
                var movieInfoReader = new MovieInfoReader(this.BaseTime);　//集計日を基準にすると仮集計のたびにXML取得し直しになるので、基準日より古くなければOKとする
                if ( !movieInfoReader.AnalyzeRank(ref rankingList) )
                {
                    return false;
                }

                //動画情報が取れなかった分は予備情報で補う（案B）。取れなくても除外しない。
                //なぜここか: 投稿日は直後の新着救済判定に使うため、判定前に埋める必要があるから。
                _fallback.ComplementMovieInfo(rankingList, this.BaseTime, this.AnalyzeTime);

                StatusLog.WriteLine("基準日からの差分値を計算しています...");

                //差分データが無くても許容する投稿日の基準を計算する
                //１週間程度はOKとする
                DateTime targetDate = BaseTime.Date.AddDays(-7);//基準が7/1の場合、6/30投稿のデータは7/1のランキングに乗っていない可能性があるため


                using (var aCmd = this.dbCtrlBase.Connection.CreateCommand())
                {
                    foreach (var wRank in rankingList)
                    {
                        if (GetMovieData(aCmd, wRank.ID, out var CountPlay, out var CountComment, out var CountMyList, out var CountLike))
                        {// 差分あり
                            wRank.CountPlay = wRank.CountPlayTotal - CountPlay;
                            wRank.CountComment = wRank.CountCommentTotal - CountComment;
                            wRank.CountMyList = wRank.CountMyListTotal - CountMyList;
                            wRank.CountLike = wRank.CountLikeTotal - CountLike;
                            wRank.PointCalcReset();
                        }
                        else
                        {// 差分なし
                            if (wRank.Date < targetDate)
                            {//基準日時より前に投稿された動画なのに差分データがない
                                wRank.isDelete = true;//対象外にする
                            }
                            else
                            {//新着動画なのでそのまま累積データを集計値として採用する
                                wRank.CountPlay = wRank.CountPlayTotal;
                                wRank.CountComment = wRank.CountCommentTotal;
                                wRank.CountMyList = wRank.CountMyListTotal;
                                wRank.CountLike = wRank.CountLikeTotal;
                                wRank.PointCalcReset();
                            }
                        }
                    }
                }
                //データが取得できたものだけ抽出
                rankingList = rankingList.Where(wRank => !wRank.isDelete).ToList();
                //動画情報が最後まで埋まらなかった分は目印を付けて残す（除外しない。Issue #40）
                foreach (var wRank in rankingList)
                {
                    wRank.ApplyDeletedTitleMarker();
                }
            }
            catch(Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
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
                    //自前生成の接続だけ閉じる。注入された接続は呼び出し側の所有物のため触らない。
                    if (_ownsDbCtrl)
                    {
                        dbCtrlAnalyze.Close();
                        dbCtrlBase.Close();
                    }
                    //予備補完が自前で開いた接続も閉じる（注入接続は先方が閉じるため触らない）
                    _fallback?.Close();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。
                dbCtrlAnalyze = null;
                dbCtrlBase = null;
                _fallback = null;

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        ~SnapShotSabunReader()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        // 基底 BasicOptionBase の仮想 Dispose を上書きする。なぜ override が必要か:
        // 明示的実装（void IDisposable.Dispose）のままだと、基底参照からの呼び出しでは
        // 基底の空実装が呼ばれて接続が残るため。呼び出し側はリストとして一括破棄するから。
        public override void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
