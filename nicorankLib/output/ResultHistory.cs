using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;

namespace nicorankLib.output
{
    /// <summary>
    /// 集計結果を先週の結果として出力するかどうか
    /// </summary>
    public class ResultHistory : OutputBase, IDbMigratable
    {

        /// <summary>
        /// DBファイル名
        /// </summary>
        public const string DATASOURCE = DB.NiCORAN_HISTORY;

        /// <summary>
        /// DB構成バージョンの現在値。構成変更時に+1する。
        /// </summary>
        public const long DbCurrentVersion = 0;

        /// <summary>
        /// 集計日
        /// </summary>
        protected DateTime syuukeiBi;



        /// <summary>
        /// モード
        /// </summary>
        public EAnalyzeMode Mode { get; protected set; }

        protected ISQLiteCtrl _dbCtrlOverride;

        public ResultHistory(EAnalyzeMode mode, ISQLiteCtrl dbCtrl = null)
        {
            this.Mode = mode;
            _dbCtrlOverride = dbCtrl;
        }

        /// <summary>
        /// 集計日を設定する
        /// </summary>
        /// <param name="baseTime"></param>
        public void SetSyuukeiBi(DateTime syuukeiBi)
        {
            this.syuukeiBi = syuukeiBi;
        }

        /// <summary>
        /// 司令塔向けの対象DB。
        /// </summary>
        public string TargetDb => DATASOURCE;

