using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Json;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace nicorankLib.Analyze.Official
{
    public class RankingHistory : IDisposable, IDbMigratable
    {
        protected const string DBFILE_NAME = DB.LOG_OFFICEIAL;

        /// <summary>
        /// DB構成バージョンの現在値。構成変更時に+1する。
        /// </summary>
        public const long DbCurrentVersion = 1;

        /// <summary>
        /// 直近何日分のRankingを残すか。値はコードのみに持ち、文書には書かない。
        /// 境界はDB内の最新集計日（RankingDateのMAX）を起点にさかのぼって決める。
        /// </summary>
        public const int RetentionDays = 365;

        /// <summary>
        /// 新着偽装チェック用の差分元テーブル。so動画のIDごとに最新1件だけ保持する。
        /// </summary>
        private const string SoHistoryTable = "SoHistory";

        /// <summary>
        /// prune用の索引。PKが(ID,集計日)のため集計日だけの削除が全走査になるのを避ける。
        /// </summary>
        private const string RankingDateIndex = "idx_Ranking_集計日";

        protected ISQLiteCtrl dbCtrlOfficial = null;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RankingHistory(ISQLiteCtrl dbCtrl = null)
        {
            _dbCtrlOverride = dbCtrl;
        }

        protected ISQLiteCtrl _dbCtrlOverride;

        /// <summary>
        /// LogOfficial.dbを開く
        /// </summary>
        /// <returns></returns>
        public bool Open()
        {
            Close();
            dbCtrlOfficial = _dbCtrlOverride ?? new SQLiteCtrl();

            if (dbCtrlOfficial.IsOpen)
            {
                // 注入済みの開いた接続（テスト）はそのまま使う
                return true;
            }

            var dbFile = Path.Combine(Directory.GetCurrentDirectory(), DBFILE_NAME);

            if (!dbCtrlOfficial.Open(dbFile))
            {
                return false;
            }

            return true;

        }

        /// <summary>
        /// DBを閉じる
        /// </summary>
        public void Close()
        {
            if (dbCtrlOfficial != null && dbCtrlOfficial.IsOpen)
            {
                dbCtrlOfficial.Close();
            }
            dbCtrlOfficial = null;
        }

        /// <summary>
        /// 司令塔向けの対象DB。
        /// </summary>
        public string TargetDb => DBFILE_NAME;

        /// <summary>
        /// LogOfficial.dbを最新の構成に更新する（冪等）。集計開始時に司令塔から呼ばれる。
        /// </summary>
        /// <returns>正常終了時true、失敗時false</returns>
        public bool EnsureMigrated()
        {
            if (dbCtrlOfficial == null || !dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                // 前提条件（全バージョン共通）。移行手順ではないためループ外で確認する
                if (!IsRankingTableExist())
                {
                    StatusLog.WriteLine($"{DB.LOG_OFFICEIAL}にRankingテーブルがありません。");
                    return false;
                }
                // 未記録=旧DBはVer0から順に適用する。未定義バージョンは失敗させる
                long ver = GetDbVersion();
                while (ver < DbCurrentVersion)
                {
                    ver++;
                    if (!MigrateToVersion(ver))
                    {
                        return false;
                    }
                    SetDbVersion(ver);
                }
                return true;
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::EnsureMigrated)");
                errLog.Write(ex);
                return false;
            }
        }

        /// <summary>
        /// 未記録時の番兵。Ver0から順に適用するための起点。
        /// </summary>
        private const long DbNoVersion = -1;

        /// <summary>
        /// Rankingテーブルの存在確認。
        /// </summary>
        private bool IsRankingTableExist()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='Ranking';";
                return Convert.ToInt64(aCmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// 記録済みバージョンを取得する。未記録時はDbNoVersionを返す。
        /// </summary>
        private long GetDbVersion()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "CREATE TABLE IF NOT EXISTS DBVersion (Ver INTEGER);";
                aCmd.ExecuteNonQuery();

                aCmd.CommandText = "SELECT Ver FROM DBVersion LIMIT 1;";
                using (var reader = aCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Convert.ToInt64(reader["Ver"]);
                    }
                }
            }
            return DbNoVersion;
        }

        /// <summary>
        /// 指定バージョンへの移行を行う。将来のバージョン追加時はここにcaseを足す。
        /// </summary>
        private bool MigrateToVersion(long version)
        {
            switch (version)
            {
                case 0:
                    // ベース：メンテナンス日管理テーブルの確保
                    return createRankingDateTable();
                case 1:
                    // 1年保持＋SoHistory併設：SoHistory作成＋初期移行＋初期prune＋Movie廃止。
                    // VACUUMはトランザクション内で実行できないため除外し、確定後に実行する
                    return MigrateToVersion1();
                default:
                    // 未定義は取りこぼし防止のため失敗させる
                    ErrLog.GetInstance().Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::MigrateToVersion 未対応Ver={version})");
                    return false;
            }
        }

        /// <summary>
        /// Ver1移行：SoHistoryの作成＋so最新行の初期移行＋古いRankingの削除＋Movie廃止＋最適化。
        /// データ量が多いため一括トランザクションにせず、日付区切りで少しずつ確定する。
        /// どの段階もやり直し可能（SoHistory移行はREPLACE、削除とDROPはIF EXISTS系）。
        /// バージョン記録は全工程の成功後に呼び出し側が行う。
        /// </summary>
        private bool MigrateToVersion1()
        {
            try
            {
                EnsureSoHistoryTable();
                EnsureRankingDateIndex();

                StatusLog.WriteLine("公式動画の差分元を退避しています...");
                BackfillSoHistory();

                DropMovieTableIfExists();

                if (!PruneOldRankingsChunked())
                {
                    return false;
                }

                StatusLog.WriteLine($"{DB.LOG_OFFICEIAL}を最適化しています（数十分かかることがあります。PCのスリープを無効にしてください）...");
                // 確定済みデータの最適化。失敗時はバージョン未記録のため再実行でやり直せる
                using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
                {
                    aCmd.CommandText = "VACUUM;";
                    aCmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::MigrateToVersion1)");
                errLog.Write(ex);
                return false;
            }
        }

        /// <summary>
        /// SoHistoryテーブルがなければ作る（あれば何もしない）。
        /// </summary>
        private void EnsureSoHistoryTable()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText =
                    $"CREATE TABLE IF NOT EXISTS {SoHistoryTable} (" +
                    "ID TEXT PRIMARY KEY, " +
                    "集計日 INTEGER, " +
                    "再生数 INTEGER, " +
                    "コメント数 INTEGER, " +
                    "マイリスト数 INTEGER, " +
                    "いいね数 INTEGER " +
                    ");";
                aCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// prune用の索引がなければ作る（あれば何もしない）。
        /// </summary>
        private void EnsureRankingDateIndex()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = $"CREATE INDEX IF NOT EXISTS {RankingDateIndex} ON Ranking (集計日);";
                aCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 全so動画の最新1件をSoHistoryに移す。新しい集計日から順に登録し、
        /// 登録済みIDは無視するため最新1件が残る（再実行時も同結果で冪等）。
        /// 1回分の確定を小さくするため集計日区切りで少しずつ入れる。
        /// </summary>
        private void BackfillSoHistory()
        {
            var targetDates = new List<long>();
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "SELECT DISTINCT 集計日 FROM Ranking WHERE ID LIKE 'so%' ORDER BY 集計日 DESC;";
                using (var reader = aCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        targetDates.Add(Convert.ToInt64(reader["集計日"]));
                    }
                }
            }

            if (targetDates.Count < 1)
            {
                return;
            }

            StatusLog.WriteLine($"公式動画の差分元を退避しています（{targetDates.Count}日分）...");
            int done = 0;
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                foreach (var date in targetDates)
                {
                    aCmd.Parameters.Clear();
                    aCmd.Parameters.AddWithValue("@Date", date);
                    aCmd.CommandText =
                        $"INSERT OR IGNORE INTO {SoHistoryTable} (ID, 集計日, 再生数, コメント数, マイリスト数, いいね数) " +
                        "SELECT ID, 集計日, 再生数, コメント数, マイリスト数, いいね数 FROM Ranking " +
                        "WHERE 集計日 = @Date AND ID LIKE 'so%';";
                    aCmd.ExecuteNonQuery();
                    done++;
                    if (done % 100 == 0)
                    {
                        StatusLog.WriteLine($"退避中... ({done}/{targetDates.Count}日)");
                    }
                }
            }
            StatusLog.WriteLine("公式動画の差分元の退避が終わりました。");
        }

        /// <summary>
        /// Movieテーブルがあれば廃止する。読み手は呼出元なし、書込みも今回で止めるため。
        /// </summary>
        private void DropMovieTableIfExists()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "DROP TABLE IF EXISTS Movie;";
                aCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 保持境界より古いRankingを集計日区切りで少しずつ消す。境界当日は残す。
        /// </summary>
        /// <returns>正常終了時true、失敗時false</returns>
        private bool PruneOldRankingsChunked()
        {
            long? cutoff = GetRetentionCutoff();
            if (!cutoff.HasValue)
            {
                // データがなければ消すものなし
                return true;
            }

            var targetDates = new List<long>();
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "SELECT DISTINCT 集計日 FROM Ranking WHERE 集計日 < @Cutoff ORDER BY 集計日;";
                aCmd.Parameters.AddWithValue("@Cutoff", cutoff.Value);
                using (var reader = aCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        targetDates.Add(Convert.ToInt64(reader["集計日"]));
                    }
                }
            }

            if (targetDates.Count < 1)
            {
                return true;
            }

            StatusLog.WriteLine($"古いランキングデータ {targetDates.Count} 日分を削除しています...");
            int done = 0;
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                foreach (var date in targetDates)
                {
                    aCmd.Parameters.Clear();
                    aCmd.Parameters.AddWithValue("@Date", date);
                    aCmd.CommandText = "DELETE FROM Ranking WHERE 集計日 = @Date;";
                    aCmd.ExecuteNonQuery();
                    done++;
                    if (done % 100 == 0)
                    {
                        StatusLog.WriteLine($"削除中... ({done}/{targetDates.Count}日)");
                    }
                }
            }
            StatusLog.WriteLine($"古いランキングデータの削除が終わりました（{targetDates.Count}日分）。");
            return true;
        }

        /// <summary>
        /// 保持境界（この値未満を削除）を求める。DB内の最新集計日を起点にさかのぼる。
        /// データがなければnullを返す。
        /// </summary>
        private long? GetRetentionCutoff()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "SELECT MAX(集計日) FROM RankingDate;";
                object result = aCmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    return null;
                }
                DateTime latest = DateConvert.String2Time(result.ToString(), false);
                DateTime cutoffDate = latest.AddDays(-RetentionDays);
                return long.Parse(DateConvert.Time2String(cutoffDate, false));
            }
        }

        /// <summary>
        /// SoHistoryテーブルがあるかを確認する。移行前のDBでフォールバック検索を壊さないため。
        /// </summary>
        private bool SoHistoryExists()
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='{SoHistoryTable}';";
                return Convert.ToInt64(aCmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>
        /// バージョンを記録する（初回はINSERT、以後はUPDATE）。
        /// </summary>
        private void SetDbVersion(long version)
        {
            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                aCmd.CommandText = "SELECT COUNT(*) FROM DBVersion;";
                bool hasRow = Convert.ToInt64(aCmd.ExecuteScalar()) > 0;
                if (hasRow)
                {
                    aCmd.CommandText = "UPDATE DBVersion SET Ver = @Ver;";
                }
                else
                {
                    aCmd.CommandText = "INSERT INTO DBVersion (Ver) VALUES (@Ver);";
                }
                aCmd.Parameters.Clear();
                aCmd.Parameters.AddWithValue("@Ver", version);
                aCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 公式チャンネルの動画が本当に新着なのか確認する
        /// </summary>
        /// <param name="id"></param>
        /// <param name="baseTime"></param>
        /// <param name="ranking">差分データ。nullは差分なし。</param>
        /// <returns>正常終了時true、エラー時false </returns>
        public bool CheckSoMovieNeedSabun(string id, long baseTime, out Ranking ranking)
        {
            ranking = null;
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
                {
                    aCmd.CommandText =
                        @"select * from Ranking 
                        Where ID = @ID and 集計日 <= @Date 
                        order by 集計日 desc 
                        Limit 1 ";

                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@Date", baseTime);

                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            //過去のランキングに記載されている＝新着ではない
                            ranking = new Ranking()
                            {
                                ID = id,
                                CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                CountLike = System.Convert.ToInt64(reader["いいね数"])
                            };
                        }
                        else
                        {
                            // 新着 or 新着偽造（過去のランキングだけでは判断できない）
                            // 差分データなし
                            ranking = null;
                        }
                    }

                    if (ranking == null && SoHistoryExists())
                    {
                        // 1年保持で古いRankingが消えている場合、SoHistoryの最新1件を差分元にする。
                        // SoHistoryは基準日より新しい値になることがあるが、差分は小さめに出る方向のため
                        // 新着誤除外にはならない。なければ新着扱い（ranking=nullのまま）
                        aCmd.Parameters.Clear();
                        aCmd.CommandText =
                            $"select 再生数, コメント数, マイリスト数, いいね数 from {SoHistoryTable} " +
                            "Where ID = @ID " +
                            "Limit 1 ";

                        aCmd.Parameters.AddWithValue("@ID", id);

                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ranking = new Ranking()
                                {
                                    ID = id,
                                    CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                    CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                    CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                    CountLike = System.Convert.ToInt64(reader["いいね数"])
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。ID={id}(RankingHistory::CheckSoMovieNeedSabun)");
                errLog.Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        ///  公式の過去ログから差分を取得する
        /// </summary>
        /// <param name="id"></param>
        /// <param name="baseTime"></param>
        /// <param name="ranking"></param>
        /// <returns></returns>
        public bool GetRankingSabunDataLogOfficial(string id, long baseTime, long baseTime2, out Ranking ranking)
        {
            ranking = null;
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
                {
                    aCmd.CommandText =
                        @"select * from Ranking 
                        Where ID = @ID and 集計日 BETWEEN @Date2 AND @Date1
                        order by 集計日 desc 
                        Limit 1 ";

 
                    aCmd.Parameters.AddWithValue("@ID", id);
                    aCmd.Parameters.AddWithValue("@Date1", baseTime);

                    aCmd.Parameters.AddWithValue("@Date2", baseTime2);

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ranking = new Ranking()
                            {
                                ID = id,
                                CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                CountLike = System.Convert.ToInt64(reader["いいね数"])
                            };
                        }
                    }

                    if (ranking == null)
                    {//取得できなかった場合
                        aCmd.Parameters.Clear();

                        aCmd.CommandText =
                        @"select * from Ranking 
                        Where ID = @ID and 集計日 >= @Date
                        order by 集計日 
                        Limit 1 ";

                        aCmd.Parameters.AddWithValue("@ID", id);
                        aCmd.Parameters.AddWithValue("@Date", baseTime);

                        //実行結果の取得
                        using (var reader = aCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ranking = new Ranking()
                                {
                                    ID = id,
                                    CountPlay = System.Convert.ToInt64(reader["再生数"]),
                                    CountComment = System.Convert.ToInt64(reader["コメント数"]),
                                    CountMyList = System.Convert.ToInt64(reader["マイリスト数"]),
                                    CountLike = System.Convert.ToInt64(reader["いいね数"])
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。ID={id}(RankingHistory::GetRankingSabunData)");
                errLog.Write(ex);
                return false;
            }

            return true;
        }




        /// <summary>
        /// LogOfficial.dbの過去ログを更新する
        /// </summary>
        /// <returns></returns>
        public bool UpdateOfficialRankingDB()
        {
            try
            {
                if (!this.dbCtrlOfficial.IsOpen)
                {
                    return false;
                }
                //RankingDateテーブルが存在しなければ作成する
                if (!createRankingDateTable())
                {
                    return false;
                }

                ////更新の必要性をチェック
                if (!checkNeedUpdateOfficialRankingDB(out var needDailyList))
                {//エラー
                    return false;
                }
                if (needDailyList.Count < 1)
                {
                    StatusLog.WriteLine($"過去ランキングデータは最新です。");
                    //更新の必要性はない
                    return true;
                }
                bool isMaintenanceOK = false;
                foreach (var targetDate in needDailyList)
                {

                    StatusLog.WriteLine($"{targetDate.ToShortDateString()}の過去ランキングデータを取得しています...");
                    if (!analyzeDailyRanking(targetDate, out var rankings))
                    {
                        StatusLog.WriteLine($"\n{ targetDate.ToShortDateString() } のランキングデータ取得でエラー発生");
                        break;
                    }
                    if (rankings.Count < 1)
                    {
                        if (!isMaintenanceOK)
                        {
                            //1件も取得できなかった場合、対象の日にニコ動がメンテナンスしていた可能性がある
                            var askResult = MessageBox.Show(
    $@"{targetDate.ToShortDateString()}のランキングデータが取得できませんでした。
メンテナンス日として登録しますか？

OK: この後の取得不可日は全てメンテナンス日として登録します
キャンセル : 現在の処理を中断します"
                                , "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);

                            if (askResult == DialogResult.OK)
                            {
                                isMaintenanceOK = true;
                            }
                        }

                        if (isMaintenanceOK)
                        {
                            //このまま登録処理を続ける
                            if (!updateOfficialRankingDB_Daily(targetDate, rankings, true))
                            {
                                StatusLog.WriteLine($"\n{targetDate.ToShortDateString()} のランキングデータ登録でエラー発生");
                                break;
                            }
                        }
                        else
                        {
                            StatusLog.WriteLine($"\n{targetDate.ToShortDateString()} のランキングデータ取得でエラー発生。集計を中断します");
                            return false;
                        }

                    }
                    else
                    {
                        StatusLog.Write($"DB登録開始..");
                        if (!updateOfficialRankingDB_Daily(targetDate, rankings))
                        {
                            StatusLog.WriteLine($"\n{targetDate.ToShortDateString()} のランキングデータ登録でエラー発生");
                            break;
                        }
                    }
                    StatusLog.WriteLine($"DB登録終了。");

                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::UpdateOfficialRankingDB)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 指定日付のランキング情報を取得する
        /// </summary>
        /// <param name="targetDate"></param>
        /// <returns></returns>
        protected bool analyzeDailyRanking(DateTime targetDate, out List<Ranking> rankings)
        {
            rankings = new List<Ranking>();
            var readerList = new List<JsonReaderBase>()
            { 
                //デイリーと総合ランキングは集計する
                new JsonReaderDaily(targetDate),
                new JsonReaderTotal(targetDate)
            };
            if (targetDate.DayOfWeek == DayOfWeek.Monday)
            {// 月曜日の場合は週刊ランキングも集計する
                readerList.Add(new JsonReaderWeekly(targetDate));
            }
            if (targetDate.Day == 1)
            {// 1日の場合は月間ランキングも集計する
                readerList.Add(new JsonReaderMonthly(targetDate));
            }

            var rankListList = new List<List<Ranking>>();
            foreach (var reader in readerList)
            {
                if (reader.AnalyzeRank(out List<Ranking> workList))
                {
                    rankListList.Add(workList);
                }
            }
            if (rankListList.Count == 1)
            {
                rankings = rankListList[0];
            }
            else
            {
                rankings = Ranking.MergeRankingList(rankListList);
            }
            return true;
        }

        /// <summary>
        /// 更新の必要性をチェック
        /// </summary>
        /// <param name="needDailyList"></param>
        /// <returns></returns>
        protected bool checkNeedUpdateOfficialRankingDB(out List<DateTime> needDailyList)
        {
            needDailyList = new List<DateTime>();
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            try
            {
                using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
                {
                    //どこまで集計されているか取得する
                    //NULL=集計されていない場合、20190611から集計できるように設定しておく
                    aCmd.CommandText =
                        @"SELECT IFNULL(Max(集計日), 20190610) as '集計日' 
                          FROM RankingDate;";

                    DateTime today = DateTime.Now.Date;
                    DateTime baseDateTime = today;

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            object result = reader["集計日"];

                            // 最終集計した次の日から取得する
                            baseDateTime = DateConvert.String2Time(result.ToString(), false);
                            baseDateTime = baseDateTime.AddDays(1);
                            break;
                        }

                        //集計が必要な開始日～最新のデイリーまでログを取得する
                        while (baseDateTime <= today)
                        {
                            if (!JsonReaderBase.CheckAnalyzeTime(DateTime.Now))
                            {//当日の0:30前の場合は、まだ集計されていない可能性があるので、集計しない
                                break;
                            }

                            needDailyList.Add(baseDateTime);
                            baseDateTime = baseDateTime.AddDays(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::checkNeedUpdateOfficialRankingDB)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }


        /// <summary>
        /// LogOfficial.dbの過去ログを更新する(1日分）
        /// </summary>
        /// <param name="analyzeTime"></param>
        /// <param name="rankings"></param>
        /// <returns></returns>
        protected bool updateOfficialRankingDB_Daily(DateTime analyzeTime, List<Ranking> rankings,bool isMaintenance = false)
        {
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }

            using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
            {
                try
                {
                    //トランザクションの開始
                    aCmd.Transaction = (SqliteTransaction)dbCtrlOfficial.Connection.BeginTransaction();

                    long analyzeDate = long.Parse(DateConvert.Time2String(analyzeTime, false));

                    if (!isMaintenance)
                    {//←メンテナンス日以外の時実行

      

                        var strSQL = @"INSERT INTO Ranking
                                  ( ID,     集計日,再生数,コメント数,マイリスト数,いいね数,人気のタグ )
                                    VALUES
                                  ( @ID,    @Date,  @Play, @Comment, @MyList ,@いいね数 ,@人気のタグ)";

                        aCmd.CommandText = strSQL;

                        
                        Regex regDelete = new Regex(@"/video_deleted");

                        foreach (var wRank in rankings)
                        {
                            if (regDelete.IsMatch(wRank.ThumbnailURL))
                            {// 削除 or 非表示動画は登録しない
                            }
                            else
                            {
                                aCmd.Parameters.Clear();
                                aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                                aCmd.Parameters.AddWithValue("@Date", analyzeDate);
                                aCmd.Parameters.AddWithValue("@Play", wRank.CountPlay);
                                aCmd.Parameters.AddWithValue("@Comment", wRank.CountComment);
                                aCmd.Parameters.AddWithValue("@MyList", wRank.CountMyList);
                                aCmd.Parameters.AddWithValue("@いいね数", wRank.CountLike);
                                aCmd.Parameters.AddWithValue("@人気のタグ", JsonConvert.SerializeObject(wRank.FavoriteTags));
                                //更新の実行
                                aCmd.ExecuteNonQuery();
                            }
                        }
                    }//←メンテナンス日以外の時実行


                    {//RankingDateテーブルの更新

                        var strSQL = @"INSERT INTO RankingDate
                                  ( '集計日','メンテナンス' )
                                    VALUES
                                  ( @Date,  @メンテナンス)";

                        aCmd.CommandText = strSQL;
                        aCmd.Parameters.Clear();
                        aCmd.Parameters.AddWithValue("@Date", analyzeDate);
                        aCmd.Parameters.AddWithValue("@メンテナンス", isMaintenance? 1:0 );

                        //更新の実行
                        aCmd.ExecuteNonQuery();
                    }

                    if (!isMaintenance)
                    {
                        // 当日分のso動画でSoHistoryを足し替え、古いRankingを消す。
                        // 同一日次トランザクションに同梱する（日次単位は維持する）
                        RefreshSoHistoryAndPrune(aCmd, analyzeDate);
                    }


                    aCmd.Transaction.Commit();
                }
                catch (Exception ex)
                {
                    try { aCmd.Transaction?.Rollback(); } catch { }

                    var errLog = ErrLog.GetInstance();
                    errLog.Write($"{DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::updateOfficialRankingDB_Daily)");
                    errLog.Write(ex);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 当日分のso動画でSoHistoryを足し替え、保持境界より古いRankingを消す。
        /// 日次トランザクションの中から呼ぶ（VACUUMはしない）。
        /// </summary>
        /// <param name="aCmd">日次トランザクション実行中のコマンド</param>
        /// <param name="analyzeDate">当日（yyyyMMdd）。SoHistory足し替えの対象日</param>
        private void RefreshSoHistoryAndPrune(SqliteCommand aCmd, long analyzeDate)
        {
            // SoHistoryがなければ作る（移行前のDBで日次更新だけ走った場合の保険）
            aCmd.CommandText =
                $"CREATE TABLE IF NOT EXISTS {SoHistoryTable} (" +
                "ID TEXT PRIMARY KEY, " +
                "集計日 INTEGER, " +
                "再生数 INTEGER, " +
                "コメント数 INTEGER, " +
                "マイリスト数 INTEGER, " +
                "いいね数 INTEGER " +
                ");";
            aCmd.Parameters.Clear();
            aCmd.ExecuteNonQuery();
            aCmd.CommandText = $"CREATE INDEX IF NOT EXISTS {RankingDateIndex} ON Ranking (集計日);";
            aCmd.ExecuteNonQuery();

            // 当日登録したso動画の分だけSoHistoryを最新化する（当日が最新のため置き換えでよい）
            aCmd.CommandText =
                $"INSERT OR REPLACE INTO {SoHistoryTable} (ID, 集計日, 再生数, コメント数, マイリスト数, いいね数) " +
                "SELECT ID, 集計日, 再生数, コメント数, マイリスト数, いいね数 FROM Ranking " +
                "WHERE 集計日 = @Today AND ID LIKE 'so%';";
            aCmd.Parameters.Clear();
            aCmd.Parameters.AddWithValue("@Today", analyzeDate);
            aCmd.ExecuteNonQuery();

            // 保持境界より古い分を消す（境界当日は残す）。1日分ずつのため1文で足りる
            long? cutoff = GetRetentionCutoffInTransaction(aCmd);
            if (cutoff.HasValue)
            {
                aCmd.CommandText = "DELETE FROM Ranking WHERE 集計日 < @Cutoff;";
                aCmd.Parameters.Clear();
                aCmd.Parameters.AddWithValue("@Cutoff", cutoff.Value);
                aCmd.ExecuteNonQuery();
            }
            aCmd.Parameters.Clear();
        }

        /// <summary>
        /// トランザクション内から見た保持境界。データがなければnull。
        /// </summary>
        private long? GetRetentionCutoffInTransaction(SqliteCommand aCmd)
        {
            aCmd.CommandText = "SELECT MAX(集計日) FROM RankingDate;";
            aCmd.Parameters.Clear();
            object result = aCmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                return null;
            }
            DateTime latest = DateConvert.String2Time(result.ToString(), false);
            DateTime cutoffDate = latest.AddDays(-RetentionDays);
            return long.Parse(DateConvert.Time2String(cutoffDate, false));
        }
        /// <summary>
        /// RankingDateテーブルが存在しなければ追加する
        /// </summary>
        /// <param name="needDailyList"></param>
        /// <returns></returns>
        protected bool createRankingDateTable()
        {
            // メンテナンス日かどうかを判定するための、RankingDateテーブルが存在しなければ追加する
            if (!this.dbCtrlOfficial.IsOpen)
            {
                return false;
            }
            SqliteTransaction transaction = null;
            try
            {
                using (var aCmd = dbCtrlOfficial.Connection.CreateCommand())
                {
                    //どこまで集計されているか取得する
                    //NULL=集計されていない場合、20190611から集計できるように設定しておく
                    aCmd.CommandText =
                        @"SELECT * FROM sqlite_master WHERE TYPE='table' AND name='RankingDate';";


                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            //存在しているので作成する必要なし
                            return true;
                        }

                    }

                    StatusLog.WriteLine("メンテナンス日を管理するテーブルを作成しています(数分程度かかります。気長にお待ちください)...");


                    //存在しないので作成する
                    //トランザクションの開始
                    transaction = (SqliteTransaction)dbCtrlOfficial.Connection.BeginTransaction();
                    aCmd.Transaction = transaction;

                    aCmd.CommandText =
                        @"CREATE TABLE RankingDate (
                            '集計日'	    INTEGER             ,
                            'メンテナンス'	INTEGER DEFAULT 0   ,
                            PRIMARY KEY('集計日')
                            );";

                    //DEB作成の実行
                    aCmd.ExecuteNonQuery();

                    //テーブルの中身を、現時点のデータを元に作成する
                    aCmd.CommandText =
                        @"INSERT INTO RankingDate ('集計日', 'メンテナンス')
                            SELECT 集計日,
                              CASE
                                WHEN COUNT(集計日) > 0 THEN 0
                                ELSE 1
                              END AS 'メンテナンス'
                          FROM Ranking
                          Group by 集計日;";

                    //テーブル更新の実行
                    aCmd.ExecuteNonQuery();

                    aCmd.Transaction.Commit();
                    aCmd.Transaction = null;
                }

            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                var errLog = ErrLog.GetInstance();
                errLog.Write($"{ DB.LOG_OFFICEIAL}更新でエラー発生。(RankingHistory::createRankingDateTable)");
                errLog.Write(ex);
                return false;
            }
            return true;
        }

        ///メンテナンス日かどうかをチェックする
        public bool CheckMaintananceDay(DateTime chechDay)
        {
            try
            {
                if (!this.dbCtrlOfficial.IsOpen)
                {
                    return false;
                }

                using (var aCmd = this.dbCtrlOfficial.Connection.CreateCommand())
                {
                    //すでに集計済みか確認する
                    aCmd.CommandText =
                        @"SELECT メンテナンス FROM RankingDate
                                Where 集計日 = @集計日
                                LIMIT 1";
                    aCmd.Parameters.Clear();
                    aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(chechDay, false));

                    //実行結果の取得
                    using (var reader = aCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (Convert.ToInt64(reader["メンテナンス"].ToString()) > 0)
                            {
                                //メンテナンス中
                                return true;
                            }
                            else
                            {
                                return false;

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return false;
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
                    Close();
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        ~RankingHistory()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
