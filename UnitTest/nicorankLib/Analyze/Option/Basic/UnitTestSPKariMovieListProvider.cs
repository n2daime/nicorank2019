using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Analyze.Option.Basic;
using UnitTest.Helpers;

namespace UnitTest.nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// Issue #54: 仮集計の動画ID抽出の検証。
    /// 「種別=Weekly・総合ランク300位以内・基準日以降」をID重複なしで拾い、
    /// それ以外（301位・基準日前・種別違い）を拾わないことを担保する。
    /// </summary>
    [TestClass]
    public class UnitTestSPKariMovieListProvider
    {
        private static readonly DateTime BaseDay = new DateTime(2026, 7, 1);

        [TestMethod]
        public void 条件に合うIDだけをID順に重複なく抽出する()
        {
            using (var dbCtrl = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(dbCtrl);
                // 抽出対象（300位ちょうどは残す）
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260706, "sm1", 300, 100, "{}");
                // 301位は対象外
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260706, "sm2", 301, 90, "{}");
                // 基準日より前の週は対象外
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260629, "sm3", 10, 80, "{}");
                // 種別が違うものは対象外
                TestDbHelper.InsertLastResultData(dbCtrl, "Monthly", 20260706, "sm4", 10, 70, "{}");
                // 同じIDが複数週にある場合は1件にまとめる
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260707, "sm1", 5, 60, "{}");
                // 基準日以降の別の週
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260713, "sm5", 1, 50, "{}");

                using (var provider = new SPKariMovieListProvider("in-memory", dbCtrl))
                {
                    Assert.IsTrue(provider.Open(), "注入した接続が開いていること");
                    Assert.IsTrue(provider.TryGetMovieIds(BaseDay, 300, out var result));
                    CollectionAssert.AreEqual(new[] { "sm1", "sm5" }, new List<string>(result.Ids));
                    Assert.AreEqual(300, result.RankLimit);
                    Assert.AreEqual(BaseDay, result.BaseDay);
                }
            }
        }

        [TestMethod]
        public void 参照した週刊結果の最新集計日を返す()
        {
            using (var dbCtrl = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(dbCtrl);
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260706, "sm1", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260713, "sm2", 1, 90, "{}");
                // 種別が違うものは最新日の判定に混ぜない
                TestDbHelper.InsertLastResultData(dbCtrl, "Monthly", 20260720, "sm3", 1, 80, "{}");

                using (var provider = new SPKariMovieListProvider("in-memory", dbCtrl))
                {
                    Assert.IsTrue(provider.Open());
                    Assert.IsTrue(provider.TryGetMovieIds(BaseDay, 300, out var result));
                    Assert.AreEqual(new DateTime(2026, 7, 13), result.LatestWeeklyDay);
                }
            }
        }

        [TestMethod]
        public void 上限を変えると抽出件数が変わる()
        {
            using (var dbCtrl = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(dbCtrl);
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260706, "sm1", 1, 100, "{}");
                TestDbHelper.InsertLastResultData(dbCtrl, "Weekly", 20260706, "sm2", 500, 90, "{}");

                using (var provider = new SPKariMovieListProvider("in-memory", dbCtrl))
                {
                    Assert.IsTrue(provider.Open());
                    Assert.IsTrue(provider.TryGetMovieIds(BaseDay, 300, out var result));
                    CollectionAssert.AreEqual(new[] { "sm1" }, new List<string>(result.Ids));
                    Assert.IsTrue(provider.TryGetMovieIds(BaseDay, 1000, out var wide));
                    CollectionAssert.AreEqual(new[] { "sm1", "sm2" }, new List<string>(wide.Ids));
                }
            }
        }

        [TestMethod]
        public void 対象がなければ空のまま成功する()
        {
            using (var dbCtrl = TestDbHelper.CreateInMemoryDb())
            {
                TestDbHelper.CreateLastResultTable(dbCtrl);

                using (var provider = new SPKariMovieListProvider("in-memory", dbCtrl))
                {
                    Assert.IsTrue(provider.Open());
                    // 0件は抽出としては成功。中断するかは呼び出し側（Factory）が判断する
                    Assert.IsTrue(provider.TryGetMovieIds(BaseDay, 300, out var result));
                    Assert.AreEqual(0, result.Ids.Count);
                    Assert.IsNull(result.LatestWeeklyDay);
                }
            }
        }

        [TestMethod]
        public void DBを開けなければ失敗する()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
            using (var provider = new SPKariMovieListProvider(path))
            {
                Assert.IsFalse(provider.Open());
                Assert.IsFalse(provider.TryGetMovieIds(BaseDay, 300, out var result));
                Assert.IsNull(result);
            }
        }

        [TestMethod]
        public void 既定の上限は300()
        {
            Assert.AreEqual(300, SPKariMovieListProvider.DefaultRankLimit);
        }
    }
}
