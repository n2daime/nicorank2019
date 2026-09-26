using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using nicorankLib.Analyze.model;

namespace nicorankLib.Util
{
    /// <summary>
    /// 手動DB最適化（メンテナンスタブ・Issue #32）の対象DB1件分。
    /// 表示名とパスの対であり、UIのチェックボックス行と1対1に対応する。
    /// </summary>
    public class DbOptimizeTarget
    {
        public string DisplayName;
        public string DbPath;

        public DbOptimizeTarget(string displayName, string dbPath)
        {
            DisplayName = displayName;
            DbPath = dbPath;
        }
    }

    /// <summary>
    /// 手動DB最適化1件分の実行結果。
    /// ファイル不在時はスキップ扱い（Executed=false・Success=true）とし、呼び出し側はサイズ欄に「なし」と出す。
    /// なぜ失敗にしないのか：Dailylog.db等は未実行モードでは存在しないのが正常であり、不在自体は異常ではないため。
    /// </summary>
    public class DbOptimizeResult
    {
        public string DbPath;
        public bool Executed;
        public bool Success;
        public long SizeBefore;
        public long SizeAfter;
        // pruneで削除した行数の合計（VACUUMのみのDBは0）。StatusLogへの報告用。
        public long DeletedRows;
        public string ErrorMessage;
    }

    /// <summary>
    /// 手動DB最適化の実行本体。UIを持たず、単体テストから直接呼べる。
    /// 処理内容はIssue #32の壁打ちコメント準拠であり、DBごとに異なる（素のVACUUMだけではない）。
    /// 順序はDROP→DELETE→VACUUM。VACUUMはトランザクション不可のため、BEGIN/COMMITで包まない（移行時と同一の理由）。
    /// </summary>
    public static class DbOptimizer
    {
        // ApiXML.db／Dailylog.db は DB.cs に定数がないため、既存各所と同一値をここに持つ。
        // 値がずれると最適化対象と集計の参照先が食い違うため、変更時は NicoApi／ApiXmlCacheImporter／TyukanAnalyze と同時に直すこと。
        public const string ApiXmlDbPath = @"DB/ApiXML.db";
        public const string DailylogDbPath = "DB/Dailylog.db";

        // pruneの保持期間（年）。壁打ちの「1年より前」に相当する。
        private const int RetentionYears = 1;

        // NicoranHistoryのprune境界。総合ランク1001位以下（1000位ちょうどは残す）が削除対象。仕様値のため定数化する。
        public const long WeeklyRankKeepLimit = 1000;

        // ファイルサイズ表示の進数。1024固定（Windowsのエクスプローラ表示と合わせるため）。
        public const long BytesPerUnit = 1024;

        // UIの表示順（集計への影響が大きい順）。チェック既定ON/OFFはUI側（Designer）が持つ。
        public static IReadOnlyList<DbOptimizeTarget> GetDefaultTargets()
        {
            return new List<DbOptimizeTarget>
            {
                new DbOptimizeTarget("LogOfficial.db", DB.LOG_OFFICEIAL),
                new DbOptimizeTarget("NicoranHistory.db", DB.NiCORAN_HISTORY),
                new DbOptimizeTarget("ApiXML.db", ApiXmlDbPath),
                new DbOptimizeTarget("Dailylog.db", DailylogDbPath),
            };
        }

        /// <summary>
        /// prune境界の「1年前」をyyyyMMdd整数で求める。起点は実行日（ローリング計算。壁打ち申送り通り）。
        /// 境界当日を含む（集計日 &lt;= 境界が削除対象）のため、厳密な1年保持ではなく「1年前の日も消す」点に注意。
        /// </summary>
        public static long CutoffOneYearAgo(DateTime today)
        {
            return long.Parse(today.AddYears(-RetentionYears).ToString("yyyyMMdd"));
        }

        /// <summary>
        /// 指定DBを1件最適化する。DBごとに壁打ち準拠の処理を行い、最後にVACUUMする。
        /// 実行前後のファイルサイズ（.db本体のみ。-wal/-shm合算はしない）を結果に載せる。
        /// </summary>
        public static DbOptimizeResult Optimize(string dbPath)
        {
            return Optimize(dbPath, DateTime.Today);
        }

