using nicorank2019.Util;
using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nicorank2019.frm
{
    /// <summary>
    /// 集計タブの「EDサムネイルDL」「ユーザーアイコンDL」（Issue #56）。
    ///
    /// 既存の queue.irv / queue_UserIcon.irv は Irvine などの外部ツールが後から画像を取得する前提で、
    /// 利用者は集計後に別途DL操作をする必要がある。ここでは同じファイル名・同じ対象範囲のまま、
    /// アプリ内で直接保存できるようにする（.irv の出力自体は残す。ユーザーアイコンは .irv も
    /// UserInfo.Num の範囲に揃えた）。
    ///
    /// 注意している点:
    /// - コントロールの値は必ずUIスレッドで読み、Task.Run には値だけを渡す（pitfalls 項目19）。
    /// - 実行中は他タブ・実行系ボタン・モード選択を止める。中断ボタンを押せるよう、
    ///   集計タブ全体は無効化しない。
    /// - 保存先は毎回ユーザーに選ばせ、直前に選んだフォルダを初期値にする（設定ファイルには保存しない）。
    /// </summary>
    public partial class frmMain
    {
        /// <summary>DL実行中か。集計・最適化との二重起動防止に使う。</summary>
        private bool _imageDlRunning = false;

        /// <summary>集計実行中か。集計中にDLボタンが有効に見えないようにする。</summary>
        private bool _analyzeRunning = false;

        /// <summary>UserInfo/Icon取得数の入力が不正か。不正中はユーザーアイコンDLを押せない。</summary>
        private bool _userInfoNumInvalid = false;

        /// <summary>直前に選んだ保存先。アプリ起動中だけ覚える（nicorank.xml は変更しない）。</summary>
        private string _lastImageDownloadDir = null;

        /// <summary>実行中のダウンロードを中断するためのトークン。実行中以外は null。</summary>
        private CancellationTokenSource _imageDownloadCts = null;

        /// <summary>今回の実行で失敗したファイル（進捗コールバックがUIスレッドなので排他不要）。</summary>
        private readonly List<ImageDownloadProgress> _imageDownloadFailures = new List<ImageDownloadProgress>();

        /// <summary>今回の実行でスキップしたファイル（既存・URLなし）。</summary>
        private readonly List<ImageDownloadProgress> _imageDownloadSkips = new List<ImageDownloadProgress>();

        /// <summary>
        /// DLボタンの有効・無効と予定件数を現在の状態から決める。
        /// 有効条件は「集計結果がある」「DL中でない」「最適化等の実行中でない」「中間モードでない」。
        /// 中間モードは UserImageURL が取得されず、EDサムネイルも queue.irv を出さないため対象外。
        /// </summary>
        private void UpdateImageDownloadButtons()
        {
            bool hasResult = MainFactory != null
                && MainFactory.RankingList != null
                && MainFactory.RankingList.Count > 0;
            bool enabled = hasResult
                && !_imageDlRunning
                && !_analyzeRunning
                && !_vacuumRunning
                && !rbTyukan.Checked;

            btnThumbnailDownload.Enabled = enabled;
            // UserInfo取得数が不正な間は、ユーザーアイコンDLだけを止める（EDサムネイルは影響しない）。
            btnUserIconDownload.Enabled = enabled && !_userInfoNumInvalid;
            chkHighQuality.Enabled = enabled;
            rbSkipExisting.Enabled = enabled;
            rbOverwrite.Enabled = enabled;
            // 中断は実行中だけ押せる。実行中にタブごと無効化すると押せなくなるため、
            // 実行中は他のタブ・実行系ボタンだけを止める（SetImageDownloadRunning）。
            btnImageDownloadCancel.Enabled = _imageDlRunning;

            UpdateImageDownloadCountLabel(hasResult);
        }

        /// <summary>
        /// 「何件取得する予定か」を表示する。既存ファイルのスキップは実行後に分かるため、
        /// ここでは対象件数（ED枠・重複排除後のユーザー数）を出す。
        /// </summary>
        private void UpdateImageDownloadCountLabel(bool hasResult)
        {
            if (lblImageDownloadCount == null)
            {
                return;
            }
            if (_userInfoNumInvalid)
            {
                lblImageDownloadCount.Text = "予定: ユーザーアイコン取得数が不正です（0以上の整数）";
                return;
            }
            if (!hasResult)
            {
                lblImageDownloadCount.Text = "予定: 集計後に表示されます";
                return;
            }
            if (rbTyukan.Checked)
            {
                lblImageDownloadCount.Text = "予定: 中間モードは対象外です";
                return;
            }

            lblImageDownloadCount.Text =
                $"予定: EDサムネイル {CountMovieTargets()}件 / ユーザーアイコン {CountUserIconTargets()}件";
        }

        /// <summary>EDサムネイルの対象件数（queue.irv と同じ「紹介枠＋ED枠」）。</summary>
        private int CountMovieTargets()
        {
            if (MainFactory == null || MainFactory.RankingList == null)
            {
                return 0;
            }
            return Math.Min(
                Math.Max(MainFactory.GetMovieThumbnailEndRank(), 0),
                MainFactory.RankingList.Count);
        }

        /// <summary>ユーザーアイコンの対象件数（保存名で重複排除した数）。</summary>
        private int CountUserIconTargets()
        {
            if (MainFactory == null || MainFactory.RankingList == null)
            {
                return 0;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int userInfoNum = Config.GetInstance().UserNum;
            foreach (var rank in MainFactory.RankingList)
            {
                if (!ImageDownloader.IsUserIconTarget(rank, userInfoNum))
                {
                    // 直接DLは UserInfo.Num の範囲（上位＋カテゴリ1位）だけを対象にする。
                    continue;
                }

                string fileName = ImageFileNaming.GetUserIconFileName(rank);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    seen.Add(fileName);
                }
            }
            return seen.Count;
        }

        /// <summary>
        /// UserInfo/Icon取得数の変更を即時反映する。
        /// 予定件数と直接DLの対象は `Config.UserNum` を見るため、ここで書き換えるだけで
        /// 再集計なしに追従する。なお `queue_UserIcon.irv` は集計時に生成されるため、
        /// 変更後に .irv も更新したい場合は再集計が必要（直接DLは即時反映）。
        /// </summary>
        private void UserInfoNum_TextChanged(object sender, EventArgs e)
        {
            if (!int.TryParse(tbUserInfoNum.Text, out int value) || value < 0)
            {
                _userInfoNumInvalid = true;
                UpdateImageDownloadButtons();
                return;
            }

            _userInfoNumInvalid = false;
            Config.GetInstance().UserNum = value;
            UpdateImageDownloadButtons();
        }

        /// <summary>
        /// DL実行中の状態を切り替える。既存の SetVacuumRunning と同型にし、
        /// finally で必ず戻すことで、例外時にもボタンが無効のまま残らないようにする。
        /// </summary>
        private void SetImageDownloadRunning(bool running)
        {
            _imageDlRunning = running;

            btnAnalyze.Enabled = !running && !_vacuumRunning;
            // 実行中に取得数を変えても、走っているDLには反映されない。混乱を避けて編集を止める。
            tbUserInfoNum.Enabled = !running;
            if (running)
            {
                btnAnalyzeTag.Enabled = false;
            }
            else
            {
                btnAnalyzeTag.Enabled = !_vacuumRunning && !_tagCountOverLimit
                    && !string.IsNullOrWhiteSpace(tbTagCondition.Text);
            }

            SetVacuumControlsEnabled(!running && !_vacuumRunning);

            // DL中は中断ボタンを押せる必要があるため、集計タブ全体は無効化しない。
            // 代わりにモード選択・他タブ・実行系ボタンを止め、別の処理を開始できないようにする。
            groupBox1.Enabled = !running;
            tabPageTag.Enabled = !running;
            tabPageMaint.Enabled = !running;
            tabPageSystem.Enabled = !running;

            UpdateImageDownloadButtons();
        }

        private async void btnThumbnailDownload_Click(object sender, EventArgs e)
        {
            await ExecuteImageDownloadAsync(true);
        }

        private async void btnUserIconDownload_Click(object sender, EventArgs e)
        {
            await ExecuteImageDownloadAsync(false);
        }

        /// <summary>
        /// サムネイル（movie=true）またはユーザーアイコン（movie=false）をダウンロードする。
        /// </summary>
        private async Task ExecuteImageDownloadAsync(bool movie)
        {
            if (_imageDlRunning)
            {
                return;
            }
            if (_vacuumRunning)
            {
                // ボタンは SetVacuumRunning 側でも無効化するが、状態の取りこぼしに備えて
                // ハンドラでも確認する。DBを開く最適化等との同時実行はロック競合のもとである。
                MessageBox.Show("DB最適化などの実行中は画像をダウンロードできません。完了後に実行してください。",
                    "画像ダウンロード", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MainFactory == null || MainFactory.RankingList == null)
            {
                MessageBox.Show("先に集計を実行してください。", "画像ダウンロード",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (rbTyukan.Checked)
            {
                MessageBox.Show("中間モードでは画像をダウンロードできません。週間・SP・タグ検索の集計後に実行してください。",
                    "画像ダウンロード", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!movie && _userInfoNumInvalid)
            {
                MessageBox.Show("UserInfo/Icon取得数が不正です。0以上の整数を入力してください。",
                    "画像ダウンロード", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int plannedCount = movie ? CountMovieTargets() : CountUserIconTargets();
            string folder = SelectImageDownloadFolder(movie, plannedCount);
            if (string.IsNullOrWhiteSpace(folder))
            {
                // キャンセル時は何もしない。
                return;
            }

            // UIスレッドで値を退避する。Task.Run の中からコントロールを参照しないため。
            var rankingList = MainFactory.RankingList;
            var options = new ImageDownloadOptions()
            {
                HighQuality = chkHighQuality.Checked,
                OverwriteExisting = rbOverwrite.Checked,
            };
            int endRank = movie ? MainFactory.GetMovieThumbnailEndRank() : 0;
            int userInfoNum = Config.GetInstance().UserNum;

            _lastImageDownloadDir = folder;
            _imageDownloadFailures.Clear();
            _imageDownloadSkips.Clear();
            _imageDownloadCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _imageDownloadCts.Token;
            SetImageDownloadRunning(true);

            try
            {
                string label = movie ? "EDサムネイル" : "ユーザーアイコン";
                StatusLog.WriteLine("");
                StatusLog.WriteLine($"{label}のダウンロードを開始します（予定 {plannedCount} 件 / 保存先: {folder}）");
                if (movie && options.HighQuality)
                {
                    StatusLog.WriteLine("高画質(1280x720)を優先します。取得できない場合は 360x270 にフォールバックします。");
                }

                // Progress<T> は生成時の同期コンテキスト（UIスレッド）へコールバックするため、
                // 進捗表示からコントロールや StatusLog を触ってもスレッド違反にならない。
                var progress = new Progress<ImageDownloadProgress>(ReportImageDownloadProgress);

                var summary = await Task.Run(() =>
                {
                    var downloader = new ImageDownloader(new HttpWebDownloader());
                    return movie
                        ? downloader.DownloadMovieThumbnails(rankingList, endRank, folder, options, progress, cancellationToken)
                        : downloader.DownloadUserIcons(rankingList, folder, options, progress, userInfoNum, cancellationToken);
                });

                string counts = $"対象 {summary.Total} 件 / 成功 {summary.Success} 件 / スキップ {summary.Skipped} 件 / 失敗 {summary.Failed} 件";
                if (summary.Cancelled)
                {
                    StatusLog.WriteLine($"{label}のダウンロードを中断しました: {counts}");
                    MessageBox.Show(
                        $"{label}のダウンロードを中断しました。\n\n{counts.Replace(" / ", "\n")}",
                        "ダウンロード中断", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (summary.Failed > 0)
                {
                    StatusLog.WriteLine($"{label}のダウンロードが完了しました（失敗あり）: {counts}");
                    ShowImageDownloadFailureReport(label, folder, counts, summary);
                }
                else
                {
                    StatusLog.WriteLine($"{label}のダウンロードが完了しました: {counts}");
                    MessageBox.Show(
                        $"{label}のダウンロードが完了しました。\n\n{counts.Replace(" / ", "\n")}",
                        "ダウンロード完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_imageDownloadCts != null)
                {
                    _imageDownloadCts.Dispose();
                    _imageDownloadCts = null;
                }
                SetImageDownloadRunning(false);
            }
        }

        /// <summary>
        /// 中断ボタン。次のリクエストの区切りで止まる（実行中の通信は Abort する）。
        /// </summary>
        private void btnImageDownloadCancel_Click(object sender, EventArgs e)
        {
            if (!_imageDlRunning || _imageDownloadCts == null)
            {
                return;
            }

            _imageDownloadCts.Cancel();
            btnImageDownloadCancel.Enabled = false;
            StatusLog.WriteLine("中断を要求しました。実行中の通信が終わり次第停止します。");
        }

        /// <summary>
        /// 保存先フォルダを選ぶ。キャンセル時は null を返す。
        /// </summary>
        private string SelectImageDownloadFolder(bool movie, int plannedCount)
        {
            string label = movie ? "EDサムネイル" : "ユーザーアイコン";
            // 何件取得する予定かをダイアログのタイトルにも出す（画面のラベルと同じ内容）。
            string title = $"{label} {plannedCount}件の保存先フォルダを選択してください";

            // 古い FolderBrowserDialog のツリー表示ではなく、Windows 標準の
            // 「フォルダーの選択」ダイアログを使う（FolderPicker 内でフォールバックあり）。
            return FolderPicker.Show(this, title, _lastImageDownloadDir);
        }

        /// <summary>
        /// 失敗があった場合の完了ダイアログ。件数だけでなく「どのファイルが」「なぜ」失敗したかと、
        /// 再実行で直る見込みを出す。詳細は保存先の download_errors_*.txt に残す。
        /// </summary>
        private void ShowImageDownloadFailureReport(string label, string folder, string counts, ImageDownloadSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{label}のダウンロードが完了しました（失敗 {summary.Failed} 件）。");
            sb.AppendLine();
            sb.AppendLine(counts.Replace(" / ", "\n"));
            sb.AppendLine();
            sb.AppendLine("■ 失敗したファイル");

            const int maxListCount = 20;
            for (int i = 0; i < _imageDownloadFailures.Count && i < maxListCount; i++)
            {
                ImageDownloadProgress failure = _imageDownloadFailures[i];
                sb.AppendLine($"{failure.FileName} — {failure.Message}（{GetFailureKindLabel(failure.Kind)}）");
            }
            if (_imageDownloadFailures.Count > maxListCount)
            {
                sb.AppendLine($"他 {_imageDownloadFailures.Count - maxListCount} 件（詳細はログを参照）");
            }

            sb.AppendLine();
            sb.AppendLine("■ 対処");
            foreach (string advice in BuildFailureAdvice(_imageDownloadFailures))
            {
                sb.AppendLine("・" + advice);
            }

            string logPath = TryWriteImageDownloadErrorLog(folder, label, counts);
            if (!string.IsNullOrEmpty(logPath))
            {
                sb.AppendLine();
                sb.AppendLine($"詳細ログ: {logPath}");
            }

            MessageBox.Show(sb.ToString(), "ダウンロード完了（失敗あり）",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string GetFailureKindLabel(ImageDownloadFailureKind kind)
        {
            switch (kind)
            {
                case ImageDownloadFailureKind.NotFound:
                    return "404/400 画像なし";
                case ImageDownloadFailureKind.Forbidden:
                    return "403 アクセス拒否";
                case ImageDownloadFailureKind.RateLimited:
                    return "429 レート制限";
                case ImageDownloadFailureKind.ServerError:
                    return "5xx サーバーエラー";
                case ImageDownloadFailureKind.Network:
                    return "通信エラー";
                case ImageDownloadFailureKind.SaveError:
                    return "保存失敗";
                default:
                    return "不明";
            }
        }

        /// <summary>
        /// 失敗の種類に応じた案内。ユーザーが次に何をすればよいか分かるようにする。
        /// </summary>
        private static List<string> BuildFailureAdvice(List<ImageDownloadProgress> failures)
        {
            bool retryable = false;
            bool rateLimited = false;
            bool notFound = false;
            bool saveError = false;

            foreach (ImageDownloadProgress failure in failures)
            {
                switch (failure.Kind)
                {
                    case ImageDownloadFailureKind.ServerError:
                    case ImageDownloadFailureKind.Network:
                        retryable = true;
                        break;
                    case ImageDownloadFailureKind.Forbidden:
                    case ImageDownloadFailureKind.RateLimited:
                        rateLimited = true;
                        break;
                    case ImageDownloadFailureKind.NotFound:
                        notFound = true;
                        break;
                    case ImageDownloadFailureKind.SaveError:
                        saveError = true;
                        break;
                }
            }

            var advice = new List<string>();
            if (retryable)
            {
                advice.Add("通信エラー・HTTP 5xx は一時的な可能性があります。「既存ファイルはスキップ」を選んで、もう一度実行してください。");
            }
            if (rateLimited)
            {
                advice.Add("HTTP 403/429 はアクセス制限です。時間を置いてから再実行してください。");
            }
            if (notFound)
            {
                advice.Add("HTTP 404/400 は動画側に画像が無いか、URL形式が想定外です。再実行しても改善しない可能性があります。");
            }
            if (saveError)
            {
                advice.Add("保存に失敗しています。保存先の空き容量・書き込み権限・同名ファイルの使用中を確認してください。");
            }
            advice.Add("再実行時は「既存ファイルはスキップ」を選ぶと、成功済みの画像は再取得せず、失敗した分だけ取得します。");
            return advice;
        }

        /// <summary>
        /// 失敗・スキップの詳細を保存先へ書き出す。ダイアログに出しきれない件数やURLを残すため。
        /// 書き出しに失敗してもダウンロード結果自体は変えない。
        /// </summary>
        private string TryWriteImageDownloadErrorLog(string folder, string label, string counts)
        {
            try
            {
                string path = Path.Combine(folder, $"download_errors_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                var sb = new StringBuilder();
                sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {label}");
                sb.AppendLine(counts);
                sb.AppendLine();
                sb.AppendLine("[失敗]");
                foreach (ImageDownloadProgress failure in _imageDownloadFailures)
                {
                    sb.AppendLine($"{failure.FileName}\t{GetFailureKindLabel(failure.Kind)}\t{failure.Message}\t{failure.Url}");
                }

                sb.AppendLine();
                sb.AppendLine("[スキップ（既存以外）]");
                foreach (ImageDownloadProgress skip in _imageDownloadSkips)
                {
                    if (skip.Message == ImageDownloader.SkipExistingMessage)
                    {
                        continue;
                    }
                    sb.AppendLine($"{skip.FileName}\t{skip.Message}");
                }

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                return path;
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return null;
            }
        }

        /// <summary>
        /// 進捗を1件ごとにコンソールへ出す。8分程度かかるユーザーアイコン全件でも
        /// 進行が見えるように、累計件数も添える。
        /// </summary>
        private void ReportImageDownloadProgress(ImageDownloadProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            if (progress.Outcome == ImageDownloadOutcome.Failed)
            {
                _imageDownloadFailures.Add(progress);
            }
            else if (progress.Outcome == ImageDownloadOutcome.Skipped)
            {
                _imageDownloadSkips.Add(progress);
            }

            string mark;
            switch (progress.Outcome)
            {
                case ImageDownloadOutcome.Success:
                    mark = "成功";
                    break;
                case ImageDownloadOutcome.Skipped:
                    mark = "スキップ";
                    break;
                default:
                    mark = "失敗";
                    break;
            }

            string detail = string.IsNullOrEmpty(progress.Message) ? "" : $" ({progress.Message})";
            StatusLog.WriteLine(
                $"[{progress.Index}/{progress.Total}] {mark} {progress.FileName}{detail}"
                + $"  成功{progress.SuccessCount} スキップ{progress.SkippedCount} 失敗{progress.FailedCount}");
        }
    }
}
