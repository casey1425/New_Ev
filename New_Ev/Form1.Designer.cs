namespace Ev
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            statusStrip1 = new StatusStrip();
            splitContainer1 = new SplitContainer();
            splitContainer2 = new SplitContainer();
            panel_tree = new Panel();
            panel_battery = new Panel();
            batteryControl1 = new New_Ev.BatteryControl();
            panel_log = new Panel();
            menuStrip1 = new MenuStrip();
            파일FToolStripMenuItem = new ToolStripMenuItem();
            새시뮬레이션ToolStripMenuItem = new ToolStripMenuItem();
            저장ToolStripMenuItem = new ToolStripMenuItem();
            로그내보내기ToolStripMenuItem = new ToolStripMenuItem();
            종료ToolStripMenuItem = new ToolStripMenuItem();
            편집EToolStripMenuItem = new ToolStripMenuItem();
            보기FtoolStripMenuItem = new ToolStripMenuItem();
            로그창ToolStripMenuItem = new ToolStripMenuItem();
            실시간그래프ToolStripMenuItem = new ToolStripMenuItem();
            시뮬레이션SToolStripMenuItem = new ToolStripMenuItem();
            시작ToolStripMenuItem = new ToolStripMenuItem();
            일시정지ToolStripMenuItem = new ToolStripMenuItem();
            중지ToolStripMenuItem = new ToolStripMenuItem();
            리셋ToolStripMenuItem = new ToolStripMenuItem();
            도움말HtoolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            panel_battery.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Location = new Point(0, 525);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(846, 22);
            statusStrip1.TabIndex = 0;
            statusStrip1.Text = "statusStrip1";
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(panel_log);
            splitContainer1.Size = new Size(846, 525);
            splitContainer1.SplitterDistance = 344;
            splitContainer1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(panel_tree);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(panel_battery);
            splitContainer2.Panel2.Paint += splitContainer2_Panel2_Paint;
            splitContainer2.Size = new Size(846, 344);
            splitContainer2.SplitterDistance = 281;
            splitContainer2.TabIndex = 0;
            // 
            // panel_tree
            // 
            panel_tree.BackColor = SystemColors.AppWorkspace;
            panel_tree.Dock = DockStyle.Fill;
            panel_tree.Location = new Point(0, 0);
            panel_tree.Name = "panel_tree";
            panel_tree.Size = new Size(281, 344);
            panel_tree.TabIndex = 0;
            // 
            // panel_battery
            // 
            panel_battery.BackColor = SystemColors.AppWorkspace;
            panel_battery.Controls.Add(batteryControl1);
            panel_battery.Dock = DockStyle.Fill;
            panel_battery.Location = new Point(0, 0);
            panel_battery.Name = "panel_battery";
            panel_battery.Size = new Size(561, 344);
            panel_battery.TabIndex = 0;
            // 
            // batteryControl1
            // 
            batteryControl1.Dock = DockStyle.Fill;
            batteryControl1.Location = new Point(0, 0);
            batteryControl1.Name = "batteryControl1";
            batteryControl1.Size = new Size(561, 344);
            batteryControl1.TabIndex = 0;
            // 
            // panel_log
            // 
            panel_log.Dock = DockStyle.Fill;
            panel_log.Location = new Point(0, 0);
            panel_log.Name = "panel_log";
            panel_log.Size = new Size(846, 177);
            panel_log.TabIndex = 0;
            // 
            // menuStrip1
            // 
            menuStrip1.GripStyle = ToolStripGripStyle.Visible;
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { 파일FToolStripMenuItem, 편집EToolStripMenuItem, 보기FtoolStripMenuItem, 시뮬레이션SToolStripMenuItem, 도움말HtoolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(846, 28);
            menuStrip1.TabIndex = 3;
            menuStrip1.Text = "menuStrip1";
            // 
            // 파일FToolStripMenuItem
            // 
            파일FToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 새시뮬레이션ToolStripMenuItem, 저장ToolStripMenuItem, 로그내보내기ToolStripMenuItem, 종료ToolStripMenuItem });
            파일FToolStripMenuItem.Name = "파일FToolStripMenuItem";
            파일FToolStripMenuItem.Size = new Size(70, 24);
            파일FToolStripMenuItem.Text = "파일(&F)";
            // 
            // 새시뮬레이션ToolStripMenuItem
            // 
            새시뮬레이션ToolStripMenuItem.Name = "새시뮬레이션ToolStripMenuItem";
            새시뮬레이션ToolStripMenuItem.Size = new Size(187, 26);
            새시뮬레이션ToolStripMenuItem.Text = "새 시뮬레이션";
            새시뮬레이션ToolStripMenuItem.Click += 새시뮬레이션ToolStripMenuItem_Click;
            // 
            // 저장ToolStripMenuItem
            // 
            저장ToolStripMenuItem.Name = "저장ToolStripMenuItem";
            저장ToolStripMenuItem.Size = new Size(187, 26);
            저장ToolStripMenuItem.Text = "저장";
            저장ToolStripMenuItem.Click += 저장ToolStripMenuItem_Click;
            // 
            // 로그내보내기ToolStripMenuItem
            // 
            로그내보내기ToolStripMenuItem.Name = "로그내보내기ToolStripMenuItem";
            로그내보내기ToolStripMenuItem.Size = new Size(187, 26);
            로그내보내기ToolStripMenuItem.Text = "로그 내보내기";
            // 
            // 종료ToolStripMenuItem
            // 
            종료ToolStripMenuItem.Name = "종료ToolStripMenuItem";
            종료ToolStripMenuItem.Size = new Size(187, 26);
            종료ToolStripMenuItem.Text = "종료";
            종료ToolStripMenuItem.Click += 종료ToolStripMenuItem_Click;
            // 
            // 편집EToolStripMenuItem
            // 
            편집EToolStripMenuItem.Name = "편집EToolStripMenuItem";
            편집EToolStripMenuItem.Size = new Size(71, 24);
            편집EToolStripMenuItem.Text = "편집(E)";
            // 
            // 보기FtoolStripMenuItem
            // 
            보기FtoolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 로그창ToolStripMenuItem, 실시간그래프ToolStripMenuItem });
            보기FtoolStripMenuItem.Name = "보기FtoolStripMenuItem";
            보기FtoolStripMenuItem.Size = new Size(73, 24);
            보기FtoolStripMenuItem.Text = "보기(&V)";
            // 
            // 로그창ToolStripMenuItem
            // 
            로그창ToolStripMenuItem.Name = "로그창ToolStripMenuItem";
            로그창ToolStripMenuItem.Size = new Size(187, 26);
            로그창ToolStripMenuItem.Text = "로그 창";
            // 
            // 실시간그래프ToolStripMenuItem
            // 
            실시간그래프ToolStripMenuItem.Name = "실시간그래프ToolStripMenuItem";
            실시간그래프ToolStripMenuItem.Size = new Size(187, 26);
            실시간그래프ToolStripMenuItem.Text = "실시간 그래프";
            // 
            // 시뮬레이션SToolStripMenuItem
            // 
            시뮬레이션SToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { 시작ToolStripMenuItem, 일시정지ToolStripMenuItem, 중지ToolStripMenuItem, 리셋ToolStripMenuItem });
            시뮬레이션SToolStripMenuItem.Name = "시뮬레이션SToolStripMenuItem";
            시뮬레이션SToolStripMenuItem.Size = new Size(116, 24);
            시뮬레이션SToolStripMenuItem.Text = "시뮬레이션(&S)";
            // 
            // 시작ToolStripMenuItem
            // 
            시작ToolStripMenuItem.Name = "시작ToolStripMenuItem";
            시작ToolStripMenuItem.Size = new Size(224, 26);
            시작ToolStripMenuItem.Text = "시작";
            시작ToolStripMenuItem.Click += 시작ToolStripMenuItem_Click;
            // 
            // 일시정지ToolStripMenuItem
            // 
            일시정지ToolStripMenuItem.Name = "일시정지ToolStripMenuItem";
            일시정지ToolStripMenuItem.Size = new Size(224, 26);
            일시정지ToolStripMenuItem.Text = "일시정지";
            일시정지ToolStripMenuItem.Click += 일시정지ToolStripMenuItem_Click;
            // 
            // 중지ToolStripMenuItem
            // 
            중지ToolStripMenuItem.Name = "중지ToolStripMenuItem";
            중지ToolStripMenuItem.Size = new Size(224, 26);
            중지ToolStripMenuItem.Text = "중지";
            중지ToolStripMenuItem.Click += 중지ToolStripMenuItem_Click;
            // 
            // 리셋ToolStripMenuItem
            // 
            리셋ToolStripMenuItem.Name = "리셋ToolStripMenuItem";
            리셋ToolStripMenuItem.Size = new Size(224, 26);
            리셋ToolStripMenuItem.Text = "리셋";
            리셋ToolStripMenuItem.Click += 리셋ToolStripMenuItem_Click;
            // 
            // 도움말HtoolStripMenuItem
            // 
            도움말HtoolStripMenuItem.Name = "도움말HtoolStripMenuItem";
            도움말HtoolStripMenuItem.Size = new Size(89, 24);
            도움말HtoolStripMenuItem.Text = "도움말(&H)";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(846, 547);
            Controls.Add(menuStrip1);
            Controls.Add(splitContainer1);
            Controls.Add(statusStrip1);
            Name = "Form1";
            Text = "EV Simulator";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            panel_battery.ResumeLayout(false);
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private StatusStrip statusStrip1;
        private SplitContainer splitContainer1;
        private SplitContainer splitContainer2;
        private Panel panel_tree;
        private Panel panel_battery;
        private Panel panel_log;
        private New_Ev.BatteryControl batteryControl1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem 파일FToolStripMenuItem;
        private ToolStripMenuItem 편집EToolStripMenuItem;
        private ToolStripMenuItem 보기FtoolStripMenuItem;
        private ToolStripMenuItem 도움말HtoolStripMenuItem;
        private ToolStripMenuItem 시뮬레이션SToolStripMenuItem;
        private ToolStripMenuItem 새시뮬레이션ToolStripMenuItem;
        private ToolStripMenuItem 저장ToolStripMenuItem;
        private ToolStripMenuItem 로그내보내기ToolStripMenuItem;
        private ToolStripMenuItem 종료ToolStripMenuItem;
        private ToolStripMenuItem 로그창ToolStripMenuItem;
        private ToolStripMenuItem 실시간그래프ToolStripMenuItem;
        private ToolStripMenuItem 시작ToolStripMenuItem;
        private ToolStripMenuItem 일시정지ToolStripMenuItem;
        private ToolStripMenuItem 중지ToolStripMenuItem;
        private ToolStripMenuItem 리셋ToolStripMenuItem;
    }
}
