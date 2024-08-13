namespace nicorank2019.frm
{
    partial class frmMain
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージ リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.pnlOutput = new System.Windows.Forms.Panel();
            this.tabPageSyukei = new System.Windows.Forms.TabPage();
            this.btnAnalyze = new System.Windows.Forms.Button();
            this.panel3 = new System.Windows.Forms.Panel();
            this.label18 = new System.Windows.Forms.Label();
            this.tbUserInfoNum = new System.Windows.Forms.TextBox();
            this.label15 = new System.Windows.Forms.Label();
            this.cmbHoseiPointAll = new System.Windows.Forms.ComboBox();
            this.cmbHoseiComment = new System.Windows.Forms.ComboBox();
            this.cmbHoseiPlay = new System.Windows.Forms.ComboBox();
            this.cmbHoseiMylist = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.tbCalcMylist = new System.Windows.Forms.TextBox();
            this.tbHoseiCommentUnderLimit = new System.Windows.Forms.TextBox();
            this.tbCalcLike = new System.Windows.Forms.TextBox();
            this.tbCalcPlay = new System.Windows.Forms.TextBox();
            this.label17 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.tbCalcComment = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.panelSP = new System.Windows.Forms.Panel();
            this.btnLastResult = new System.Windows.Forms.Button();
            this.btnMovieSPList = new System.Windows.Forms.Button();
            this.tbLastResult = new System.Windows.Forms.TextBox();
            this.tbMovieSPList = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.btnAnalyzeDB = new System.Windows.Forms.Button();
            this.btnBaseDB = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.tbAnalyzeDB = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tbBaseDB = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
            this.rbSP = new System.Windows.Forms.RadioButton();
            this.dtPAnalyzeDay = new System.Windows.Forms.DateTimePicker();
            this.rbTyukan = new System.Windows.Forms.RadioButton();
            this.rbWeekly = new System.Windows.Forms.RadioButton();
            this.tabPageOut = new System.Windows.Forms.TabControl();
            this.label19 = new System.Windows.Forms.Label();
            this.dtPLastweekDay = new System.Windows.Forms.DateTimePicker();
            this.tabPage2.SuspendLayout();
            this.tabPageSyukei.SuspendLayout();
            this.panel3.SuspendLayout();
            this.panelSP.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabPageOut.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.pnlOutput);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(828, 635);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "出力1";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // pnlOutput
            // 
            this.pnlOutput.BackColor = System.Drawing.SystemColors.Control;
            this.pnlOutput.Enabled = false;
            this.pnlOutput.Location = new System.Drawing.Point(0, 0);
            this.pnlOutput.Name = "pnlOutput";
            this.pnlOutput.Size = new System.Drawing.Size(832, 639);
            this.pnlOutput.TabIndex = 0;
            // 
            // tabPageSyukei
            // 
            this.tabPageSyukei.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageSyukei.Controls.Add(this.btnAnalyze);
            this.tabPageSyukei.Controls.Add(this.panel3);
            this.tabPageSyukei.Controls.Add(this.panelSP);
            this.tabPageSyukei.Controls.Add(this.groupBox1);
            this.tabPageSyukei.Location = new System.Drawing.Point(4, 22);
            this.tabPageSyukei.Name = "tabPageSyukei";
            this.tabPageSyukei.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageSyukei.Size = new System.Drawing.Size(828, 635);
            this.tabPageSyukei.TabIndex = 0;
            this.tabPageSyukei.Text = "集計";
            // 
            // btnAnalyze
            // 
            this.btnAnalyze.Location = new System.Drawing.Point(329, 467);
            this.btnAnalyze.Name = "btnAnalyze";
            this.btnAnalyze.Size = new System.Drawing.Size(139, 39);
            this.btnAnalyze.TabIndex = 4;
            this.btnAnalyze.Text = "ランキング計算";
            this.btnAnalyze.UseVisualStyleBackColor = true;
            this.btnAnalyze.Click += new System.EventHandler(this.btnAnalyze_Click);
            // 
            // panel3
            // 
            this.panel3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel3.Controls.Add(this.label18);
            this.panel3.Controls.Add(this.tbUserInfoNum);
            this.panel3.Controls.Add(this.label15);
            this.panel3.Controls.Add(this.cmbHoseiPointAll);
            this.panel3.Controls.Add(this.cmbHoseiComment);
            this.panel3.Controls.Add(this.cmbHoseiPlay);
            this.panel3.Controls.Add(this.cmbHoseiMylist);
            this.panel3.Controls.Add(this.label8);
            this.panel3.Controls.Add(this.label9);
            this.panel3.Controls.Add(this.label10);
            this.panel3.Controls.Add(this.tbCalcMylist);
            this.panel3.Controls.Add(this.tbHoseiCommentUnderLimit);
            this.panel3.Controls.Add(this.tbCalcLike);
            this.panel3.Controls.Add(this.tbCalcPlay);
            this.panel3.Controls.Add(this.label17);
            this.panel3.Controls.Add(this.label16);
            this.panel3.Controls.Add(this.tbCalcComment);
            this.panel3.Controls.Add(this.label11);
            this.panel3.Controls.Add(this.label12);
            this.panel3.Controls.Add(this.label13);
            this.panel3.Controls.Add(this.label14);
            this.panel3.Location = new System.Drawing.Point(11, 224);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(794, 228);
            this.panel3.TabIndex = 3;
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(153, 127);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(89, 12);
            this.label18.TabIndex = 17;
            this.label18.Text = "ポイント全体補正";
            // 
            // tbUserInfoNum
            // 
            this.tbUserInfoNum.Location = new System.Drawing.Point(347, 194);
            this.tbUserInfoNum.Name = "tbUserInfoNum";
            this.tbUserInfoNum.Size = new System.Drawing.Size(92, 19);
            this.tbUserInfoNum.TabIndex = 16;
            this.tbUserInfoNum.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(9, 194);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(326, 12);
            this.label15.TabIndex = 15;
            this.label15.Text = "UserInfo/Iconを取得する動画数。長期は考慮しないので単純指定";
            // 
            // cmbHoseiPointAll
            // 
            this.cmbHoseiPointAll.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbHoseiPointAll.FormattingEnabled = true;
            this.cmbHoseiPointAll.Items.AddRange(new object[] {
            "0: しない",
            "1: 補正値D = 2.5 * コメント数 ÷ 再生数 * 100 (米率0.4％未満に補正)"});
            this.cmbHoseiPointAll.Location = new System.Drawing.Point(251, 124);
            this.cmbHoseiPointAll.Name = "cmbHoseiPointAll";
            this.cmbHoseiPointAll.Size = new System.Drawing.Size(450, 20);
            this.cmbHoseiPointAll.TabIndex = 14;
            // 
            // cmbHoseiComment
            // 
            this.cmbHoseiComment.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbHoseiComment.FormattingEnabled = true;
            this.cmbHoseiComment.Items.AddRange(new object[] {
            "0: しない",
            "1: 補正 (再生＋マイリスト＋コメント＊N）／（再生＋マイリスト＋コメント",
            "2: 補正 (再生＋マイリスト＋コメント）／（再生＋マイリスト＋コメント"});
            this.cmbHoseiComment.Location = new System.Drawing.Point(251, 73);
            this.cmbHoseiComment.Name = "cmbHoseiComment";
            this.cmbHoseiComment.Size = new System.Drawing.Size(450, 20);
            this.cmbHoseiComment.TabIndex = 14;
            // 
            // cmbHoseiPlay
            // 
            this.cmbHoseiPlay.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbHoseiPlay.FormattingEnabled = true;
            this.cmbHoseiPlay.Items.AddRange(new object[] {
            "0: しない",
            "1: 補正値＝マイリスト率が1%未満の作品に適用する",
            "2: 補正値＝(コメント数＋マイリスト数＋いいね数）/ 再生数 ×1000"});
            this.cmbHoseiPlay.Location = new System.Drawing.Point(251, 49);
            this.cmbHoseiPlay.Name = "cmbHoseiPlay";
            this.cmbHoseiPlay.Size = new System.Drawing.Size(450, 20);
            this.cmbHoseiPlay.TabIndex = 13;
            // 
            // cmbHoseiMylist
            // 
            this.cmbHoseiMylist.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbHoseiMylist.FormattingEnabled = true;
            this.cmbHoseiMylist.Items.AddRange(new object[] {
            "0: しない",
            "1: 弱い方",
            "2: 強い方 "});
            this.cmbHoseiMylist.Location = new System.Drawing.Point(251, 23);
            this.cmbHoseiMylist.Name = "cmbHoseiMylist";
            this.cmbHoseiMylist.Size = new System.Drawing.Size(450, 20);
            this.cmbHoseiMylist.TabIndex = 12;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(180, 76);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(62, 12);
            this.label8.TabIndex = 11;
            this.label8.Text = "コメント補正";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(189, 50);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(53, 12);
            this.label9.TabIndex = 10;
            this.label9.Text = "再生補正";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(170, 26);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(71, 12);
            this.label10.TabIndex = 9;
            this.label10.Text = "マイリスト補正";
            // 
            // tbCalcMylist
            // 
            this.tbCalcMylist.Location = new System.Drawing.Point(89, 27);
            this.tbCalcMylist.Name = "tbCalcMylist";
            this.tbCalcMylist.Size = new System.Drawing.Size(49, 19);
            this.tbCalcMylist.TabIndex = 8;
            this.tbCalcMylist.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // tbHoseiCommentUnderLimit
            // 
            this.tbHoseiCommentUnderLimit.Location = new System.Drawing.Point(251, 99);
            this.tbHoseiCommentUnderLimit.Name = "tbHoseiCommentUnderLimit";
            this.tbHoseiCommentUnderLimit.Size = new System.Drawing.Size(49, 19);
            this.tbHoseiCommentUnderLimit.TabIndex = 6;
            this.tbHoseiCommentUnderLimit.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // tbCalcLike
            // 
            this.tbCalcLike.Location = new System.Drawing.Point(89, 99);
            this.tbCalcLike.Name = "tbCalcLike";
            this.tbCalcLike.Size = new System.Drawing.Size(49, 19);
            this.tbCalcLike.TabIndex = 6;
            this.tbCalcLike.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // tbCalcPlay
            // 
            this.tbCalcPlay.Location = new System.Drawing.Point(89, 50);
            this.tbCalcPlay.Name = "tbCalcPlay";
            this.tbCalcPlay.Size = new System.Drawing.Size(49, 19);
            this.tbCalcPlay.TabIndex = 7;
            this.tbCalcPlay.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(144, 101);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(98, 12);
            this.label17.TabIndex = 3;
            this.label17.Text = "コメント補正下限値";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(14, 101);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(60, 12);
            this.label16.TabIndex = 3;
            this.label16.Text = "いいね倍率";
            // 
            // tbCalcComment
            // 
            this.tbCalcComment.Location = new System.Drawing.Point(89, 73);
            this.tbCalcComment.Name = "tbCalcComment";
            this.tbCalcComment.Size = new System.Drawing.Size(49, 19);
            this.tbCalcComment.TabIndex = 6;
            this.tbCalcComment.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(14, 75);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(62, 12);
            this.label11.TabIndex = 3;
            this.label11.Text = "コメント倍率";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(14, 53);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(53, 12);
            this.label12.TabIndex = 2;
            this.label12.Text = "再生倍率";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(14, 30);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(71, 12);
            this.label13.TabIndex = 1;
            this.label13.Text = "マイリスト倍率";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(9, 6);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(179, 12);
            this.label14.TabIndex = 0;
            this.label14.Text = "ポイント計算方法(nicorank.xmlより）";
            // 
            // panelSP
            // 
            this.panelSP.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelSP.Controls.Add(this.btnLastResult);
            this.panelSP.Controls.Add(this.btnMovieSPList);
            this.panelSP.Controls.Add(this.tbLastResult);
            this.panelSP.Controls.Add(this.tbMovieSPList);
            this.panelSP.Controls.Add(this.label5);
            this.panelSP.Controls.Add(this.label4);
            this.panelSP.Controls.Add(this.btnAnalyzeDB);
            this.panelSP.Controls.Add(this.btnBaseDB);
            this.panelSP.Controls.Add(this.label6);
            this.panelSP.Controls.Add(this.tbAnalyzeDB);
            this.panelSP.Controls.Add(this.label3);
            this.panelSP.Controls.Add(this.label2);
            this.panelSP.Controls.Add(this.tbBaseDB);
            this.panelSP.Controls.Add(this.label1);
            this.panelSP.Location = new System.Drawing.Point(8, 79);
            this.panelSP.Name = "panelSP";
            this.panelSP.Size = new System.Drawing.Size(799, 133);
            this.panelSP.TabIndex = 1;
            // 
            // btnLastResult
            // 
            this.btnLastResult.Location = new System.Drawing.Point(694, 101);
            this.btnLastResult.Name = "btnLastResult";
            this.btnLastResult.Size = new System.Drawing.Size(75, 23);
            this.btnLastResult.TabIndex = 27;
            this.btnLastResult.Text = "参照";
            this.btnLastResult.UseVisualStyleBackColor = true;
            this.btnLastResult.Click += new System.EventHandler(this.btnLastResult_Click);
            // 
            // btnMovieSPList
            // 
            this.btnMovieSPList.Location = new System.Drawing.Point(694, 74);
            this.btnMovieSPList.Name = "btnMovieSPList";
            this.btnMovieSPList.Size = new System.Drawing.Size(75, 23);
            this.btnMovieSPList.TabIndex = 26;
            this.btnMovieSPList.Text = "参照";
            this.btnMovieSPList.UseVisualStyleBackColor = true;
            this.btnMovieSPList.Click += new System.EventHandler(this.btnMovieSPList_Click);
            // 
            // tbLastResult
            // 
            this.tbLastResult.Location = new System.Drawing.Point(72, 102);
            this.tbLastResult.Name = "tbLastResult";
            this.tbLastResult.Size = new System.Drawing.Size(616, 19);
            this.tbLastResult.TabIndex = 25;
            // 
            // tbMovieSPList
            // 
            this.tbMovieSPList.Location = new System.Drawing.Point(72, 77);
            this.tbMovieSPList.Name = "tbMovieSPList";
            this.tbMovieSPList.Size = new System.Drawing.Size(616, 19);
            this.tbMovieSPList.TabIndex = 25;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(5, 100);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(66, 24);
            this.label5.TabIndex = 24;
            this.label5.Text = "前回結果\r\n(Result.csv)";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(5, 80);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(64, 12);
            this.label4.TabIndex = 24;
            this.label4.Text = "動画IDリスト";
            // 
            // btnAnalyzeDB
            // 
            this.btnAnalyzeDB.Location = new System.Drawing.Point(694, 47);
            this.btnAnalyzeDB.Name = "btnAnalyzeDB";
            this.btnAnalyzeDB.Size = new System.Drawing.Size(75, 23);
            this.btnAnalyzeDB.TabIndex = 3;
            this.btnAnalyzeDB.Text = "参照";
            this.btnAnalyzeDB.UseVisualStyleBackColor = true;
            this.btnAnalyzeDB.Click += new System.EventHandler(this.btnAnalyzeDB_Click);
            // 
            // btnBaseDB
            // 
            this.btnBaseDB.Location = new System.Drawing.Point(694, 24);
            this.btnBaseDB.Name = "btnBaseDB";
            this.btnBaseDB.Size = new System.Drawing.Size(75, 23);
            this.btnBaseDB.TabIndex = 1;
            this.btnBaseDB.Text = "参照";
            this.btnBaseDB.UseVisualStyleBackColor = true;
            this.btnBaseDB.Click += new System.EventHandler(this.btnBaseDB_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(448, 4);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(241, 12);
            this.label6.TabIndex = 23;
            this.label6.Text = "※dbファイルはnicorank_SnapShot.exeで作成する";
            // 
            // tbAnalyzeDB
            // 
            this.tbAnalyzeDB.Location = new System.Drawing.Point(72, 49);
            this.tbAnalyzeDB.Name = "tbAnalyzeDB";
            this.tbAnalyzeDB.Size = new System.Drawing.Size(616, 19);
            this.tbAnalyzeDB.TabIndex = 2;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(25, 52);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 19;
            this.label3.Text = "集計日";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(27, 31);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(29, 12);
            this.label2.TabIndex = 18;
            this.label2.Text = "基準";
            // 
            // tbBaseDB
            // 
            this.tbBaseDB.Location = new System.Drawing.Point(72, 24);
            this.tbBaseDB.Name = "tbBaseDB";
            this.tbBaseDB.Size = new System.Drawing.Size(616, 19);
            this.tbBaseDB.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 4);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(380, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "SPモード用の集計ファイルを指定 (例:基準 20xx0701.db 集計日:20xx1226.db)";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label19);
            this.groupBox1.Controls.Add(this.dtPLastweekDay);
            this.groupBox1.Controls.Add(this.label7);
            this.groupBox1.Controls.Add(this.rbSP);
            this.groupBox1.Controls.Add(this.dtPAnalyzeDay);
            this.groupBox1.Controls.Add(this.rbTyukan);
            this.groupBox1.Controls.Add(this.rbWeekly);
            this.groupBox1.Location = new System.Drawing.Point(6, 20);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(806, 53);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "集計方法";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(459, 15);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(145, 12);
            this.label7.TabIndex = 7;
            this.label7.Text = "集計基準日指定(週間専用)";
            // 
            // rbSP
            // 
            this.rbSP.AutoSize = true;
            this.rbSP.Location = new System.Drawing.Point(178, 27);
            this.rbSP.Name = "rbSP";
            this.rbSP.Size = new System.Drawing.Size(37, 16);
            this.rbSP.TabIndex = 2;
            this.rbSP.Text = "SP";
            this.rbSP.UseVisualStyleBackColor = true;
            this.rbSP.CheckedChanged += new System.EventHandler(this.rbSP_CheckedChanged);
            // 
            // dtPAnalyzeDay
            // 
            this.dtPAnalyzeDay.Location = new System.Drawing.Point(610, 12);
            this.dtPAnalyzeDay.Name = "dtPAnalyzeDay";
            this.dtPAnalyzeDay.Size = new System.Drawing.Size(189, 19);
            this.dtPAnalyzeDay.TabIndex = 6;
            this.dtPAnalyzeDay.ValueChanged += new System.EventHandler(this.dtPAnalyzeDay_ValueChanged);
            // 
            // rbTyukan
            // 
            this.rbTyukan.AutoSize = true;
            this.rbTyukan.Location = new System.Drawing.Point(104, 27);
            this.rbTyukan.Name = "rbTyukan";
            this.rbTyukan.Size = new System.Drawing.Size(47, 16);
            this.rbTyukan.TabIndex = 1;
            this.rbTyukan.Text = "中間";
            this.rbTyukan.UseVisualStyleBackColor = true;
            this.rbTyukan.CheckedChanged += new System.EventHandler(this.rbTyukan_CheckedChanged);
            // 
            // rbWeekly
            // 
            this.rbWeekly.AutoSize = true;
            this.rbWeekly.Checked = true;
            this.rbWeekly.Location = new System.Drawing.Point(33, 27);
            this.rbWeekly.Name = "rbWeekly";
            this.rbWeekly.Size = new System.Drawing.Size(47, 16);
            this.rbWeekly.TabIndex = 0;
            this.rbWeekly.TabStop = true;
            this.rbWeekly.Text = "週間";
            this.rbWeekly.UseVisualStyleBackColor = true;
            this.rbWeekly.CheckedChanged += new System.EventHandler(this.rbWeekly_CheckedChanged);
            // 
            // tabPageOut
            // 
            this.tabPageOut.Controls.Add(this.tabPageSyukei);
            this.tabPageOut.Controls.Add(this.tabPage2);
            this.tabPageOut.Location = new System.Drawing.Point(-3, 4);
            this.tabPageOut.Name = "tabPageOut";
            this.tabPageOut.SelectedIndex = 0;
            this.tabPageOut.Size = new System.Drawing.Size(836, 661);
            this.tabPageOut.TabIndex = 0;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(429, 37);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(175, 12);
            this.label19.TabIndex = 9;
            this.label19.Text = "先週基準日指定(週間・中間専用)";
            // 
            // dtPLastweekDay
            // 
            this.dtPLastweekDay.Location = new System.Drawing.Point(610, 34);
            this.dtPLastweekDay.Name = "dtPLastweekDay";
            this.dtPLastweekDay.Size = new System.Drawing.Size(189, 19);
            this.dtPLastweekDay.TabIndex = 8;
            this.dtPLastweekDay.ValueChanged += new System.EventHandler(this.dtPLastweekDay_ValueChanged);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(833, 662);
            this.Controls.Add(this.tabPageOut);
            this.Name = "frmMain";
            this.Text = "ニコラン2019 集計プログラム";
            this.Load += new System.EventHandler(this.frmMain_Load);
            this.tabPage2.ResumeLayout(false);
            this.tabPageSyukei.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.panelSP.ResumeLayout(false);
            this.panelSP.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabPageOut.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Panel pnlOutput;
        private System.Windows.Forms.TabPage tabPageSyukei;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TabControl tabPageOut;
        private System.Windows.Forms.RadioButton rbTyukan;
        private System.Windows.Forms.RadioButton rbWeekly;
        private System.Windows.Forms.Panel panelSP;
        private System.Windows.Forms.RadioButton rbSP;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox tbCalcMylist;
        private System.Windows.Forms.TextBox tbCalcPlay;
        private System.Windows.Forms.TextBox tbCalcComment;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.ComboBox cmbHoseiComment;
        private System.Windows.Forms.ComboBox cmbHoseiPlay;
        private System.Windows.Forms.ComboBox cmbHoseiMylist;
        private System.Windows.Forms.Button btnAnalyze;
        private System.Windows.Forms.TextBox tbUserInfoNum;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox tbCalcLike;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox tbAnalyzeDB;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbBaseDB;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnAnalyzeDB;
        private System.Windows.Forms.Button btnBaseDB;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button btnMovieSPList;
        private System.Windows.Forms.TextBox tbMovieSPList;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnLastResult;
        private System.Windows.Forms.TextBox tbLastResult;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.DateTimePicker dtPAnalyzeDay;
        private System.Windows.Forms.TextBox tbHoseiCommentUnderLimit;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.ComboBox cmbHoseiPointAll;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.DateTimePicker dtPLastweekDay;
    }
}

