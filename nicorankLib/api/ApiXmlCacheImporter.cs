using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace nicorankLib.api
{
    /// <summary>
    /// 週刊の動画情報キャッシュ（ApiXML.db）の受け渡しを担う。
    /// oldlogが週刊JSONに出たID全部を一括取得して日付フォルダへ置き、2019側が取り込む。
    /// なぜ受け渡すか: 取得時点をoldlog実行の1点に固定すれば、2019側の再実行や実行日ずれで
    /// 動画情報の有無が変わらず、欠落そのものを減らせるから（Issue #40）。
    /// 取込に失敗しても本地のまま流し、中断しない（キャッシュのため）。
    /// </summary>
    public class ApiXmlCacheImporter
    {
        /// <summary>日付フォルダに置くファイル名（JSON群と同じ場所）</summary>
        public const string CacheFileName = "ApiXML.db";

        /// <summary>
        /// NicovideoThumb表がなければ作る。配布DB・取込先DB・テストDBのいずれにも使える。
        /// なぜIF NOT EXISTSか: 既存キャッシュを壊さずに確保だけ行うため。
        /// </summary>
        public static void EnsureNicovideoThumbTable(ISQLiteCtrl dbCtrl)
        {
            using (var aCmd = dbCtrl.Connection.CreateCommand())
            {
                aCmd.CommandText =
                    @"CREATE TABLE IF NOT EXISTS NicovideoThumb (
                        取得日 INTEGER,
                        ID TEXT,
                        Status INTEGER,
                        XML TEXT
                    )";
                aCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 週刊の日付フォルダから ApiXML.db を落として本地DBへ取り込む。失敗時は警告だけで続ける。
        /// </summary>
        /// <param name="analyzeDay">週刊の集計日（JsonReaderWeeklyと同一の日付書式でフォルダを決める）</param>
        /// <returns>常にtrue（取込失敗でも中断しない）</returns>
        public virtual bool ImportWeeklyCache(DateTime analyzeDay)
        {
            string tempPath = null;
            try
            {
                string baseURL = Config.GetInstance().URL_JSON_TARGET;
                string targetURL = string.Format(baseURL, "weekly", analyzeDay.ToString("yyyy-MM-dd"));
                string downloadUrl = targetURL + CacheFileName;

                tempPath = Path.Combine(Path.GetTempPath(), $"nicorank_{CacheFileName}");
                StatusLog.WriteLine($"週刊の動画情報キャッシュを取得します...");
                if (!InternetUtil.FileDownLoad(downloadUrl, tempPath))
                {
                    //運搬ファイルがなければ本地のまま流す（旧運用との互換）
                    StatusLog.WriteLine("動画情報キャッシュが見つからないため、本地のキャッシュで続けます");
                    return true;
                }
                int merged = MergeCacheFile(tempPath);
                StatusLog.WriteLine($"動画情報キャッシュを{merged}件取り込みました");
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                StatusLog.WriteLine("動画情報キャッシュの取込に失敗しました。本地のキャッシュで続けます");
            }
            finally
            {
                if (tempPath != null)
                {
                    try { if (File.Exists(tempPath)) { File.Delete(tempPath); } } catch { }
                }
            }
            return true;
        }

        /// <summary>
        /// 指定ファイルのNicovideoThumb行を本地DBへ取り込む。
        /// IDごとに運搬側の取得日が本地の最新より新しい（または本地にない）場合だけ置き換える。
        /// なぜ新しい方を残すか: 本地にしかない古い貯金を消さないため。上書きコピーはしない。
        /// </summary>
        /// <param name="sourceDbPath">運搬ファイルのパス</param>
        /// <param name="localDbPath">本地DBのパス（省略時は規定のDB/ApiXML.db。テストで一時DBを指定する）</param>
        /// <returns>取り込んだ件数</returns>
        public virtual int MergeCacheFile(string sourceDbPath, string localDbPath = null)
        {
            string localPath = localDbPath ?? DATA_SOURCE;
            int merged = 0;
            using (var sourceCtrl = new SQLiteCtrl())
            {
                if (!sourceCtrl.Open(sourceDbPath))
                {
                    return 0;
                }
                var rows = new List<CacheRow>();
                using (var aCmd = sourceCtrl.Connection.CreateCommand())
                {
                    aCmd.CommandText = @"SELECT 取得日, ID, Status, XML FROM NicovideoThumb";
                    using (var reader = aCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new CacheRow
                            {
                                GetDate = reader["取得日"].ToString(),
                                ID = reader["ID"].ToString(),
                                Status = Convert.ToInt64(reader["Status"]),
                                Xml = reader["XML"].ToString()
                            });
                        }
                    }
                }
                sourceCtrl.Close();

                using (var localCtrl = new SQLiteCtrl())
                {
                    if (!localCtrl.Open(localPath))
                    {
                        return 0;
                    }
                    EnsureNicovideoThumbTable(localCtrl);
                    using (var aCmd = localCtrl.Connection.CreateCommand())
                    {
                        const int commitBatch = 5000;
                        int rowCounter = 0;
                        using (var transaction = (SqliteTransaction)localCtrl.Connection.BeginTransaction())
                        {
                            aCmd.Transaction = transaction;
                            try
                            {
                                foreach (var row in rows)
                                {
                                    if (IsSourceNewer(aCmd, row))
                                    {
                                        ReplaceRow(aCmd, row);
                                        merged++;
                                    }
                                    rowCounter++;
                                    if ((rowCounter % commitBatch) == 0)
                                    {
                                        aCmd.Transaction.Commit();
                                        aCmd.Transaction = (SqliteTransaction)localCtrl.Connection.BeginTransaction();
                                    }
                                }
                                aCmd.Transaction.Commit();
                            }
                            catch
                            {
                                try { aCmd.Transaction?.Rollback(); } catch { }
                                throw;
                            }
                        }
                    }
                    localCtrl.Close();
                }
            }
            return merged;
        }

        private const string DATA_SOURCE = @"DB/ApiXML.db";

        protected class CacheRow
        {
            public string GetDate;
            public string ID;
            public long Status;
            public string Xml;
        }

        /// <summary>
        /// 運搬行が本地より新しいかどうか。本地に行がなければtrue。
        /// 取得日はyyyyMMdd文字列またはINTEGERのいずれでも辞書式・数値式で比較できるため文字列比較する。
        /// </summary>
        protected static bool IsSourceNewer(SqliteCommand aCmd, CacheRow row)
        {
            aCmd.Parameters.Clear();
            aCmd.CommandText = @"SELECT MAX(取得日) AS 取得日 FROM NicovideoThumb WHERE ID = @ID";
            aCmd.Parameters.AddWithValue("@ID", row.ID);
            using (var reader = aCmd.ExecuteReader())
            {
                if (reader.Read() && reader["取得日"] != DBNull.Value)
                {
                    string localDate = reader["取得日"].ToString();
                    return string.Compare(row.GetDate, localDate, StringComparison.Ordinal) > 0;
                }
            }
            return true;
        }

        protected static void ReplaceRow(SqliteCommand aCmd, CacheRow row)
        {
            //Microsoft.Data.Sqlite は同名パラメータの重複追加を許さないため、都度クリアして再設定する
            aCmd.Parameters.Clear();
            aCmd.CommandText = @"DELETE FROM NicovideoThumb WHERE ID = @ID";
            aCmd.Parameters.AddWithValue("@ID", row.ID);
            aCmd.ExecuteNonQuery();

            aCmd.Parameters.Clear();
            aCmd.CommandText =
                @"INSERT INTO NicovideoThumb(取得日, ID, Status, XML)
                  VALUES(@取得日, @ID, @Status, @XML)";
            aCmd.Parameters.AddWithValue("@取得日", row.GetDate);
            aCmd.Parameters.AddWithValue("@ID", row.ID);
            aCmd.Parameters.AddWithValue("@Status", row.Status);
            aCmd.Parameters.AddWithValue("@XML", row.Xml);
            aCmd.ExecuteNonQuery();
        }
    }
}
