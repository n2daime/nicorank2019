using nicorank2019.frm;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
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
using System.Diagnostics;
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

        // アプリ埋め込みアイコンのマニフェスト名。csproj の EmbeddedResource の LogicalName と一致させること。
        private const string AppIconResourceName = "nicorank2019.icon.ico";

        public frmMain()
        {
            InitializeComponent();
            // exe と同じアイコンをフォーム左上・タスクバーに表示する (#49)。
            // Designer 再生成の差分 churn を避けるためコード側で設定する。アイコンは起動の必須要素ではないため、読めなくても既定アイコンで起動を続ける。
            try
            {
                using (var stream = typeof(frmMain).Assembly.GetManifestResourceStream(AppIconResourceName))
                {
                    if (stream != null)
                    {
                        this.Icon = new System.Drawing.Icon(stream);
                    }
                }
            }
            catch (ArgumentException)
            {
                // 埋め込みアイコンが破損している場合は既定アイコンのまま起動する
            }
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
                // 起動直後の更新確認（Issue #47）。UI をブロックしないよう非同期で投げっぱなしにする。
                // Load 自体は先に返る。確認不能時は黙って旧版のまま動かす。
                StartAppUpdateCheckOnStartup();
                // 更新確認ラベルの初期表示。版数は実行時にしか分からないため Load で設定する。
                SetUpdateCheckStatus("更新: 未確認");
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
            // 週刊モードでは集計開始前に抜けを自動確認する（Issue #45）。
            // なぜ週刊のみか：抜けの害（長期判定の欠け）が週刊集計に限られるためである。
            // 中止が選ばれたら集計を開始しない。確認不能時は開始する（初回利用者の詰み防止）。
            // 対象日はUIスレッドで読む（集計スレッドからコントロールに触らない。pitfalls項目19）。
            // チェック中も実行系ボタンを止め、連打による二重起動を防ぐ。
            if (rbWeekly.Checked)
            {
                DateTime targetDay = dtPAnalyzeDay.Value.Date;
                SetVacuumRunning(true);
                try
                {
                    if (!await CheckWeeklyGapBeforeAnalyzeAsync(targetDay))
                    {
                        return;
                    }
                }
                finally
                {
                    SetVacuumRunning(false);
                }
            }
            _tagExecuteContext = null;
            await ExecuteAnalyzeAsync(btnAnalyze);
        }

        /// <summary>
        /// 集計を実行して結果を報告する（集計タブ・タグタブ共通）。
        /// 集計中はタブ切替も止める。タブ切替で IsSP／IsTagRank が変わると、Ranking.CalcPoint が
        /// Config を都度読むため残りの動画が別モードのOFFSETで計算され得る。これを防ぐためである。
        /// </summary>
        private async Task ExecuteAnalyzeAsync(Button execButton)
        {
            try
            {
                execButton.Enabled = false;
                // 集計実行中は最適化を開始できないようにする（逆方向の同時実行防止。最適化側も集計ボタンを止める）
                SetVacuumControlsEnabled(false);
                tabPageOut.Enabled = false;

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
                tabPageOut.Enabled = true;
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
        // 更新案内ダイアログの表示中フラグ。起動時確認と手動確認が重なった場合の二重表示と二重適用を防ぐ。
        private bool _updateDialogOpen = false;

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
            // 抜けチェック・復旧もDBを開くため、最適化・集計との同時実行を防ぐ（DBロック競合の防止）。
            btnGapCheckExec.Enabled = enabled;
            chkGapCheckOneYear.Enabled = enabled;
            btnBaselineRestore.Enabled = enabled;
            // 更新確認もネットワークと配置先を使う実行系のため、同時実行を防ぐ対象に含める。
            btnUpdateCheck.Enabled = enabled;
        }

        /// <summary>
        /// メンテナンスタブの「抜けをチェック」ボタン(Issue #45)。
        /// 直近3か月（既定）または直近1年の月曜期待週に対し、LastResult(Weekly) の歯抜けを検出する。
        /// 実処理は集計スレッド側で行い、UI更新はawait復帰後のUIスレッドで行う
        /// （集計スレッドからコントロールに触らない。pitfalls項目19）。
        /// 実行ログは集計タブと同様にコンソール側（StatusLog）へ出すため、タブ内にログ欄は持たない。
        /// </summary>
        private async void btnGapCheckExec_Click(object sender, EventArgs e)
        {
            // チェック状態の読み取りはUIスレッドで行う（タグ検索のTagExecuteContextと同一理由）。
            // ONなら直近1年（52週）、OFFなら直近3か月（13週・動作変わらず）とする。
            int weeks = chkGapCheckOneYear.Checked ? WeeklyGapChecker.YearLookbackWeeks : WeeklyGapChecker.DefaultLookbackWeeks;
            SetVacuumRunning(true);
            lblGapCheckStatus.Text = "状態: 実行中...";
            lblGapCheckResult.Text = "結果: 実行中...";
            try
            {
                GapCheckResult result = await System.Threading.Tasks.Task.Run(() =>
                    WeeklyGapChecker.Check(
                        historyDbPath: DB.NiCORAN_HISTORY,
                        officialDbPath: DB.LOG_OFFICEIAL,
                        today: DateTime.Today,
                        weeks: weeks));
                ShowGapCheckResult(result);
            }
            catch (Exception ex)
            {
                lblGapCheckStatus.Text = "状態: 失敗";
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetVacuumRunning(false);
            }
        }

        /// <summary>
        /// 抜けチェック結果の表示共通化（手動ボタンと自動警告で共用）。
        /// 抜けがあれば日付を列挙する。なぜ列挙か：長期判定の欠けは画面上では気づきにくいため、
        /// 日付の列挙が必須だからである。
        /// </summary>
        private void ShowGapCheckResult(GapCheckResult result)
        {
            if (result == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                lblGapCheckStatus.Text = "状態: 確認不能";
                lblGapCheckResult.Text = "結果: " + result.ErrorMessage;
                StatusLog.WriteLine("集計抜けチェックができませんでした: " + result.ErrorMessage);
                MessageBox.Show(result.ErrorMessage, "集計抜けチェック", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (result.Missing == null || result.Missing.Count == 0)
            {
                lblGapCheckStatus.Text = "状態: 完了（抜けなし）";
                lblGapCheckResult.Text = "結果: 抜けなし";
                StatusLog.WriteLine("集計抜けチェック：抜けはありませんでした");
                MessageBox.Show("集計の抜けはありませんでした", "集計抜けチェック", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string dates = WeeklyGapChecker.FormatMissing(result.Missing);
            lblGapCheckStatus.Text = string.Format("状態: 完了（抜け {0}件）", result.Missing.Count);
            lblGapCheckResult.Text = "結果: 抜け " + dates;
            StatusLog.WriteLine("集計抜けチェック：抜けがあります: " + dates);
            MessageBox.Show(
                "集計の抜けがあります: " + dates + "\nベースラインDBで復旧するか、回収集計している人に相談してください",
                "集計抜けチェック", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// メンテナンスタブの「ベースラインDBで復旧」ボタン(Issue #45)。
        /// 配布中のベースラインで本地の2DBを上書きする。既存DBは DB/backup 以下へ自動退避する。
        /// 配布が本地より古い場合は警告して中断する（必須仕様）。
        /// なぜ中断か：古い配布を被せると逆に抜けを増やすからである。
        /// </summary>
        private async void btnBaselineRestore_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "配布中のベースラインDBで本地の NicoranHistory.db を上書きします。\n"
                + "既存DBは DB/backup 以下へ自動退避します。LogOfficial.db には触れません。続行しますか。",
                "ベースライン復旧", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (confirm != DialogResult.OK)
            {
                return;
            }
            SetVacuumRunning(true);
            lblGapCheckStatus.Text = "状態: 復旧中...";
            try
            {
                BaselineDownloader.BaselineRestoreResult result = await System.Threading.Tasks.Task.Run(() =>
                {
                    var downloader = new BaselineDownloader();
                    return downloader.RestoreBaseline();
                });
                if (result.Success)
                {
                    lblGapCheckStatus.Text = "状態: 復旧完了";
                    lblGapCheckResult.Text = "結果: " + result.Message;
                    MessageBox.Show(result.Message, "ベースライン復旧", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (result.StaleBlocked)
                {
                    // 中断理由（配布が古い／本地を確認不能／配布内容を確認不能／配布内容が古い／配布内容にデータなし）で表示を分ける。
                    // なぜ分けるか：原因が伝わらないと次の行動（相談かDB修復か）が選べないためである。
                    string reason = string.IsNullOrEmpty(result.StaleReason) ? "配布が古い" : result.StaleReason;
                    lblGapCheckStatus.Text = "状態: 復旧中断（" + reason + "）";
                    lblGapCheckResult.Text = "結果: " + result.Message;
                    MessageBox.Show(result.Message, "ベースライン復旧", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    lblGapCheckStatus.Text = "状態: 復旧失敗";
                    lblGapCheckResult.Text = "結果: " + result.Message;
                    MessageBox.Show(result.Message + "。コンソールとnicorankerr.logを確認してください",
                        "ベースライン復旧", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                lblGapCheckStatus.Text = "状態: 失敗";
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetVacuumRunning(false);
            }
        }

        /// <summary>
        /// 週刊集計開始時の抜け自動警告(Issue #45)。
        /// UIスレッドで週刊判定し、短時間の読取だけ集計スレッド側で行う。
        /// 抜けがあれば続行／中止を選び、中止なら集計を開始しない。
        /// なぜ週刊のみか：抜けの害（長期判定の欠け）が週刊集計に限られるためである。
        /// 集計対象日は抜けに数えない。なぜ数えないか：対象週の結果はこの実行で初めて
        /// LastResult に書かれるため、数えると毎回必ず警告になるからである。
        /// </summary>
        /// <param name="targetDay">集計対象日（UIスレッドで読んだ dtPAnalyzeDay の値）</param>
        /// <returns>集計を開始してよければ true、中止なら false</returns>
        private async Task<bool> CheckWeeklyGapBeforeAnalyzeAsync(DateTime targetDay)
        {
            GapCheckResult result = await System.Threading.Tasks.Task.Run(() =>
                WeeklyGapChecker.Check(
                    historyDbPath: DB.NiCORAN_HISTORY,
                    officialDbPath: DB.LOG_OFFICEIAL,
                    today: DateTime.Today,
                    weeks: WeeklyGapChecker.DefaultLookbackWeeks));
            if (result == null || !string.IsNullOrEmpty(result.ErrorMessage))
            {
                // 確認不能時は集計を止めない。なぜ止めないか：DB不在は #36 の自動取得で解消できる正常系であり、
                // 確認不能を理由に集計全体を止めると初回利用者が詰むためである。
                return true;
            }
            result.Missing = WeeklyGapChecker.ExcludeTargetDay(result.Missing, targetDay);
            if (result.Missing == null || result.Missing.Count == 0)
            {
                return true;
            }
            string dates = WeeklyGapChecker.FormatMissing(result.Missing);
            StatusLog.WriteLine("集計抜け警告：抜けがあります: " + dates);
            // 結果ラベルにも残し、手動チェックなしで復旧に進めるようにする。
            lblGapCheckStatus.Text = string.Format("状態: 抜けあり（{0}件）", result.Missing.Count);
            lblGapCheckResult.Text = "結果: 抜け " + dates;
            var answer = MessageBox.Show(
                "過去の集計に抜けがあります: " + dates + "\n"
                + "このまま集計すると長期動画判定に欠けが残る場合があります。\n"
                + "メンテナンスタブの「ベースラインDBで復旧」で直せる場合があります"
                + "（配布が古い場合は回収集計している人に相談してください）。\n"
                + "集計を続行しますか。",
                "集計抜け警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return answer == DialogResult.Yes;
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
                // 前回成功の時点表示が残ると誤解されるため、例外時も非表示に戻す
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
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
                // 前回成功の時点表示が残ると誤解されるため、例外時も非表示に戻す
                lblTagSnapshotTime.Visible = false;
                lblTagSnapshotTime.Text = "";
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

        /// <summary>
        /// updater の exe 名（本体 zip に同梱する分離更新担当。Issue #47）。
        /// </summary>
        private const string UpdaterExeName = "nicorankUpdater.exe";

        /// <summary>
        /// 起動直後の更新確認（Issue #47）。
        /// UI スレッドをブロックしないよう取得は Task.Run で行い、復帰後の UI スレッドでダイアログを出す
        /// （集計スレッドからコントロールに触らない。pitfalls項目19）。
        /// SQLite を使わないため lib/ 欠け時でも動く。確認不能時は黙って旧版のまま動かす。
        /// </summary>
        private async void StartAppUpdateCheckOnStartup()
        {
            try
            {
                AppUpdateChecker.UpdateCheckResult result = await System.Threading.Tasks.Task.Run(() => new AppUpdateChecker().Check());
                if (result != null && result.Status == AppUpdateChecker.UpdateCheckStatus.Available && result.Entry != null)
                {
                    // 取得待ちの間に集計・最適化が始まっていたら案内を見送る。実行中の集計を殺さないため。
                    if (IsWorkRunning())
                    {
                        StatusLog.WriteLine("集計実行中のため更新案内を見送りました。メンテナンスタブの「更新を確認」から確認できます");
                        return;
                    }
                    OfferAppUpdate(result.Entry, false);
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }
        }

        /// <summary>
        /// メンテナンスタブの「更新を確認」ボタン（Issue #47）。
        /// 24時間間引きを無視して必ず取得する。押したのに何も起きない操作にしないためである。
        /// 実処理は集計スレッド側で行い、UI更新はawait復帰後のUIスレッドで行う
        /// （集計スレッドからコントロールに触らない。pitfalls項目19）。
        /// 実行ログは集計タブと同様にコンソール側（StatusLog）へ出すため、タブ内にログ欄は持たない。
        /// </summary>
        private async void btnUpdateCheck_Click(object sender, EventArgs e)
        {
            // 更新案内ダイアログの表示中は再実行しない。二重適用を防ぐため（再レビュー指摘対応）。
            if (_updateDialogOpen)
            {
                return;
            }
            SetVacuumRunning(true);
            SetUpdateCheckStatus("更新: 確認中...");
            try
            {
                AppUpdateChecker.UpdateCheckResult result = await System.Threading.Tasks.Task.Run(() => new AppUpdateChecker().Check(true));
                if (result == null || result.Status == AppUpdateChecker.UpdateCheckStatus.Unknown)
                {
                    SetUpdateCheckStatus("更新: 確認不能");
                    StatusLog.WriteLine("アプリの更新を確認できませんでした。ネットワークと配布場所を確認してください");
                    MessageBox.Show("更新を確認できませんでした。ネットワークと配布場所を確認してください", "更新の確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (result.Status == AppUpdateChecker.UpdateCheckStatus.UpToDate)
                {
                    SetUpdateCheckStatus("更新: 最新です");
                    StatusLog.WriteLine("アプリは最新版です");
                    MessageBox.Show("最新版です", "更新の確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (result.Status == AppUpdateChecker.UpdateCheckStatus.Throttled)
                {
                    SetUpdateCheckStatus("更新: 未確認");
                    return;
                }
                if (result.Entry == null)
                {
                    SetUpdateCheckStatus("更新: 確認不能");
                    return;
                }
                SetUpdateCheckStatus("更新: 新版あり " + result.Entry.Version);
                // 手動ボタン経路は SetVacuumRunning のロックを保持しているため、適用ガードの対象外とする。
                OfferAppUpdate(result.Entry, true);
            }
            catch (Exception ex)
            {
                SetUpdateCheckStatus("更新: 失敗");
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetVacuumRunning(false);
            }
        }

        /// <summary>
        /// 更新確認の状態ラベルを設定する。末尾に現状版数を付ける。
        /// なぜ付けるか：トラブル時のバージョン特定のため（Issue #47 検証での指摘）。
        /// 個別に付けると付け忘れが出るため、このヘルパーに一本化する。
        /// 版数はエントリアセンブリから読む。取得不能時は「不明」と出す。
        /// </summary>
        private void SetUpdateCheckStatus(string message)
        {
            lblUpdateCheckStatus.Text = message + " 現状バージョン " + CurrentVersionText();
        }

        /// <summary>
        /// 現状版数の表示文言。版数はエントリアセンブリから読む。取得不能時は「不明」とする。
        /// </summary>
        private static string CurrentVersionText()
        {
            try
            {
                Version current = new AppUpdateChecker().CurrentVersion;
                return current != null ? current.ToString() : "不明";
            }
            catch
            {
                return "不明";
            }
        }

        /// <summary>
        /// 更新案内ダイアログを出す。新版の版数・サイズ・詳細リンクを示し、今すぐ更新か後でかを選ぶ。
        /// 第一弾は任意適用のみであり、月曜直前の強制更新はしない（Issue 合意。必須化は将来Issueへ分離）。
        /// ダイアログ表示後は通知済み版を記録し、同一版の再通知を抑える（1日1回まで）。
        /// </summary>
        /// <param name="entry">配布情報（Check の Available で得たもの）</param>
        /// <param name="lockHeld">実行系のロック（SetVacuumRunning）を保持していれば true。手動ボタン経路のみ真になる</param>
        private void OfferAppUpdate(AppUpdateChecker.UpdateFileEntry entry, bool lockHeld)
        {
            // 二重表示の防止：起動時確認と手動確認が重なった場合は先勝ちにする。
            // 二重適用（二重 updater 起動）を防ぐためでもある。
            if (_updateDialogOpen)
            {
                return;
            }
            _updateDialogOpen = true;
            try
            {
                bool updateNow = ShowUpdateDialog(entry);
                // 表示したこと自体を記録する。後でを選んだ同一版で毎起動うるさくしないためである。
                new AppUpdateChecker().MarkNotified(entry.Version);
                if (!updateNow)
                {
                    return;
                }
                ApplyAppUpdate(entry, lockHeld);
            }
            finally
            {
                _updateDialogOpen = false;
            }
        }

        /// <summary>
        /// 更新案内ダイアログの実体。Designer を使わずコードで組み立てる。
        /// なぜコードか：リンク付きの小さな確認画面のためだけに Designer・resx を増やすと差分が大きくなるためである。
        /// </summary>
        /// <returns>今すぐ更新が選ばれれば true</returns>
        private bool ShowUpdateDialog(AppUpdateChecker.UpdateFileEntry entry)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "更新のお知らせ";
                dialog.Size = new System.Drawing.Size(460, 230);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                var lblMsg = new Label();
                lblMsg.AutoSize = true;
                lblMsg.Location = new System.Drawing.Point(12, 12);
                lblMsg.Text = "新しい版があります: " + entry.Version
                    + "（約" + DbOptimizer.FormatFileSize(entry.Size) + "）\r\n"
                    + "現状バージョン: " + CurrentVersionText() + "\r\n"
                    + "今すぐ更新しますか。集計実行中の更新はできません。";

                var linkNotes = new LinkLabel();
                linkNotes.AutoSize = true;
                linkNotes.Location = new System.Drawing.Point(12, 90);
                linkNotes.Text = "詳細（リリースノート）を開く";
                linkNotes.Visible = !string.IsNullOrWhiteSpace(entry.Notes);
                string notesUrl = entry.Notes;
                linkNotes.LinkClicked += (s, e) =>
                {
                    // リンク先を開けなくても更新可否の判断はできるため、失敗時は ErrLog に残して続ける。
                    try { Process.Start(notesUrl); }
                    catch (Exception ex) { ErrLog.GetInstance().Write(ex); }
                };

                var btnNow = new Button();
                btnNow.Text = "今すぐ更新";
                btnNow.DialogResult = DialogResult.OK;
                btnNow.Location = new System.Drawing.Point(100, 140);
                btnNow.Size = new System.Drawing.Size(120, 32);

                var btnLater = new Button();
                btnLater.Text = "後で";
                btnLater.DialogResult = DialogResult.Cancel;
                btnLater.Location = new System.Drawing.Point(240, 140);
                btnLater.Size = new System.Drawing.Size(120, 32);

                dialog.Controls.Add(lblMsg);
                dialog.Controls.Add(linkNotes);
                dialog.Controls.Add(btnNow);
                dialog.Controls.Add(btnLater);
                dialog.AcceptButton = btnNow;
                dialog.CancelButton = btnLater;
                return dialog.ShowDialog(this) == DialogResult.OK;
            }
        }

        /// <summary>
        /// 分離 updater に置換させる。本体が更新を検出→updater を起動→本体終了→updater が待機・置換・再起動する。
        /// 置換前は旧版を退避する（#45 の DB 退避と同型）。置換対象は exe・config・lib/ 一式とし、
        /// 部分欠けを構造的に起こせなくする（Issue 合意）。
        /// </summary>
        /// <param name="entry">配布情報（Check の Available で得たもの）</param>
        /// <param name="lockHeld">実行系のロック（SetVacuumRunning）を保持していれば true。手動ボタン経路のみ真になる</param>
        private void ApplyAppUpdate(AppUpdateChecker.UpdateFileEntry entry, bool lockHeld)
        {
            try
            {
                // 集計・最適化の実行中は適用しない。Application.Exit() で実行中プロセスを殺す事故を防ぐため。
                // 手動ボタン経路はロック保持中のため対象外とする（押下時点で何も実行されていないことが保証される）。
                if (!lockHeld && IsWorkRunning())
                {
                    MessageBox.Show("集計または最適化の実行中は更新できません。完了後にメンテナンスタブの「更新を確認」から更新してください",
                        "更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string updaterPath = Path.Combine(baseDir, UpdaterExeName);
                var checker = new AppUpdateChecker();
                if (!checker.WriteTaskFile(entry, baseDir, Application.ExecutablePath, Process.GetCurrentProcess().Id))
                {
                    MessageBox.Show("更新の準備に失敗しました。コンソールとnicorankerr.logを確認してください",
                        "更新エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!File.Exists(updaterPath))
                {
                    // updater 自体がない旧 zip からの更新では自動置換できない。zip 全上書きの再展開で解決する。
                    MessageBox.Show("更新担当（" + UpdaterExeName + "）が見つかりません。配布 zip の全上書きで更新してください",
                        "更新エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                StatusLog.WriteLine("アプリを更新します。再起動します...");
                Process.Start(updaterPath);
                Application.Exit();
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                MessageBox.Show(GetExceptionMessages(ex), "システムエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 集計・最適化のいずれかが実行中かを返す。更新適用のガードに使う。
        /// _vacuumRunning は最適化・抜けチェック・手動更新確認の実行中に立ち、
        /// btnAnalyze と tabPageOut の無効は集計実行中の ExecuteAnalyzeAsync が行う。
        /// btnAnalyzeTag の無効は件数超過でも起きるため判定に使わない。
        /// _updateDialogOpen は更新案内ダイアログの表示中を示す（再レビュー指摘対応）。
        /// タグ集計の件数確認中（CheckTagCountAsync の取得）は対象外とする。
        /// ダイアログを挟むため事故確率は低く、厳密化は将来課題とする。
        /// </summary>
        private bool IsWorkRunning()
        {
            return _vacuumRunning || _updateDialogOpen || !btnAnalyze.Enabled || !tabPageOut.Enabled;
        }

    }
}
