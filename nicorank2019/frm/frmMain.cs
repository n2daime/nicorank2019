using nicorank2019.frm;
using nicorankLib.Analyze.Official;
using nicorankLib.Common;
using nicorankLib.Factory;
using nicorankLib.output;
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
                SetEnableAnalyzeDay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動時にエラーが発生しました。設定項目が不正な可能性があります\n\n{ex.Message}", "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrLog.GetInstance().Write(ex);
                Application.Exit();
            }
        }



        private async void btnAnalyze_Click(object sender, EventArgs e)
        {
            try
            {
                btnAnalyze.Enabled = false;

                var config = Config.GetInstance();

                config.CalcMyList = double.Parse( tbCalcMylist.Text);
                config.CalcPlay = double.Parse(tbCalcPlay.Text);
                config.CalcComment = double.Parse(tbCalcComment.Text);

                config.CalcMyListKind = cmbHoseiMylist.SelectedIndex;
                config.CalcPlayKind = cmbHoseiPlay.SelectedIndex;
                config.CalcCommentKind = cmbHoseiComment.SelectedIndex;
                config.CalcCommentUnderLimit = double.Parse(tbHoseiCommentUnderLimit.Text);
                config.CalcPointAllKind = cmbHoseiPointAll.SelectedIndex;

                config.UserNum= int.Parse(tbUserInfoNum.Text);


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
                MessageBox.Show(ex.Message, "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAnalyze.Enabled = true;
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
            dtPLastweekDay.Value = analyzeDay.AddDays(-7).Date;
        }

        private void dtPAnalyzeDay_ValueChanged(object sender, EventArgs e)
        {
            SetEnableAnalyzeDay();
        }

        private void dtPLastweekDay_ValueChanged(object sender, EventArgs e)
        {
            // 有効な集計日になるまでループ
            var lastweekDay = dtPLastweekDay.Value.Date;
            var analyzeDay = dtPAnalyzeDay.Value.Date;
            //未来はNG
            if ( analyzeDay <= lastweekDay)
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
}
