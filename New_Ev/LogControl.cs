using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Ev
{
    public partial class LogControl : UserControl
    {
        public LogControl()
        {
            InitializeComponent();
        }
        public void AddLog(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AddLog(message)));
                return;
            }
            string log = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            richTextBox1.AppendText(log);
            richTextBox1.ScrollToCaret();
        }
    }
}
