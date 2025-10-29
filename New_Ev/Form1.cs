using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace New_Ev
{
    public partial class Form1 : Form
    {
        private Ev? evLogic;
        private CancellationTokenSource? cts;

        public Form1()
        {
            InitializeComponent(); // 이 한 줄이 디자이너가 만든 모든 것을 불러옵니다.
            this.FormClosing += Form1_FormClosing;
        }

        // 'evControl1'의 StartSimulationClicked 이벤트 핸들러
        private async void evControl1_StartSimulationClicked(object sender, EventArgs e)
        {
            if (evLogic != null)
            {
                logControl1.AddLog("이미 시뮬레이션이 실행 중입니다.");
                return;
            }

            cts = new CancellationTokenSource();

            try
            {
                batteryControl1.ResetDisplay();
                logControl1.ClearLog();

                // evControl1의 UpdateState는 EvControl.cs에 public으로 만들어야 합니다.
                evLogic = new Ev("eth", "en0", "00:11:22:33:44:55");
                evLogic.OnLog += (message) => logControl1.AddLog(message);
                evLogic.OnStateChanged += (newState) => evControl1.UpdateState(newState);
                evLogic.OnBatteryUpdate += (updatedBattery, tick) => batteryControl1.UpdateDisplay(updatedBattery, tick);

                evLogic.SetInitialBatteryState(batteryControl1.StartSOC);
                evLogic.SetChargingInputs(batteryControl1.InputVoltage, batteryControl1.InputCurrent);

                await evLogic.StartSessionAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                logControl1.AddLog("사용자에 의해 시뮬레이션이 중단되었습니다.");
            }
            catch (Exception ex)
            {
                logControl1.AddLog($"[Form1 오류] 시뮬레이션 중 오류 발생: {ex.Message}");
            }
            finally
            {
                evLogic = null;
                evControl1.UpdateState("세션 종료됨");
            }
        }

        // 폼이 닫힐 때 실행되는 이벤트
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            cts?.Cancel();
        }

        #region MenuStrip Event Handlers (이 부분은 이전과 동일)
        private void 새시뮬레이션ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // ...
        }
        private void 저장ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // ...
        }
        private void 종료ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        #endregion

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            if (evLogic == null)
            {
                MessageBox.Show("먼저 'EV 연결 시작' 버튼을 눌러 시뮬레이션을 시작해주세요.");
                return;
            }

            var newConfig = new Dictionary<string, object>
    {
        { "battery_capacity", 75000.0 }
    };

            evLogic.Load(newConfig);
        }
    }
}