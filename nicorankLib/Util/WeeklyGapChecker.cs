using nicorankLib.Analyze.model;
using System;
using System.Collections.Generic;
using System.IO;

namespace nicorankLib.Util
{
    /// <summary>
    /// 週刊集計の抜けチェック結果1件分。
    /// 期待週・実績・抜け・メンテ除外を分けて持ち、UI側が表示と復旧判断に使う。
    /// なぜ分けるか：抜けとメンテ除外を混ぜると「直すべき週」と「正常な週」の区別が付かず、
    /// 誤ってメンテ週の再集計を促す案内になるためである。
    /// </summary>
    public class GapCheckResult
    {
        /// <summary>期待される週刊集計日（月曜日の一覧・昇順）。</summary>
        public List<DateTime> Expected = new List<DateTime>();
        /// <summary>抜けている週刊集計日（昇順）。ここが空なら抜けなし。</summary>
        public List<DateTime> Missing = new List<DateTime>();
        /// <summary>メンテナンス日のため除外した期待週（昇順）。正常であり対処不要。</summary>
        public List<DateTime> MaintenanceSkipped = new List<DateTime>();
        /// <summary>全期間モードで実行したかどうか（既定は直近3か月）。</summary>
        public bool FullPeriod;
        /// <summary>DBファイル不在などでチェック自体ができなかった場合の理由（成功時はnull）。</summary>
        public string ErrorMessage;
    }

    /// <summary>
    /// 週刊集計の抜けチェック本体（Issue #45）。
    /// 判定材料は NicoranHistory.db の LastResult（種別=Weekly）の集計日一覧であり、
    /// LogOfficial.db の RankingDate はメンテナンス除外のためだけに使う。
    /// なぜ LastResult か：集計実行の存在証明として最も直接的だからである。
    /// LogOfficial.Ranking は公式取得の記録であり、自前の集計実行とは一致しない場合があるため副資料に留める。
    /// UIを持たず、単体テストから直接呼べる。DbOptimizer と同型の static 構成である。
    /// </summary>
    public static class WeeklyGapChecker
    {
        /// <summary>
        /// 通常チェックの遡及週数。約3か月分に相当する13週。
        /// なぜ13週か：全期間走査の処理負荷を避けるためと、LogOfficial が直近1年しか持たないため
        /// 古すぎる検出が対処不能になるからである。仕様値のため定数化し、変える場合は Issue で合意する。
        /// </summary>
        public const int DefaultLookbackWeeks = 13;

        /// <summary>
        /// 週刊集計日の曜日。集計は月曜起点（SabunReader の基準日は月曜のみ変更可）のため固定する。
        /// </summary>
        private const DayOfWeek WeeklyDay = DayOfWeek.Monday;

        /// <summary>
        /// 期待される週刊集計日（月曜日）の一覧を求める。純粋処理のため単体テストで直接検証する。
        /// 直近の月曜日（当日が月曜なら当日）から過去へ週数分さかのぼり、昇順で返す。
        /// </summary>
        public static List<DateTime> GetExpectedMondays(DateTime today, int weeks)
        {
            var result = new List<DateTime>();
            if (weeks <= 0)
            {
                return result;
            }
            // 当日が月曜なら当日、それ以外は直前の月曜を求める。
            // DayOfWeek は日曜=0〜土曜=6 のため、月曜起点の差分に直してから戻す。
            int diff = ((int)today.DayOfWeek - (int)WeeklyDay + 7) % 7;
            DateTime latest = today.Date.AddDays(-diff);
            for (int i = weeks - 1; i >= 0; i--)
            {
                result.Add(latest.AddDays(-7 * i));
            }
            return result;
        }

