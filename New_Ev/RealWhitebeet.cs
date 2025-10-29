using System;
using System.Collections.Generic;
using System.Linq; // Sum() 사용 위해 추가
using System.Threading;
using CH341; // CH341.cs 파일의 네임스페이스를 사용합니다.

namespace New_Ev // 또는 사용자님의 프로젝트 네임스페이스
{
    /// <summary>
    /// 실제 Whitebeet 하드웨어와 CH341A를 통해 통신하는 클래스입니다.
    /// </summary>
    public class RealWhitebeet : IDisposable
    {
        // --- 멤버 변수 ---
        private CH341A ch341Device;
        private bool isDeviceOpen = false;
        private byte currentRequestId = 0;

        // --- 이벤트 ---
        public event Action<string> OnLog;

        // --- 속성 ---
        // TODO: 실제 장치에서 버전 정보를 읽어오는 GetVersionFromDevice() 메서드 구현 필요
        public string Version => GetVersionFromDevice();

        // --- 프레임 정의 (FramingAPIDef.py 가정) ---
        private const byte START_OF_FRAME = 0xAA;
        private const byte END_OF_FRAME = 0xBB;

        // ----- 생성자 -----
        public RealWhitebeet(string iftype, string iface, string mac)
        {
            Log($"[RealWhitebeet] 실제 하드웨어({mac}) 연결 시도...");
            try
            {
                ch341Device = new CH341A(0); // 인덱스 0 장치 사용 가정
                if (ch341Device.OpenDevice())
                {
                    isDeviceOpen = true;
                    Log("[RealWhitebeet] CH341 장치 열기 성공!");
                    // TODO: 필요한 초기 설정 (예: SetTimeout)
                    // ch341Device.SetTimeout(1000, 1000); // 예시: 1초 타임아웃
                }
                else
                {
                    isDeviceOpen = false;
                    Log("[RealWhitebeet Error] CH341 장치를 열 수 없습니다!");
                    throw new Exception("CH341 Device not detected or could not be opened.");
                }
            }
            catch (Exception ex)
            {
                Log($"[RealWhitebeet Critical Error] 초기화 중 예외: {ex.Message}");
                isDeviceOpen = false;
                throw;
            }
        }

        // ----- 리소스 해제 -----
        public void Dispose()
        {
            CloseDevice();
            GC.SuppressFinalize(this);
        }

        private void CloseDevice()
        {
            if (isDeviceOpen && ch341Device != null)
            {
                ch341Device.CloseDevice();
                isDeviceOpen = false;
                Log("[RealWhitebeet] CH341 장치를 닫았습니다.");
            }
        }

        // ----- 로그 메서드 -----
        private void Log(string message) => OnLog?.Invoke(message);

        // --- 프레임 생성 및 체크섬 계산 헬퍼 ---
        private byte GenerateNextRequestId()
        {
            currentRequestId++;
            if (currentRequestId == 0xFF) currentRequestId = 0;
            return currentRequestId;
        }

        private byte ComputeChecksum(byte[] frameWithoutChecksum)
        {
            int sum = frameWithoutChecksum.Sum(b => b);
            sum = (sum & 0xFF);
            if (sum == 0xFF) return 0xFF;
            return (byte)(~sum);
        }

        private byte[] BuildFrame(byte moduleId, byte subId, byte[] payload)
        {
            byte requestId = GenerateNextRequestId();
            int payloadLength = payload?.Length ?? 0;
            int frameSize = 1 + 1 + 1 + 1 + 2 + payloadLength + 1 + 1; // SOF|Mod|Sub|ReqID|Len(2B)|Payload|Chk|EOF
            byte[] frame = new byte[frameSize];
            int index = 0;

            frame[index++] = START_OF_FRAME;
            frame[index++] = moduleId;
            frame[index++] = subId;
            frame[index++] = requestId;
            frame[index++] = (byte)(payloadLength >> 8);
            frame[index++] = (byte)(payloadLength & 0xFF);
            if (payload != null)
            {
                Array.Copy(payload, 0, frame, index, payloadLength);
                index += payloadLength;
            }
            byte[] frameForChecksum = new byte[frameSize];
            Array.Copy(frame, frameForChecksum, index);
            frameForChecksum[index] = 0x00; // 체크섬 자리
            frameForChecksum[index + 1] = END_OF_FRAME; // EOF

            frame[index++] = ComputeChecksum(frameForChecksum);
            frame[index++] = END_OF_FRAME;
            return frame;
        }

