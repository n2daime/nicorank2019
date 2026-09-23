using System;
using System.Threading;

namespace nicorankLib.SnapShot
{
    /// <summary>
    /// Snapshot API v2 の更新待ち（ポーリング）制御（Issue #38）。
    /// 未更新／確認不能の間は一定間隔で再チェックし、更新検知またはタイムアウトで抜ける。
    /// 待機の判定だけに専念し、取得実行・終了コードの決定は呼び出し側（CLI）が行う。
    /// 理由は、待機と取得を分離すると「タイムアウト時は取得せず終了」の分岐が
    /// 呼び出し側に素直に書けるためである。
    /// </summary>
    public class SnapShotVersionPoller
    {
        /// <summary>再チェック間隔。5分以上空けるのは公式ガイドの注意書き（503時は5分以上空けてリトライ）に倣う</summary>
        public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

        /// <summary>待ち合わせの上限。定期タスクの1枠分（約1時間）に合わせる</summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromHours(1);

        private readonly SnapShotVersionChecker _checker;
        private readonly Action<TimeSpan> _sleep;
        private readonly Func<DateTimeOffset> _clock;

        /// <summary>
        /// ポーラーを生成する。引数なしなら本番動作（実チェック＋Thread.Sleep＋実時計）、
        /// 引数ありならテスト用フェイクに差し替える。実時間で1時間待つテストは実行不能なため、
        /// 時間と待機の差し替え口は削らない。
        /// </summary>
        public SnapShotVersionPoller(
            SnapShotVersionChecker checker = null,
            Action<TimeSpan> sleep = null,
            Func<DateTimeOffset> clock = null)
        {
            _checker = checker ?? new SnapShotVersionChecker();
            _sleep = sleep ?? (s => Thread.Sleep(s));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// 更新されるまで待つ。初回チェック＋タイムアウトまで5分ごとの再チェックを行う。
        /// 毎回の結果を log に渡すのは、cron運用ではログだけが頼りのため無言の1時間待機を避けるためである。
        /// 精度は問わないため、経過時間の超過分だけ待機が延びても許容する。
        /// </summary>
        /// <param name="log">チェックごとのログ出力先（null可）。文面は SnapShotVersionChecker.ToStatusLogLine を使う</param>
        /// <returns>最後のチェック結果（Updated＝取得へ進む、それ以外＝取得せず終了）</returns>
        public SnapShotVersionResult WaitForUpdate(Action<string> log = null)
        {
            DateTimeOffset start = _clock();

            SnapShotVersionResult result = _checker.Check();
            log?.Invoke(SnapShotVersionChecker.ToStatusLogLine(result));

            while (result.Status != SnapShotVersionStatus.Updated && (_clock() - start) < Timeout)
            {
                _sleep(RetryInterval);
                result = _checker.Check();
                log?.Invoke(SnapShotVersionChecker.ToStatusLogLine(result));
            }
            return result;
        }
    }
}