        /// <summary>
        /// 全期間モードの期待週を求める。実績の最小週の月曜から直近の月曜まで毎週列挙する。
        /// なぜ実績起点か：2019年起点などの固定開始日を持つと、運用開始前の週まで抜け扱いになるためである。
        /// 実績が空の場合は通常モードの週数分にフォールバックする（空DBでも何も検出しないより正直なため）。
        /// </summary>
        public static List<DateTime> GetExpectedMondaysFullPeriod(DateTime today, ICollection<DateTime> actual)
        {
            int diff = ((int)today.DayOfWeek - (int)WeeklyDay + 7) % 7;
            DateTime latest = today.Date.AddDays(-diff);
            if (actual == null || actual.Count == 0)
            {
                return GetExpectedMondays(today, DefaultLookbackWeeks);
            }
            DateTime min = DateTime.MaxValue;
            foreach (var d in actual)
            {
                if (d.Date < min)
                {
                    min = d.Date;
                }
            }
            int minDiff = ((int)min.DayOfWeek - (int)WeeklyDay + 7) % 7;
            DateTime first = min.AddDays(-minDiff);
            var result = new List<DateTime>();
            for (DateTime d = first; d <= latest; d = d.AddDays(7))
            {
                result.Add(d);
            }
            return result;
        }

        /// <summary>
        /// 期待週と実績週から抜けを求める。純粋処理のため単体テストで直接検証する。
        /// 実績にある週は正常、メンテ週は除外、それ以外を抜けとする。
        /// </summary>
        public static List<DateTime> FindMissing(
            ICollection<DateTime> expected,
            ICollection<DateTime> actual,
            Func<DateTime, bool> isMaintenance,
            out List<DateTime> maintenanceSkipped)
        {
            maintenanceSkipped = new List<DateTime>();
            var missing = new List<DateTime>();
            if (expected == null)
            {
                return missing;
            }
            var actualSet = new HashSet<DateTime>();
            if (actual != null)
            {
                foreach (var d in actual)
                {
                    actualSet.Add(d.Date);
                }
            }
            foreach (var day in expected)
            {
                DateTime date = day.Date;
                if (actualSet.Contains(date))
                {
                    continue;
                }
                bool maint = false;
                try
                {
                    maint = isMaintenance != null && isMaintenance(date);
                }
                catch
                {
                    // メンテ判定の失敗は除外不能として扱う（抜け側に倒す）。
                    // なぜ抜け側か：除外側に倒すと本当の抜けを見逃し、長期判定の欠けが残るためである。
                    maint = false;
                }
                if (maint)
                {
                    maintenanceSkipped.Add(date);
                }
                else
                {
                    missing.Add(date);
                }
            }
            return missing;
        }

