namespace New_Ev
{
    partial class Form_Log
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
            listView_log = new ListView();
            toolStrip1 = new ToolStrip();
            richTextBox1 = new RichTextBox();
            SuspendLayout();
            // 
            // listView_log
            // 
            listView_log.Dock = DockStyle.Fill;
            listView_log.Location = new Point(0, 25);
            listView_log.Name = "listView_log";
            listView_log.Size = new Size(812, 473);
            listView_log.TabIndex = 1;
            listView_log.UseCompatibleStateImageBehavior = false;
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new Size(20, 20);
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(812, 25);
            toolStrip1.TabIndex = 2;
            toolStrip1.Text = "toolStrip1";
            // 
            // richTextBox1
            // 
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.Location = new Point(0, 25);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(812, 473);
            richTextBox1.TabIndex = 3;
            richTextBox1.Text = "";
            // 
            // Form_Log
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(812, 498);
            Controls.Add(richTextBox1);
            Controls.Add(listView_log);
            Controls.Add(toolStrip1);
            Name = "Form_Log";
            Text = "Form_Log";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListView listView_log;
        private ToolStrip toolStrip1;
        private RichTextBox richTextBox1;
    }
}