        // --- 실제 통신 및 ACK 확인 메서드 ---
        private byte[] SendAndReceiveAck(byte moduleId, byte subId, byte[] payload)
        {
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");

            byte[] frameToSend = BuildFrame(moduleId, subId, payload);
            Log($"[TX] >> {BitConverter.ToString(frameToSend).Replace("-", " ")}");

            if (!ch341Device.WriteData(frameToSend))
            {
                throw new Exception("WriteData 실패");
            }

            // --- 응답 대기 및 읽기 ---
            Thread.Sleep(200); // 응답 대기 시간 (하드웨어 따라 조절 필요)
            byte[] readBuffer;
            if (ch341Device.ReadData(out readBuffer) && readBuffer != null && readBuffer.Length >= 8) // 최소 프레임 길이 확인
            {
                Log($"[RX] << {BitConverter.ToString(readBuffer).Replace("-", " ")}");

                // TODO: 받은 프레임(readBuffer) 유효성 검증 (SOF, EOF, Checksum) 필요

                int responsePayloadLength = (readBuffer[4] << 8) | readBuffer[5];
                if (responsePayloadLength > 0 && readBuffer.Length >= 7 && readBuffer[6] == 0x00) // 첫 페이로드 바이트가 ACK(0)인지 확인 + 길이 확인
                {
                    byte[] responsePayload = new byte[responsePayloadLength - 1];
                    if (responsePayload.Length > 0 && readBuffer.Length >= 7 + responsePayload.Length)
                    {
                        Array.Copy(readBuffer, 7, responsePayload, 0, responsePayload.Length);
                    }
                    return responsePayload;
                }
                else if (responsePayloadLength == 1 && readBuffer.Length >= 7 && readBuffer[6] != 0x00)
                {
                    throw new Exception($"Command failed or NACK received. Code: {readBuffer[6]:X2}");
                }
                else if (responsePayloadLength == 0 && readBuffer.Length >= 8 && readBuffer[7] == END_OF_FRAME) // ACK만 오고 데이터 없는 경우 EOF로 확인
                {
                    return Array.Empty<byte>();
                }
                else
                {
                    throw new Exception("Invalid response frame structure received.");
                }
            }
            else if (readBuffer != null && readBuffer.Length > 0)
            {
                throw new Exception($"Incomplete or invalid frame received: {BitConverter.ToString(readBuffer).Replace("-", " ")}");
            }
            else
            {
                throw new TimeoutException("Timeout waiting for response or no data received.");
            }
        }

        // ----- Whitebeet 기능 구현 -----

        private string GetVersionFromDevice()
        {
            if (!isDeviceOpen) return "N/A";
            try
            {
                byte[] responsePayload = SendAndReceiveAck(0x10, 0x41, null);
                // TODO: responsePayload 분석 (payloadReader 로직 구현 필요)
                return "Hardware vX.X.X (Parsed)";
            }
            catch (Exception ex)
            {
                Log($"[Error] GetVersionFromDevice 실패: {ex.Message}");
                return "Error";
            }
        }

        public void ControlPilotSetMode(int mode)
        {
            Log($"ControlPilot 모드 설정 요청: {mode}");
            if (!isDeviceOpen) return;
            try
            {
                byte[] payload = new byte[] { (byte)mode };
                SendAndReceiveAck(0x29, 0x40, payload);
                Log("ControlPilot 모드 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] ControlPilotSetMode 실패: {ex.Message}");
                throw;
            }
        }

        public void ControlPilotStart()
        {
            Log("ControlPilot 서비스 시작 요청...");
            if (!isDeviceOpen) return;
            try
            {
                SendAndReceiveAck(0x29, 0x42, null);
                Log("ControlPilot 서비스 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] ControlPilotStart 실패: {ex.Message}");
                throw;
            }
        }

        public void ControlPilotSetResistorValue(int value)
        {
            Log($"ControlPilot 저항 값 설정 요청: {value}");
            if (!isDeviceOpen) return;
            try
            {
                byte[] payload = new byte[] { (byte)value };
                SendAndReceiveAck(0x29, 0x46, payload);
                Log("ControlPilot 저항 값 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] ControlPilotSetResistorValue 실패: {ex.Message}");
                throw;
            }
        }

        public void SlacSetValidationConfiguration(int config)
        {
            Log($"SLAC 유효성 검사 설정 요청: {config}");
            if (!isDeviceOpen) return;
            try
            {
                byte[] payload = new byte[] { (byte)config };
                SendAndReceiveAck(0x28, 0x4B, payload);
                Log("SLAC 유효성 검사 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] SlacSetValidationConfiguration 실패: {ex.Message}");
                throw;
            }
        }

        public void SlacStart(int mode)
        {
            Log($"SLAC 시작 요청: {mode}");
            if (!isDeviceOpen) return;
            try
            {
                byte[] payload = new byte[] { (byte)mode };
                SendAndReceiveAck(0x28, 0x42, payload);
                Log("SLAC 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] SlacStart 실패: {ex.Message}");
                throw;
            }
        }

        public double ControlPilotGetDutyCycle()
        {
            Log("ControlPilot 듀티 사이클 요청...");
            if (!isDeviceOpen) return -1.0;
            try
            {
                byte[] responsePayload = SendAndReceiveAck(0x29, 0x45, null);
                if (responsePayload.Length == 2)
                {
                    int dutyCyclePermille = (responsePayload[0] << 8) | responsePayload[1];
                    double dutyCyclePercent = dutyCyclePermille / 10.0;
                    Log($"ControlPilot 듀티 사이클 수신: {dutyCyclePercent:F1}%");
                    return dutyCyclePercent;
                }
                else
                {
                    Log($"[Error] ControlPilotGetDutyCycle: 예상치 못한 응답 길이 ({responsePayload.Length} bytes)");
                    return -1.0;
                }
            }
            catch (Exception ex)
            {
                Log($"[Error] ControlPilotGetDutyCycle 실패: {ex.Message}");
                return -1.0;
            }
        }

