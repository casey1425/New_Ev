using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting; // 차트 사용을 위해 추가

namespace New_Ev
{
    public partial class BatteryControl : System.Windows.Forms.UserControl
    {
        // ----- 멤버 변수 -----
        private Battery myBattery = null!;
        private Label lblVoltage = null!, lblCurrent = null!, lblSocTitle = null!, lblSocValue = null!, lblStartSoc = null!;
        private TextBox txtVoltage = null!, txtCurrent = null!, txtStartSoc = null!;
        private Button btnStart = null!, btnStop = null!;
        private ProgressBar progressBarSoc = null!;
        private System.Windows.Forms.Timer timer1 = null!;
        private Chart chart1 = null!; // 차트 변수 추가
        private int tickCount = 0;

        // ----- 공개(Public) 이벤트 -----
        public event EventHandler SimulationStarted;
        public event EventHandler SimulationStopped;

        // ----- 생성자 -----
        public BatteryControl()
        {
            InitializeComponent();
            this.MinimumSize = new Size(340, 400);
            this.Size = new Size(340, 400);
            myBattery = new Battery();
            InitializeControls();
            InitializeChart();
            UpdateUI();
        }

        // ----- 공개(Public) 메서드 -----
        public void StartSimulation() => btnStart_Click(this, EventArgs.Empty);
        public void StopSimulation() => btnStop_Click(this, EventArgs.Empty);
        public void PauseSimulation() => timer1.Stop();
        public void ResetSimulation()
        {
            btnStop_Click(this, EventArgs.Empty);
            myBattery = new Battery();
            txtStartSoc.Text = "0";
            txtVoltage.Text = "200";
            txtCurrent.Text = "50";
            UpdateUI();
            chart1.Series["SOC"].Points.Clear(); // 차트 데이터 초기화
            tickCount = 0;
        }
        public string GetCurrentSettings()
        {
            string settings = $"시작 잔량: {txtStartSoc.Text}\n";
            settings += $"입력 전압: {txtVoltage.Text}\n";
            settings += $"입력 전류: {txtCurrent.Text}";
            return settings;
        }

        private void InitializeControls()
        {
            int topMargin = 40; // 상단 여백

            lblStartSoc = new Label() { Text = "시작 잔량 (%)", Location = new Point(10, topMargin), AutoSize = true };
            txtStartSoc = new TextBox() { Text = "0", Location = new Point(130, topMargin - 3), Size = new Size(100, 25) };

            lblVoltage = new Label() { Text = "입력 전압 (V)", Location = new Point(10, topMargin + 30), AutoSize = true };
            txtVoltage = new TextBox() { Text = "200", Location = new Point(130, topMargin + 27), Size = new Size(100, 25) };

            lblCurrent = new Label() { Text = "입력 전류 (A)", Location = new Point(10, topMargin + 60), AutoSize = true };
            txtCurrent = new TextBox() { Text = "50", Location = new Point(130, topMargin + 57), Size = new Size(100, 25) };

            lblSocTitle = new Label() { Text = "현재 배터리 상태 (SOC)", Location = new Point(10, topMargin + 100), AutoSize = true };
            lblSocValue = new Label() { Text = "0 %", Location = new Point(10, topMargin + 120), Font = new Font(this.Font, FontStyle.Bold), AutoSize = true };

            btnStart = new Button() { Text = "충전 시작", Location = new Point(130, topMargin + 118) };
            btnStop = new Button() { Text = "충전 중지", Location = new Point(220, topMargin + 118) };

            progressBarSoc = new ProgressBar() { Location = new Point(10, topMargin + 150), Size = new Size(300, 23) };

            chart1 = new Chart() { Location = new Point(10, topMargin + 180), Size = new Size(300, 150) };
            chart1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            timer1 = new System.Windows.Forms.Timer() { Interval = 50 };

            timer1 = new System.Windows.Forms.Timer() { Interval = 50 };

            btnStart.Click += new EventHandler(btnStart_Click);
            btnStop.Click += new EventHandler(btnStop_Click);
            timer1.Tick += new EventHandler(timer1_Tick);

            this.Controls.AddRange(new Control[] {
        lblStartSoc, txtStartSoc, lblVoltage, txtVoltage, lblCurrent, txtCurrent,
        lblSocTitle, lblSocValue, btnStart, btnStop, progressBarSoc, chart1
    });
        }

        // ----- 차트 초기화 -----
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

        // ----- 이벤트 핸들러 -----
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!double.TryParse(txtStartSoc.Text, out double startSoc)) { MessageBox.Show("올바른 시작 잔량 값을 입력하세요."); return; }
            if (!double.TryParse(txtVoltage.Text, out double voltage)) { MessageBox.Show("올바른 전압 값을 입력하세요."); return; }
            if (!double.TryParse(txtCurrent.Text, out double current)) { MessageBox.Show("올바른 전류 값을 입력하세요."); return; }

            myBattery.SetInitialState(startSoc);
            UpdateUI();
            myBattery.in_voltage = voltage;
            myBattery.in_current = current;
            myBattery.is_charging = true;
            timer1.Start();
            SimulationStarted?.Invoke(this, EventArgs.Empty);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            myBattery.is_charging = false;
            timer1.Stop();
            SimulationStopped?.Invoke(this, EventArgs.Empty);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            myBattery.TickSimulation();
            UpdateUI();
            tickCount++;
            chart1.Series["SOC"].Points.AddXY(tickCount, myBattery.SocAsDouble);
            chart1.ChartAreas["Default"].RecalculateAxesScale();

            if (myBattery.is_full)
            {
                timer1.Stop();
                MessageBox.Show("충전이 완료되었습니다!");
            }
        }

        private void UpdateUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateUI));
                return;
            }
            lblSocValue.Text = myBattery.SocAsDouble.ToString("F2") + " %";
            if (myBattery.SOC >= progressBarSoc.Minimum && myBattery.SOC <= progressBarSoc.Maximum)
            {
                progressBarSoc.Value = myBattery.SOC;
            }
        }

        private void BatteryControl_Load(object sender, EventArgs e)
        {
            if (chart1 != null)
            {
                chart1.Width = this.ClientSize.Width - 20;
            }
        }
    }
}