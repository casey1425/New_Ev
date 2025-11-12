using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;

namespace New_Ev
{
    // 충전 스케줄 관련 클래스 (Ev.cs 파일 내에 포함)

    public class ChargingProfileEntry
    {
        public int Start { get; set; }
        public int Interval { get; set; }
        public double Power { get; set; }
    }

    /// <summary>
    /// Python: v2gEvParseScheduleReceived의 반환 값 및
    /// Ev.py의 self.schedule에 해당
    /// </summary>
    public class ChargingProfile
    {
        // --- 1단계에서 RealWhitebeet.cs가 필요로 하는 속성들 ---
        public int TupleCount { get; set; }
        public int TupleId { get; set; }
        public int EntriesCount { get; set; }
        public List<ChargingProfileEntry> Entries { get; set; }
         
        // --- Ev.cs의 로직이 추가로 필요로 하는 속성들 (Python 원본 기반) ---
        public int ScheduleTupleId { get; set; }
        public List<int> StartList { get; set; }
        public List<int> IntervalList { get; set; }
        public List<double> PowerList { get; set; }

        public ChargingProfile()
        {
            Entries = new List<ChargingProfileEntry>();
            StartList = new List<int>();
            IntervalList = new List<int>();
            PowerList = new List<double>();
        }
    }

    // EV 클래스
    public class Ev
    {
        // ----- 멤버 변수 -----
        private RealWhitebeet whitebeet; // RealWhitebeet 사용
        private Battery battery;
        private DateTime chargingStartTime;
        private ChargingProfile? currentSchedule;
        private double evseMaxPower = 0;
        private double evseMaxVoltage = 0;
        private double evseMaxCurrent = 0;
        private double initialChargingCurrent;
        private Dictionary<string, object> config = new Dictionary<string, object>();
        private Dictionary<string, object> dcChargingParams = new Dictionary<string, object>();

        // SessionStarted에서 받은 정보 저장
        private string? sessionId = null;
        private string? evseId = null;
        private string? selectedPaymentMethod = null;
        private string? selectedEnergyTransferMode = null;

        // ----- UI 통신 이벤트 -----
        public event Action<string> OnLog;
        public event Action<string> OnStateChanged;
        public event Action<Battery, int> OnBatteryUpdate;

        // ----- 상태 속성 -----
        private string _state = "init";
        public string State { get => _state; private set { _state = value; OnStateChanged?.Invoke(_state); } }

        // ----- 생성자 -----
        public Ev(string iftype, string iface, string mac)
        {
            battery = new Battery();
            whitebeet = new RealWhitebeet(iftype, iface, mac); // RealWhitebeet 생성
            whitebeet.OnLog += this.Log; // 로그 이벤트 연결
            Log($"WHITE-beet-PI firmware version: {whitebeet.Version}");

            config["evid"] = Convert.FromHexString(mac.Replace(":", ""));
            config["protocol_count"] = 2;
            config["protocols"] = new int[] { 0, 1 };
            config["payment_method_count"] = 1;
            config["payment_method"] = new int[] { 0 };
            config["energy_transfer_mode_count"] = 2;
            config["energy_transfer_mode"] = new int[] { 0, 4 };
            config["battery_capacity"] = battery.Capacity;
            UpdateChargingParameter();
        }

        // ----- 공개 메서드 -----
        public async Task StartSessionAsync(CancellationToken cancellationToken)
        {
            Log("EV 세션 시작...");

            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                Log($"[치명적 오류] 하드웨어 초기화 실패: {ex.Message}");
                State = "end";
                return; // 초기화 실패 시 즉시 종료
            }

            await Task.Delay(2000, cancellationToken); // 하드웨어 초기화 대기 시간

            if (await WaitEvseConnectedAsync(null, cancellationToken))
            {
                await HandleEvseConnectedAsync(cancellationToken);
            }
            else
            {
                Log("EVSE 연결 시간 초과.");
            }
        }

        public void SetInitialBatteryState(double startSoc) => this.battery.SetInitialState(startSoc);

        public void SetChargingInputs(double voltage, double current)
        {
            this.battery.in_voltage = voltage;
            this.initialChargingCurrent = current;
            this.battery.in_current = current;
            Log($"초기 충전 입력 설정: {voltage}V, {current}A");
        }

        public void Load(Dictionary<string, object> configDict)
        {
            Log("새로운 설정을 불러옵니다...");
            if (configDict.TryGetValue("battery_capacity", out object capacityValue) && capacityValue is double newCapacity)
            {
                this.battery.Capacity = newCapacity;
                Log($"배터리 용량이 {newCapacity} W/h로 변경되었습니다.");
            }
        }

