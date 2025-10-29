using System;
using System.Windows.Forms;

namespace New_Ev
{
    public partial class EvControl : UserControl
    {
        private Ev? evLogic;
        public event Action<string> LogGenerated; // 외부로 로그를 전달할 이벤트

        public EvControl()
        {
            InitializeComponent();
        }

        public void UpdateState(string state)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateState(state)));
                return;
            }
            lblEvState.Text = $"상태: {state}";
        }

        public event EventHandler StartSimulationClicked;

        private async void btnStartEv_Click(object sender, EventArgs e)
        {
            StartSimulationClicked?.Invoke(this, EventArgs.Empty);
        }

        // Ev.cs에서 받은 로그를 그대로 밖으로 전달합니다.
        private void EvLogic_OnLog(string message)
        {
            LogGenerated?.Invoke(message);
        }

        // Ev.cs에서 받은 상태 변경 신호를 처리합니다.
        private void EvLogic_OnStateChanged(string newState)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => EvLogic_OnStateChanged(newState)));
                return;
            }
            lblEvState.Text = $"상태: {newState}";
        }

        // Ev.cs에서 받은 배터리 업데이트 신호를 중계합니다. (이 부분은 이전과 동일)
        public event Action<Battery, int> BatteryStateUpdated;
        private void EvLogic_OnBatteryUpdate(Battery updatedBattery, int tickCount)
        {
            BatteryStateUpdated?.Invoke(updatedBattery, tickCount);
        }
    }
}