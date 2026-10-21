using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.Util;
using System;
using System.Collections.Generic;

namespace UnitTest.nicorankLib.Util
{
    /// <summary>
    /// RetryPoller のテスト（Issue #51）。
    /// 実時間では1時間待てないため、時計・待機・試行結果をすべてフェイクにして
    /// ループ回数と打ち切り位置だけを検証する。
    /// </summary>
    [TestClass]
    public class UnitTestRetryPoller
    {
        /// <summary>フェイク時計。待機のたびに指定分だけ進む（実スリープなし）</summary>
        private sealed class FakeClock
        {
            public DateTimeOffset Current;
            public readonly List<TimeSpan> Sleeps = new List<TimeSpan>();

            public void Sleep(TimeSpan span)
            {
                Sleeps.Add(span);
                Current += span;
            }
        }

        [TestMethod]
        public void Spec_RetryIntervalIs5Minutes_TimeoutIs1Hour()
        {
            // ニコ動APIの503は5分以上空けてリトライする公式ガイドと、定期タスク1枠（約1時間）に合わせた仕様値。
            // 変える場合は Issue #51 の見直しと判断するため、テストで縛る
            Assert.AreEqual(TimeSpan.FromMinutes(5), RetryPoller.RetryInterval);
            Assert.AreEqual(TimeSpan.FromHours(1), RetryPoller.Timeout);
        }

        [TestMethod]
        public void RunUntilSuccess_SucceedsFirst_DoesNotSleep()
        {
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var poller = new RetryPoller(clock.Sleep, () => clock.Current);

            var result = poller.RunUntilSuccess(() => true);

            Assert.IsTrue(result);
            Assert.AreEqual(0, clock.Sleeps.Count);
        }

        [TestMethod]
        public void RunUntilSuccess_SucceedsOnThirdAttempt_SleepsTwice()
        {
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var poller = new RetryPoller(clock.Sleep, () => clock.Current);
            int attempts = 0;
            var logs = new List<string>();

            // 2回失敗して3回目で成功する（一時的な503から復旧する想定）
            var result = poller.RunUntilSuccess(() => ++attempts >= 3, logs.Add);

            Assert.IsTrue(result);
            Assert.AreEqual(3, attempts);
            Assert.AreEqual(2, clock.Sleeps.Count);
            Assert.AreEqual(TimeSpan.FromMinutes(5), clock.Sleeps[0]);
            Assert.AreEqual(TimeSpan.FromMinutes(5), clock.Sleeps[1]);
            // 失敗ごとに再試行の通知を出す（cronではログだけが頼りのため）
            Assert.AreEqual(2, logs.Count);
        }

        [TestMethod]
        public void RunUntilSuccess_NeverSucceeds_StopsAfterOneHour()
        {
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var poller = new RetryPoller(clock.Sleep, () => clock.Current);
            int attempts = 0;
            var logs = new List<string>();
            var start = clock.Current;

            // 初回(t0)＋5分×12回の再試行＝計13回・待機12回で約60分。
            // SnapShotVersionPoller と同じ数え方に揃え、境界ちょうどで打ち切る
            var result = poller.RunUntilSuccess(() => { attempts++; return false; }, logs.Add);

            Assert.IsFalse(result);
            Assert.AreEqual(13, attempts);
            Assert.AreEqual(12, clock.Sleeps.Count);
            // 再試行12回＋打ち切り1回
            Assert.AreEqual(13, logs.Count);
            Assert.AreEqual(TimeSpan.FromHours(1), clock.Current - start);
        }

        [TestMethod]
        public void RunUntilSuccess_BudgetIsSharedBetweenCalls()
        {
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var poller = new RetryPoller(clock.Sleep, () => clock.Current);
            var start = clock.Current;

            // 1回目の呼び出し: t0・t5と失敗し、t10で成功（ここで予算の起点が t0 に確定）
            int firstAttempts = 0;
            Assert.IsTrue(poller.RunUntilSuccess(() => ++firstAttempts >= 3));
            Assert.AreEqual(2, clock.Sleeps.Count);

            // 2回目の呼び出しは t10 開始。予算は t0 起点のままなので、t60 で打ち切られる
            int secondAttempts = 0;
            int sleepsBeforeSecond = clock.Sleeps.Count;
            Assert.IsFalse(poller.RunUntilSuccess(() => { secondAttempts++; return false; }));

            Assert.AreEqual(11, secondAttempts);                       // t10〜t60の11回
            Assert.AreEqual(10, clock.Sleeps.Count - sleepsBeforeSecond); // 残り50分を5分刻み
            Assert.AreEqual(TimeSpan.FromHours(1), clock.Current - start);
        }

        [TestMethod]
        public void RunUntilSuccess_AttemptThrows_Propagates()
        {
            // 取得失敗は false で返す契約。予期しない例外はリトライで隠さず、
            // 呼び出し元（oldlogのProgram）の異常終了に委ねる
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var poller = new RetryPoller(clock.Sleep, () => clock.Current);

            Assert.ThrowsException<InvalidOperationException>(
                () => poller.RunUntilSuccess(() => { throw new InvalidOperationException("テスト用"); }));

            Assert.AreEqual(0, clock.Sleeps.Count);
        }
    }
}
