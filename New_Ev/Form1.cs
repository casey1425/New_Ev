using New_Ev;
using System.Security.Cryptography;

namespace Ev
{
    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void splitContainer2_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void 새시뮬레이션ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            batteryControl1.ResetSimulation();
            MessageBox.Show("초기화 완료!");
            logControl1?.AddLog("새 시뮬레이션 시작");
        }

        private void 시작ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            batteryControl1.StartSimulation();
            logControl1?.AddLog("충전 시뮬레이션 시작");
        }

        private void 중지ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            batteryControl1.StopSimulation();
            logControl1?.AddLog("충전 시뮬레이션 중지");
        }

        private void 저장ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*";
            saveFileDialog.Title = "설정 저장";
            saveFileDialog.FileName = "battery_settings.txt";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string settings = batteryControl1.GetCurrentSettings();
                try
                {
                    System.IO.File.WriteAllText(saveFileDialog.FileName, settings);
                    MessageBox.Show("설정이 저장되었습니다.");
                    logControl1.AddLog("설정 파일 저장: " + saveFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("파일 저장 중 오류가 발생했습니다: " + ex.Message);
                    logControl1.AddLog("파일 저장 오류: " + ex.Message);
                }
            }
        }

        private void 종료ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void 일시정지ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            batteryControl1.PauseSimulation();
            logControl1?.AddLog("충전 시뮬레이션 일시정지");
        }

        private void 리셋ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            batteryControl1.ResetSimulation();
            MessageBox.Show("초기화 완료!");
            logControl1?.AddLog("시뮬레이션 리셋");
        }

        private void batteryControl1_SimulationStarted(object sender, EventArgs e)
        {
            logControl1.AddLog("시뮬레이션 시작 버튼 클릭");
        }

        private void batteryControl1_SimulationStopped(object sender, EventArgs e)
        {
            logControl1.AddLog("시뮬레이션 종료 버튼 클릭");
        }
    }
}