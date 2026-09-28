using Microsoft.VisualStudio.TestTools.UnitTesting;
using nicorankLib.SnapShot;
using System;
using System.Collections.Generic;

namespace UnitTest.nicorankLib.SnapShot
{
    /// <summary>
    /// SnapShotVersionPoller のテスト（Issue #38）。
    /// 実時間では待てないため、時計・待機・応答列をすべてフェイクにしてループ回数だけを検証する。
    /// </summary>
    [TestClass]
    public class UnitTestSnapShotVersionPoller
    {
        private static readonly DateTimeOffset NowJst = new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.FromHours(9));
        private const string TodayJson = "{\"last_modified\":\"2026-09-20T07:08:34+09:00\"}";
        private const string YesterdayJson = "{\"last_modified\":\"2026-09-19T08:34:12+09:00\"}";

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

        private static SnapShotVersionPoller CreatePoller(Queue<string> responses, FakeClock clock)
        {
            var checker = new SnapShotVersionChecker(
                (string url, out string text) => { text = responses.Dequeue(); return true; },
                () => NowJst);
            return new SnapShotVersionPoller(checker, clock.Sleep, () => clock.Current);
        }

        [TestMethod]
        public void Spec_RetryIntervalIs5Minutes_TimeoutIs1Hour()
        {
            // 仕様値（5分ごと・最大1時間）の固定。変える場合はIssue #38の見直しと判断するためテストで縛る
            Assert.AreEqual(TimeSpan.FromMinutes(5), SnapShotVersionPoller.RetryInterval);
            Assert.AreEqual(TimeSpan.FromHours(1), SnapShotVersionPoller.Timeout);
        }

        [TestMethod]
        public void WaitForUpdate_AlreadyUpdated_ChecksOnceWithoutSleep()
        {
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var logs = new List<string>();
            var poller = CreatePoller(new Queue<string>(new[] { TodayJson }), clock);

            var result = poller.WaitForUpdate(logs.Add);

            Assert.AreEqual(SnapShotVersionStatus.Updated, result.Status);
            Assert.AreEqual(0, clock.Sleeps.Count);
            Assert.AreEqual(1, logs.Count);
        }

        [TestMethod]
        public void WaitForUpdate_UpdatedOnRetry_SleepsOnceThenReturnsUpdated()
        {
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var logs = new List<string>();
            var poller = CreatePoller(new Queue<string>(new[] { YesterdayJson, TodayJson }), clock);

            var result = poller.WaitForUpdate(logs.Add);

            Assert.AreEqual(SnapShotVersionStatus.Updated, result.Status);
            Assert.AreEqual(1, clock.Sleeps.Count);
            Assert.AreEqual(TimeSpan.FromMinutes(5), clock.Sleeps[0]);
            Assert.AreEqual(2, logs.Count);
        }

        [TestMethod]
        public void WaitForUpdate_NeverUpdated_StopsAfterTimeout()
        {
            // 応答が尽きないよう十分な未更新応答を用意する。経過はフェイク時計で進める
            var responses = new Queue<string>();
            for (int i = 0; i < 20; i++) { responses.Enqueue(YesterdayJson); }
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var logs = new List<string>();
            var poller = CreatePoller(responses, clock);

            var result = poller.WaitForUpdate(logs.Add);

            Assert.AreEqual(SnapShotVersionStatus.NotUpdated, result.Status);
            // t0の初回＋5分×12回の再チェック＝計13回・待機12回で約60分。精度不要のため境界ちょうどで打ち切る
            Assert.AreEqual(12, clock.Sleeps.Count);
            Assert.AreEqual(13, logs.Count);
        }

        [TestMethod]
        public void WaitForUpdate_UnknownUntilTimeout_StopsAfterTimeout()
        {
            // 確認不能（取得失敗）のままの場合も未更新と同様に打ち切る。
            // 将来 Unknown だけ別分岐が追加されても退行を見逃さないためのテスト
            var checker = new SnapShotVersionChecker(
                (string url, out string text) => { text = null; return false; },
                () => NowJst);
            var clock = new FakeClock { Current = DateTimeOffset.UtcNow };
            var logs = new List<string>();
            var poller = new SnapShotVersionPoller(checker, clock.Sleep, () => clock.Current);

            var result = poller.WaitForUpdate(logs.Add);

            Assert.AreEqual(SnapShotVersionStatus.Unknown, result.Status);
            Assert.AreEqual(12, clock.Sleeps.Count);
            Assert.AreEqual(13, logs.Count);
        }
    }
}
