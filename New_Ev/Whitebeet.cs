using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
// using CH341; // 실제 하드웨어 사용 시 주석 해제

namespace New_Ev
{
    // Whitebeet 클래스 (Mock 구현 상태)
    public class Whitebeet // 실제 하드웨어 사용 시 : IDisposable 추가
    {
        public event Action<string> OnLog;
        public string Version => "Mock v1.0"; // 실제 구현 시 GetVersionFromDevice() 호출
        private int messageSequence = 0;

        // 실제 하드웨어 사용 시 CH341A 멤버 변수 및 생성자 로직 필요
        // private CH341A ch341Device;
        // private bool isDeviceOpen = false;

        public Whitebeet(string iftype, string iface, string mac)
        {
            Log($"[Whitebeet Mock] 초기화: {iftype}, {iface}, {mac}");
            // 실제 하드웨어 사용 시 OpenDevice() 호출
        }

        public void Log(string message) => OnLog?.Invoke($"[Whitebeet] {message}");

        // --- Ev.cs가 호출하는 모든 가짜 메서드들 (public 확인) ---
        public void ControlPilotSetMode(int mode) => Log($"ControlPilot 모드 설정: {mode}");
        public void ControlPilotStart() => Log("ControlPilot 서비스 시작");
        public void ControlPilotSetResistorValue(int value) => Log($"ControlPilot 저항 값 설정: {value}");
        public void SlacSetValidationConfiguration(int config) => Log($"SLAC 유효성 검사 설정: {config}");
        public void SlacStart(int mode) => Log($"SLAC 시작: {mode}");
        public double ControlPilotGetDutyCycle() { Log("ControlPilot 듀티 사이클 요청 받음 (5.0 반환)"); return 5.0; }
        public void SlacStartMatching() => Log("SLAC 매칭 시작");
        public bool SlacMatched() { Log("SLAC 매칭 성공 여부 확인 (성공으로 반환)"); return true; }
        public void V2gSetMode(int mode) => Log($"V2G 모드 설정: {mode}");
        public void V2gStartSession() => Log("V2G 세션 시작");
        public void V2gEvSetConfiguration(Dictionary<string, object> config) => Log("V2G EV 구성 설정 완료");
        public void V2gSetDCChargingParameters(Dictionary<string, object> parameters) => Log("V2G DC 충전 파라미터 설정 완료");
        public void V2gSetACChargingParameters(Dictionary<string, object> parameters) => Log("V2G AC 충전 파라미터 설정 완료");
        public void V2gStopSession() => Log("V2G 세션 중지");
        public void V2gStartCableCheck() => Log("V2G 케이블 체크 시작");
        public void V2gStartPreCharging() => Log("V2G 사전 충전 시작");
        public void V2gStartCharging() => Log("V2G 충전 시작");
        public void V2gStopCharging(bool reneg) => Log($"V2G 충전 중지 (재협상: {reneg})");
        public void V2gUpdateDCChargingParameters(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("soc", out object soc))
                Log($"EV로부터 주기적인 상태 업데이트 수신. 현재 SOC: {soc}%");
        }

        // --- 가짜 메시지 파서들 ---
        public Dictionary<string, object> V2gEvParseSessionStarted(byte[] data)
        {
            Log("가짜 'SessionStarted' 상세 데이터를 파싱합니다.");
            return new Dictionary<string, object>
            {
                { "protocol", "ISO_15118_2" },
                { "session_id", "ABCDEF123456" },
                { "evse_id", "KR-EVSE-007" },
                { "payment_method", "EIM" },
                { "energy_transfer_mode", "DC_extended" }
            };
        }
        public Dictionary<string, object> V2gEvParseDCChargeParametersChanged(byte[] data)
        {
            Log("가짜 'DC Charge Parameters' 상세 데이터를 파싱합니다.");
            return new Dictionary<string, object>
             {
                 { "evse_max_voltage", 400.0 },
                 { "evse_max_current", 150.0 },
                 { "evse_max_power", 50000.0 },
                 { "evse_present_voltage", 200.5 },
                 { "evse_present_current", 50.1 }
             };
        }
        public Dictionary<string, object> V2gEvParseACChargeParametersChanged(byte[] data)
        {
            Log("가짜 'AC Charge Parameters' 상세 데이터를 파싱합니다.");
            return new Dictionary<string, object>
            {
                { "nominal_voltage", 220.0 },
                { "max_current", 32.0 }
            };
        }
        public Dictionary<string, object> V2gEvParseNotificationReceived(byte[] data)
        {
            Log("가짜 'Notification Received' 상세 데이터를 파싱합니다 (타입: 1 - 상태 보고 요청).");
            return new Dictionary<string, object> { { "type", 1 } };
        }
        public ChargingProfile V2gEvParseScheduleReceived(byte[] data)
        {
            Log("다단계 충전 스케줄을 생성합니다 (10초->25A, 20초->40A).");
            var profile = new ChargingProfile();
            profile.Entries.Add(new ChargingProfileEntry { Start = 0, Power = 10000 });
            profile.Entries.Add(new ChargingProfileEntry { Start = 10, Power = 5000 });
            profile.Entries.Add(new ChargingProfileEntry { Start = 20, Power = 8000 });
            return profile;
        }

        // --- 가짜 메시지 전송 로직 ---
        public (int, byte[]) V2gEvReceiveRequest()
        {
            Log("V2G 요청 수신 대기...");
            messageSequence++;
            switch (messageSequence)
            {
                case 1: Log("가짜 'SessionStarted' 메시지(0xC0)를 보냅니다."); return (0xC0, Array.Empty<byte>());
                case 2: Log("가짜 'CableCheckReady' 메시지(0xC4)를 보냅니다."); return (0xC4, Array.Empty<byte>());
                case 3: Log("가짜 'CableCheckFinished' 메시지(0xC5)를 보냅니다."); return (0xC5, Array.Empty<byte>());
                case 4: Log("가짜 'PreChargingReady' 메시지(0xC6)를 보냅니다."); return (0xC6, Array.Empty<byte>());
                case 5: Log("가짜 'ChargingReady' 메시지(0xC7)를 보냅니다."); return (0xC7, Array.Empty<byte>());
                case 6: Log("가짜 'ChargingStarted' 메시지(0xC8)를 보냅니다."); return (0xC8, Array.Empty<byte>());
                case 7: Log("가짜 'DCChargeParametersChanged' 메시지(0xC1)를 보냅니다."); return (0xC1, Array.Empty<byte>());
                case 8: Log("가짜 'ACChargeParametersChanged' 메시지(0xC2)를 보냅니다."); return (0xC2, Array.Empty<byte>());
                case 9: Log("가짜 'ScheduleReceived' 메시지(0xC3)를 보냅니다."); return (0xC3, Array.Empty<byte>());
                case 12: Log("가짜 'NotificationReceived' 메시지(0xCC)를 보냅니다."); return (0xCC, Array.Empty<byte>());
                default:
                    throw new TimeoutException("No more messages from EVSE.");
            }
        }
    }
}