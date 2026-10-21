using System;
using System.Threading;

namespace nicorankLib.Util
{
    /// <summary>
    /// 「失敗したら一定間隔で再試行し、一定時間で打ち切る」制御（Issue #51）。
    /// ニコニコ動画APIが503を返す障害は復旧まで時間がかかるため、公式ガイドの
    /// 「503時は5分以上空けてリトライ」と定期タスク1枠（約1時間）に合わせて、
    /// 5分間隔・最大1時間で再試行する。
    /// SnapShotVersionPoller（Issue #38）と待機値は同じだが、あちらは「更新判定」専用である。
    /// こちらは任意の取得処理を再試行する汎用部品として分けている（片方の都合で値や
    /// 打ち切り条件を変えたときに、もう片方の運用を巻き込まないため）。
    /// </summary>
    public class RetryPoller
    {
        /// <summary>
        /// 再試行の間隔。503時に5分以上空ける公式ガイドに合わせる（定数化の理由は仕様値のため）。
        /// </summary>
        public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 再試行を続ける上限。定期タスクの1枠（約1時間）に合わせる。
        /// 値を変える場合は Issue で運用（NASのタスク間隔・メール通知）と合わせて見直す。
        /// </summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromHours(1);

        private readonly Action<TimeSpan> _sleep;
        private readonly Func<DateTimeOffset> _clock;

        /// <summary>
        /// 最初に失敗した時刻。呼び出し（ジャンル一覧／term）をまたいで共有する。
        /// なぜ実行開始ではなく初回失敗から数えるか:
        /// 週刊の更新待ちのように正常系で長時間かかる処理の後で障害が起きても、その障害に
        /// 1時間の猶予を与えたいためである。
        /// なぜ呼び出しをまたいで共有するか:
        /// ジャンル一覧の復旧待ちとランキング取得の再試行がそれぞれ1時間になると、
        /// 最悪2時間かかって定期タスク1枠に収まらなくなるためである。
        /// </summary>
        private DateTimeOffset? _firstFailureAt;
        private readonly object _lock = new object();

        /// <summary>
        /// ポーラーを生成する。引数なしなら本番動作（実待機＋実時計）、
        /// 引数ありならテスト用フェイクに差し替える。実時間で1時間待つテストは
        /// 実行できないため、待機と時計の差し替え口は削らない。
        /// </summary>
        public RetryPoller(Action<TimeSpan> sleep = null, Func<DateTimeOffset> clock = null)
        {
            _sleep = sleep ?? (s => Thread.Sleep(s));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// 成功するまで再試行する。初回は即実行し、失敗するたびに5分待って再試行する。
        /// 予算（1時間）はこのインスタンスで最初に失敗した時点から数える。
        /// attempt が投げた例外は捕捉しない。取得失敗は各実装が false で返す契約であり、
        /// 予期しない例外まで1時間リトライするとバグを隠して実行時間だけ延ばすため、
        /// 従来どおり呼び出し元（oldlogのProgram）の異常終了に委ねる。
        /// </summary>
        /// <param name="attempt">1回分の取得処理。成功なら true</param>
        /// <param name="log">再試行・打ち切りの通知先（null可。cronではログだけが頼りのため）</param>
        /// <returns>成功したか（false は1時間リトライしても復旧しなかった）</returns>
        public bool RunUntilSuccess(Func<bool> attempt, Action<string> log = null)
        {
            int failedCount = 0;
            while (true)
            {
                if (attempt())
                {
                    return true;
                }
                failedCount++;

                DateTimeOffset firstFailure = RecordFirstFailure();
                TimeSpan elapsed = _clock() - firstFailure;
                if (elapsed >= Timeout)
                {
                    log?.Invoke($"再試行の上限（約{Timeout.TotalMinutes:F0}分）に達したため打ち切ります（失敗{failedCount}回）");
                    return false;
                }

                int remaining = (int)Math.Ceiling((Timeout - elapsed).TotalMinutes);
                log?.Invoke($"{RetryInterval.TotalMinutes:F0}分後に再試行します（失敗{failedCount}回・残り約{remaining}分）");
                _sleep(RetryInterval);
            }
        }

        /// <summary>
        /// 最初の失敗時刻を記録して返す。複数のtermが並行して失敗しても
        /// 予算の起点を1つに揃えるため、lockで保護する。
        /// </summary>
        private DateTimeOffset RecordFirstFailure()
        {
            lock (_lock)
            {
                if (_firstFailureAt == null)
                {
                    _firstFailureAt = _clock();
                }
                return _firstFailureAt.Value;
            }
        }
    }
}
