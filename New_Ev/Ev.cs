using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace New_Ev
{
    public class Ev
    {
        // ------------------------------------------------------------------------------
        // 1. 멤버 변수 및 이벤트
        // ------------------------------------------------------------------------------
        private RealWhitebeet whitebeet;
        private PollingWorker worker;
        private Battery battery;

        // UI 통신 이벤트
        public event Action<string> OnLog;
        public event Action<string> OnStateChanged;
        public event Action<Battery, int> OnBatteryUpdate;

        // 상태 변수
        private string _state = "init";
        public string State
        {
            get => _state;
            private set { _state = value; OnStateChanged?.Invoke(_state); }
        }

        private Dictionary<string, object> config = new Dictionary<string, object>();
        private Dictionary<string, object> dcChargingParams = new Dictionary<string, object>();

        // 실행 제어
        public bool IsRunning { get; private set; } = false;

        // ------------------------------------------------------------------------------
        // 2. 생성자
        // ------------------------------------------------------------------------------
        public Ev(string iftype, string iface, string mac)
        {
            battery = new Battery();

            // 1. 하드웨어 연결
            whitebeet = new RealWhitebeet(iftype, iface, mac);
            whitebeet.OnLog += this.Log; // 하드웨어 로그 연결

            // 2. 워커 생성 및 이벤트 구독
            worker = new PollingWorker(whitebeet);
            worker.OnDataReceived += Worker_OnDataReceived;

            Log($"Connected to WhiteBeet ({mac})");

            // 기본 설정 초기화
            InitializeConfig(mac);
        }

        private void InitializeConfig(string mac)
        {
            config["battery_capacity"] = battery.Capacity;
            UpdateChargingParameter();
        }

        // ------------------------------------------------------------------------------
        // 3. 공개 메서드 (Start, Stop)
        // ------------------------------------------------------------------------------
        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            Log("EV 시뮬레이션 시작...");

            // 1. 감시 스레드 시작
            worker.Start();

            // 2. 초기화 명령 전송 (별도 스레드나 Task로 실행 권장)
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(500); // 워커 안정화 대기

                    Log("CP 모드 설정 (EV)...");
                    whitebeet.ControlPilotSetMode(0);
                    Thread.Sleep(100);

                    Log("CP 서비스 시작...");
                    whitebeet.ControlPilotStart();
                    Thread.Sleep(100);

                    Log("SLAC 서비스 시작 (EV)...");
                    whitebeet.SlacStart(0);
                    Thread.Sleep(100);

                    Log("V2G 세션 시작 요청...");
                    whitebeet.V2gStartSession();
                }
                catch (Exception ex)
                {
                    Log($"[초기화 오류] {ex.Message}");
                }
            });
        }

        public void Stop()
        {
            Log("시뮬레이션 중지 요청.");
            IsRunning = false;
            worker.Stop();
            whitebeet.Dispose();
        }

        public void SetInitialBatteryState(double startSoc) => battery.SetInitialState(startSoc);
        public void SetChargingInputs(double v, double c)
        {
            battery.in_voltage = v;
            battery.in_current = c;
        }

        // ------------------------------------------------------------------------------
        // 4. [핵심] 워커 이벤트 핸들러 (메시지 수신 처리)
        // ------------------------------------------------------------------------------
        private void Worker_OnDataReceived(object sender, WhitebeetEventArgs e)
        {
            if (!IsRunning) return;

            if (e.IsError)
            {
                Log($"[통신 오류] {e.Message}");
                return;
            }

            // 메시지 ID(StatusId)에 따라 분기 처리
            try
            {
                switch (e.StatusId)
                {
                    case 0xC0: HandleSessionStarted(e.Payload); break;
                    case 0xC1: HandleDCChargeParametersChanged(e.Payload); break;
                    case 0xC2: HandleACChargeParametersChanged(e.Payload); break;
                    case 0xC3: HandleScheduleReceived(e.Payload); break;
                    case 0xC4: HandleCableCheckReady(e.Payload); break;
                    case 0xC5: HandleCableCheckFinished(e.Payload); break;
                    case 0xC6: HandlePreChargingReady(e.Payload); break;
                    case 0xC7: HandleChargingReady(e.Payload); break;
                    case 0xC8: HandleChargingStarted(e.Payload); break;
                    case 0xCC: HandleNotificationReceived(e.Payload); break;
                    case 0xCD: HandleSessionError(e.Payload); break;
                    case 0x80: Log("SLAC 매칭 성공!"); break;
                    case 0x81: Log("SLAC 매칭 실패."); break;
                    // 필요한 경우 추가
                    default: Log($"알 수 없는 메시지 수신: 0x{e.StatusId:X2}"); break;
                }
            }
            catch (Exception ex)
            {
                Log($"[핸들러 오류] {ex.Message}");
            }
        }

        // ------------------------------------------------------------------------------
        // 5. 각 메시지별 핸들러 (기존 로직 이식)
        // ------------------------------------------------------------------------------
        private void HandleSessionStarted(byte[] data)
        {
            Log(">> [세션 시작] Session Started");
            State = "sessionStarted";
            var msg = whitebeet.V2gEvParseSessionStarted(data);
            whitebeet.V2gEvSetConfiguration(config);
        }

        private void HandleDCChargeParametersChanged(byte[] data)
        {
            // Log(">> [파라미터 변경] DC Charge Params Changed");
            var msg = whitebeet.V2gEvParseDCChargeParametersChanged(data);
        }

        private void HandleACChargeParametersChanged(byte[] data)
        {
            Log(">> [파라미터 변경] AC Charge Params Changed");
            var msg = whitebeet.V2gEvParseACChargeParametersChanged(data);
        }

        private void HandleScheduleReceived(byte[] data)
        {
            Log(">> [스케줄 수신] Schedule Received");
            var profile = whitebeet.V2gEvParseScheduleReceived(data);
        }

        private void HandleCableCheckReady(byte[] data)
        {
            Log(">> [케이블 체크] Ready -> Start");
            State = "cableCheckReady";
            whitebeet.V2gStartCableCheck();
        }

        private void HandleCableCheckFinished(byte[] data)
        {
            Log(">> [케이블 체크] Finished");
            State = "cableCheckFinished";
        }

        private void HandlePreChargingReady(byte[] data)
        {
            Log(">> [프리차지] Ready -> Start");
            State = "preChargingReady";
            whitebeet.V2gStartPreCharging();
        }

        private void HandleChargingReady(byte[] data)
        {
            Log(">> [충전 준비 완료] Charging Ready -> Start");
            State = "chargingReady";
            whitebeet.V2gStartCharging();
        }

        private void HandleChargingStarted(byte[] data)
        {
            Log(">> [충전 시작] Charging Started");
            State = "chargingStarted";
            battery.is_charging = true;
        }

        private void HandleNotificationReceived(byte[] data)
        {
            var msg = whitebeet.V2gEvParseNotificationReceived(data);
            Log($">> [알림 수신] Type: {msg["type"]}");
        }

        private void HandleSessionError(byte[] data)
        {
            var msg = whitebeet.V2gEvParseSessionError(data);
            Log($">> [세션 에러] Code: {msg["code"]}");
            State = "end";
        }

        // ------------------------------------------------------------------------------
        // 6. 헬퍼 메서드
        // ------------------------------------------------------------------------------
        private void Log(string msg) => OnLog?.Invoke(msg);

        private void UpdateChargingParameter()
        {
            dcChargingParams["max_voltage"] = battery.max_voltage;
            dcChargingParams["max_power"] = battery.max_power;
            dcChargingParams["soc"] = battery.SOC;
        }

        // 충전 시뮬레이션 틱
        public void Tick()
        {
            if (State == "chargingStarted" && battery.is_charging)
            {
                battery.TickSimulation();
                OnBatteryUpdate?.Invoke(battery, 0);
                UpdateChargingParameter();
                whitebeet.V2gUpdateDCChargingParameters(dcChargingParams);
            }
        }
    }
}