        // ----- 내부 헬퍼 및 메시지 핸들러 -----
        private void Log(string message) => OnLog?.Invoke(message);

        // Initialize 메서드는 이제 RealWhitebeet의 실제 통신 메서드를 호출
        private void Initialize()
        {
            Log("CP 모드를 EV로 설정");
            whitebeet.ControlPilotSetMode(0);
            Log("CP 서비스 시작");
            whitebeet.ControlPilotStart();
            Log("CP 상태를 State B로 설정");
            whitebeet.ControlPilotSetResistorValue(0);
            Log("SLAC을 EV 모드로 설정");
            whitebeet.SlacSetValidationConfiguration(0);
            Log("SLAC 시작");
            whitebeet.SlacStart(0);
        }

        private void UpdateChargingParameter()
        {
            dcChargingParams["max_voltage"] = battery.max_voltage;
            dcChargingParams["max_power"] = battery.max_power;
            dcChargingParams["soc"] = battery.SOC;
        }

        // WaitEvseConnectedAsync 메서드는 RealWhitebeet의 실제 상태 확인 메서드 호출
        private async Task<bool> WaitEvseConnectedAsync(int? timeout, CancellationToken cancellationToken)
        {
            Log("EVSE가 연결될 때까지 대기...");
            var startTime = DateTime.Now;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double dutyCycle = whitebeet.ControlPilotGetDutyCycle(); // 실제 하드웨어 값 읽기
                Log($"현재 듀티 사이클: {dutyCycle:F1}%");

                if (dutyCycle > 0.1 && dutyCycle < 10.0) // 0.1% ~ 10% 사이 (State B)인지 확인
                {
                    Log("EVSE 연결됨 (State B 감지).");
                    return true;
                }

                if (timeout.HasValue && (DateTime.Now - startTime).TotalSeconds > timeout.Value)
                {
                    Log("EVSE 연결 시간 초과.");
                    return false;
                }
                await Task.Delay(500, cancellationToken); // 0.5초마다 듀티 사이클 확인
            }
        }

        // HandleEvseConnectedAsync 메서드는 RealWhitebeet의 실제 통신 메서드 호출
        private async Task HandleEvseConnectedAsync(CancellationToken cancellationToken)
        {
            Log("SLAC 매칭 시작");
            whitebeet.SlacStartMatching(); // 실제 통신 명령 전송
            bool matched = await Task.Run(() => whitebeet.SlacMatched(), cancellationToken);

            if (matched)
            {
                Log("SLAC 매칭 성공.");
                await HandleNetworkEstablishedAsync(cancellationToken);
            }
            else
            {
                Log("SLAC 매칭 실패.");
                State = "end"; // 매칭 실패 시 세션 종료
            }
        }

        // --- 메시지 핸들러들 (RealWhitebeet의 파서 호출로 변경 필요) ---
        private void HandleSessionStarted(byte[] data)
        {
            Log("\"세션 시작됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseSessionStarted(data); // TODO: RealWhitebeet에 실제 파서 구현 필요
            // ... (정보 저장 및 검증 로직 - 현재는 파서가 비어있으므로 실행 안 됨)
            State = "sessionStarted";
        }
        private void HandleCableCheckReady(byte[] data) { Log("\"케이블 체크 준비됨\" 메시지 수신."); State = "cableCheckReady"; whitebeet.V2gStartCableCheck(); State = "cableCheckStarted"; }
        private void HandleCableCheckFinished(byte[] data) { Log("\"케이블 체크 완료됨\" 메시지 수신."); State = "cableCheckFinished"; }
        private void HandlePreChargingReady(byte[] data) { Log("\"사전 충전 준비됨\" 메시지 수신."); State = "preChargingReady"; whitebeet.V2gStartPreCharging(); State = "preChargingStarted"; }
        private void HandleChargingReady(byte[] data)
        {
            Log("\"충전 준비 완료됨\" 메시지 수신.");
            State = "chargingReady";
            // ... (충전 시작 전 조건 검사 로직) ...
            whitebeet.V2gStartCharging();
        }
        private void HandleChargingStarted(byte[] data) { Log("\"충전 시작됨\" 메시지 수신."); State = "chargingStarted"; battery.is_charging = true; }
        private void HandleDCChargeParametersChanged(byte[] data)
        {
            Log("\"DC 충전 파라미터 변경됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseDCChargeParametersChanged(data); // TODO: RealWhitebeet에 실제 파서 구현 필요
            // ... (정보 저장 및 반영 로직) ...
        }
        private void HandleACChargeParametersChanged(byte[] data)
        {
            Log("\"AC 충전 파라미터 변경됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseACChargeParametersChanged(data); // TODO: RealWhitebeet에 실제 파서 구현 필요
        }
        private void HandleScheduleReceived(byte[] data)
        {
            Log("\"충전 스케줄 수신됨\" 메시지 수신.");
            currentSchedule = whitebeet.V2gEvParseScheduleReceived(data); // TODO: RealWhitebeet에 실제 파서 구현 필요
            // ... (스케줄 저장 로직) ...
        }
        private void HandleNotificationReceived(byte[] data)
        {
            Log("\"알림 수신됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseNotificationReceived(data); // TODO: RealWhitebeet에 실제 파서 구현 필요
            // ... (상태 보고 요청 처리 로직) ...
        }
        private void HandleSessionError(byte[] data)
        {
            Log("\"세션 오류\" 메시지 수신.");
            // ... (오류 코드 해석 로직) ...
            State = "end";
        }

        // ----- 메인 통신 및 충전 루프 -----
        private async Task HandleNetworkEstablishedAsync(CancellationToken cancellationToken)
        {
            whitebeet.V2gSetMode(0); // TODO: RealWhitebeet에 실제 통신 구현 필요
            whitebeet.V2gStartSession(); // TODO: RealWhitebeet에 실제 통신 구현 필요
            State = "sessionStarting";
            int tickCount = 0;
            double lastLoggedSoc = -1;

            while (State != "end")
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (State == "chargingStarted")
                    {
                        // --- 지능형 충전 로직 (CC-CV) ---
                        if (currentSchedule == null)
                        {
                            if (battery.SOC < battery.bulk_soc) { battery.in_current = this.initialChargingCurrent; }
                            else
                            {
                                double taperProgress = (battery.SocAsDouble - battery.bulk_soc) / (100.0 - battery.bulk_soc);
                                double minCurrent = 5.0;
                                battery.in_current = initialChargingCurrent - (initialChargingCurrent - minCurrent) * taperProgress;
                                battery.in_current = Math.Max(battery.in_current, minCurrent);
                            }
                        }

                        battery.TickSimulation();
                        tickCount++;

                        if (Math.Floor(battery.SocAsDouble) > lastLoggedSoc)
                        {
                            lastLoggedSoc = Math.Floor(battery.SocAsDouble);
                            Log($"충전 중... SOC: {battery.SocAsDouble:F2}%, 전류: {battery.in_current:F1}A");
                            dcChargingParams["soc"] = battery.SOC;
                            whitebeet.V2gUpdateDCChargingParameters(dcChargingParams); // TODO: RealWhitebeet에 실제 통신 구현 필요
                        }
                        OnBatteryUpdate?.Invoke(battery, tickCount);
                        if (battery.is_full) { State = "chargingStopped"; }
                        await Task.Delay(50, cancellationToken);
                        // 충전 중에도 메시지 수신을 위해 continue 제거
                    }
                    else if (State == "chargingStopped" || State == "sessionStopped")
                    {
                        if (State == "chargingStopped") { Log("충전 중지. 세션 종료 중..."); whitebeet.V2gStopSession(); State = "sessionStopped"; } // TODO: RealWhitebeet에 실제 통신 구현 필요
                        else { Log("세션 종료됨."); State = "end"; }
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    // --- 실제 통신 메시지 수신 시도 ---
                    var (id, data) = whitebeet.V2gEvReceiveRequest(); // 실제 수신 로직 호출

                    // --- 수신된 메시지 처리 ---
                    if (id == 0xC0) HandleSessionStarted(data);
                    else if (id == 0xC1) HandleDCChargeParametersChanged(data);
                    else if (id == 0xC2) HandleACChargeParametersChanged(data);
                    else if (id == 0xC3) HandleScheduleReceived(data);
                    else if (id == 0xC4) HandleCableCheckReady(data);
                    else if (id == 0xC5) HandleCableCheckFinished(data);
                    else if (id == 0xC6) HandlePreChargingReady(data);
                    else if (id == 0xC7) HandleChargingReady(data);
                    else if (id == 0xC8) HandleChargingStarted(data);
                    else if (id == 0xCC) HandleNotificationReceived(data);
                    else if (id == 0xCD) HandleSessionError(data);
                    else if (id == 0x00) await Task.Delay(100, cancellationToken); // 메시지 없음 (Timeout 대신)
                    else Log($"알 수 없는 메시지 ID 수신: {id:X2}");
                }
                catch (OperationCanceledException) { throw; } // 정상 종료
                catch (TimeoutException) { Log("응답 대기 중..."); await Task.Delay(100, cancellationToken); } // 타임아웃 처리
                catch (Exception ex) { Log($"오류 발생: {ex.Message}"); State = "end"; }
            }
            Log("메인 루프 종료.");
        }
    }
}