        // --- 메시지 수신 로직 (기초 구현) ---
        public (int, byte[]) V2gEvReceiveRequest()
        {
            if (!isDeviceOpen) throw new InvalidOperationException("Device is not open.");
            Log("V2G 요청 수신 시도...");

            byte[] readBuffer;
            if (ch341Device.ReadData(out readBuffer) && readBuffer != null && readBuffer.Length >= 8)
            {
                Log($"[RX] << {BitConverter.ToString(readBuffer).Replace("-", " ")}");
                if (readBuffer[0] == START_OF_FRAME && readBuffer[readBuffer.Length - 1] == END_OF_FRAME)
                {
                    // TODO: 체크섬 검증
                    byte receivedSubId = readBuffer[2];
                    int payloadLength = (readBuffer[4] << 8) | readBuffer[5];
                    byte[] payload = new byte[payloadLength];
                    if (payloadLength > 0 && readBuffer.Length >= 6 + payloadLength)
                    {
                        Array.Copy(readBuffer, 6, payload, 0, payloadLength);
                        Log($"프레임 분석 성공: Sub={receivedSubId:X2}, PayloadLen={payloadLength}");
                        return (receivedSubId, payload);
                    }
                    else if (payloadLength == 0)
                    {
                        Log($"프레임 분석 성공 (페이로드 없음): Sub={receivedSubId:X2}");
                        return (receivedSubId, Array.Empty<byte>());
                    }
                }
            }
            throw new TimeoutException("No valid frame received within timeout.");
        }


        // TODO: 다른 모든 Whitebeet 기능들도 위와 같이 SendAndReceiveAck 또는 SendAndReceive를 사용하여 구현
        public void SlacStartMatching() => Log("SlacStartMatching() 호출됨 (구현 필요)");
        public bool SlacMatched() { Log("SlacMatched() 호출됨 (구현 필요, 임시 true 반환)"); return true; } // 실제 통신 필요
        public void V2gSetMode(int mode) => Log($"V2gSetMode({mode}) 호출됨 (구현 필요)");
        public void V2gEvSetConfiguration(Dictionary<string, object> config) => Log("V2gEvSetConfiguration() 호출됨 (구현 필요)");
        public void V2gSetDCChargingParameters(Dictionary<string, object> parameters) => Log("V2gSetDCChargingParameters() 호출됨 (구현 필요)");
        public void V2gSetACChargingParameters(Dictionary<string, object> parameters) => Log("V2gSetACChargingParameters() 호출됨 (구현 필요)");
        public void V2gStartSession() => Log("V2gStartSession() 호출됨 (구현 필요)");
        public void V2gStopSession() => Log("V2gStopSession() 호출됨 (구현 필요)");
        public void V2gStartCableCheck() => Log("V2gStartCableCheck() 호출됨 (구현 필요)");
        public void V2gStartPreCharging() => Log("V2gStartPreCharging() 호출됨 (구현 필요)");
        public void V2gStartCharging() => Log("V2gStartCharging() 호출됨 (구현 필요)");
        public void V2gStopCharging(bool reneg) => Log($"V2gStopCharging({reneg}) 호출됨 (구현 필요)");
        public void V2gUpdateDCChargingParameters(Dictionary<string, object> parameters) => Log("V2gUpdateDCChargingParameters() 호출됨 (구현 필요)");

        // --- 메시지 파서들 (실제 통신 구현 시에는 실제 파서로 교체 필요) ---
        public Dictionary<string, object> V2gEvParseSessionStarted(byte[] data) { Log("[Parser Needs Implementation] V2gEvParseSessionStarted 호출됨"); return new Dictionary<string, object>(); }
        public Dictionary<string, object> V2gEvParseDCChargeParametersChanged(byte[] data) { Log("[Parser Needs Implementation] V2gEvParseDCChargeParametersChanged 호출됨"); return new Dictionary<string, object>(); }
        public Dictionary<string, object> V2gEvParseACChargeParametersChanged(byte[] data) { Log("[Parser Needs Implementation] V2gEvParseACChargeParametersChanged 호출됨"); return new Dictionary<string, object>(); }
        public Dictionary<string, object> V2gEvParseNotificationReceived(byte[] data) { Log("[Parser Needs Implementation] V2gEvParseNotificationReceived 호출됨"); return new Dictionary<string, object>(); }
        public ChargingProfile V2gEvParseScheduleReceived(byte[] data) { Log("[Parser Needs Implementation] V2gEvParseScheduleReceived 호출됨"); return new ChargingProfile(); }

    }
}