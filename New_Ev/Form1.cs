using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
using New_Ev;
using Microsoft.VisualBasic.Logging;

namespace New_Ev
{
    public partial class Form1 : Form
    {
        // ========================================================
        // 1. 멤버 변수 선언
        // ========================================================
        private RealWhitebeet _whitebeet;
        private PollingWorker _worker;

        public Form1()
        {
            InitializeComponent();

            // 폼 종료 이벤트 연결
            this.FormClosing += Form1_FormClosing;
        }

        // ========================================================
        // 2. [시작 버튼] 연결 및 초기화 명령 전송
        // ========================================================
        private void evControl1_StartSimulationClicked(object sender, EventArgs e)
        {
            // 중복 실행 방지
            if (_worker != null)
            {
                Log("이미 연결되어 있습니다.");
                return;
            }

            try
            {
                Log("하드웨어 연결 시도 중...");

                // 1. 하드웨어 객체 생성 (CH341A 연결)
                _whitebeet = new RealWhitebeet("SPI", "CH341", "00:01:02:03:04:05");
                _whitebeet.OnLog += (msg) => Log(msg);
                Log("하드웨어(CH341A) 연결 성공.");

                // 2. 워커(스레드) 객체 생성 및 하드웨어 주입
                _worker = new PollingWorker(_whitebeet);

                // 3. [중요] 이벤트 연결 (구독)
                // 워커가 데이터를 찾으면 Worker_OnDataReceived 함수가 실행됨
                _worker.OnDataReceived += Worker_OnDataReceived;

                // 4. 감시 스레드 시작
                _worker.Start();
                Log("감시 스레드 시작됨. (TX_Pending 확인 중...)");

                Application.DoEvents();
                Thread.Sleep(200);

                Log(">> [초기화] 모듈 설정 시작...");

                // (1) Control Pilot (CP) 서비스 설정 (EV 모드 = 0)
                _whitebeet.ControlPilotSetMode(0);
                Thread.Sleep(100); // 명령 간 간격

                // (2) Control Pilot 시작
                _whitebeet.ControlPilotStart();
                Thread.Sleep(100);

                // (3) SLAC 서비스 시작 (EV 모드 = 0)
                _whitebeet.SlacStart(0);
                Thread.Sleep(100);

                // (4) V2G 세션 시작 (여기서부터 본격적인 통신 시작)
                _whitebeet.V2gStartSession();

                Log(">> [초기화] 모든 시작 명령 전송 완료. 응답 대기 중...");

                if (!evControl1.IsDisposed)
                    evControl1.UpdateState("연결됨 / 초기화 완료");
            }
            catch (Exception ex)
            {
                Log($"[연결 실패] {ex.Message}");
                MessageBox.Show($"장치 연결에 실패했습니다.\n\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 실패 시 자원 정리
                CleanUp();
            }
        }

        // ========================================================
        // 3. [중지 버튼] 연결 해제 및 스레드 종료
        // ========================================================
        private void btnStop_Click(object sender, EventArgs e)
        {
            if (_worker == null)
            {
                Log("실행 중인 연결이 없습니다.");
                return;
            }

            Log("연결 종료 요청...");
            CleanUp();
            Log("연결이 종료되었습니다.");

            if (!evControl1.IsDisposed)
                evControl1.UpdateState("연결 종료");
        }

        // 자원 해제 공통 메서드
        private void CleanUp()
        {
            if (_worker != null)
            {
                _worker.Stop();
                _worker = null;
            }

            if (_whitebeet != null)
            {
                _whitebeet.Dispose();
                _whitebeet = null;
            }
        }

        // ========================================================
        // 4. [이벤트 핸들러] 데이터 수신 처리 (스레드 -> UI)
        // ========================================================
        private void Worker_OnDataReceived(object sender, WhitebeetEventArgs e)
        {
            // 백그라운드 스레드에서 호출되므로 Invoke 필수
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Worker_OnDataReceived(sender, e)));
                return;
            }

            // 에러 메시지 처리
            if (e.IsError)
            {
                Log($"[오류] {e.Message}");
                return;
            }

            // 정상 데이터 수신 시 로그 출력 (Payload 내용도 보고 싶다면 여기서 처리)
            Log(e.Message);

            // 수신된 StatusId(메시지 ID)에 따라 UI를 갱신하거나 다음 동작 수행
            switch (e.StatusId)
            {
                case 0xC0: // Session Started (EV)
                    Log(">> [이벤트] V2G 세션이 시작되었습니다!");
                    if (!evControl1.IsDisposed) evControl1.UpdateState("Charging Session Started");
                    break;

                case 0x80: // SLAC Success
                    Log(">> [이벤트] SLAC 매칭 성공!");
                    if (!evControl1.IsDisposed) evControl1.UpdateState("SLAC Matched");

                    // SLAC이 성공하면 자동으로 V2G 매칭 프로세스 시작하도록 할 수도 있음
                    // _whitebeet.SlacStartMatching(); 
                    break;

                case 0x81: // SLAC Failed
                    Log(">> [이벤트] SLAC 매칭 실패.");
                    break;
            }
        }

        // ========================================================
        // 5. 유틸리티 및 기타 이벤트
        // ========================================================

        // 로그 출력 헬퍼
        private void Log(string msg)
        {
            if (!logControl1.IsDisposed)
            {
                // logControl1이 UserControl이라면 내부 메서드를 호출
                if (logControl1.InvokeRequired)
                    logControl1.Invoke(new Action(() => logControl1.AddLog(msg)));
                else
                    logControl1.AddLog(msg);
            }
        }

        // 폼 종료 시 정리
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanUp();
        }

        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            if (_whitebeet == null)
            {
                MessageBox.Show("먼저 시뮬레이션(하드웨어 연결)을 시작해주세요.");
                return;
            }

            var newConfig = new Dictionary<string, object>
            {
                { "battery_capacity", 75000.0 }
            };

            // _whitebeet.V2gEvSetConfiguration(newConfig);
            Log("설정 로드됨 (명령 전송은 구현 필요)");
        }

        // 빈 이벤트 핸들러들
        private void batteryControl1_Load(object sender, EventArgs e) { }
        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) { }
        private void evControl1_Load(object sender, EventArgs e) { }
        private void 새시뮬레이션ToolStripMenuItem_Click(object sender, EventArgs e) { }
        private void 저장ToolStripMenuItem_Click(object sender, EventArgs e) { }
        private void 종료ToolStripMenuItem_Click(object sender, EventArgs e) { Application.Exit(); }
    }
}