using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;

namespace nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// SP集計で動画情報（ApiXML）が取れなかった場合の予備補完（案B・Issue #40）。
    /// 新しく外部取得はせず、すでにあるDBから埋める。取れなくても除外しない。
    /// なぜ予備が必要か: SPは動画IDの一覧から出発し、投稿日・タイトルを動画情報に頼っている。
    /// 投稿日は差分後の新着救済（基準-7日）の判断に使うため、空のままだと誤判定になるから。
    /// 優先順位は ApiXML（先行）→ LastResultのタイトル → LogOfficialの期間内初見日 → 空のまま残す。
    /// ジャンル（カテゴリ）が空のままなのは許容する。影響が大きければ案A（Issue #41）で検討する。
    /// </summary>
    public class SpMovieInfoFallback
    {
        protected ISQLiteCtrl _historyDbCtrl;
        protected ISQLiteCtrl _officialDbCtrl;

        //注入された接続は呼び出し側の所有物のため破棄しない。自前生成分のみ破棄する（FavoriteTagReaderと同一の流儀）
        protected bool _ownsHistoryDbCtrl;
        protected bool _ownsOfficialDbCtrl;
        public SpMovieInfoFallback(ISQLiteCtrl historyDbCtrl = null, ISQLiteCtrl officialDbCtrl = null)
        {
            _historyDbCtrl = historyDbCtrl;
            _officialDbCtrl = officialDbCtrl;
            _ownsHistoryDbCtrl = historyDbCtrl == null;
            _ownsOfficialDbCtrl = officialDbCtrl == null;
        }

        /// <summary>
        /// 動画情報が欠落した項目を予備情報で補う。失敗しても集計は続ける（戻り値は常にtrue）。
        /// 判定材料はタイトルの空欄とする。タイトルと投稿日は同じ取得経路のため、両方欠けるから。
        /// </summary>
        /// <param name="rankingList">補完対象（参照更新）</param>
        /// <param name="baseDay">差分の基準日（初見日の検索期間の下限に使う）</param>
        /// <param name="targetDay">集計日（初見日の検索期間の上限に使う）</param>
        /// <returns>常にtrue（予備のため失敗でも中断しない）</returns>
        public bool ComplementMovieInfo(List<Ranking> rankingList, DateTime baseDay, DateTime targetDay)
        {
            try
            {
                foreach (var wRank in rankingList)
                {
                    if (!string.IsNullOrWhiteSpace(wRank.Title))
                    {
                        continue;
                    }
                    string title = FindLastTitle(wRank.ID);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        wRank.Title = title;
                    }
                    DateTime? firstSeen = FindFirstSeenDay(wRank.ID, baseDay, targetDay);
                    if (firstSeen.HasValue)
                    {
                        //本物の投稿日ではなく「期間内で初めて見かけた日」を参考値にする。
                        //古い動画なのに基準側に数字がない場合は除外、新しく見かけた場合は残す判断に使うため。
                        wRank.Date = firstSeen.Value;
                    }
                    //どちらも見つからなくても残す（除外しない）。出力時に【集計後削除】の目印を付ける。
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return true;
        }

        /// <summary>
        /// 過去の週刊結果から最新のタイトルを拾う。なければnull。
        /// SPは履歴DBに登録しないため、週刊の LastResult を参照する（SP対象は週刊1000位以内のため存在する見込み）。
        /// </summary>
        protected string FindLastTitle(string id)
        {
            try
            {
                var dbCtrl = OpenHistory();
                if (dbCtrl == null)
                {
                    return null;
                }
                using (var aCmd = dbCtrl.Connection.CreateCommand())
                {
                    aCmd.CommandText =
                        @"SELECT タイトル FROM LastResult
                          WHERE ID = @ID ORDER BY 集計日 DESC LIMIT 1";
                    aCmd.Parameters.AddWithValue("@ID", id);
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string title = reader["タイトル"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(title))
                            {
                                return title;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return null;
        }

        /// <summary>
        /// 公式履歴から集計期間内で初めて見かけた集計日を拾う。なければnull。
        /// なければ新着扱いで残す（除外しない）。
        /// </summary>
        protected DateTime? FindFirstSeenDay(string id, DateTime baseDay, DateTime targetDay)
        {
            try
            {
                var dbCtrl = OpenOfficial();
                if (dbCtrl == null)
                {
                    return null;
                }
                using (var aCmd = dbCtrl.Connection.CreateCommand())
                {
                    //新着救済の猶予（基準-7日）より前も含めて期間内すべてを見る。下限は基準-7日に合わせる。
                    long fromDay = long.Parse(DateConvert.Time2String(baseDay.Date.AddDays(-7), false));
                    long toDay = long.Parse(DateConvert.Time2String(targetDay.Date, false));
                    aCmd.CommandText =
                        @"SELECT MIN(集計日) AS 初見日 FROM Ranking
                          WHERE ID = @ID AND 集計日 BETWEEN @FromDay AND @ToDay";
                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@FromDay", fromDay);
                    aCmd.Parameters.AddWithValue("@ToDay", toDay);
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read() && reader["初見日"] != DBNull.Value)
                        {
                            return DateConvert.String2Time(reader["初見日"].ToString(), false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return null;
        }

        private ISQLiteCtrl OpenHistory()
        {
            //注入済みの接続（テスト用インメモリDB等）はそのまま使う。開いていなければ補完なし。
            if (_historyDbCtrl != null)
            {
                return _historyDbCtrl.IsOpen ? _historyDbCtrl : null;
            }
            var dbCtrl = new SQLiteCtrl();
            if (!dbCtrl.Open(DB.NiCORAN_HISTORY))
            {
                StatusLog.WriteLine($"{DB.NiCORAN_HISTORY}を開けませんでした。タイトル補完なしで続けます");
                dbCtrl.Dispose();
                return null;
            }
            //自前生成の接続は使い回すため保持する。Close()で閉じる。
            _historyDbCtrl = dbCtrl;
            return dbCtrl;
        }

        private ISQLiteCtrl OpenOfficial()
        {
            //注入済みの接続（テスト用インメモリDB等）はそのまま使う。開いていなければ補完なし。
            if (_officialDbCtrl != null)
            {
                return _officialDbCtrl.IsOpen ? _officialDbCtrl : null;
            }
            var dbCtrl = new SQLiteCtrl();
            if (!dbCtrl.Open(DB.LOG_OFFICEIAL))
            {
                StatusLog.WriteLine($"{DB.LOG_OFFICEIAL}を開けませんでした。初見日補完なしで続けます");
                dbCtrl.Dispose();
                return null;
            }
            _officialDbCtrl = dbCtrl;
            return dbCtrl;
        }

        /// <summary>
        /// 自前生成の接続だけ閉じる。注入された接続は呼び出し側の所有物のため触らない。
        /// </summary>
        public void Close()
        {
            if (_ownsHistoryDbCtrl && _historyDbCtrl != null)
            {
                _historyDbCtrl.Dispose();
                _historyDbCtrl = null;
            }
            if (_ownsOfficialDbCtrl && _officialDbCtrl != null)
            {
                _officialDbCtrl.Dispose();
                _officialDbCtrl = null;
            }
        }
    }
}
