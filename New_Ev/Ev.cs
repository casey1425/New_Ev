using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;

namespace New_Ev
{
    // ---👇 스케줄 관련 클래스를 Ev 클래스 '밖', 네임스페이스 '안'에 정의합니다 ---
    public class ProfileEntry
    {
        public int Start { get; set; }
        public int Power { get; set; }
    }

    public class ChargingProfile
    {
        public List<ProfileEntry> Entries { get; set; } = new List<ProfileEntry>();
    }
    // ---👆 여기까지 ---

    public class Ev
    {
        // ----- 멤버 변수 -----
        private Whitebeet whitebeet; // MockWhitebeet 대신 Whitebeet 사용 확인
        private Battery battery;
        private DateTime chargingStartTime;
        private ChargingProfile? currentSchedule;
        private double evseMaxPower = 0;
        private double evseMaxVoltage = 0;
        private double evseMaxCurrent = 0;
        private double initialChargingCurrent;
        private Dictionary<string, object> config = new Dictionary<string, object>();
        private Dictionary<string, object> dcChargingParams = new Dictionary<string, object>();

        private string? sessionId = null;
        private string? evseId = null;
        private string? selectedPaymentMethod = null;
        private string? selectedEnergyTransferMode = null;

        // ----- UI 통신 이벤트 (복구됨) -----
        public event Action<string> OnLog;
        public event Action<string> OnStateChanged;
        public event Action<Battery, int> OnBatteryUpdate;

        // ----- 상태 속성 (private set 확인) -----
        private string _state = "init";
        public string State
        {
            get => _state;
            private set // 내부에서는 수정 가능해야 함
            {
                _state = value;
                OnStateChanged?.Invoke(_state);
            }
        }

        // ----- 생성자 -----
        public Ev(string iftype, string iface, string mac)
        {
            battery = new Battery();
            whitebeet = new Whitebeet(iftype, iface, mac);
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
            Initialize();
            await Task.Delay(2000, cancellationToken);
            if (await WaitEvseConnectedAsync(null, cancellationToken))
            {
                await HandleNetworkEstablishedAsync(cancellationToken);
            }
            else { Log("EVSE 연결 시간 초과."); }
        }
        public void SetInitialBatteryState(double startSoc) => this.battery.SetInitialState(startSoc);
        public void SetChargingInputs(double voltage, double current)
        {
            this.battery.in_voltage = voltage;
            this.initialChargingCurrent = current;
            this.battery.in_current = current;
            Log($"초기 충전 입력 설정: {voltage}V, {current}A");
        }
        public void Load(Dictionary<string, object> configDict) { /* ... 이전과 동일 ... */ }

        // ----- 내부 헬퍼 및 메시지 핸들러 -----
        private void Log(string message) => OnLog?.Invoke(message);
        private void Initialize() { /* ... 이전과 동일 ... */ }
        private void UpdateChargingParameter() { /* ... 이전과 동일 ... */ }
        private async Task<bool> WaitEvseConnectedAsync(int? timeout, CancellationToken cancellationToken) { /* ... 이전과 동일 ... */ return true; } // 실제 구현 필요 시 수정
        private async Task HandleEvseConnectedAsync(CancellationToken cancellationToken)
        {
            Log("SLAC 매칭 시작");
            await Task.Delay(1000, cancellationToken);
            whitebeet.SlacStartMatching();
            if (whitebeet.SlacMatched())
            {
                Log("SLAC 매칭 성공.");
                await HandleNetworkEstablishedAsync(cancellationToken);
            }
            else { Log("SLAC 매칭 실패."); }
        }
        private void HandleSessionStarted(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleCableCheckReady(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleCableCheckFinished(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandlePreChargingReady(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleChargingReady(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleChargingStarted(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleDCChargeParametersChanged(byte[] data) { /* ... 이전과 동일 ... */ } // 메서드 이름 확인!
        private void HandleACChargeParametersChanged(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleScheduleReceived(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleNotificationReceived(byte[] data) { /* ... 이전과 동일 ... */ }
        private void HandleSessionError(byte[] data) { /* ... 이전과 동일 ... */ }

        // ----- 메인 통신 및 충전 루프 -----
        private async Task HandleNetworkEstablishedAsync(CancellationToken cancellationToken)
        {
            whitebeet.V2gSetMode(0);
            whitebeet.V2gStartSession();
            State = "sessionStarting"; // State 속성의 set 접근자를 통해 이벤트 발생
            int tickCount = 0;
            double lastLoggedSoc = -1;

            while (State != "end")
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (State == "chargingStarted")
                    { /* ... 충전 로직 (이전과 동일) ... */ }
                    else if (State == "chargingStopped" || State == "sessionStopped")
                    { /* ... 충전 종료 로직 (이전과 동일) ... */ continue; }
                    else
                    {
                        var (id, data) = whitebeet.V2gEvReceiveRequest();
                        if (id == 0xC0) HandleSessionStarted(data);
                        else if (id == 0xC1) HandleDCChargeParametersChanged(data); // 이름 확인!
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
                }
                catch (OperationCanceledException) { throw; }
                catch (TimeoutException) { await Task.Delay(100, cancellationToken); }
                catch (Exception ex) { Log($"[HandleNetwork Loop 오류] {ex.Message}"); State = "end"; } // 오류 로그 강화
            }
            Log("메인 루프 종료.");
        }
    }
}