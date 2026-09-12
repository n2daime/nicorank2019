using Microsoft.Data.Sqlite;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// タグ検索のv2最新値モード（基準日DBあり）用。集計値はライブ検索の最新値、基準値は基準日DBから取り差分計算する。
    /// 集計日DBは使わないため、SnapshotDBの取得待ちなしで差分集計できる。
    /// 投稿日による新着救済（基準-7日）・MovieInfoへの基準日渡しはSnapShotSabunReaderと同一の考え方。
    /// 数値の出所はTagRankAnalyze.LiveCounters（工場が同一インスタンスを両者に渡す共有参照）。
    /// </summary>
    public class TagRankLiveSabunReader : BasicOptionBase, IDisposable
    {
        public DateTime AnalyzeTime { get; protected set; }
        public DateTime BaseTime { get; protected set; }

        public string BaseDB;

        protected readonly TagRankAnalyze Input;

        ISQLiteCtrl dbCtrlBase;

        public TagRankLiveSabunReader(TagRankAnalyze input, DateTime analyzeTime, string baseDB, ISQLiteCtrl dbCtrl = null)
        {
            Input = input;
            AnalyzeTime = analyzeTime;
            BaseDB = baseDB;
            dbCtrlBase = dbCtrl ?? new SQLiteCtrl();
        }

        /// <summary>
        /// 開く（基準日DBのみ開く。集計値はライブ検索から取るため集計日DBは不要）
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            if (Input == null || Input.Query == null)
            {
                return false;
            }
            if (dbCtrlBase.Open(BaseDB))
            {
                this.BaseTime = GetBaseTime(dbCtrlBase);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 基準日DBのDBVersionから集計日（＝BaseTime）を取得する
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <returns></returns>
        protected DateTime GetBaseTime(ISQLiteCtrl dbCtrl)
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
            //エラーは想定してない（手抜き。SnapShotSabunReaderの踏襲）
            //なおこのフォールバックは実行日（TargetDay＝Today）より未来になり得るため、新着救済の基準-7日が効きにくくなる点に注意する
            return DateTime.Now;
        }

        /// <summary>
        /// 基準日DBのRankingから4数値を読む。存在しなければfalse（新着救済の判定材料）
        /// </summary>
        /// <param name="aCmd"></param>
        /// <param name="ID"></param>
        /// <param name="countPlay"></param>
        /// <param name="countComment"></param>
        /// <param name="countMylist"></param>
        /// <param name="countLike"></param>
        /// <returns></returns>
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
        /// 差分値を集計する
        /// </summary>
        /// <remarks>
        /// 事前条件: RankingAnalyzeがInput→Optionの順に実行するため、Input成功後のLiveCountersが確定していること。
        /// Input失敗時は本メソッドに到達しない。単独呼び出しでLiveCountersが空なら全件isDeleteの空成功になるが、0件検索の空成功を維持するため動作は変えない。
        /// </remarks>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            try
            {
                if (Input == null)
                {
                    return false;
                }

                //集計値のデータ（ライブ検索の最新値）を取得する
                TagRankLiveTotalReader.ApplyLiveTotals(Input, rankingList);
                //データが取得できたものだけ抽出
                rankingList = rankingList.Where(wRank => !wRank.isDelete).ToList();

                // 動画情報を取得できていないので、取得する
                var movieInfoReader = new MovieInfoReader(this.BaseTime); //集計日を基準にすると仮集計のたびにXML取得し直しになるので、基準日より古くなければOKとする
                if (!movieInfoReader.AnalyzeRank(ref rankingList))
                {
                    return false;
                }

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
            }
            catch (Exception ex)
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
                    dbCtrlBase.Close();
                }

                dbCtrlBase = null;

                disposedValue = true;
            }
        }

        ~TagRankLiveSabunReader()
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
