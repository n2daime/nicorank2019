using Microsoft.WindowsAPICodePack.Dialogs;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
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
