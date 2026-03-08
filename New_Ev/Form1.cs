using CH341;
using New_Ev;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace New_Ev
{
    public partial class Form1 : Form
    {
        private RealWhitebeet _whitebeet;
        private PollingWorker _worker;

        //2025.12.08    SPI 통신 처리를 위한 SPI device 추가
        private CH341A spi_driver = new CH341A();
        private WB_Frame device;


        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;

//2025.12.08    SPI 통신 처리를 분리
            if (!spi_driver.OpenDevice())
            {
                MessageBox.Show("Device is not detected");
                return;
            }
            device = new WB_Frame(spi_driver);
            device.Event_Notifier += OnReceivedStatus;

        }
//2025.12.08 수신된 메시지 처리
        private void OnReceivedStatus(object sender, WB_EventArgsStatus e)
        {
            //string msg = "status".PadRight(10);
            //txtLog.AppendText($"{msg}:{get_hex_string(e.received_buf)}\r\n");
        }
        // [연결 버튼] 시작 및 단계별 초기화 (실패 시 즉시 중단)
        private void evControl1_StartSimulationClicked(object sender, EventArgs e)
        {
            if (_worker != null) { Log("이미 연결되어 있습니다."); return; }

            try
            {
                Log("============== [연결 시작] ==============");

                // 1. 하드웨어 연결
                _whitebeet = new RealWhitebeet("SPI", "CH341", "00:01:02:03:04:05");
                _whitebeet.OnLog += (msg) => Log(msg);
                Log("하드웨어(CH341A) 초기화 성공.");

                // 2. 워커 시작
                _worker = new PollingWorker(_whitebeet);
                _worker.OnDataReceived += Worker_OnDataReceived;
                _worker.Start();
                Log("감시 스레드 시작됨.");

                // 스레드 안정화 대기
                Application.DoEvents();
                Thread.Sleep(500);

                // ------------------------------------------------------------
                // [초기화 시퀀스] 단계별 실행 (하나라도 실패하면 즉시 종료)
                // ------------------------------------------------------------

                Log(">> [Step 1] CP 모드 설정 (EV) 전송...");
                // 성공 여부 확인 -> 실패 시 return
                if (!_whitebeet.ControlPilotSetMode(0))
                {
                    Log(">> [중단] Step 1 실패! 하드웨어 연결(MISO/MOSI)을 점검하세요.");
                    CleanUp(); // 자원 정리
                    return;    // 함수 강제 종료 (다음 단계 실행 안 함)
                }
                Thread.Sleep(1000); // 성공했으면 잠시 대기 후 다음으로

                Log(">> [Step 2] CP 서비스 시작 전송...");
                if (!_whitebeet.ControlPilotStart())
                {
                    Log(">> [중단] Step 2 실패! 모듈 상태를 확인하세요.");
                    CleanUp();
                    return;
                }
                Thread.Sleep(1000);

                Log(">> [Step 3] SLAC 서비스 시작 전송...");
                if (!_whitebeet.SlacStart(0))
                {
                    Log(">> [중단] Step 3 실패.");
                    CleanUp();
                    return;
                }
                Thread.Sleep(1000);

                Log(">> [Step 4] V2G 세션 시작 전송...");
                if (!_whitebeet.V2gStartSession())
                {
                    Log(">> [중단] Step 4 실패.");
                    CleanUp();
                    return;
                }

                // 모든 단계 통과 시
                Log("============== [초기화 완료] ==============");
                Log("모든 명령이 정상적으로 전송되었습니다. 응답 대기 중...");

                if (!evControl1.IsDisposed) evControl1.UpdateState("Connected");
            }
            catch (Exception ex)
            {
                Log($"[오류 발생] {ex.Message}");
                MessageBox.Show($"연결 중 오류가 발생했습니다.\n\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CleanUp();
            }
        }

        // [중지 버튼]
        private void btnStop_Click(object sender, EventArgs e)
        {
            Log("연결 종료 요청...");
            CleanUp();
            Log("연결이 종료되었습니다.");

            if (!evControl1.IsDisposed)
                evControl1.UpdateState("Disconnected");
        }

        // 데이터 수신 핸들러
        private void Worker_OnDataReceived(object sender, WhitebeetEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Worker_OnDataReceived(sender, e)));
                return;
            }

            if (e.IsError)
            {
                Log($"[Worker 오류] {e.Message}");
                return;
            }

            Log(e.Message);

            switch (e.StatusId)
            {
                case 0xC0:
                    Log(">> [이벤트] V2G 세션 시작됨!");
                    if (!evControl1.IsDisposed) evControl1.UpdateState("Session Started");
                    break;
                case 0x80:
                    Log(">> [이벤트] SLAC 매칭 성공!");
                    if (!evControl1.IsDisposed) evControl1.UpdateState("SLAC Matched");
                    break;
                case 0x81:
                    Log(">> [이벤트] SLAC 매칭 실패.");
                    break;
            }
        }

        // 자원 정리
        private void CleanUp()
        {
            if (_worker != null) { _worker.Stop(); _worker = null; }
            if (_whitebeet != null) { _whitebeet.Dispose(); _whitebeet = null; }
        }

        private void Log(string msg)
        {
            if (logControl1 != null && !logControl1.IsDisposed)
            {
                if (logControl1.InvokeRequired)
                    logControl1.Invoke(new Action(() => logControl1.AddLog(msg)));
                else
                    logControl1.AddLog(msg);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) => CleanUp();

        // 기타 이벤트
        private void btnLoadConfig_Click(object sender, EventArgs e) { }
        private void batteryControl1_Load(object sender, EventArgs e) { }
        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e) { }
        private void evControl1_Load(object sender, EventArgs e) { }
        private void 새시뮬레이션ToolStripMenuItem_Click(object sender, EventArgs e) { }
        private void 저장ToolStripMenuItem_Click(object sender, EventArgs e) { }
        private void 종료ToolStripMenuItem_Click(object sender, EventArgs e) { Application.Exit(); }
    }
}