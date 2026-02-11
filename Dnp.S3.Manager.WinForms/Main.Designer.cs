// -----------------------------------------------------------------------
// <copyright file="Main.Designer.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Dnp.S3.Manager.WinForms
{
    partial class Main
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            accountToolStripMenuItem = new ToolStripMenuItem();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            statusStrip1 = new StatusStrip();
            splitContainer1 = new SplitContainer();
            splitContainer2 = new SplitContainer();
            splitContainer3 = new SplitContainer();
            tv_buckets = new TreeView();
            splitContainer4 = new SplitContainer();
            ts_path = new ToolStrip();
            dgv_bucket_contents = new DataGridView();
            btn_delete = new Button();
            btn_up = new Button();
            btn_rename = new Button();
            btn_down = new Button();
            tc_main = new TabControl();
            tabPage1 = new TabPage();
            dgv_transfer_status = new DataGridView();
            tabPage2 = new TabPage();
            dgv_logs = new DataGridView();
            btn_refresh = new Button();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer3).BeginInit();
            splitContainer3.Panel1.SuspendLayout();
            splitContainer3.Panel2.SuspendLayout();
            splitContainer3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer4).BeginInit();
            splitContainer4.Panel1.SuspendLayout();
            splitContainer4.Panel2.SuspendLayout();
            splitContainer4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgv_bucket_contents).BeginInit();
            tc_main.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgv_transfer_status).BeginInit();
            tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgv_logs).BeginInit();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { accountToolStripMenuItem, settingsToolStripMenuItem, aboutToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(943, 33);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // accountToolStripMenuItem
            // 
            accountToolStripMenuItem.Name = "accountToolStripMenuItem";
            accountToolStripMenuItem.Size = new Size(89, 29);
            accountToolStripMenuItem.Text = "Account";
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(88, 29);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(74, 29);
            aboutToolStripMenuItem.Text = "About";
            // 
            // statusStrip1
            // 
            statusStrip1.Location = new Point(0, 519);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(943, 22);
            statusStrip1.TabIndex = 1;
            statusStrip1.Text = "statusStrip1";
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 33);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(splitContainer2);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(tc_main);
            splitContainer1.Size = new Size(943, 486);
            splitContainer1.SplitterDistance = 327;
            splitContainer1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            splitContainer2.Orientation = Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(splitContainer3);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(btn_refresh);
            splitContainer2.Panel2.Controls.Add(btn_delete);
            splitContainer2.Panel2.Controls.Add(btn_up);
            splitContainer2.Panel2.Controls.Add(btn_rename);
            splitContainer2.Panel2.Controls.Add(btn_down);
            splitContainer2.Size = new Size(943, 327);
            splitContainer2.SplitterDistance = 264;
            splitContainer2.TabIndex = 0;
            // 
            // splitContainer3
            // 
            splitContainer3.Dock = DockStyle.Fill;
            splitContainer3.Location = new Point(0, 0);
            splitContainer3.Name = "splitContainer3";
            // 
            // splitContainer3.Panel1
            // 
            splitContainer3.Panel1.Controls.Add(tv_buckets);
            // 
            // splitContainer3.Panel2
            // 
            splitContainer3.Panel2.Controls.Add(splitContainer4);
            splitContainer3.Size = new Size(943, 264);
            splitContainer3.SplitterDistance = 314;
            splitContainer3.TabIndex = 0;
            // 
            // tv_buckets
            // 
            tv_buckets.Dock = DockStyle.Fill;
            tv_buckets.Location = new Point(0, 0);
            tv_buckets.Name = "tv_buckets";
            tv_buckets.Size = new Size(314, 264);
            tv_buckets.TabIndex = 0;
            // 
            // splitContainer4
            // 
            splitContainer4.Dock = DockStyle.Fill;
            splitContainer4.FixedPanel = FixedPanel.Panel1;
            splitContainer4.Location = new Point(0, 0);
            splitContainer4.Name = "splitContainer4";
            splitContainer4.Orientation = Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            splitContainer4.Panel1.Controls.Add(ts_path);
            // 
            // splitContainer4.Panel2
            // 
            splitContainer4.Panel2.Controls.Add(dgv_bucket_contents);
            splitContainer4.Size = new Size(625, 264);
            splitContainer4.SplitterDistance = 39;
            splitContainer4.TabIndex = 0;
            // 
            // ts_path
            // 
            ts_path.Dock = DockStyle.Fill;
            ts_path.Location = new Point(0, 0);
            ts_path.Name = "ts_path";
            ts_path.Size = new Size(625, 39);
            ts_path.TabIndex = 1;
            ts_path.Text = "toolStrip1";
            // 
            // dgv_bucket_contents
            // 
            dgv_bucket_contents.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv_bucket_contents.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgv_bucket_contents.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv_bucket_contents.Dock = DockStyle.Fill;
            dgv_bucket_contents.Location = new Point(0, 0);
            dgv_bucket_contents.Name = "dgv_bucket_contents";
            dgv_bucket_contents.Size = new Size(625, 221);
            dgv_bucket_contents.TabIndex = 0;
            // 
            // btn_delete
            // 
            btn_delete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_delete.BackgroundImage = Properties.Resources.Delete_32;
            btn_delete.BackgroundImageLayout = ImageLayout.Center;
            btn_delete.Location = new Point(836, 3);
            btn_delete.Name = "btn_delete";
            btn_delete.Size = new Size(75, 53);
            btn_delete.TabIndex = 3;
            btn_delete.UseVisualStyleBackColor = true;
            // 
            // btn_up
            // 
            btn_up.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_up.BackgroundImage = Properties.Resources.Upload_32;
            btn_up.BackgroundImageLayout = ImageLayout.Center;
            btn_up.Location = new Point(608, 3);
            btn_up.Name = "btn_up";
            btn_up.Size = new Size(75, 53);
            btn_up.TabIndex = 2;
            btn_up.UseVisualStyleBackColor = true;
            // 
            // btn_rename
            // 
            btn_rename.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_rename.BackgroundImage = Properties.Resources.Rename_32;
            btn_rename.BackgroundImageLayout = ImageLayout.Center;
            btn_rename.Location = new Point(722, 3);
            btn_rename.Name = "btn_rename";
            btn_rename.Size = new Size(75, 53);
            btn_rename.TabIndex = 1;
            btn_rename.UseVisualStyleBackColor = true;
            // 
            // btn_down
            // 
            btn_down.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_down.BackgroundImage = Properties.Resources.Download_32;
            btn_down.BackgroundImageLayout = ImageLayout.Center;
            btn_down.Location = new Point(494, 3);
            btn_down.Name = "btn_down";
            btn_down.Size = new Size(75, 53);
            btn_down.TabIndex = 0;
            btn_down.UseVisualStyleBackColor = true;
            // 
            // tc_main
            // 
            tc_main.Controls.Add(tabPage1);
            tc_main.Controls.Add(tabPage2);
            tc_main.Dock = DockStyle.Fill;
            tc_main.Location = new Point(0, 0);
            tc_main.Name = "tc_main";
            tc_main.SelectedIndex = 0;
            tc_main.Size = new Size(943, 155);
            tc_main.TabIndex = 1;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(dgv_transfer_status);
            tabPage1.Location = new Point(4, 34);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(935, 117);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Status";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // dgv_transfer_status
            // 
            dgv_transfer_status.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv_transfer_status.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            dgv_transfer_status.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv_transfer_status.Dock = DockStyle.Fill;
            dgv_transfer_status.Location = new Point(3, 3);
            dgv_transfer_status.Name = "dgv_transfer_status";
            dgv_transfer_status.Size = new Size(929, 111);
            dgv_transfer_status.TabIndex = 0;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(dgv_logs);
            tabPage2.Location = new Point(4, 34);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(935, 117);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Logs";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // dgv_logs
            // 
            dgv_logs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv_logs.Dock = DockStyle.Fill;
            dgv_logs.Location = new Point(3, 3);
            dgv_logs.Name = "dgv_logs";
            dgv_logs.Size = new Size(929, 111);
            dgv_logs.TabIndex = 0;
            // 
            // btn_refresh
            // 
            btn_refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btn_refresh.BackgroundImage = Properties.Resources.refresh_32;
            btn_refresh.BackgroundImageLayout = ImageLayout.Center;
            btn_refresh.Location = new Point(380, 2);
            btn_refresh.Name = "btn_refresh";
            btn_refresh.Size = new Size(75, 53);
            btn_refresh.TabIndex = 4;
            btn_refresh.UseVisualStyleBackColor = true;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(943, 541);
            Controls.Add(splitContainer1);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Main";
            Text = "Main";
            this.Load += new System.EventHandler(this.Main_Load);
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            splitContainer3.Panel1.ResumeLayout(false);
            splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer3).EndInit();
            splitContainer3.ResumeLayout(false);
            splitContainer4.Panel1.ResumeLayout(false);
            splitContainer4.Panel1.PerformLayout();
            splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer4).EndInit();
            splitContainer4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgv_bucket_contents).EndInit();
            tc_main.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgv_transfer_status).EndInit();
            tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgv_logs).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem accountToolStripMenuItem;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private StatusStrip statusStrip1;
        private SplitContainer splitContainer1;
        private SplitContainer splitContainer2;
        private SplitContainer splitContainer3;
        private TreeView tv_buckets;
        private DataGridView dgv_bucket_contents;
        private Button btn_down;
        private DataGridView dgv_transfer_status;
        private Button btn_delete;
        private Button btn_up;
        private Button btn_rename;
        private ToolStrip pathToolStrip;
        private ToolStripLabel pathLabel;
        private ToolStrip ts_path;
        private SplitContainer splitContainer4;
        private TabControl tc_main;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private DataGridView dgv_logs;
        private Button btn_refresh;
    }
}