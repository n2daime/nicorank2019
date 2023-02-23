using nicorank2019.frm;
using nicorankLib.Analyze.model;
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
    public partial class frmMain
    {

        EAnalyzeMode eAnalyzeMode;

        /// <summary>
        /// 集計モードを選択する
        /// </summary>
        private void SelectMode()
        {
            var config = Config.GetInstance();
            config.IsSP = false;
            if (rbWeekly.Checked)
            {
                this.eAnalyzeMode = EAnalyzeMode.Weekly;
                this.dtPAnalyzeDay.Enabled = true;
            }
            else if (rbTyukan.Checked)
            {
                this.eAnalyzeMode = EAnalyzeMode.Tyukan;
                this.dtPAnalyzeDay.Enabled = false;

            }
            else if (rbSP.Checked)
            {
                this.eAnalyzeMode = EAnalyzeMode.SP;
                this.dtPAnalyzeDay.Enabled = false;

                config.IsSP = true;
            }
            else
            {
                //サポートしていない
                return;
            }
            tbCalcMylist.Text = config.CalcMyList.ToString();
            tbCalcPlay.Text = config.CalcPlay.ToString();
            tbCalcComment.Text = config.CalcComment.ToString();
            tbCalcLike.Text = config.CalcLike.ToString();

            cmbHoseiMylist.SelectedIndex = config.CalcMyListKind;
            cmbHoseiPlay.SelectedIndex = config.CalcPlayKind;
            cmbHoseiComment.SelectedIndex = config.CalcCommentKind;
            tbHoseiCommentUnderLimit.Text = config.CalcCommentUnderLimit.ToString();

            tbUserInfoNum.Text = config.UserNum.ToString();

            panelSP.Enabled = config.IsSP;

        }

        /// <summary>
        /// 適切なモードファクトリーを取得する
        /// </summary>
        /// <returns></returns>
        private ModeFactoryBase GetModeFactory()
        {
            if (rbWeekly.Checked)
            {
                var factory = new ModeFactoryWeekly();
                factory.SetTargetTime(dtPAnalyzeDay.Value);
                return factory;
            }
            else if (rbTyukan.Checked)
            {
                var factory = new ModeFactoryTyukan();
                return factory;
            }
            else if (rbSP.Checked)
            {
                var factory = new ModeFactroySP();
                factory.SetInputFile(
                    tbAnalyzeDB.Text
                    ,tbBaseDB.Text
                    ,tbMovieSPList.Text
                    ,tbLastResult.Text);
                return factory;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// ランキングを集計する
        /// </summary>
        /// <returns></returns>
        private async Task<bool> AnalyzeAsync()
        {
            bool returnVal = true;
            await Task.Run(() =>
            {
                using (var history = new RankingHistory())

                {
                    if (!history.Open())
                    {
                        StatusLog.WriteLine("データベースがOpenできません");
                        returnVal = false;
                    }
                    else
                    {
                        history.UpdateOfficialRankingDB();

                        this.MainFactory = GetModeFactory();
                        this.MainFactory.CreateAnalyzer();

                        if (!MainFactory.AnalyzeRank())
                        {
                            returnVal = false;
                        }
                        StatusLog.WriteLine("集計成功");

                    }
                    history.Close();
                }

                if (returnVal)
                {
                    var outputList = new List<OutputBase>()
                    {
                        MainFactory.CreateHistory(),
                        MainFactory.TyokiHantei,
                        MainFactory.CreateNRMRank(),
                        MainFactory.CreateNRMRank1000(),
                        MainFactory.CreateNRMRankED(),
                        MainFactory.CreateOutputCSV(),
                        MainFactory.CreateOutputCSV_rankDB(),
                        MainFactory.CreateOutputHTML(),
                        MainFactory.CreateOutputMovieIconGet(),
                        MainFactory.CreateOutputUserIconGet(),
                        MainFactory.CreateOutputWORK()
                     };

                    foreach (var output in outputList)
                    {
                        output?.Execute(MainFactory.RankingList);
                    }
                }
            });
            return returnVal;
        }

    }
}
