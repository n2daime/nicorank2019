using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// 週刊集計の抜けチェックテスト（Issue #45）。
    /// 純粋処理（期待週・抜け判定）は直接検証し、DB読取はインメモリSQLiteで検証する。
    /// なぜ分けるか：日付計算の誤りとSQLの誤りを切り分けるためである。
    /// </summary>
    [TestClass]
    public class UnitTestWeeklyGapChecker
    {
        [TestMethod]
        public void GetExpectedMondays_MondayItself_IncludesToday()
        {
            // 2026-09-28 は月曜日であり、当日自体が期待週に含まれる。
            var result = WeeklyGapChecker.GetExpectedMondays(new DateTime(2026, 9, 28), 3);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(new DateTime(2026, 9, 14), result[0]);
            Assert.AreEqual(new DateTime(2026, 9, 21), result[1]);
            Assert.AreEqual(new DateTime(2026, 9, 28), result[2]);
        }

        [TestMethod]
        public void GetExpectedMondays_Saturday_BackToLastMonday()
        {
            // 2026-10-03 は土曜であり、直近の月曜 2026-09-28 からさかのぼる。
            var result = WeeklyGapChecker.GetExpectedMondays(new DateTime(2026, 10, 3), 2);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(new DateTime(2026, 9, 21), result[0]);
            Assert.AreEqual(new DateTime(2026, 9, 28), result[1]);
        }

        [TestMethod]
        public void GetExpectedMondays_Sunday_BackToLastMonday()
        {
            // 日曜は週の終わりであり、6日前の月曜が直近になる。
            var result = WeeklyGapChecker.GetExpectedMondays(new DateTime(2026, 10, 4), 1);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new DateTime(2026, 9, 28), result[0]);
        }

        [TestMethod]
        public void GetExpectedMondays_ZeroWeeks_ReturnsEmpty()
        {
            var result = WeeklyGapChecker.GetExpectedMondays(new DateTime(2026, 10, 3), 0);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void FindMissing_NoGap_ReturnsEmpty()
        {
            var expected = new List<DateTime> { new DateTime(2026, 9, 21), new DateTime(2026, 9, 28) };
            var actual = new List<DateTime> { new DateTime(2026, 9, 21), new DateTime(2026, 9, 28) };

            List<DateTime> skipped;
            var missing = WeeklyGapChecker.FindMissing(expected, actual, null, out skipped);

            Assert.AreEqual(0, missing.Count);
            Assert.AreEqual(0, skipped.Count);
        }

        [TestMethod]
        public void FindMissing_OneGap_ReturnsTheWeek()
        {
            // 担当Cの長期空きの再現であり、中間の週だけ抜ける。
            var expected = new List<DateTime>
            {
                new DateTime(2026, 9, 14), new DateTime(2026, 9, 21), new DateTime(2026, 9, 28)
            };
            var actual = new List<DateTime> { new DateTime(2026, 9, 14), new DateTime(2026, 9, 28) };

            List<DateTime> skipped;
            var missing = WeeklyGapChecker.FindMissing(expected, actual, null, out skipped);

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual(new DateTime(2026, 9, 21), missing[0]);
        }

        [TestMethod]
        public void FindMissing_MaintenanceWeek_Excluded()
        {
            // メンテ週は集計不能が正常であり、抜けに数えない。
            var expected = new List<DateTime> { new DateTime(2026, 9, 21), new DateTime(2026, 9, 28) };
            var actual = new List<DateTime> { new DateTime(2026, 9, 28) };

            List<DateTime> skipped;
            var missing = WeeklyGapChecker.FindMissing(
                expected, actual, d => d == new DateTime(2026, 9, 21), out skipped);

            Assert.AreEqual(0, missing.Count);
            Assert.AreEqual(1, skipped.Count);
            Assert.AreEqual(new DateTime(2026, 9, 21), skipped[0]);
        }

        [TestMethod]
        public void GetExpectedMondays_YearSpan_Has52Weeks()
        {
            // 1年オプションは52週分を列挙する。先頭と末尾の間隔は51週（364日の前日）になる。
            var result = WeeklyGapChecker.GetExpectedMondays(
                new DateTime(2026, 9, 28), WeeklyGapChecker.YearLookbackWeeks);

            Assert.AreEqual(52, result.Count);
            Assert.AreEqual(new DateTime(2026, 9, 28), result[result.Count - 1]);
            Assert.AreEqual(new DateTime(2026, 9, 28).AddDays(-7 * 51), result[0]);
        }

        [TestMethod]
        public void ReadWeeklyDates_OnlyWeekly_Returned()
        {
            // SP種別の行が混ざっても Weekly だけ返す。History には種別列がないため
            // 明示的な種別絞りを持つ LastResult 側を見る方針の確認である。
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(db);
                TestDbHelper.InsertLastResultData(db, "Weekly", 20260921, "sm1", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(db, "Weekly", 20260928, "sm2", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(db, "SP", 20260921, "sm9", 1, 100, "{}");

                var dates = WeeklyGapChecker.ReadWeeklyDates(db);

                Assert.AreEqual(2, dates.Count);
                Assert.IsTrue(dates.Contains(new DateTime(2026, 9, 21)));
                Assert.IsTrue(dates.Contains(new DateTime(2026, 9, 28)));
            }
        }

        [TestMethod]
        public void IsMaintenance_FlagOne_ReturnsTrue()
        {
            using (var db = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingDateTable(db);
                TestDbHelper.InsertRankingDateData(db, 20260921, 1);
                TestDbHelper.InsertRankingDateData(db, 20260928, 0);

                Assert.IsTrue(WeeklyGapChecker.IsMaintenance(db, new DateTime(2026, 9, 21)));
                Assert.IsFalse(WeeklyGapChecker.IsMaintenance(db, new DateTime(2026, 9, 28)));
                // 行なしはメンテ扱いにしない（単なる未取得と区別できないため）。
                Assert.IsFalse(WeeklyGapChecker.IsMaintenance(db, new DateTime(2026, 10, 5)));
            }
        }

        [TestMethod]
        public void Check_Integration_GapAndMaintenance()
        {
            // 9/21 が抜け、9/14 がメンテ除外、9/28 が正常の組み合わせ。
            using (var hist = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(hist);
                TestDbHelper.InsertLastResultData(hist, "Weekly", 20260914, "sm1", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(hist, "Weekly", 20260928, "sm2", 1, 100, "{}");
                TestDbHelper.CreateRankingDateTable(official);
                TestDbHelper.InsertRankingDateData(official, 20260914, 0);
                TestDbHelper.InsertRankingDateData(official, 20260921, 0);
                TestDbHelper.InsertRankingDateData(official, 20260928, 0);

                var result = WeeklyGapChecker.Check(hist, official, new DateTime(2026, 9, 28), 3);

                Assert.IsNull(result.ErrorMessage);
                Assert.AreEqual(1, result.Missing.Count);
                Assert.AreEqual(new DateTime(2026, 9, 21), result.Missing[0]);
            }
        }

        [TestMethod]
        public void Check_MaintenanceExcluded_NotMissing()
        {
            using (var hist = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(hist);
                TestDbHelper.InsertLastResultData(hist, "Weekly", 20260928, "sm2", 1, 100, "{}");
                TestDbHelper.CreateRankingDateTable(official);
                TestDbHelper.InsertRankingDateData(official, 20260921, 1);

                // 期待週 9/21・9/28 のうち 9/21 はメンテ除外のため抜けなしになる。
                var result = WeeklyGapChecker.Check(hist, official, new DateTime(2026, 9, 28), 2);

                Assert.AreEqual(0, result.Missing.Count);
                Assert.AreEqual(1, result.MaintenanceSkipped.Count);
            }
        }

        [TestMethod]
        public void FindMissing_MaintenanceThrows_TreatedAsMissing()
        {
            // メンテ判定の失敗は除外不能として抜け側に倒す（本当の抜けを見逃す方が害が大きいため）。
            var expected = new List<DateTime> { new DateTime(2026, 9, 21) };
            var actual = new List<DateTime>();

            List<DateTime> skipped;
            var missing = WeeklyGapChecker.FindMissing(
                expected, actual, d => { throw new InvalidOperationException("判定失敗"); }, out skipped);

            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual(0, skipped.Count);
        }

        [TestMethod]
        public void ExcludeTargetDay_RemovesOnlyTarget()
        {
            // 自動警告では集計対象日を抜けに数えない（毎回必ず警告になるため）。
            var missing = new List<DateTime> { new DateTime(2026, 9, 21), new DateTime(2026, 9, 28) };

            var result = WeeklyGapChecker.ExcludeTargetDay(missing, new DateTime(2026, 9, 28));

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new DateTime(2026, 9, 21), result[0]);
        }

        [TestMethod]
        public void ExcludeTargetDay_Null_ReturnsEmpty()
        {
            var result = WeeklyGapChecker.ExcludeTargetDay(null, new DateTime(2026, 9, 28));

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void Check_WithPaths_ReadsCorrectDatabases()
        {
            // UIが実際に使うパス版の口を実ファイルで検証する。
            // 引数の順序を取り違えると LastResult が空になり全週が抜け扱いになるため、
            // このテストが呼び出し側の退行を検出する。
            string dir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "nicorank_t045_" + Guid.NewGuid().ToString("N"));
            try
            {
                string dbDir = System.IO.Path.Combine(dir, "DB");
                System.IO.Directory.CreateDirectory(dbDir);
                string histPath = System.IO.Path.Combine(dbDir, "NicoranHistory.db");
                System.IO.File.Create(histPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(histPath));
                    TestDbHelper.CreateLastResultTable(ctrl);
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260914, "sm1", 1, 100, "{}");
                    TestDbHelper.InsertLastResultData(ctrl, "Weekly", 20260928, "sm2", 1, 100, "{}");
                    ctrl.Close();
                }
                string logPath = System.IO.Path.Combine(dbDir, "LogOfficial.db");
                System.IO.File.Create(logPath).Dispose();
                using (var ctrl = new SQLiteCtrl())
                {
                    Assert.IsTrue(ctrl.Open(logPath));
                    TestDbHelper.CreateRankingDateTable(ctrl);
                    TestDbHelper.InsertRankingDateData(ctrl, 20260914, 0);
                    TestDbHelper.InsertRankingDateData(ctrl, 20260921, 0);
                    TestDbHelper.InsertRankingDateData(ctrl, 20260928, 0);
                    ctrl.Close();
                }

                var result = WeeklyGapChecker.Check(histPath, logPath, new DateTime(2026, 9, 28), 3);

                Assert.IsNull(result.ErrorMessage);
                Assert.AreEqual(1, result.Missing.Count);
                Assert.AreEqual(new DateTime(2026, 9, 21), result.Missing[0]);
            }
            finally
            {
                try { if (System.IO.Directory.Exists(dir)) { System.IO.Directory.Delete(dir, true); } } catch { }
            }
        }
        [TestMethod]
        public void FormatMissing_JoinedByJapaneseComma()
        {
            var text = WeeklyGapChecker.FormatMissing(new List<DateTime>
            {
                new DateTime(2026, 10, 5), new DateTime(2026, 9, 21)
            });

            Assert.AreEqual("2026-09-21、2026-10-05", text);
        }

        [TestMethod]
        public void Check_MissingTable_ReturnsErrorNotMissing()
        {
            // LastResult 表なし（破損・移行前）は確認不能に倒し、期待週の誤警告にしない。
            using (var hist = TestDbHelper.CreateInMemoryDb())
            using (var official = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateRankingDateTable(official);

                var result = WeeklyGapChecker.Check(hist, official, new DateTime(2026, 9, 28), 3);

                Assert.IsNotNull(result.ErrorMessage);
                Assert.AreEqual(0, result.Missing.Count);
            }
        }
    }
}
