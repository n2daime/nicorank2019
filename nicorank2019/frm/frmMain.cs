using nicorank2019.frm;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.Official;
using nicorankLib.Common;
using nicorankLib.Factory;
using nicorankLib.output;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nicorank2019.frm
{
    public partial class frmMain : Form
    {

        protected ModeFactoryBase MainFactory;

        // ポイント計算パネル(panel3)のタブ間付け替え状態
        private bool _tagMockLoaded = false;
        private System.Drawing.Point _panel3SyukeiLocation;
        private bool _switchingTab = false;
        /// <summary>
        /// タグ検索集計の実行条件（UIスレッドで退避。集計スレッドからはコントロールに触れないため）
        /// </summary>
        private class TagExecuteContext
        {
            public TagSearchQuery Query;
            public string AnalyzeDB;
            public string BaseDB;
            public string LastResult;
        }
        private TagExecuteContext _tagExecuteContext = null;
        // 直近の件数確認で上限超過だったか（超過時はランキング計算ボタンを押せなくする）
        private bool _tagCountOverLimit = false;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            var config = Config.GetInstance();

            try
            {
                SelectMode();
                _panel3SyukeiLocation = panel3.Location;
                lblTagCount.Text = "検索件数: 未確認（上限50000件）";
                lblTagWarn.Visible = false;
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
                // v2最新値チェックの切替配線はコード側で行う（Designerの再生成差分を増やさないため）。初期状態（既定ON）もここで反映する
                chkUseLiveCounter.CheckedChanged += new EventHandler(this.chkUseLiveCounter_CheckedChanged);
                UpdateLiveCounterControls();
                _tagMockLoaded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動時にエラーが発生しました。設定項目が不正な可能性があります\n\n{GetExceptionMessages(ex)}", "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrLog.GetInstance().Write(ex);
                Application.Exit();
            }
        }



        private static string GetExceptionMessages(Exception ex)
        {
            var messages = new List<string>();
            for (var e = ex; e != null; e = e.InnerException)
            {
                messages.Add(e.Message);
            }
            return string.Join("\n→ ", messages);
        }

        private async void btnAnalyze_Click(object sender, EventArgs e)
        {
            if (!SavePointCalcPanel())
            {
                MessageBox.Show("ポイント計算の入力値が不正です", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _tagExecuteContext = null;
            await ExecuteAnalyzeAsync(btnAnalyze);
        }

        /// <summary>
        /// 集計を実行して結果を報告する（集計タブ・タグタブ共通）
        /// </summary>
        private async Task ExecuteAnalyzeAsync(Button execButton)
        {
            try
            {
                execButton.Enabled = false;
                // 集計実行中は最適化を開始できないようにする（逆方向の同時実行防止。最適化側も集計ボタンを止める）
                SetVacuumControlsEnabled(false);

                bool result = await AnalyzeAsync();
                if (!result)
                {
                    MessageBox.Show($"集計時時にエラーが発生しました。コマンドプロンプトとnicorankerr.logを確認してください", "集計エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                }
                else
                {
                    StatusLog.WriteLine("\n集計に成功しました");


                    MessageBox.Show("集計成功","確認",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                execButton.Enabled = true;
                SetVacuumControlsEnabled(true);
            }
        }


        private void rbWeekly_CheckedChanged(object sender, EventArgs e)
        {
            SelectMode();
        }

        private void rbTyukan_CheckedChanged(object sender, EventArgs e)
        {
            SelectMode();
        }

        private void rbSP_CheckedChanged(object sender, EventArgs e)
        {
            SelectMode();
        }


        private void btnBaseDB_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbBaseDB, "SnapShotDB|*.db", "Open File");
        }

        private void btnAnalyzeDB_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbAnalyzeDB, "SnapShotDB|*.db", "Open File");
        }

        private void btnMovieSPList_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbMovieSPList, "ニコランWebからDL|*.csv;*.txt", "Open File");
        }

        private void btnLastResult_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbLastResult, "result.csv|*.csv", "Open File");
        }

        // タグ検索集計のファイル選択
        private void btnBaseDB_Tag_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbBaseDB_Tag, "SnapShotDB|*.db", "Open File");
        }

        private void btnAnalyzeDB_Tag_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbAnalyzeDB_Tag, "SnapShotDB|*.db", "Open File");
        }

        private void btnLastResult_Tag_Click(object sender, EventArgs e)
        {
            OpenFileDialogNicoran(this.tbLastResult_Tag, "result.csv|*.csv", "Open File");
        }

        /// <summary>
        /// メンテナンスタブの「DBの最適化を実行」ボタン(Issue #32)。
        /// チェックされたDBを1件ずつVACUUMする。数十分かかりうるため実処理は集計スレッド側で行い、
        /// UI更新はawait復帰後のUIスレッドで行う（集計スレッドからコントロールに触らない。pitfalls項目19）。
        /// 実行ログは集計タブと同様にコンソール側（StatusLog）へ出すため、タブ内にログ欄は持たない。
        /// </summary>
        private async void btnVacuumExec_Click(object sender, EventArgs e)
        {
            // パスの単一源は DbOptimizer.GetDefaultTargets() とし、UI側にパス値を二重定義しない。
            // 配列の並び順は GetDefaultTargets() の順序と対応させること（順序を変えると対応がずれる）。
            var definitions = DbOptimizer.GetDefaultTargets();
            CheckBox[] checkBoxes = { chkVacuumLogOfficial, chkVacuumNicoranHistory, chkVacuumApiXml, chkVacuumDailylog };
            Label[] beforeLabels = { lblVacuumBeforeLogOfficial, lblVacuumBeforeNicoranHistory, lblVacuumBeforeApiXml, lblVacuumBeforeDailylog };
            Label[] afterLabels = { lblVacuumAfterLogOfficial, lblVacuumAfterNicoranHistory, lblVacuumAfterApiXml, lblVacuumAfterDailylog };
            // 件数ずれは別DBの行への誤表示・範囲外例外になるため開発時に検出する（件数一致を検証。順序は単体テストが担保）。
            System.Diagnostics.Debug.Assert(definitions.Count == checkBoxes.Length
                && definitions.Count == beforeLabels.Length
                && definitions.Count == afterLabels.Length, "メンテナンスタブの対象配列は GetDefaultTargets() と件数・順序を合わせること");
            // チェック状態の読み取りはUIスレッドで行う（タグ検索のTagExecuteContextと同一理由）
            var targets = new List<VacuumUiTarget>();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (checkBoxes[i].Checked)
                {
                    targets.Add(new VacuumUiTarget { DbPath = definitions[i].DbPath, BeforeLabel = beforeLabels[i], AfterLabel = afterLabels[i] });
                }
            }
            if (targets.Count == 0)
            {
                MessageBox.Show("最適化するDBにチェックを入れてください", "メンテナンス", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 実行中は二重実行と集計との同時実行（DBロック競合）を防ぐため、実行系ボタンを止める
            SetVacuumRunning(true);
            progressVacuum.Maximum = targets.Count;
            progressVacuum.Value = 0;
            lblVacuumStatus.Text = "状態: 実行中...";
            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;
            try
            {
                foreach (var target in targets)
                {
                    // 不在判定は Optimize 側に一本化する。UI側で事前判定すると、
                    // 判定と実行の隙にファイルが消えた場合に残りのDB処理ごと中断してしまうため
                    // （specs「そのDBだけ失敗とし残りを続ける」を守る）。
                    target.AfterLabel.Text = "実行後: 実行中...";
                    DbOptimizeResult result = await System.Threading.Tasks.Task.Run(() => DbOptimizer.Optimize(target.DbPath));
                    if (!result.Executed)
                    {
                        target.BeforeLabel.Text = "実行前: なし";
                        target.AfterLabel.Text = "実行後: なし";
                        skipCount++;
                    }
                    else
                    {
                        target.BeforeLabel.Text = "実行前: " + DbOptimizer.FormatFileSize(result.SizeBefore);
                        if (result.Success)
                        {
                            target.AfterLabel.Text = "実行後: " + DbOptimizer.FormatFileSize(result.SizeAfter);
                            successCount++;
                        }
                        else
                        {
                            target.AfterLabel.Text = "実行後: 失敗";
                            failCount++;
                        }
                    }
                    progressVacuum.Value++;
                }
                if (failCount == 0)
                {
                    lblVacuumStatus.Text = "状態: 完了";
                    MessageBox.Show($"最適化が完了しました（成功 {successCount}件・スキップ {skipCount}件）", "メンテナンス", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblVacuumStatus.Text = "状態: 一部失敗";
                    MessageBox.Show($"最適化で失敗がありました（成功 {successCount}件・スキップ {skipCount}件・失敗 {failCount}件）。コンソールとnicorankerr.logを確認してください", "メンテナンス", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                lblVacuumStatus.Text = "状態: 失敗";
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetVacuumRunning(false);
            }
        }

        /// <summary>
        /// メンテナンスタブの実行対象1件分（UIスレッドで退避した表示先付き）。
        /// </summary>
        private class VacuumUiTarget
        {
            public string DbPath;
            public Label BeforeLabel;
            public Label AfterLabel;
        }

        // 最適化の実行中フラグ。ResetTagCountState が実行中の条件編集で集計ボタンを復活させないために見る。
        // 完了時は退避値ではなく現在の条件から求め直すため、開始前の Enabled 退避は持たない。
        private bool _vacuumRunning = false;

        /// <summary>
        /// 実行中の二重実行・同時集計を防ぐため、実行系の有効・無効を切り替える。
        /// 完了時はタグボタンを現在の条件（件数超過時は無効のまま）から求め直す。
        /// 退避値戻しにしないのは、実行中の条件編集を取りこぼすため。
        /// </summary>
        private void SetVacuumRunning(bool running)
        {
            _vacuumRunning = running;
            SetVacuumControlsEnabled(!running);
            btnAnalyze.Enabled = !running;
            if (running)
            {
                btnAnalyzeTag.Enabled = false;
            }
            else
            {
                btnAnalyzeTag.Enabled = !_tagCountOverLimit && !string.IsNullOrWhiteSpace(tbTagCondition.Text);
            }
        }

        /// <summary>
        /// 最適化タブ側の操作部だけを切り替える。集計実行中にも最適化を開始できないよう、
        /// ExecuteAnalyzeAsync 側からも呼ぶ（逆方向の同時実行防止）。
        /// </summary>
        private void SetVacuumControlsEnabled(bool enabled)
        {
            btnVacuumExec.Enabled = enabled;
            chkVacuumLogOfficial.Enabled = enabled;
            chkVacuumNicoranHistory.Enabled = enabled;
            chkVacuumApiXml.Enabled = enabled;
            chkVacuumDailylog.Enabled = enabled;
        }

        /// <summary>
        /// タグタブの入力値から検索条件を組み立てる
        /// </summary>
        private bool TryBuildTagSearchQuery(out TagSearchQuery query, out string error)
        {
            query = null;
            if (string.IsNullOrWhiteSpace(tbTagCondition.Text))
            {
                error = "タグ条件を入力してください";
                return false;
            }
            if (!TryParseMin(tbViewMin, "再生", out long viewMin, out error)) { return false; }
            if (!TryParseMin(tbMylistMin, "マイリス", out long mylistMin, out error)) { return false; }
            if (!TryParseMin(tbLikeMin, "いいね", out long likeMin, out error)) { return false; }
            if (!TryParseMin(tbCommentMin, "コメント", out long commentMin, out error)) { return false; }
            if (chkDateFilter.Checked && dtEnd.Value.Date <= dtStart.Value.Date)
            {
                error = "投稿日の終了日は開始日より後にしてください";
                return false;
            }
            string contentType = null;
            if (cmbContentType.SelectedIndex == 1) { contentType = "long"; }
            else if (cmbContentType.SelectedIndex == 2) { contentType = "short"; }
            query = new TagSearchQuery()
            {
                TagCondition = tbTagCondition.Text.Trim(),
                ViewMin = viewMin,
                MylistMin = mylistMin,
                LikeMin = likeMin,
                CommentMin = commentMin,
                UseDateFilter = chkDateFilter.Checked,
                StartGte = dtStart.Value.Date,
                StartLt = dtEnd.Value.Date,
                ContentType = contentType,
                UseLiveCounter = chkUseLiveCounter.Checked
            };
            error = null;
            return true;
        }

        private bool TryParseMin(TextBox textBox, string name, out long value, out string error)
        {
            value = 0;
            error = null;
            string text = textBox.Text.Trim();
            if (text == string.Empty)
            {
                return true;
            }
            if (!long.TryParse(text, out value) || value < 0)
            {
                error = $"{name}下限は0以上の整数で入力してください";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 検索条件の変更で件数表示を未確認に戻し、ランキング計算ボタンを押せるようにする
        /// </summary>
        private void TagCondition_Changed(object sender, EventArgs e)
        {
            ResetTagCountState();
        }

        private void ResetTagCountState()
        {
            if (!_tagMockLoaded)
            {
                return;
            }
            _tagCountOverLimit = false;
            // 最適化実行中は無効のままにする（実行中の条件編集で集計ボタンを復活させない。同時実行防止）
            btnAnalyzeTag.Enabled = !_vacuumRunning && !string.IsNullOrWhiteSpace(tbTagCondition.Text);
            lblTagWarn.Visible = false;
            lblTagCount.Text = "検索件数: 未確認（上限50000件）";
            // 古い時点表示が残ると誤解されるため、条件変更時は時点ラベルも消して非表示に戻す
            lblTagSnapshotTime.Visible = false;
            lblTagSnapshotTime.Text = "";
        }

        /// <summary>
        /// タグ条件でEnter確定したら件数確認を実行する
        /// </summary>
        private void tbTagCondition_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnTagSearch.PerformClick();
            }
        }

        /// <summary>
        /// 件数確認して上限超過なら実行ボタンを押せなくする。超過でなければ件数を返す。
        /// v2最新値モード（query.UseLiveCounter）では件数取得成功後に version を取得してデータ時点ラベルを出す。
        /// version確認不能時はエラー中断（null返却）し、不明な時点のまま集計させない。
        /// 取得は await Task.Run で行い、UIスレッドをブロックしない（#38と同型の罠回避）。
        /// </summary>
        /// <returns>集計に進める件数。進めない場合（超過・取得失敗・時点確認不能）は null</returns>
        private async Task<long?> CheckTagCountAsync(TagSearchQuery query)
        {
            var analyzer = new TagRankAnalyze(DateTime.Now, query);
            long count = 0;
            bool ok = await Task.Run(() => analyzer.GetTotalCount(out count));
            if (!ok)
            {
                _tagCountOverLimit = false;
                lblTagCount.Text = "検索件数: 取得失敗";
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
                MessageBox.Show("検索件数の取得に失敗しました。ネットワークと条件を確認してください", "検索エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            lblTagCount.Text = $"検索件数: {count} 件";
            if (count > TagRankAnalyze.MaxTotalCount)
            {
                _tagCountOverLimit = true;
                lblTagWarn.Text = $"検索結果が多すぎます。({count}件) {TagRankAnalyze.MaxTotalCount}件以下になるように条件を追加して下さい";
                lblTagWarn.Visible = true;
                btnAnalyzeTag.Enabled = false;
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
                return null;
            }
            _tagCountOverLimit = false;
            lblTagWarn.Visible = false;
            // DB使用モード（UseLiveCounter=OFF）は時点表示の対象外のため、ラベルは非表示のままにする
            if (query == null || !query.UseLiveCounter)
            {
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
                return count;
            }
            // 件数確認のたびに version を取得する（タブ滞在中の使い回しはせず、常に最新の切り替え日時を掴む）
            var versionResult = await Task.Run(() => new SnapShotVersionChecker().Check());
            string snapshotLabel;
            if (!TagSnapshotTimestamp.TryFormat(versionResult, out snapshotLabel))
            {
                // 確認不能時はエラーとして中断する。不明な時点表示のまま集計させないためラベルは出さない
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
                MessageBox.Show("データ時点（スナップショットversion）の取得に失敗しました。ネットワークを確認して件数確認をやり直してください", "検索エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            lblTagSnapshotTime.Text = snapshotLabel;
            lblTagSnapshotTime.Visible = true;
            return count;
        }

        private async void btnTagSearch_Click(object sender, EventArgs e)
        {
            if (!TryBuildTagSearchQuery(out TagSearchQuery query, out string buildError))
            {
                MessageBox.Show(buildError, "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                btnTagSearch.Enabled = false;
                lblTagCount.Text = "検索件数: 取得中...";
                lblTagWarn.Visible = false;

                await CheckTagCountAsync(query);
            }
            catch (Exception ex)
            {
                lblTagCount.Text = "検索件数: 取得失敗";
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTagSearch.Enabled = true;
            }
        }

        private async void btnAnalyzeTag_Click(object sender, EventArgs e)
        {
            if (!TryBuildTagSearchQuery(out TagSearchQuery query, out string buildError))
            {
                MessageBox.Show(buildError, "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 集計日DBの要否はv2最新値モードで変わる。最新値モードはDBなし実行のため存在確認自体を行わない
            // （無効化された欄に古い不正パスが残っていてもブロックしない。工場もAnalyzeDBを無視する）
            if (!query.UseLiveCounter && !IsExistingFile(tbAnalyzeDB_Tag.Text))
            {
                MessageBox.Show("集計日のDBを指定してください", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!string.IsNullOrWhiteSpace(tbBaseDB_Tag.Text) && !File.Exists(tbBaseDB_Tag.Text.Trim()))
            {
                MessageBox.Show("基準日のDBファイルが見つかりません", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!string.IsNullOrWhiteSpace(tbLastResult_Tag.Text) && !File.Exists(tbLastResult_Tag.Text.Trim()))
            {
                MessageBox.Show("前回結果のファイルが見つかりません", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 実行前に件数確認し、上限超過時は集計しない（ボタンも押せなくする）
            btnAnalyzeTag.Enabled = false;
            long? totalCount;
            try
            {
                totalCount = await CheckTagCountAsync(query);
            }
            catch (Exception ex)
            {
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnAnalyzeTag.Enabled = true;
                return;
            }
            if (!totalCount.HasValue)
            {
                if (_tagCountOverLimit)
                {
                    MessageBox.Show(lblTagWarn.Text, "検索エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    btnAnalyzeTag.Enabled = true;
                }
                return;
            }
            btnAnalyzeTag.Enabled = true;
            if (!SavePointCalcPanel())
            {
                MessageBox.Show("ポイント計算の入力値が不正です", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 集計スレッドからはコントロールに触れないため、UIスレッドで値を退避する
            _tagExecuteContext = new TagExecuteContext()
            {
                Query = query,
                AnalyzeDB = tbAnalyzeDB_Tag.Text.Trim(),
                BaseDB = tbBaseDB_Tag.Text.Trim(),
                LastResult = tbLastResult_Tag.Text.Trim()
            };
            await ExecuteAnalyzeAsync(btnAnalyzeTag);
        }

        private static bool IsExistingFile(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path.Trim());
        }

        private void chkDateFilter_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkDateFilter.Checked;
            dtStart.Enabled = enabled;
            dtEnd.Enabled = enabled;
            ResetTagCountState();
        }

        /// <summary>
        /// v2最新値モードの切替で集計日DB欄の有効・無効を切り替える
        /// </summary>
        private void chkUseLiveCounter_CheckedChanged(object sender, EventArgs e)
        {
            UpdateLiveCounterControls();
        }

        /// <summary>
        /// v2最新値モードONなら集計日DB欄を無効化する（DBなし実行のため）。
        /// OFF（DB使用モード）では時点ラベルは対象外のため、切り替え時に非表示に戻す。
        /// </summary>
        private void UpdateLiveCounterControls()
        {
            bool useDb = !chkUseLiveCounter.Checked;
            tbAnalyzeDB_Tag.Enabled = useDb;
            btnAnalyzeDB_Tag.Enabled = useDb;
            if (useDb)
            {
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
            }
        }

        // ポイント計算パネルを集計タブとタグタブで付け替える（タグ選択時はTAGRANK値に切り替える）
        // メンテナンスタブ選択時は何もしない（集計モードと無関係のためpanel3は触らず、モード切替も行わない）
        // 固定座標はAutoScaleの対象外でずれるため、スケール済みのコントロールを基準に相対配置する
        private void tabPageOut_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_tagMockLoaded || _switchingTab)
            {
                return;
            }
            if (tabPageOut.SelectedTab == tabPageTag)
            {
                if (!SavePointCalcPanel())
                {
                    MessageBox.Show("ポイント計算の入力値が不正です", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RevertTabSelection(tabPageSyukei);
                    return;
                }
                SelectTagMode();
                tabPageTag.SuspendLayout();
                panel3.Parent = tabPageTag;
                int margin = grpDb.Left;
                panel3.Location = new System.Drawing.Point(margin, grpDb.Bottom + 8);
                panel3.Width = tabPageTag.ClientSize.Width - margin * 2;
                btnAnalyzeTag.Location = new System.Drawing.Point(
                    (tabPageTag.ClientSize.Width - btnAnalyzeTag.Width) / 2,
                    panel3.Bottom + 8);
                tabPageTag.ResumeLayout(false);
                tabPageTag.PerformLayout();
            }
            else if (tabPageOut.SelectedTab == tabPageSyukei)
            {
                if (!SavePointCalcPanel())
                {
                    MessageBox.Show("ポイント計算の入力値が不正です", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RevertTabSelection(tabPageTag);
                    return;
                }
                SelectSyukeiMode();
                panel3.Parent = tabPageSyukei;
                panel3.Location = _panel3SyukeiLocation;
            }
        }

        private void RevertTabSelection(TabPage page)
        {
            _switchingTab = true;
            tabPageOut.SelectedTab = page;
            _switchingTab = false;
        }

        protected void OpenFileDialogNicoran(TextBox textBox, string filter, string caption)
        {
            using (OpenFileDialog fileDialog = new OpenFileDialog())
            {
                textBox.Text = textBox.Text.Trim();

                //すでにファイルもしくはフォルダが選択済みの場合は、デフォルトのパスに指定する
                if (!textBox.Text.Equals(string.Empty))
                {
                    if (System.IO.Directory.Exists(textBox.Text))
                    {
                        fileDialog.InitialDirectory = textBox.Text;
                    }
                    else
                    {
                        fileDialog.InitialDirectory = new System.IO.FileInfo(textBox.Text).DirectoryName;
                    }
                }
                //[ファイルの種類に表示される選択肢を指定する
                fileDialog.Filter = filter;

                //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする
                fileDialog.RestoreDirectory = true;

                // 存在しないファイルの名前が指定されたときに警告を表示する
                fileDialog.CheckFileExists = true;

                // 存在しないパスが指定されたときに警告を表示する
                fileDialog.CheckPathExists = true;

                // タイトルを設定する
                string tmptitle = caption;

                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox.Text = fileDialog.FileName;
                    fileDialog.FileName = "";
                }
            }
        }

        public void SetEnableAnalyzeDay()
        {
            {
                // 有効な集計日になるまでループ
                var analyzeDay = dtPAnalyzeDay.Value;

                //未来はNG
                if (DateTime.Now <= analyzeDay)
                {
                    analyzeDay = DateTime.Now;
                }
                while (true)
                {
                    if (analyzeDay.DayOfWeek == DayOfWeek.Monday)
                    {
                        if (!nicorankLib.Analyze.Json.JsonReaderBase.CheckAnalyzeTime(analyzeDay))
                        {
                            //当日の0:30 前＝まだ集計されていない可能性がある
                        }
                        else
                        {
                            //集計日確定
                            break;
                        }
                    }
                    analyzeDay = analyzeDay.AddDays(-1);
                }
                dtPAnalyzeDay.Value = analyzeDay.Date;
                //dtPLastweekDay.Value = analyzeDay.AddDays(-7).Date;
            }
            {
                // 有効な集計日になるまでループ
                var lastweekDay = dtPLastweekDay.Value.Date;

                DateTime analyzeDay;
                if (rbWeekly.Checked)
                {
                    analyzeDay = dtPAnalyzeDay.Value.Date;
                }
                else if (rbTyukan.Checked)
                {
                    analyzeDay = DateTime.Now;

                }
                else
                {
                    return;
                }

                //未来はNG
                if (analyzeDay <= lastweekDay)
                {
                    lastweekDay = analyzeDay.AddDays(-7).Date;
                }
                while (true)
                {
                    if (lastweekDay.DayOfWeek == DayOfWeek.Monday)
                    {
                        //集計日確定
                        break;
                    }
                    lastweekDay = lastweekDay.AddDays(-1);
                }
                dtPLastweekDay.Value = lastweekDay;
            }
        }

        private void dtPAnalyzeDay_ValueChanged(object sender, EventArgs e)
        {
            SetEnableAnalyzeDay();
        }

        private void dtPLastweekDay_ValueChanged(object sender, EventArgs e)
        {
            SetEnableAnalyzeDay();
        }
    }
}
