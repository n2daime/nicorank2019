using Microsoft.WindowsAPICodePack.Dialogs;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nicorank_SnapShot
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }


        private async void btnOK_Click(object sender, EventArgs e)
        {
            this.btnOK.Enabled = false;

            // 取得前に Snapshot API v2 の更新有無を確認する（Issue #38）。
            // 前日データでDBを作ると後段の集計差分がすべてずれるため、未更新時は確認ダイアログで続行可否を問う。
            // 通信は別スレッドで行う。UIスレッド同期だと回線不調時にフォームが無応答になるため
            var versionResult = await Task.Run(() => new SnapShotVersionChecker().Check());
            StatusLog.WriteLine(SnapShotVersionChecker.ToStatusLogLine(versionResult));

            if (versionResult.Status == SnapShotVersionStatus.NotUpdated)
            {
                // 実行日と last_modified（日時まで表示）の両方を出す。何時のデータになるかが分からないと続行判断ができないため。
                // 区切り文字の決定性のため InvariantCulture を指定する
                string today = SnapShotVersionChecker.ToJst(DateTimeOffset.Now).ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                string modified = versionResult.LastModified.HasValue
                    ? SnapShotVersionChecker.ToJst(versionResult.LastModified.Value).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
                    : "不明";
                var confirm = MessageBox.Show(
                    $"ニコ動公式側で本日（{today}）のデータの更新が終わっていません。\n{modified} 時点のデータになる可能性が高いですが、続行しますか？",
                    "ニコラン用スナップショット取得ツール",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.OK)
                {
                    // キャンセル時は取得せず終了する（DBを作らない。誤データ防止が目的のため）
                    this.btnOK.Enabled = true;
                    return;
                }
            }
            else if (versionResult.Status == SnapShotVersionStatus.Unknown)
            {
                // version取得失敗時は更新確認ができないため中断する。古いデータで進めるより止める方が安全なため
                MessageBox.Show(
                    "Snapshot API v2 の更新確認ができませんでした（version取得失敗）。取得を中断します。",
                    "ニコラン用スナップショット取得ツール",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                this.btnOK.Enabled = true;
                return;
            }

            var ctrl = new SnapController();
            bool result = await ctrl.GetSnapShotAsync();

            if (result)
            {
                //アプリケーションを終了する
                if (this.cbSuspend.Checked)
                {
                    //サスペンドダイアログを表示する
                    ShowSuspendDialog();
                }
                else
                {
                    MessageBox.Show("集計が終了しました。アプリケーションを終了します");
                    Application.Exit();
                }
            }
            else
            {
                this.btnOK.Enabled = true;
                MessageBox.Show("集計がエラーになりました。コマンドプロンプトおよび、nicorankerr.logをご確認ください。");
            }
        }

        public static void ShowSuspendDialog()
        {
            var dialog = new TaskDialog();

            dialog.Caption = "ニコラン用スナップショット取得ツール";
            dialog.InstructionText = "集計が終了しました。";
            dialog.Text = "n秒後にPCをサスペンド（休止)します";

            bool isCanceled = false;
            void countMessage(object sender, EventArgs e)
            {
                Task.Run(() =>
                {
                    const int WAIT_TIME = 30;
                    for (int sec = WAIT_TIME; sec >= 0; sec--)
                    {
                        if (isCanceled)
                        {
                            break;
                        }
                        dialog.Text = $"{sec:00}秒後にPCをサスペンド（休止)します..";
                        Thread.Sleep(1000);
                    }
                    dialog?.Close();

                    if (!isCanceled)
                    {
                    //サスペンド
                    Application.SetSuspendState(PowerState.Suspend, false, false);
                    }
                    Application.Exit();
                });
            }

            var button = new TaskDialogButton("button", "Cancel");
            button.Enabled = true;
            button.Click += (sender, e) =>
            {
                isCanceled = true;
                dialog.Close();
                dialog = null;
            };

            dialog.Controls.Add(button);

            dialog.Opened += countMessage;

            dialog.Show();
        }
    }
}
