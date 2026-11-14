using nicorankLib.Util;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nicorank2019.frm
{
    /// <summary>
    /// 集計タブの「EDサムネイルDL」「ユーザーアイコンDL」（Issue #56）。
    ///
    /// 既存の queue.irv / queue_UserIcon.irv は Irvine などの外部ツールが後から画像を取得する前提で、
    /// 利用者は集計後に別途DL操作をする必要がある。ここでは同じファイル名・同じ対象件数のまま、
    /// アプリ内で直接保存できるようにする（既存の .irv 出力は残す）。
    ///
    /// 注意している点:
    /// - コントロールの値は必ずUIスレッドで読み、Task.Run には値だけを渡す（pitfalls 項目19）。
    /// - 実行中はタブごと無効化し、集計・最適化との同時実行を防ぐ。
    /// - 保存先は毎回ユーザーに選ばせ、直前に選んだフォルダを初期値にする（設定ファイルには保存しない）。
    /// </summary>
    public partial class frmMain
    {
        /// <summary>DL実行中か。集計・最適化との二重起動防止に使う。</summary>
        private bool _imageDlRunning = false;

        /// <summary>集計実行中か。集計中にDLボタンが有効に見えないようにする。</summary>
        private bool _analyzeRunning = false;

        // 画像ダウンロードUI。Designer を編集すると環境によってデザイナーが読み込めなくなる事象が
        // 繰り返されたため、既存のアイコン設定やチェックボックス配線（#35・#54 の前例）と同じく
        // コード側で生成する。frmMain のコンストラクタから InitializeImageDownloadControls() を呼ぶ。
        private GroupBox grpImageDownload;
        private Label lblImageDownloadDesc;
        private Button btnThumbnailDownload;
        private Button btnUserIconDownload;
        private CheckBox chkHighQuality;
        private RadioButton rbSkipExisting;
        private RadioButton rbOverwrite;
        private Label lblImageDownloadNote;

        /// <summary>
        /// 集計タブ下部の画像ダウンロードUIを生成する。
        /// なぜ Designer ではなくコードか: Designer ファイルへの追記が、この環境では
        /// 「基本クラスを読み込めませんでした」でデザイナーを開けなくすることがあったため。
        /// 座標・サイズ・タブ順は当初 Designer に置いていた値と同じにしてある。
        /// </summary>
        private void InitializeImageDownloadControls()
        {
            grpImageDownload = new GroupBox();
            lblImageDownloadDesc = new Label();
            btnThumbnailDownload = new Button();
            btnUserIconDownload = new Button();
            chkHighQuality = new CheckBox();
            rbSkipExisting = new RadioButton();
            rbOverwrite = new RadioButton();
            lblImageDownloadNote = new Label();

            grpImageDownload.SuspendLayout();

            grpImageDownload.Controls.Add(lblImageDownloadDesc);
            grpImageDownload.Controls.Add(btnThumbnailDownload);
            grpImageDownload.Controls.Add(btnUserIconDownload);
            grpImageDownload.Controls.Add(chkHighQuality);
            grpImageDownload.Controls.Add(rbSkipExisting);
            grpImageDownload.Controls.Add(rbOverwrite);
            grpImageDownload.Controls.Add(lblImageDownloadNote);
            grpImageDownload.Location = new System.Drawing.Point(6, 570);
            grpImageDownload.Name = "grpImageDownload";
            grpImageDownload.Size = new System.Drawing.Size(806, 130);
            grpImageDownload.TabIndex = 5;
            grpImageDownload.TabStop = false;
            grpImageDownload.Text = "画像ダウンロード（既存の queue.irv とは別に保存します）";

            lblImageDownloadDesc.AutoSize = true;
            lblImageDownloadDesc.Location = new System.Drawing.Point(12, 20);
            lblImageDownloadDesc.Name = "lblImageDownloadDesc";
            lblImageDownloadDesc.Size = new System.Drawing.Size(461, 12);
            lblImageDownloadDesc.TabIndex = 0;
            lblImageDownloadDesc.Text = "集計後に、サムネイルとユーザーアイコンを選択したフォルダへ直接保存します。";

            btnThumbnailDownload.Enabled = false;
            btnThumbnailDownload.Location = new System.Drawing.Point(12, 40);
            btnThumbnailDownload.Name = "btnThumbnailDownload";
            btnThumbnailDownload.Size = new System.Drawing.Size(150, 30);
            btnThumbnailDownload.TabIndex = 1;
            btnThumbnailDownload.Text = "EDサムネイルDL";
            btnThumbnailDownload.UseVisualStyleBackColor = true;
            btnThumbnailDownload.Click += btnThumbnailDownload_Click;

            btnUserIconDownload.Enabled = false;
            btnUserIconDownload.Location = new System.Drawing.Point(168, 40);
            btnUserIconDownload.Name = "btnUserIconDownload";
            btnUserIconDownload.Size = new System.Drawing.Size(150, 30);
            btnUserIconDownload.TabIndex = 2;
            btnUserIconDownload.Text = "ユーザーアイコンDL";
            btnUserIconDownload.UseVisualStyleBackColor = true;
            btnUserIconDownload.Click += btnUserIconDownload_Click;

            chkHighQuality.AutoSize = true;
            chkHighQuality.Enabled = false;
            chkHighQuality.Location = new System.Drawing.Point(12, 78);
            chkHighQuality.Name = "chkHighQuality";
            chkHighQuality.Size = new System.Drawing.Size(210, 16);
            chkHighQuality.TabIndex = 3;
            chkHighQuality.Text = "可能であれば高画質(1280x720)";
            chkHighQuality.UseVisualStyleBackColor = true;

            rbSkipExisting.AutoSize = true;
            rbSkipExisting.Checked = true;
            rbSkipExisting.Enabled = false;
            rbSkipExisting.Location = new System.Drawing.Point(300, 78);
            rbSkipExisting.Name = "rbSkipExisting";
            rbSkipExisting.Size = new System.Drawing.Size(140, 16);
            rbSkipExisting.TabIndex = 4;
            rbSkipExisting.TabStop = true;
            rbSkipExisting.Text = "既存ファイルはスキップ";
            rbSkipExisting.UseVisualStyleBackColor = true;

            rbOverwrite.AutoSize = true;
            rbOverwrite.Enabled = false;
            rbOverwrite.Location = new System.Drawing.Point(460, 78);
            rbOverwrite.Name = "rbOverwrite";
            rbOverwrite.Size = new System.Drawing.Size(55, 16);
            rbOverwrite.TabIndex = 5;
            rbOverwrite.Text = "上書き";
            rbOverwrite.UseVisualStyleBackColor = true;

            lblImageDownloadNote.AutoSize = true;
            lblImageDownloadNote.ForeColor = System.Drawing.SystemColors.GrayText;
            lblImageDownloadNote.Location = new System.Drawing.Point(12, 104);
            lblImageDownloadNote.Name = "lblImageDownloadNote";
            lblImageDownloadNote.Size = new System.Drawing.Size(332, 12);
            lblImageDownloadNote.TabIndex = 6;
            lblImageDownloadNote.Text = "保存先は実行時に選択します。中間モードは対象外です。";

            // 集計タブの既存コントロールと重ならない位置（btnAnalyze の下）に追加する。
            tabPageSyukei.Controls.Add(grpImageDownload);

            grpImageDownload.ResumeLayout(false);
            grpImageDownload.PerformLayout();
        }

        /// <summary>直前に選んだ保存先。アプリ起動中だけ覚える（nicorank.xml は変更しない）。</summary>
        private string _lastImageDownloadDir = null;

        /// <summary>
        /// DLボタンの有効・無効を現在の状態から決める。
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
            btnUserIconDownload.Enabled = enabled;
            chkHighQuality.Enabled = enabled;
            rbSkipExisting.Enabled = enabled;
            rbOverwrite.Enabled = enabled;
        }

        /// <summary>
        /// DL実行中の状態を切り替える。既存の SetVacuumRunning と同型にし、
        /// finally で必ず戻すことで、例外時にもボタンが無効のまま残らないようにする。
        /// </summary>
        private void SetImageDownloadRunning(bool running)
        {
            _imageDlRunning = running;

            btnAnalyze.Enabled = !running && !_vacuumRunning;
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

            // 実行中はモード切替も止める。DL中に IsSP／IsTagRank が変わると、
            // 表示や以降の集計条件と保存結果が食い違うためである。
            tabPageOut.Enabled = !running && !_vacuumRunning;

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

            string folder = SelectImageDownloadFolder(movie);
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

            _lastImageDownloadDir = folder;
            SetImageDownloadRunning(true);

            try
            {
                string label = movie ? "EDサムネイル" : "ユーザーアイコン";
                StatusLog.WriteLine("");
                StatusLog.WriteLine($"{label}のダウンロードを開始します（保存先: {folder}）");
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
                        ? downloader.DownloadMovieThumbnails(rankingList, endRank, folder, options, progress)
                        : downloader.DownloadUserIcons(rankingList, folder, options, progress);
                });

                string message = $"対象 {summary.Total} 件 / 成功 {summary.Success} 件 / スキップ {summary.Skipped} 件 / 失敗 {summary.Failed} 件";
                StatusLog.WriteLine($"{label}のダウンロードが完了しました: {message}");

                MessageBox.Show(
                    $"{label}のダウンロードが完了しました。\n\n{message.Replace(" / ", "\n")}",
                    "ダウンロード完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetImageDownloadRunning(false);
            }
        }

        /// <summary>
        /// 保存先フォルダを選ぶ。キャンセル時は null を返す。
        /// </summary>
        private string SelectImageDownloadFolder(bool movie)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = movie
                    ? "EDサムネイルの保存先フォルダを選択してください"
                    : "ユーザーアイコンの保存先フォルダを選択してください";
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(_lastImageDownloadDir)
                    && Directory.Exists(_lastImageDownloadDir))
                {
                    dialog.SelectedPath = _lastImageDownloadDir;
                }

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return null;
                }
                return dialog.SelectedPath;
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