        /// <summary>
        /// NicoranHistory.dbを最新の構成に更新する（冪等）。集計開始時に司令塔から呼ばれる。
        /// いいね列の追加＋既存JSONの空文字化＋VACUUM＋DBVersion記録を行う。
        /// </summary>
        /// <returns>正常終了時true、失敗時false</returns>
        public bool EnsureMigrated()
        {
            // 注入済みの開いた接続（テスト）はそのまま使う。自前生成分のみ開閉する
            bool ownsDbCtrl = _dbCtrlOverride == null;
            ISQLiteCtrl dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl();
            try
            {
                if (!dbCtrl.IsOpen && !dbCtrl.Open(DATASOURCE))
                {
                    StatusLog.WriteLine($"{ DATASOURCE }が参照できません。");
                    return false;
                }
                using (var aCmd = dbCtrl.Connection.CreateCommand())
                {
                    // 前提条件（全バージョン共通）。移行手順ではないためループ外で確認する
                    if (!AreHistoryTablesExist(aCmd))
                    {
                        StatusLog.WriteLine($"{ DATASOURCE }にHistory/LastResultテーブルがありません。");
                        return false;
                    }
                    // 未記録=旧DBはVer0から順に適用する。未定義バージョンは失敗させる
                    long ver = GetDbVersion(aCmd);
                    while (ver < DbCurrentVersion)
                    {
                        ver++;
                        if (!MigrateToVersion(aCmd, ver))
                        {
                            return false;
                        }
                        SetDbVersion(aCmd, ver);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            finally
            {
                if (ownsDbCtrl)
                {
                    dbCtrl.Dispose();
                }
            }
        }

        /// <summary>
        /// History/LastResultテーブルの存在確認。
        /// </summary>
        private static bool AreHistoryTablesExist(SqliteCommand aCmd)
        {
            aCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name IN ('History','LastResult');";
            return Convert.ToInt64(aCmd.ExecuteScalar()) >= 2;
        }

        /// <summary>
        /// いいね関連列がなければ追加する（Execute内と集計開始時の両方から使う）。
        /// </summary>
        private static void EnsureLikeColumns(SqliteCommand aCmd)
        {
            bool isLikeFieldExist = false;
            aCmd.CommandText = "PRAGMA table_info('LastResult');";
            using (var reader = aCmd.ExecuteReader())
            {
                while (reader.Read())
                {

                    if (reader["name"].ToString().Equals("いいね数"))
                    {
                        isLikeFieldExist = true;
                        break;
                    }

                }
                reader.Close();
            }
            if (isLikeFieldExist == false)
            {
                //いいねフィールドがない→アップデートする
                aCmd.CommandText =
                    "ALTER TABLE History ADD いいね数 INTEGER DEFAULT 0; " +
                    "ALTER TABLE LastResult ADD いいね数 INT DEFAULT 0;" +
                    "ALTER TABLE LastResult ADD 累計いいね数 INT DEFAULT 0;";

                aCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 未記録時の番兵。Ver0から順に適用するための起点。
        /// </summary>
        private const long DbNoVersion = -1;

        /// <summary>
        /// 記録済みバージョンを取得する。未記録時はDbNoVersionを返す。
        /// </summary>
        private static long GetDbVersion(SqliteCommand aCmd)
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
            return DbNoVersion;
        }

        /// <summary>
        /// 指定バージョンへの移行を行う。将来のバージョン追加時はここにcaseを足す。
        /// </summary>
        private static bool MigrateToVersion(SqliteCommand aCmd, long version)
        {
            switch (version)
            {
                case 0:
                    // ベース：いいね列追加＋既存JSON空文字化（トランザクション化）
                    // VACUUMはトランザクション内で実行できないため除外し、確定後に実行する
                    aCmd.Transaction = (SqliteTransaction)aCmd.Connection.BeginTransaction();
                    try
                    {
                        EnsureLikeColumns(aCmd);
                        // 肥大化対策：既存JSONを空文字化する。読み側はJSON列を使わないため動作不変
                        aCmd.CommandText = "UPDATE LastResult SET JSON = '' WHERE JSON IS NOT NULL AND JSON <> '';";
                        aCmd.ExecuteNonQuery();
                        aCmd.Transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { aCmd.Transaction?.Rollback(); } catch { }
                        ErrLog.GetInstance().Write(ex);
                        return false;
                    }
                    finally
                    {
                        // 後続のSetDbVersionが同一コマンドを使い回すため外す
                        aCmd.Transaction = null;
                    }
                    StatusLog.WriteLine($"{DATASOURCE}を最適化しています...");
                    // 確定済みデータの最適化。失敗時はバージョン未記録のため再実行でリトライできる
                    aCmd.CommandText = "VACUUM;";
                    aCmd.ExecuteNonQuery();
                    return true;
                default:
                    // 未定義は取りこぼし防止のため失敗させる
                    ErrLog.GetInstance().Write($"未対応のDBバージョンです。(ResultHistory::MigrateToVersion Ver={version})");
                    return false;
            }
        }

        /// <summary>
        /// バージョンを記録する（初回はINSERT、以後はUPDATE）。
        /// </summary>
        private static void SetDbVersion(SqliteCommand aCmd, long version)
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
            aCmd.Parameters.Clear();
        }

        /// <summary>
        /// 出力する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                // 注入された接続は呼び出し側の所有物のため破棄しない。自前生成分のみ破棄する
                bool ownsDbCtrl = _dbCtrlOverride == null;
                ISQLiteCtrl dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl();
                try
                {
                    if (!dbCtrl.Open(DATASOURCE))
                    {
                        StatusLog.WriteLine($"{ DATASOURCE }が参照できません。");
                        return false;
                    }
                    using (var aCmd = dbCtrl.Connection.CreateCommand())
                    {
                        try
                        {
                            // トランザクションの開始
                            aCmd.Transaction = (SqliteTransaction)dbCtrl.Connection.BeginTransaction();

                            {//DBの更新確認
                                EnsureLikeColumns(aCmd);
                            }

                            switch (this.Mode)
                            {
                                case EAnalyzeMode.Weekly:
                                    if (!updateHistory(aCmd, rankingList).Result)
                                    {
                                        StatusLog.WriteLine($"{ DATASOURCE }のHistroyテーブルの更新に失敗しました。エラーログを確認してください");
                                        aCmd.Transaction.Rollback();
                                        return false;
                                    }
                                    break;
                            }
                            aCmd.Parameters.Clear();

                            if (!updateLastResult(aCmd, rankingList).Result)
                            {
                                StatusLog.WriteLine($"{ DATASOURCE }のLastResultテーブルの更新に失敗しました。エラーログを確認してください");

                                aCmd.Transaction.Rollback();
                                return false;
                            }
                            // トランザクションの開始
                            aCmd.Transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            ErrLog.GetInstance().Write(ex);
                            try { aCmd.Transaction?.Rollback(); } catch { }
                            return false;
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
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }

        /// <summary>
        /// LastResultテーブルを更新する
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        protected async Task<bool> updateLastResult(SqliteCommand sqlCmd, IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                //{// 登録前に古いデータを削除する
                //    sqlCmd.CommandText =
                //        @"DELETE FROM LastResult
                //      WHERE 集計日 <= @削除日 and 種別=@種別;";

                //    // 前回よりさらに1週間以上前のデータは不要なので駆除する
                //    var deleteBi = this.syuukeiBi.AddDays(-14);
                //    sqlCmd.Parameters.AddWithValue("@削除日", DateConvert.Time2String(deleteBi, false));
                //    sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());

                //    // 削除実行
                //    await sqlCmd.ExecuteNonQueryAsync();
                //}

                {// すでに登録済みなのかチェックする
                    sqlCmd.CommandText =
                        @"SELECT * FROM LastResult
                          WHERE 集計日 = @集計日 and 種別=@種別
                          LIMIT 1;";

                    sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));
                    sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());

                    using (var reader = sqlCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            reader.Close();
                            //すでに集計済→上書きのため削除
                            sqlCmd.CommandText =
                                @"DELETE FROM LastResult
                                  WHERE 集計日 = @集計日 and 種別=@種別;";


                            await sqlCmd.ExecuteNonQueryAsync();

                            sqlCmd.CommandText =
                            @"DELETE FROM LastResultInfo
                                  WHERE 集計日 = @集計日 and 種別=@種別;";


                            await sqlCmd.ExecuteNonQueryAsync();
                        }
                        reader.Close();
                    }
                }


                sqlCmd.Parameters.Clear();
                sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));
                sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());
                sqlCmd.CommandText =
                                @"INSERT INTO LastResult( 
                                '種別' , '集計日' , 'ID' , 
                                'タイトル' ,'総合ランク' , 'ポイント' ,
                                '再生数' , 'コメント数' , 'マイリスト数' ,
                                '累計再生数' , '累計コメント数' , '累計マイリスト数' , 'JSON'  ,'いいね数', '累計いいね数')
                                  VALUES( 
                                @種別 , @集計日 , @ID , 
                                @タイトル ,@総合ランク , @ポイント ,
                                @再生数 , @コメント数 , @マイリスト数 ,
                                @累計再生数 , @累計コメント数 , @累計マイリスト数 , @JSON ,@いいね数, @累計いいね数);";


                foreach (var rank in rankingList)
                {
                    // Microsoft.Data.Sqlite は同名パラメータの重複追加を許さないため、ループ内でクリアして再設定する
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));
                    sqlCmd.Parameters.AddWithValue("@種別", this.Mode.ToString());
                    sqlCmd.Parameters.AddWithValue("@ID", rank.ID);
                    sqlCmd.Parameters.AddWithValue("@タイトル", rank.Title);
                    sqlCmd.Parameters.AddWithValue("@総合ランク", rank.RankTotal);
                    sqlCmd.Parameters.AddWithValue("@ポイント", rank.PointTotal);
                    sqlCmd.Parameters.AddWithValue("@再生数", rank.CountPlay);
                    sqlCmd.Parameters.AddWithValue("@コメント数", rank.CountComment);
                    sqlCmd.Parameters.AddWithValue("@マイリスト数", rank.CountMyList);
                    sqlCmd.Parameters.AddWithValue("@累計再生数", rank.CountPlayTotal);
                    sqlCmd.Parameters.AddWithValue("@累計コメント数", rank.CountCommentTotal);
                    sqlCmd.Parameters.AddWithValue("@累計マイリスト数", rank.CountMyListTotal);
                    // 肥大化対策：JSON列は空文字で登録する（読み側はJSON列を使わないため動作不変）
                    sqlCmd.Parameters.AddWithValue("@JSON", string.Empty);
                    sqlCmd.Parameters.AddWithValue("@いいね数", rank.CountLike);
                    sqlCmd.Parameters.AddWithValue("@累計いいね数", rank.CountLikeTotal);

                    await sqlCmd.ExecuteNonQueryAsync();
                }
                sqlCmd.CommandText =
                @"INSERT INTO LastResultInfo( 
                                '種別' , '集計日' , 'XML'  )
                                  VALUES( 
                                @種別 , @集計日 , @XML );";

                sqlCmd.Parameters.AddWithValue("@XML",Config.GetInstance().GetXMLString());
                await sqlCmd.ExecuteNonQueryAsync();

            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Historyテーブルを更新する
        /// </summary>
        /// <param name="dbCtrl"></param>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        protected async Task<bool> updateHistory(SqliteCommand sqlCmd, IReadOnlyList<Ranking> rankingList)
        {
            var config = Config.GetInstance();
            try
            {
                // すでに登録済みなのかチェックする
                sqlCmd.CommandText =
                    @"SELECT * FROM History
                 WHERE 集計日 = @集計日
                 LIMIT 1;";

                sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));

                using (var reader = sqlCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        //すでに集計済→上書きのため削除
                        reader.Close();

                        sqlCmd.CommandText =
                            @"DELETE FROM History
                                  WHERE 集計日 = @集計日;";
                        await sqlCmd.ExecuteNonQueryAsync();
                    }
                }

                sqlCmd.CommandText =
                                @"INSERT INTO History( 
                                 '集計日' , 'ID', '総合ランク',
                                 'ポイント' , '再生数' ,'コメント数' , 'マイリスト数','いいね数')
                                  VALUES( 
                                 @集計日 , @ID, @総合ランク,
                                 @ポイント , @再生数 ,@コメント数 , @マイリスト数 , @いいね数)";

                int movieCnt = 0;

                foreach (var rank in rankingList)
                {
                    // Microsoft.Data.Sqlite は同名パラメータの重複追加を許さないため、ループ内でクリアして再設定する
                    sqlCmd.Parameters.Clear();
                    sqlCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.syuukeiBi, false));
                    sqlCmd.Parameters.AddWithValue("@ID", rank.ID);
                    sqlCmd.Parameters.AddWithValue("@総合ランク", rank.RankTotal);
                    sqlCmd.Parameters.AddWithValue("@ポイント", rank.PointTotal);
                    sqlCmd.Parameters.AddWithValue("@再生数", rank.CountPlay);
                    sqlCmd.Parameters.AddWithValue("@コメント数", rank.CountComment);
                    sqlCmd.Parameters.AddWithValue("@マイリスト数", rank.CountMyList);
                    sqlCmd.Parameters.AddWithValue("@いいね数", rank.CountLike);
                    await sqlCmd.ExecuteNonQueryAsync();

                    movieCnt++;
                    if (movieCnt >= config.Rank)
                    {
                        //設定された紹介枠まで登録する（長期枠は考慮しない）
                        break;
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