        /// <summary>
        /// 日付指定付きの最適化（単体テストで境界日を固定するため）。本番は <see cref="Optimize(string)"/> を使う。
        /// </summary>
        public static DbOptimizeResult Optimize(string dbPath, DateTime today)
        {
            var result = new DbOptimizeResult { DbPath = dbPath };
            if (!File.Exists(dbPath))
            {
                result.Executed = false;
                result.Success = true;
                result.ErrorMessage = "ファイルが存在しないためスキップしました";
                StatusLog.WriteLine($"{dbPath}が存在しないためスキップしました");
                return result;
            }
            result.SizeBefore = new FileInfo(dbPath).Length;
            try
            {
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (!dbCtrl.IsOpen && !dbCtrl.Open(dbPath))
                    {
                        throw new InvalidOperationException("DBを開けませんでした");
                    }
                    using (var cmd = dbCtrl.Connection.CreateCommand())
                    {
                        // DBごとのprune。対象外のDB（LogOfficial・未知のパス）は何もしない。
                        result.DeletedRows = PruneByDb(cmd, dbPath, CutoffOneYearAgo(today));
                        cmd.CommandText = "VACUUM;";
                        cmd.ExecuteNonQuery();
                    }
                }
                result.SizeAfter = new FileInfo(dbPath).Length;
                result.Executed = true;
                result.Success = true;
                StatusLog.WriteLine($"{dbPath}を最適化しました（削除 {result.DeletedRows}行・{FormatFileSize(result.SizeBefore)} → {FormatFileSize(result.SizeAfter)}）");
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                result.Executed = true;
                result.Success = false;
                result.ErrorMessage = ex.Message;
                try { result.SizeAfter = new FileInfo(dbPath).Length; } catch { result.SizeAfter = result.SizeBefore; }
            }
            return result;
        }

        /// <summary>
        /// パスでDBを判別し、壁打ち準拠のpruneを行う。削除行数を返す。
        /// </summary>
        private static long PruneByDb(SqliteCommand cmd, string dbPath, long cutoff)
        {
            if (IsSamePath(dbPath, ApiXmlDbPath))
            {
                return PruneApiXml(cmd, cutoff);
            }
            if (IsSamePath(dbPath, DailylogDbPath))
            {
                return PruneDailylog(cmd);
            }
            if (IsSamePath(dbPath, DB.NiCORAN_HISTORY))
            {
                return PruneNicoranHistory(cmd, cutoff);
            }
            return 0;
        }

        private static bool IsSamePath(string a, string b)
        {
            // Windowsのファイルパスとして比較する（大文字小文字を区別しない）。
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ApiXML.db：未使用のIDConvertをDROPし、1年以上未更新のNicovideoThumb行を削除する。
        /// DROPはIF EXISTSで冪等にする（2回目以降は何も起きない）。
        /// 削除行は再取得で自己回復する（中間集計のisLocalOnly時は古いロックタグ補完が素通りするが、対象は直近中心のため実害小。壁打ち確認済み）。
        /// </summary>
        private static long PruneApiXml(SqliteCommand cmd, long cutoff)
        {
            cmd.CommandText = "DROP TABLE IF EXISTS IDConvert;";
            cmd.ExecuteNonQuery();
            // 取得日はyyyyMMddの文字列またはINTEGERのいずれでも格納されうるが、
            // どちらも数値比較できるため整数パラメータで比較する（ApiXmlCacheImporterの比較と同一考え）。
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@取得日", cutoff);
            cmd.CommandText = "DELETE FROM NicovideoThumb WHERE 取得日 < @取得日;";
            long deleted = cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            return deleted;
        }

        /// <summary>
        /// Dailylog.db：中間集計の日別キャッシュを全削除する。
        /// DROP禁止（本番コードにCREATE経路がなく、表を消すと再作成されないため）。再集計で自己回復する。
        /// </summary>
        private static long PruneDailylog(SqliteCommand cmd)
        {
            cmd.CommandText = "DELETE FROM Dailylog;";
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// NicoranHistory.db：Weeklyの1年以上前の下位（1001位以下）だけ削除する。
        /// SP削除はしない（Ver0移行で削除済みのため毎回0件の空振りになる。壁打ち確認済み）。
        /// LastResultInfoには触れない（上位1000行が残る日の設定XMLを守るため）。
        /// 種別はパラメータ化する（ダブルクォート直書き回避。壁打ち申送り通り）。
        /// 境界：1000位ちょうどは残し、1年前当日を含む（&lt;=）。
        /// </summary>
        private static long PruneNicoranHistory(SqliteCommand cmd, long cutoff)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@種別", EAnalyzeMode.Weekly.ToString());
            cmd.Parameters.AddWithValue("@集計日", cutoff);
            cmd.Parameters.AddWithValue("@総合ランク", WeeklyRankKeepLimit);
            cmd.CommandText = "DELETE FROM LastResult WHERE 種別 = @種別 AND 総合ランク > @総合ランク AND 集計日 <= @集計日;";
            long deleted = cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            return deleted;
        }

        /// <summary>
        /// バイト数を人間可読にする（例：1536 → "1.50 KB"）。負値は未取得扱いの "—" とする。
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 0)
            {
                return "—";
            }
            if (bytes < BytesPerUnit)
            {
                return $"{bytes} B";
            }
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= BytesPerUnit && unit < units.Length - 1)
            {
                size /= BytesPerUnit;
                unit++;
            }
            return $"{size:F2} {units[unit]}";
        }
    }
}
