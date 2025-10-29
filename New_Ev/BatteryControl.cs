using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace New_Ev
{
    public partial class BatteryControl : UserControl
    {
        private Label lblVoltage, lblCurrent, lblSocTitle, lblSocValue, lblStartSoc;
        private TextBox txtVoltage, txtCurrent, txtStartSoc;
        private Button btnStart, btnStop;
        private ProgressBar progressBarSoc;
        private Chart chart1;

        public double InputVoltage => double.TryParse(txtVoltage.Text, out var v) ? v : 0;
        public double InputCurrent => double.TryParse(txtCurrent.Text, out var c) ? c : 0;
        public double StartSOC => double.TryParse(txtStartSoc.Text, out var s) ? s : 0;

        public BatteryControl()
        {
            InitializeComponent();
            this.Size = new Size(340, 400);
            InitializeControls();
            InitializeChart();
        }

        public void UpdateDisplay(Battery externalBattery, int tickCount)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateDisplay(externalBattery, tickCount)));
                return;
            }

            lblSocValue.Text = externalBattery.SocAsDouble.ToString("F2") + " %";

            if (externalBattery.SOC >= 0 && externalBattery.SOC <= 100)
            {
                progressBarSoc.Value = externalBattery.SOC;
            }

            chart1.Series["SOC"].Points.AddXY(tickCount, externalBattery.SocAsDouble);

            if (chart1.ChartAreas.Count > 0)
            {
                chart1.ChartAreas[0].RecalculateAxesScale();
            }
        }

        public void ResetDisplay()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(ResetDisplay));
                return;
            }

            lblSocValue.Text = "0.00 %";
            progressBarSoc.Value = 0;

            if (chart1.Series.IndexOf("SOC") != -1)
            {
                chart1.Series["SOC"].Points.Clear();
            }
        }

        private void InitializeControls()
        {
            int topMargin = 40;
            lblStartSoc = new Label() { Text = "시작 잔량 (%)", Location = new Point(10, topMargin), AutoSize = true };
            txtStartSoc = new TextBox() { Text = "0", Location = new Point(130, topMargin - 3), Size = new Size(100, 25) };
            lblVoltage = new Label() { Text = "입력 전압 (V)", Location = new Point(10, topMargin + 30), AutoSize = true };
            txtVoltage = new TextBox() { Text = "200", Location = new Point(130, topMargin + 27), Size = new Size(100, 25) };
            lblCurrent = new Label() { Text = "입력 전류 (A)", Location = new Point(10, topMargin + 60), AutoSize = true };
            txtCurrent = new TextBox() { Text = "50", Location = new Point(130, topMargin + 57), Size = new Size(100, 25) };
            lblSocTitle = new Label() { Text = "현재 배터리 상태 (SOC)", Location = new Point(10, topMargin + 100), AutoSize = true };
            lblSocValue = new Label() { Text = "0.00 %", Location = new Point(10, topMargin + 120), Font = new Font(this.Font, FontStyle.Bold), AutoSize = true };
            btnStart = new Button() { Text = "충전 시", Location = new Point(130, topMargin + 118), Enabled = false };
            btnStop = new Button() { Text = "충전 중", Location = new Point(220, topMargin + 118), Enabled = false };
            progressBarSoc = new ProgressBar() { Location = new Point(10, topMargin + 150), Size = new Size(300, 23) };
            chart1 = new Chart() { Location = new Point(10, topMargin + 180), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

            this.Controls.AddRange(new Control[] {
                lblStartSoc, txtStartSoc, lblVoltage, txtVoltage, lblCurrent, txtCurrent,
                lblSocTitle, lblSocValue, btnStart, btnStop, progressBarSoc, chart1
            });
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            var chartArea = new ChartArea("Default");
            var socSeries = new Series("SOC");
            chart1.Series.Add(socSeries);
        }
        private void InitializeChart()
        {
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();

            var chartArea = new ChartArea("Default");
            chartArea.AxisY.Minimum = 0;
            chartArea.AxisY.Maximum = 100;
            chartArea.AxisY.Title = "SOC (%)";
            chartArea.AxisX.Title = "시간 (Tick)";
            chart1.ChartAreas.Add(chartArea);

            var socSeries = new Series("SOC");
            socSeries.ChartArea = "Default";
            socSeries.ChartType = SeriesChartType.Line;
            socSeries.BorderWidth = 2;
            socSeries.Color = Color.Green;
            chart1.Series.Add(socSeries);
        }
    }
}