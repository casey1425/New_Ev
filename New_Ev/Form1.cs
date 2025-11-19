using System;
using System.Collections.Generic; // Dictionary 사용을 위해 필요
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace New_Ev
{
    public partial class Form1 : Form
    {
        // 멤버 변수 이름이 'evLogic' 입니다.
        private Ev? evLogic;
        private CancellationTokenSource? cts;

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
        }

        // [시작 버튼] 'evControl1'의 StartSimulationClicked 이벤트 핸들러
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

                // evLogic 객체 생성 (변수명 evLogic 사용)
                evLogic = new Ev("eth", "en0", "00:11:22:33:44:55");

                // 이벤트 연결
                evLogic.OnLog += (message) =>
                {
                    // UI 스레드 안전하게 로그 추가
                    if (!logControl1.IsDisposed)
                        logControl1.Invoke(new Action(() => logControl1.AddLog(message)));
                };

                evLogic.OnStateChanged += (newState) =>
                {
                    if (!evControl1.IsDisposed)
                        evControl1.Invoke(new Action(() => evControl1.UpdateState(newState)));
                };

                evLogic.OnBatteryUpdate += (updatedBattery, tick) =>
                {
                    if (!batteryControl1.IsDisposed)
                        batteryControl1.Invoke(new Action(() => batteryControl1.UpdateDisplay(updatedBattery, tick)));
                };

                // 초기값 설정
                evLogic.SetInitialBatteryState(batteryControl1.StartSOC);
                evLogic.SetChargingInputs(batteryControl1.InputVoltage, batteryControl1.InputCurrent);

                // [핵심] 비동기 세션 시작
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
                // 종료 처리
                evLogic = null;
                if (!evControl1.IsDisposed)
                    evControl1.UpdateState("세션 종료됨");
            }
        }

        // [중지 버튼] 새로 추가된 중지 버튼 클릭 이벤트
        // 디자인 보기에서 버튼을 만들고 이 함수와 연결해야 합니다.
        private void btnStop_Click(object sender, EventArgs e)
        {
            // evLogic이 null이 아니면(실행 중이면) 중지 시도
            if (evLogic != null)
            {
                logControl1.AddLog("중지 요청 중...");

                // 1. Ev 클래스 내부의 루프 제어 플래그 끄기
                evLogic.IsRunning = false;

                // 2. 비동기 대기(Task.Delay 등)를 즉시 취소하기 위해 토큰 취소
                cts?.Cancel();
            }
            else
            {
                logControl1.AddLog("실행 중인 시뮬레이션이 없습니다.");
            }
        }

        // 폼이 닫힐 때 실행되는 이벤트
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (evLogic != null)
            {
                evLogic.IsRunning = false; // 루프 종료 플래그
            }
            cts?.Cancel(); // 비동기 작업 취소
        }

        #region MenuStrip Event Handlers
        private void 새시뮬레이션ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 구현 내용
        }
        private void 저장ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 구현 내용
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

        private void batteryControl1_Load(object sender, EventArgs e) { }
        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) { }
        private void evControl1_Load(object sender, EventArgs e) { }
    }
}