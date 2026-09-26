using System;
using System.Collections.Generic;
using System.IO;
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
        public string ErrorMessage;
    }

    /// <summary>
    /// 手動DB最適化（VACUUM）の実行本体。UIを持たず、単体テストから直接呼べる。
    /// 移行時のVACUUM（ResultHistory／RankingHistory）と同一手順（SQLiteCtrlで開いて VACUUM; を1発）で行う。
    /// VACUUMはトランザクション不可のため、BEGIN/COMMITで包まない（移行時と同一の理由）。
    /// </summary>
    public static class DbOptimizer
    {
        // ApiXML.db／Dailylog.db は DB.cs に定数がないため、既存各所と同一値をここに持つ。
        // 値がずれると最適化対象と集計の参照先が食い違うため、変更時は NicoApi／ApiXmlCacheImporter／TyukanAnalyze と同時に直すこと。
        public const string ApiXmlDbPath = @"DB/ApiXML.db";
        public const string DailylogDbPath = "DB/Dailylog.db";

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
        /// 指定DBを1件最適化する。実行前後のファイルサイズ（.db本体のみ。-wal/-shm合算はしない）を結果に載せる。
        /// </summary>
        public static DbOptimizeResult Optimize(string dbPath)
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
                        cmd.CommandText = "VACUUM;";
                        cmd.ExecuteNonQuery();
                    }
                }
                result.SizeAfter = new FileInfo(dbPath).Length;
                result.Executed = true;
                result.Success = true;
                StatusLog.WriteLine($"{dbPath}を最適化しました（{FormatFileSize(result.SizeBefore)} → {FormatFileSize(result.SizeAfter)}）");
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