        /// <summary>
        /// NicoranHistory.db から Weekly の集計日一覧を読む。
        /// 集計日は yyyyMMdd の8桁（文字列束縛と数値束縛が混在するが固定長のため順序は一致する）で読む。
        /// 読み取り失敗行は素通りする。なぜ中断しないか：1行の崩れで全体の検出を止めると、
        /// 本当の抜けを見逃す方が害が大きいためである（Issue #40 と同一の考え方）。
        /// </summary>
        public static HashSet<DateTime> ReadWeeklyDates(ISQLiteCtrl historyCtrl)
        {
            var result = new HashSet<DateTime>();
            try
            {
                if (historyCtrl == null || !historyCtrl.IsOpen)
                {
                    return result;
                }
                using (var cmd = historyCtrl.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT 集計日 FROM LastResult WHERE 種別 = @種別 ORDER BY 集計日;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@種別", EAnalyzeMode.Weekly.ToString());
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try
                            {
                                object value = reader["集計日"];
                                if (value == null || value == DBNull.Value)
                                {
                                    continue;
                                }
                                result.Add(DateConvert.String2Time(value.ToString(), false).Date);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
            return result;
        }

        /// <summary>
        /// LogOfficial.db の RankingDate でメンテ日かを確認する。
        /// RankingHistory.CheckMaintananceDay と同一の判定（メンテナンス&gt;0）であり、
        /// 日次更新のしおりと基準をずらさないためにこちらを使う。Ranking 本体の有無では代用しない。
        /// なぜ代用しないか：メンテ日と単なる取得漏れの区別が付かないためである。
        /// </summary>
        public static bool IsMaintenance(ISQLiteCtrl officialCtrl, DateTime day)
        {
            try
            {
                if (officialCtrl == null || !officialCtrl.IsOpen)
                {
                    return false;
                }
                using (var cmd = officialCtrl.Connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT メンテナンス FROM RankingDate WHERE 集計日 = @集計日 LIMIT 1;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(day, false));
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return Convert.ToInt64(reader["メンテナンス"].ToString()) > 0;
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

        /// <summary>
        /// 開済み接続に対するチェック本体（ファイルを開かないため単体テストで直接検証する）。
        /// </summary>
        public static GapCheckResult Check(
            ISQLiteCtrl historyCtrl,
            ISQLiteCtrl officialCtrl,
            DateTime today,
            int weeks,
            bool fullPeriod)
        {
            var result = new GapCheckResult { FullPeriod = fullPeriod };
            try
            {
                var actual = ReadWeeklyDates(historyCtrl);
                List<DateTime> expected = fullPeriod
                    ? GetExpectedMondaysFullPeriod(today, actual)
                    : GetExpectedMondays(today, weeks);
                result.Expected = expected;
                List<DateTime> skipped;
                result.Missing = FindMissing(expected, actual, d => IsMaintenance(officialCtrl, d), out skipped);
                result.MaintenanceSkipped = skipped;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// ファイルパス指定のチェック本体。UI側はこの口を使う。
        /// 読取のみ・短時間で開閉し、呼び出し側の集計接続と競合させない。
        /// 履歴DB不在時はチェック不能として理由を返す（失敗にせず理由付きで返すのは、
        /// 不在自体は #36 の自動取得で解消できる正常系のためである）。
        /// </summary>
        public static GapCheckResult Check(string historyDbPath, string officialDbPath, DateTime today, int weeks, bool fullPeriod)
        {
            var result = new GapCheckResult { FullPeriod = fullPeriod };
            if (!File.Exists(historyDbPath))
            {
                result.ErrorMessage = "NicoranHistory.db がありません。ベースライン取得後に再実行してください";
                return result;
            }
            try
            {
                HashSet<DateTime> actual;
                using (var historyCtrl = new SQLiteCtrl())
                {
                    if (!historyCtrl.Open(historyDbPath))
                    {
                        result.ErrorMessage = "NicoranHistory.db を開けませんでした";
                        return result;
                    }
                    actual = ReadWeeklyDates(historyCtrl);
                }
                List<DateTime> expected = fullPeriod
                    ? GetExpectedMondaysFullPeriod(today, actual)
                    : GetExpectedMondays(today, weeks);
                result.Expected = expected;
                var skipped = new List<DateTime>();
                var missing = new List<DateTime>();
                var actualSet = new HashSet<DateTime>(actual);
                if (File.Exists(officialDbPath))
                {
                    using (var officialCtrl = new SQLiteCtrl())
                    {
                        if (officialCtrl.Open(officialDbPath))
                        {
                            List<DateTime> s;
                            missing = FindMissing(expected, actualSet, d => IsMaintenance(officialCtrl, d), out s);
                            skipped = s;
                        }
                        else
                        {
                            List<DateTime> s;
                            missing = FindMissing(expected, actualSet, null, out s);
                            skipped = s;
                        }
                    }
                }
                else
                {
                    // 公式DB不在時はメンテ除外なしで検出する。除外不能を理由に中断すると、
                    // 本当の抜けの検出自体が止まるためである。公式DBは #36 で自動取得される。
                    List<DateTime> s;
                    missing = FindMissing(expected, actualSet, null, out s);
                    skipped = s;
                }
                result.Missing = missing;
                result.MaintenanceSkipped = skipped;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                result.ErrorMessage = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 抜け週リストの表示用整形（例：2026-09-21、2026-10-05）。
        /// なぜ yyyy-MM-dd か：StatusLog とダイアログの両方で日付の列挙が必須であり、
        /// yyyyMMdd のままでは人間が週を数えにくいためである。
        /// </summary>
        public static string FormatMissing(ICollection<DateTime> missing)
        {
            if (missing == null)
            {
                return string.Empty;
            }
            var parts = new List<string>();
            foreach (var d in missing)
            {
                parts.Add(d.ToString("yyyy-MM-dd"));
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("、", parts);
        }
    }
}
