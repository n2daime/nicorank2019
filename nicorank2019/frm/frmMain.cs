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
        // タグ検索集計の実行時に使う検索条件（実行ボタンで検証済みのもの）
        private TagSearchQuery _currentTagQuery = null;

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
                ContentType = contentType
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

                var analyzer = new TagRankAnalyze(DateTime.Now, query);
                long totalCount = 0;
                bool ok = await Task.Run(() => analyzer.GetTotalCount(out totalCount));
                if (!ok)
                {
                    lblTagCount.Text = "検索件数: 取得失敗";
                    MessageBox.Show("検索件数の取得に失敗しました。ネットワークと条件を確認してください", "検索エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                lblTagCount.Text = $"検索件数: {totalCount} 件";
                if (totalCount > TagRankAnalyze.MaxTotalCount)
                {
                    lblTagWarn.Text = $"検索結果が多すぎます。({totalCount}件) {TagRankAnalyze.MaxTotalCount}件以下になるように条件を追加して下さい";
                    lblTagWarn.Visible = true;
                }
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
            if (!SavePointCalcPanel())
            {
                MessageBox.Show("ポイント計算の入力値が不正です", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _currentTagQuery = query;
            await ExecuteAnalyzeAsync(btnAnalyzeTag);
        }

        private void chkDateFilter_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkDateFilter.Checked;
            dtStart.Enabled = enabled;
            dtEnd.Enabled = enabled;
        }

        // ポイント計算パネルを集計タブとタグタブで付け替える（タグ選択時はTAGRANK値に切り替える）
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
