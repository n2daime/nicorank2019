using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Linq;

namespace nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// タグ検索集計の基準日DBなし用。集計日DBの累積値をそのまま集計値にする（差分なし）。
    /// SnapShotSabunReaderを使わない経路のためSP側には影響しない。
    /// </summary>
    public class TagRankTotalReader : BasicOptionBase, IDisposable
    {
        public DateTime AnalyzeTime { get; protected set; }

        public string AnalyzeDB;

        ISQLiteCtrl dbCtrlAnalyze;

        public TagRankTotalReader(string analyzeDB, ISQLiteCtrl dbCtrl = null)
        {
            AnalyzeDB = analyzeDB;
            dbCtrlAnalyze = dbCtrl ?? new SQLiteCtrl();
        }

        /// <summary>
        /// 開く
        /// </summary>
        public bool Open()
        {
            if (dbCtrlAnalyze.Open(AnalyzeDB))
            {
                this.AnalyzeTime = GetTargetTime(dbCtrlAnalyze);
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

        protected bool GetMovieData(SqliteCommand aCmd, string ID, out long countPlay, out long countComment, out long countMylist, out long countLike)
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
        /// 累積値をそのまま集計値にする（差分なし）
        /// </summary>
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            try
            {
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
                var movieInfoReader = new MovieInfoReader(this.AnalyzeTime);
                if (!movieInfoReader.AnalyzeRank(ref rankingList))
                {
                    return false;
                }

                // 差分なしのため累積データを集計値として採用する
                foreach (var wRank in rankingList)
                {
                    wRank.CountPlay = wRank.CountPlayTotal;
                    wRank.CountComment = wRank.CountCommentTotal;
                    wRank.CountMyList = wRank.CountMyListTotal;
                    wRank.CountLike = wRank.CountLikeTotal;
                    wRank.PointCalcReset();
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    dbCtrlAnalyze.Close();
                }

                dbCtrlAnalyze = null;

                disposedValue = true;
            }
        }

        ~TagRankTotalReader()
        {
            Dispose(false);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
