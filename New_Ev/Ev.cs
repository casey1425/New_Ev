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
        public int TupleCount { get; set; }
        public int TupleId { get; set; }
        public int EntriesCount { get; set; }
        public List<ChargingProfileEntry> Entries { get; set; }

        // --- Ev.cs의 로직이 추가로 필요로 하는 속성들 ---
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
        private RealWhitebeet whitebeet;
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

        // ----- 실행 제어 변수 (중지 기능) -----
        // 외부(Form1)에서 이 값을 false로 설정하면 루프가 멈춥니다.
        public bool IsRunning { get; set; } = false;

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
            whitebeet = new RealWhitebeet(iftype, iface, mac);
            whitebeet.OnLog += this.Log;
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
            IsRunning = true; // 실행 플래그 켜기

            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                Log($"[치명적 오류] 하드웨어 초기화 실패: {ex.Message}");
                State = "end";
                IsRunning = false;
                return;
            }

            await Task.Delay(2000, cancellationToken);

            if (await WaitEvseConnectedAsync(null, cancellationToken))
            {
                await HandleEvseConnectedAsync(cancellationToken);
            }
            else
            {
                Log("EVSE 연결 시간 초과.");
            }

            IsRunning = false; // 종료 시 플래그 끄기
        }

        public void Stop()
        {
            Log("사용자에 의해 중지 요청됨.");
            IsRunning = false;
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

        private async Task<bool> WaitEvseConnectedAsync(int? timeout, CancellationToken cancellationToken)
        {
            Log("EVSE가 연결될 때까지 대기...");
            var startTime = DateTime.Now;
            while (IsRunning) // [수정] IsRunning 체크 추가
            {
                cancellationToken.ThrowIfCancellationRequested();

                double dutyCycle = whitebeet.ControlPilotGetDutyCycle();
                Log($"현재 듀티 사이클: {dutyCycle:F1}%");

                if (dutyCycle > 0.1 && dutyCycle < 10.0)
                {
                    Log("EVSE 연결됨 (State B 감지).");
                    return true;
                }

                if (timeout.HasValue && (DateTime.Now - startTime).TotalSeconds > timeout.Value)
                {
                    Log("EVSE 연결 시간 초과.");
                    return false;
                }
                await Task.Delay(500, cancellationToken);
            }
            Log("대기 중단됨 (IsRunning = false)");
            return false;
        }

        private async Task HandleEvseConnectedAsync(CancellationToken cancellationToken)
        {
            Log("SLAC 매칭 시작");
            whitebeet.SlacStartMatching();
            // [참고] SlacMatched 내부에서 무한 루프를 돈다면 거기도 IsRunning 체크가 필요할 수 있음
            // 현재 RealWhitebeet 구현상 SlacMatched는 타임아웃이 있으므로 괜찮습니다.
            bool matched = await Task.Run(() => whitebeet.SlacMatched(), cancellationToken);

            if (matched && IsRunning)
            {
                Log("SLAC 매칭 성공.");
                await HandleNetworkEstablishedAsync(cancellationToken);
            }
            else
            {
                Log(matched ? "중지됨." : "SLAC 매칭 실패.");
                State = "end";
            }
        }

        private void HandleSessionStarted(byte[] data)
        {
            Log("\"세션 시작됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseSessionStarted(data);
            // ... (정보 저장 및 검증 로직)
            State = "sessionStarted";
        }
        private void HandleCableCheckReady(byte[] data) { Log("\"케이블 체크 준비됨\" 메시지 수신."); State = "cableCheckReady"; whitebeet.V2gStartCableCheck(); State = "cableCheckStarted"; }
        private void HandleCableCheckFinished(byte[] data) { Log("\"케이블 체크 완료됨\" 메시지 수신."); State = "cableCheckFinished"; }
        private void HandlePreChargingReady(byte[] data) { Log("\"사전 충전 준비됨\" 메시지 수신."); State = "preChargingReady"; whitebeet.V2gStartPreCharging(); State = "preChargingStarted"; }
        private void HandleChargingReady(byte[] data)
        {
            Log("\"충전 준비 완료됨\" 메시지 수신.");
            State = "chargingReady";
            whitebeet.V2gStartCharging();
        }
        private void HandleChargingStarted(byte[] data) { Log("\"충전 시작됨\" 메시지 수신."); State = "chargingStarted"; battery.is_charging = true; }
        private void HandleDCChargeParametersChanged(byte[] data)
        {
            Log("\"DC 충전 파라미터 변경됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseDCChargeParametersChanged(data);
        }
        private void HandleACChargeParametersChanged(byte[] data)
        {
            Log("\"AC 충전 파라미터 변경됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseACChargeParametersChanged(data);
        }
        private void HandleScheduleReceived(byte[] data)
        {
            Log("\"충전 스케줄 수신됨\" 메시지 수신.");
            currentSchedule = whitebeet.V2gEvParseScheduleReceived(data);
        }
        private void HandleNotificationReceived(byte[] data)
        {
            Log("\"알림 수신됨\" 메시지 수신.");
            var message = whitebeet.V2gEvParseNotificationReceived(data);
        }
        private void HandleSessionError(byte[] data)
        {
            Log("\"세션 오류\" 메시지 수신.");
            State = "end";
        }

        // ----- 메인 통신 및 충전 루프 -----
        private async Task HandleNetworkEstablishedAsync(CancellationToken cancellationToken)
        {
            whitebeet.V2gSetMode(0);
            whitebeet.V2gStartSession();
            State = "sessionStarting";
            int tickCount = 0;
            double lastLoggedSoc = -1;

            // [수정] 루프 조건에 IsRunning 추가
            while (State != "end" && IsRunning)
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
                            whitebeet.V2gUpdateDCChargingParameters(dcChargingParams);
                        }
                        OnBatteryUpdate?.Invoke(battery, tickCount);
                        if (battery.is_full) { State = "chargingStopped"; }
                        await Task.Delay(50, cancellationToken);
                        // continue 제거됨: 충전 중에도 메시지 수신 체크
                    }
                    else if (State == "chargingStopped" || State == "sessionStopped")
                    {
                        if (State == "chargingStopped") { Log("충전 중지. 세션 종료 중..."); whitebeet.V2gStopSession(); State = "sessionStopped"; }
                        else { Log("세션 종료됨."); State = "end"; }
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    // --- 실제 통신 메시지 수신 시도 ---
                    // ReceiveRequest 내부에서 블로킹되거나 타임아웃되므로 IsRunning 체크가 중요함
                    try
                    {
                        var (id, data) = whitebeet.V2gEvReceiveRequest();

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
                        else if (id == 0x00) await Task.Delay(100, cancellationToken);
                        else Log($"알 수 없는 메시지 ID 수신: {id:X2}");
                    }
                    catch (TimeoutException)
                    {
                        // 메시지 없음, 계속 진행
                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Log($"오류 발생: {ex.Message}"); State = "end"; }
            }
            Log("메인 루프 종료.");
        }
    }
}