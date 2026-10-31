using nicorankLib.Analyze.model;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// 仮集計用の動画IDリストを NicoranHistory.db の LastResult（週刊結果）から作る（Issue #54）。
    ///
    /// なぜ必要か: SP集計の動画IDリストは外部のニコランWebから手動でダウンロードしている。
    /// ニコランWebのSP対象は「週刊ニコラン・月刊ニコランのTOP300までにランクインした全ての動画」であり、
    /// 週刊分は本システムが過去に集計した LastResult から機械的に再現できるため、ダウンロードの手間を省ける。
    ///
    /// なぜ週刊だけか: 本システムに月間集計のDBがないため。月刊だけで上位に入った動画は週刊の上位に
    /// 現れないことがあるが、SPのポイントはスナップショット差分で決まるため、候補を広めに取るほど
    /// 取りこぼしは減る（入りすぎは順位に効きにくい）。既定の上限は週刊300位に合わせる。
    ///
    /// なぜ基準日が要るか: SPの集計期間は基準日DB（スナップショット取得日）から始まる。
    /// 基準日より前の週刊結果から動画を拾っても、差分が0で上位に来ないため候補にする意味がない。
    /// </summary>
    public class SPKariMovieListProvider : IDisposable
    {
        /// <summary>
        /// 抽出する総合ランクの上限（既定）。ニコランWebのSP対象がTOP300のため、週刊側も300位に合わせる。
        /// 月刊分を補うために広げたい場合はここを変える（UIには出さない。頻繁に変える値ではないため）。
        /// </summary>
        public const int DefaultRankLimit = 300;

        /// <summary>参照する NicoranHistory.db のパス</summary>
        public string HistoryDbPath { get; protected set; }

        protected ISQLiteCtrl _dbCtrl;
        // 注入された接続は呼び出し側の所有物のため破棄しない。自前生成分のみ破棄する（SpMovieInfoFallbackと同一の流儀）。
        protected bool _ownsDbCtrl;

        public SPKariMovieListProvider(string historyDbPath = DB.NiCORAN_HISTORY, ISQLiteCtrl dbCtrl = null)
        {
            HistoryDbPath = historyDbPath;
            _dbCtrl = dbCtrl;
            _ownsDbCtrl = dbCtrl == null;
        }

        /// <summary>
        /// DBを開く。注入された接続は既に開いている前提（テスト用インメモリDB等）とし、開き直さない。
        /// </summary>
        public bool Open()
        {
            if (_dbCtrl != null)
            {
                return _dbCtrl.IsOpen;
            }
            var dbCtrl = new SQLiteCtrl();
            if (!dbCtrl.Open(HistoryDbPath))
            {
                dbCtrl.Dispose();
                return false;
            }
            _dbCtrl = dbCtrl;
            return true;
        }

        /// <summary>
        /// 基準日以降の週刊結果から、指定ランク以内の動画IDを重複なく抽出する。
        /// IDはID順に並べる。最終順位はポイントのソートで決まるため順位には影響しないが、
        /// 入力順を決定的にして再現性とテストの安定を保つためである。
        /// </summary>
        /// <param name="baseDay">基準日（SPの集計期間開始日）。LastResult.集計日はINTEGERのためyyyyMMdd整数で比較する</param>
        /// <param name="rankLimit">総合ランクの上限（この順位を含む）</param>
        /// <param name="result">抽出結果。失敗時はnull</param>
        /// <returns>抽出に成功したらtrue。DB未オープン・例外時はfalse</returns>
        public bool TryGetMovieIds(DateTime baseDay, int rankLimit, out SPKariMovieListResult result)
        {
            result = null;
            try
            {
                if (_dbCtrl == null || !_dbCtrl.IsOpen)
                {
                    StatusLog.WriteLine($"{HistoryDbPath}が開けないため、仮集計の動画IDを抽出できません");
                    return false;
                }

                // 基準日はDateTimeのまま渡すと文字列比較になる恐れがあるため、既存の整数比較に揃える
                long baseDayValue = long.Parse(DateConvert.Time2String(baseDay.Date, false));
                // 種別は文字列直書きを避ける（DB移行時の前例に倣う）
                string syubetsu = EAnalyzeMode.Weekly.ToString();

                var ids = new List<string>();
                using (var aCmd = _dbCtrl.Connection.CreateCommand())
                {
                    // ID空欄は念のため除外する（通常は発生しないが、混ざっても空IDのRankingを作らない）
                    aCmd.CommandText =
                        @"SELECT DISTINCT ID FROM LastResult
                          WHERE 種別 = @種別
                            AND 総合ランク <= @上限
                            AND 集計日 >= @基準日
                            AND ID IS NOT NULL
                            AND ID <> ''
                          ORDER BY ID";
                    aCmd.Parameters.AddWithValue("@種別", syubetsu);
                    aCmd.Parameters.AddWithValue("@上限", rankLimit);
                    aCmd.Parameters.AddWithValue("@基準日", baseDayValue);
                    using (var reader = aCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ids.Add(reader["ID"].ToString());
                        }
                    }
                }

                result = new SPKariMovieListResult(ids, baseDay.Date, rankLimit, GetLatestWeeklyDay(syubetsu));
                return true;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
        }

        /// <summary>
        /// 参照した週刊結果の最新集計日を返す。仮集計が「どの週までの結果か」をログで示すために使う。
        /// 抽出条件（ランク・基準日）では絞らない。仮集計の鮮度は、条件に合うかではなく
        /// 週刊集計そのものがどこまで進んでいるかで決まるためである。行がなければnull。
        /// </summary>
        protected DateTime? GetLatestWeeklyDay(string syubetsu)
        {
            using (var aCmd = _dbCtrl.Connection.CreateCommand())
            {
                aCmd.CommandText = "SELECT MAX(集計日) AS 最新集計日 FROM LastResult WHERE 種別 = @種別";
                aCmd.Parameters.AddWithValue("@種別", syubetsu);
                object value = aCmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }
                return DateConvert.String2Time(value.ToString(), false);
            }
        }

        /// <summary>
        /// 自前生成の接続だけ閉じる。注入された接続は呼び出し側の所有物のため触らない。
        /// </summary>
        public void Close()
        {
            if (_ownsDbCtrl && _dbCtrl != null)
            {
                _dbCtrl.Dispose();
                _dbCtrl = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

    /// <summary>
    /// 仮集計の動画ID抽出結果。ログ表示（件数・基準日・参照した最新集計日）に必要な値をまとめて返す。
    /// </summary>
    public class SPKariMovieListResult
    {
        public IReadOnlyList<string> Ids { get; }
        public DateTime BaseDay { get; }
        public int RankLimit { get; }
        public DateTime? LatestWeeklyDay { get; }

        public SPKariMovieListResult(IReadOnlyList<string> ids, DateTime baseDay, int rankLimit, DateTime? latestWeeklyDay)
        {
            Ids = ids;
            BaseDay = baseDay;
            RankLimit = rankLimit;
            LatestWeeklyDay = latestWeeklyDay;
        }
    }
}
