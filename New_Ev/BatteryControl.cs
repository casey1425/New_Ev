using System;
using System.Drawing;
using System.Windows.Forms;

namespace New_Ev // 네임스페이스를 Battery.cs와 동일하게 설정
{
    public partial class BatteryControl : System.Windows.Forms.UserControl
    {
        private Battery myBattery;
        private Label lblVoltage, lblCurrent, lblSocTitle, lblSocValue, lblStartSoc;
        private TextBox txtVoltage, txtCurrent, txtStartSoc;
        private Button btnStart, btnStop;
        private ProgressBar progressBarSoc;
        private System.Windows.Forms.Timer timer1;
        public BatteryControl()
        {
            InitializeComponent();
            myBattery = new Battery();
            InitializeControls();
            UpdateUI();
        }
        private void InitializeControls()
        {
            lblStartSoc = new Label() { Text = "시작 잔량 (%)", Location = new Point(30, 30) };
            txtStartSoc = new TextBox() { Text = "0", Location = new Point(150, 30), Size = new Size(100, 25) };
            lblVoltage = new Label() { Text = "입력 전압 (V)", Location = new Point(30, 70) };
            txtVoltage = new TextBox() { Text = "200", Location = new Point(150, 70), Size = new Size(100, 25) };
            lblCurrent = new Label() { Text = "입력 전류 (A)", Location = new Point(30, 110) };
            txtCurrent = new TextBox() { Text = "50", Location = new Point(150, 110), Size = new Size(100, 25) };
            lblSocTitle = new Label() { Text = "현재 배터리 상태 (SOC)", Location = new Point(30, 160), AutoSize = true };
            lblSocValue = new Label() { Text = "0 %", Location = new Point(30, 190), Font = new Font(this.Font, FontStyle.Bold), AutoSize = true };
            btnStart = new Button() { Text = "충전 시작", Location = new Point(150, 190) };
            btnStop = new Button() { Text = "충전 중지", Location = new Point(240, 190) };
            progressBarSoc = new ProgressBar() { Location = new Point(30, 230), Size = new Size(320, 23) };
            timer1 = new System.Windows.Forms.Timer() { Interval = 50 };

            btnStart.Click += new EventHandler(btnStart_Click);
            btnStop.Click += new EventHandler(btnStop_Click);
            timer1.Tick += new EventHandler(timer1_Tick);

            this.Controls.Add(lblStartSoc);
            this.Controls.Add(txtStartSoc);
            this.Controls.Add(lblVoltage);
            this.Controls.Add(txtVoltage);
            this.Controls.Add(lblCurrent);
            this.Controls.Add(txtCurrent);
            this.Controls.Add(lblSocTitle);
            this.Controls.Add(lblSocValue);
            this.Controls.Add(btnStart);
            this.Controls.Add(btnStop);
            this.Controls.Add(progressBarSoc);
        }

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
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            myBattery.is_charging = false;
            timer1.Stop();
        }

        public void StartSimulation()
        {
            btnStart_Click(this, EventArgs.Empty);
        }

        public void StopSimulation()
        {
            btnStop_Click(this, EventArgs.Empty);
        }

        public void ResetSimulation()
        {
            btnStop_Click(this, EventArgs.Empty);

            myBattery = new Battery();

            txtStartSoc.Text = "0";
            txtVoltage.Text = "200";
            txtCurrent.Text = "50";
            UpdateUI();
        }

        public void PauseSimulation()
        {
            // 타이머를 멈추기만 하고, is_charging 상태는 그대로 둠.
            timer1.Stop();
        }

        public string GetCurrentSettings()
        {
            string settings = $"시작 잔량: {txtStartSoc.Text}\n";
            settings += $"입력 전압: {txtVoltage.Text}\n";
            settings += $"입력 전류: {txtCurrent.Text}";
            return settings;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            myBattery.TickSimulation();
            UpdateUI();
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
    }
}