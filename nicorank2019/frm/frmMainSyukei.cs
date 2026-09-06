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

        /// <summary>
        /// 集計モードを選択する
        /// </summary>
        private void SelectMode()
        {
            var config = Config.GetInstance();
            config.IsSP = false;
            if (rbWeekly.Checked)
            {
                this.dtPAnalyzeDay.Enabled = true;
                this.dtPLastweekDay.Enabled = true;
            }
            else if (rbTyukan.Checked)
            {
                this.dtPAnalyzeDay.Enabled = false;
                this.dtPLastweekDay.Enabled = true;
                this.dtPLastweekDay.Value = DateTime.Today;

            }
            else if (rbSP.Checked)
            {
                this.dtPAnalyzeDay.Enabled = false;
                this.dtPLastweekDay.Enabled = false;

                config.IsSP = true;
            }
            else
            {
                //サポートしていない
                return;
            }
            LoadPointCalcPanel();

            panelSP.Enabled = config.IsSP;

            SetEnableAnalyzeDay();

        }

        /// <summary>
        /// タグ検索モードを選択する（タグタブ表示時）
        /// </summary>
        private void SelectTagMode()
        {
            var config = Config.GetInstance();
            config.IsSP = false;
            config.IsTagRank = true;
            LoadPointCalcPanel();
        }

        /// <summary>
        /// 集計タブのモードに戻す（ラジオボタンに従う）
        /// </summary>
        private void SelectSyukeiMode()
        {
            Config.GetInstance().IsTagRank = false;
            SelectMode();
        }

        /// <summary>
        /// ポイント計算パネルの表示値を現在のモード設定から読み込む
        /// </summary>
        private void LoadPointCalcPanel()
        {
            var config = Config.GetInstance();
            tbCalcMylist.Text = config.CalcMyList.ToString();
            tbCalcPlay.Text = config.CalcPlay.ToString();
            tbCalcComment.Text = config.CalcComment.ToString();
            tbCalcLike.Text = config.CalcLike.ToString();

            cmbHoseiMylist.SelectedIndex = config.CalcMyListKind;
            cmbHoseiPlay.SelectedIndex = config.CalcPlayKind;
            cmbHoseiComment.SelectedIndex = config.CalcCommentKind;
            tbHoseiCommentUnderLimit.Text = config.CalcCommentUnderLimit.ToString();
            cmbHoseiPointAll.SelectedIndex = config.CalcPointAllKind;

            tbUserInfoNum.Text = config.UserNum.ToString();
        }

        /// <summary>
        /// ポイント計算パネルの入力値を現在のモード設定に書き戻す
        /// </summary>
        /// <returns>入力値がすべて有効なら true</returns>
        private bool SavePointCalcPanel()
        {
            var config = Config.GetInstance();

            if (!double.TryParse(tbCalcMylist.Text, out double calcMylist)) { return false; }
            if (!double.TryParse(tbCalcPlay.Text, out double calcPlay)) { return false; }
            if (!double.TryParse(tbCalcComment.Text, out double calcComment)) { return false; }
            if (!double.TryParse(tbCalcLike.Text, out double calcLike)) { return false; }
            if (!double.TryParse(tbHoseiCommentUnderLimit.Text, out double underLimit)) { return false; }
            if (!int.TryParse(tbUserInfoNum.Text, out int userNum)) { return false; }
            if (cmbHoseiMylist.SelectedIndex < 0 || cmbHoseiPlay.SelectedIndex < 0
                || cmbHoseiComment.SelectedIndex < 0 || cmbHoseiPointAll.SelectedIndex < 0) { return false; }

            config.CalcMyList = calcMylist;
            config.CalcPlay = calcPlay;
            config.CalcComment = calcComment;
            config.CalcLike = calcLike;

            config.CalcMyListKind = cmbHoseiMylist.SelectedIndex;
            config.CalcPlayKind = cmbHoseiPlay.SelectedIndex;
            config.CalcCommentKind = cmbHoseiComment.SelectedIndex;
            config.CalcCommentUnderLimit = underLimit;
            config.CalcPointAllKind = cmbHoseiPointAll.SelectedIndex;

            config.UserNum = userNum;
            return true;
        }

        /// <summary>
        /// 適切なモードファクトリーを取得する
        /// </summary>
        /// <returns></returns>
        private ModeFactoryBase GetModeFactory()
        {
            // 集計スレッドから呼ばれるためコントロールには触れない。実行ボタンが退避した条件を使う
            if (_tagExecuteContext != null)
            {
                var tagFactory = new ModeFactoryTagRank();
                tagFactory.SetInputFile(
                    _tagExecuteContext.AnalyzeDB
                    ,_tagExecuteContext.BaseDB
                    ,_tagExecuteContext.Query
                    ,_tagExecuteContext.LastResult);
                return tagFactory;
            }
            else if (rbWeekly.Checked)
            {
                var factory = new ModeFactoryWeekly();
                factory.SetTargetTime(dtPAnalyzeDay.Value);
                factory.SeBaseTime(dtPLastweekDay.Value.Date);
                return factory;
            }
            else if (rbTyukan.Checked)
            {
                var factory = new ModeFactoryTyukan();
                factory.SetLastWeekDay(dtPLastweekDay.Value.Date);
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
                        // 集計開始時に各DBの更新確認を指示する（失敗時は中断）
                        var migrationCoordinator = new DbMigrationCoordinator(new List<IDbMigratable>
                        {
                            history,
                            // モードは移行処理に無関係のためWeeklyを仮指定する。将来の移行処理もMode依存禁止
                            new ResultHistory(EAnalyzeMode.Weekly)
                        });
                        if (!migrationCoordinator.EnsureAllAtAnalyzeStart())
                        {
                            returnVal = false;
                        }
                        else if (!history.UpdateOfficialRankingDB())
                        {
                            returnVal = false;
                        }
                        else
                        {
                            this.MainFactory = GetModeFactory();
                            if (this.MainFactory == null)
                            {
                                StatusLog.WriteLine("集計モードを特定できません");
                                returnVal = false;
                            }
                            else if (!this.MainFactory.CreateAnalyzer())
                            {
                                StatusLog.WriteLine("集計の準備に失敗しました");
                                returnVal = false;
                            }
                            else if (!MainFactory.AnalyzeRank())
                            {
                                returnVal = false;
                            }
                            StatusLog.WriteLine("集計成功");
                        }
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
                        MainFactory.CreateOutputHTML(),
                        MainFactory.CreateOutputMovieIconGet(),
                        MainFactory.CreateOutputUserIconGet(),
                        MainFactory.CreateOutputWORK(),
                        MainFactory.CreateOutputJson_rankDB()
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
