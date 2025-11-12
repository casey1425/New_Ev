using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CH341;

namespace New_Ev
{
    public class RealWhitebeet : IDisposable
    {
        // --- 멤버 변수 ---
        private CH341A ch341Device;
        private bool isDeviceOpen = false;
        private byte currentRequestId = 0;

        // --- 이벤트 ---
        public event Action<string> OnLog;

        // --- 속성 ---
        public string Version => GetVersionFromDevice();

        // --- 프레임 정의 (WB_SPI.cs 분석 결과) ---
        private const byte DATA_HEADER_1 = 0x55;
        private const byte DATA_HEADER_2 = 0x55;
        private const byte SIZE_HEADER_1 = 0xAA;
        private const byte SIZE_HEADER_2 = 0xAA;
        private const int CHIP_SELECT = 0x80;
        private const uint MASK_TX_Pending = 0x0000_0100; // C6 핀 (데이터 준비 알림)
        private const uint MASK_RX_Ready = 0x0000_0200; // C5 핀 (수신 준비 알림)

        // ----- 생성자 -----
        public RealWhitebeet(string iftype, string iface, string mac)
        {
            Log($"[RealWhitebeet] 실제 하드웨어({mac}) 연결 시도...");
            try
            {
                ch341Device = new CH341A(0);
                if (ch341Device.OpenDevice())
                {
                    isDeviceOpen = true;
                    Log("[RealWhitebeet] CH341 장치 열기 성공!");

                    // [수정 1] SetStream(0x82)는 여기서 딱 한 번만 호출합니다.
                    if (ch341Device.SetStream(0x82))
                    {
                        Log("[RealWhitebeet] SPI 스트림 모드(0x82) 설정 성공!");

                        // [수정 2] 하드웨어가 SPI 모드로 부팅될 시간을 50ms 줍니다.
                        Thread.Sleep(50);
                    }
                    else
                    {
                        Log("[RealWhitebeet Error] SPI 스트림 모드 설정 실패!");
                        throw new Exception("Failed to set CH341 stream mode (0x82).");
                    }
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
        public void Dispose() { CloseDevice(); GC.SuppressFinalize(this); }
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

        // --- 헬퍼 메서드 (페이로드 빌더) ---
        private byte[] ValueToExponential(object value)
        {
            byte[] retValue = new byte[3];
            long longValue;
            if (value is int valInt) longValue = valInt;
            else if (value is long valLong) longValue = valLong;
            else if (value is double valDouble) longValue = (long)valDouble;
            else throw new ArgumentException($"Value '{value}' (Type: {value.GetType()}) must be a numeric type for exponential conversion.");
            sbyte exponent = 0;
            long tempBase = longValue;
            while (tempBase != 0 && (tempBase % 10) == 0 && exponent < 3)
            {
                exponent++;
                tempBase /= 10;
            }
            short baseValue = (short)tempBase;
            byte[] baseBytes = BitConverter.GetBytes(baseValue);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(baseBytes);
            }
            retValue[0] = baseBytes[0];
            retValue[1] = baseBytes[1];
            retValue[2] = (byte)exponent;
            return retValue;
        }

        private byte[] IntTo4BytesBigEndian(object value)
        {
            uint uintValue = Convert.ToUInt32(value);
            byte[] bytes = BitConverter.GetBytes(uintValue);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }


        // --- 실제 통신 (SPI) 메서드 ---
        private bool SendSpi(ref byte[] ioBuffer)
        {
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            return ch341Device.StreamSPI4(CHIP_SELECT, (uint)ioBuffer.Length, ref ioBuffer);
        }

        // --- 프레임 생성 (WB_SPI.cs 로직 기반) ---
        private byte GenerateNextRequestId() { currentRequestId++; if (currentRequestId == 0xFF) currentRequestId = 0; return currentRequestId; }

        private byte[] BuildQueryFrame(byte moduleId, byte subId, byte[] payload)
        {
            int payloadLength = payload?.Length ?? 0;
            byte[] queryFrame = new byte[3 + payloadLength];
            queryFrame[0] = moduleId;
            queryFrame[1] = subId;
            queryFrame[2] = GenerateNextRequestId();
            if (payloadLength > 0)
                Array.Copy(payload, 0, queryFrame, 3, payloadLength);
            return queryFrame;
        }

        private byte[] BuildDataFrame(byte[] queryFrame)
        {
            byte[] txBuffer = new byte[queryFrame.Length + 4];
            txBuffer[0] = DATA_HEADER_1;
            txBuffer[1] = DATA_HEADER_2;
            txBuffer[2] = 0x00;
            txBuffer[3] = 0x00;
            Array.Copy(queryFrame, 0, txBuffer, 4, queryFrame.Length);

            return txBuffer;
        }

        // --- WB_SPI.cs의 is_pin_state_high 로직 ---
        private bool IsPinHigh(uint pinMask)
        {
            uint gpioStatus;
            if (ch341Device.GetInput(out gpioStatus))
            {
                return (gpioStatus & pinMask) > 0;
            }
            throw new Exception("GPIO 상태 읽기 실패 (GetInput)");
        }

        // --- 실제 통신 및 응답 대기 로직 (WB_SPI.cs의 Send_Query 참조) ---
        private byte[] SendQuery(byte moduleId, byte subId, byte[] payload)
        {
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");

            // 1. 전송할 쿼리 프레임 생성
            byte[] queryFrame = BuildQueryFrame(moduleId, subId, payload);
            byte[] txBuffer = BuildDataFrame(queryFrame);

            // 2. 'rx_ready' (C5핀) 대기 로직
            Log("응답 수신 준비 대기 중... (RX_Ready 핀 확인)");
            DateTime startTime = DateTime.Now;
            bool rxReady = false;
            while ((DateTime.Now - startTime).TotalMilliseconds < 500)
            {
                if (IsPinHigh(MASK_RX_Ready)) { rxReady = true; break; }
                Thread.Sleep(10);
            }
            if (!rxReady) { throw new TimeoutException("응답 수신 준비 시간 초과 (RX_Ready 신호 없음)"); }
            Log("RX_Ready 감지됨. 쿼리 전송 시작...");

            // 3. 쿼리 전송 (데이터 전송 및 에코 수신)
            Log($"[TX] >> {BitConverter.ToString(txBuffer).Replace("-", " ")}");
            if (!SendSpi(ref txBuffer))
            {
                throw new Exception("SendSpi (StreamSPI4) 통신 실패");
            }
            Log($"[RX-ECHO] << {BitConverter.ToString(txBuffer).Replace("-", " ")}");

            // 4. 'is_tx_pending' (C6핀) 응답 대기 로직
            Log("응답 대기 중... (TX_Pending 핀 확인)");
            startTime = DateTime.Now;
            bool txPending = false;
            while ((DateTime.Now - startTime).TotalMilliseconds < 500)
            {
                if (IsPinHigh(MASK_TX_Pending)) { txPending = true; break; }
                Thread.Sleep(10);
            }
            if (!txPending) { throw new TimeoutException("응답 시간 초과 (TX_Pending 신호 없음)"); }

            Log("TX_Pending 감지됨. 응답 프레임 수신 시작...");

            // 5. 크기 요청 프레임 전송
            byte[] sizeTxFrame = { SIZE_HEADER_1, SIZE_HEADER_2, 0x00, 0x00 };
            if (!SendSpi(ref sizeTxFrame))
            {
                throw new Exception("SPI 크기 요청 프레임 전송 실패 (StreamSPI4)");
            }

            // 6. 응답에서 크기 읽기
            ushort size = (ushort)((sizeTxFrame[2] << 8) | sizeTxFrame[3]);
            Log($"수신할 데이터 크기: {size}");
            if (size == 0)
            {
                Log("데이터 크기 0 수신 (ACK로 간주).");
                return Array.Empty<byte>();
            }

            // 7. 실제 데이터 요청 프레임 전송 (size > 0 인 경우)
            byte[] dataTxFrame = new byte[size + 4];
            dataTxFrame[0] = DATA_HEADER_1;
            dataTxFrame[1] = DATA_HEADER_2;
            dataTxFrame[2] = 0x00;
            dataTxFrame[3] = 0x00;

            if (!SendSpi(ref dataTxFrame))
            {
                throw new Exception("SPI 데이터 요청 프레임 전송 실패 (StreamSPI4)");
            }

            byte[] readBuffer = dataTxFrame;
            Log($"[RX-DATA] << {BitConverter.ToString(readBuffer).Replace("-", " ")}");

            // 8. 응답 분석 
            if (readBuffer.Length < 8)
                throw new Exception("수신된 응답이 너무 짧습니다.");

            if (readBuffer[0] != DATA_HEADER_1 || readBuffer[1] != DATA_HEADER_2)
                throw new Exception("수신된 응답 헤더 불일치");

            if (readBuffer[4 + 0] == moduleId &&
                readBuffer[4 + 1] == subId &&
                readBuffer[4 + 2] == queryFrame[2])
            {
                byte ackCode = readBuffer[4 + 6];
                if (ackCode == 0x00) // ACK
                {
                    ushort responsePayloadLength = (ushort)((readBuffer[4 + 4] << 8) | readBuffer[4 + 5]);
                    byte[] responsePayload = new byte[responsePayloadLength - 1];
                    if (responsePayload.Length > 0)
                    {
                        Array.Copy(readBuffer, 4 + 7, responsePayload, 0, responsePayload.Length);
                    }
                    return responsePayload;
                }
                else { throw new Exception($"명령 실패, NACK 또는 오류 코드 수신: {ackCode:X2}"); }
            }
            else { throw new Exception("수신된 응답이 보낸 요청과 일치하지 않습니다."); }
        }

        // ----- Whitebeet 기능 구현 -----

        private string GetVersionFromDevice()
        {
            if (!isDeviceOpen) return "N/A";
            try
            {
                byte[] responsePayload = SendQuery(0x10, 0x41, null);
                int versionLength = (responsePayload[0] << 8) | responsePayload[1];
                string version = System.Text.Encoding.UTF8.GetString(responsePayload, 2, versionLength);
                return version;
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
                SendQuery(0x29, 0x40, payload);
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
                SendQuery(0x29, 0x42, null);
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
                SendQuery(0x29, 0x46, payload);
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
                SendQuery(0x28, 0x4B, payload);
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
                SendQuery(0x28, 0x42, payload);
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
            if (!isDeviceOpen) return -1.0; // 오류 값 반환
            try
            {
                byte[] responsePayload = SendQuery(0x29, 0x45, null);

                if (responsePayload.Length == 2) // 듀티 사이클 값 (2바이트) 확인
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

        // --- 메시지 수신 로직 (WB_SPI.cs 로직 기반) ---
        public (int, byte[]) V2gEvReceiveRequest()
        {
            if (!isDeviceOpen) throw new InvalidOperationException("Device is not open.");
            Log("V2G 요청 수신 시도...");

            uint gpioStatus;
            if (!ch341Device.GetInput(out gpioStatus))
                throw new Exception("GPIO 상태 읽기 실패 (GetInput)");

            if ((gpioStatus & MASK_TX_Pending) > 0)
            {
                Log("TX_Pending 감지됨. 프레임 수신 시작...");

                byte[] sizeTxFrame = { SIZE_HEADER_1, SIZE_HEADER_2, 0x00, 0x00 };
                if (!SendSpi(ref sizeTxFrame)) throw new Exception("SPI 크기 요청 프레임 전송 실패");

                ushort size = (ushort)((sizeTxFrame[2] << 8) | sizeTxFrame[3]);
                if (size == 0) { throw new TimeoutException("수신할 데이터 크기가 0입니다."); }

                byte[] dataTxFrame = new byte[size + 4];
                dataTxFrame[0] = DATA_HEADER_1;
                dataTxFrame[1] = DATA_HEADER_2;

                if (!SendSpi(ref dataTxFrame)) throw new Exception("SPI 데이터 요청 프레임 전송 실패");

                Log($"[RX-V2G] << {BitConverter.ToString(dataTxFrame).Replace("-", " ")}");
                byte[] readBuffer = dataTxFrame;

                if (readBuffer.Length > 4 && readBuffer[4] == 0xAA)
                {
                    byte receivedSubId = readBuffer[4 + 2];
                    int payloadLength = (readBuffer[4 + 4] << 8) | readBuffer[4 + 5];
                    byte[] payload = new byte[payloadLength];

                    if (payloadLength > 0 && readBuffer.Length >= 4 + 6 + payloadLength)
                    {
                        Array.Copy(readBuffer, 4 + 6, payload, 0, payloadLength);
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

            throw new TimeoutException("No valid frame received (TX_Pending not high).");
        }

        public void SlacStartMatching()
        {
            Log("SLAC 매칭 시작 요청...");
            if (!isDeviceOpen) return;
            try
            {
                SendQuery(0x28, 0x44, null);
                Log("SLAC 매칭 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] SlacStartMatching 실패: {ex.Message}");
                throw;
            }
        }

        public bool SlacMatched()
        {
            Log("SLAC 매칭 결과 대기 중... (최대 60초)");
            if (!isDeviceOpen) return false;

            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < 60)
            {
                try
                {
                    (int subId, byte[] payload) = V2gEvReceiveRequest();
                    if (subId == 0x80) // slac_sub_success
                    {
                        Log("SLAC 매칭 성공 메시지(0x80) 수신.");
                        return true;
                    }
                    else if (subId == 0x81) // slac_sub_failed
                    {
                        Log("SLAC 매칭 실패 메시지(0x81) 수신.");
                        return false;
                    }
                    else
                    {
                        Log($"[Warning] SLAC 매칭 대기 중 예상치 못한 메시지 수신: {subId:X2}");
                    }
                }
                catch (TimeoutException)
                {
                    Log("...매칭 대기 중...");
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    Log($"[Error] SlacMatched 중 오류: {ex.Message}");
                    return false;
                }
            }

            Log("SLAC 매칭 시간 초과.");
            return false;
        }

        // --- V2G 기능 구현 (Ev.py 기반) ---
        public void V2gSetMode(int mode)
        {
            Log($"V2G 모드 설정 요청: {mode}");
            if (!isDeviceOpen) return;
            try
            {
                byte[] payload = new byte[] { (byte)mode };
                SendQuery(0x27, 0x40, payload);
                Log("V2G 모드 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gSetMode 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gEvSetConfiguration(Dictionary<string, object> config)
        {
            Log("[V2G] EV 구성 설정 시도...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");

            try
            {
                var payload = new List<byte>();

                if (config["evid"] is byte[] evidBytes)
                    payload.AddRange(evidBytes);
                else throw new ArgumentException("config 'evid' must be byte[].");

                int protocolCount = Convert.ToInt32(config["protocol_count"]);
                payload.Add((byte)protocolCount);
                if (config["protocols"] is List<int> protocols)
                {
                    foreach (var protocol in protocols)
                        payload.Add((byte)protocol);
                }
                else throw new ArgumentException("config 'protocols' must be List<int>.");

                int paymentCount = Convert.ToInt32(config["payment_method_count"]);
                payload.Add((byte)paymentCount);
                if (config["payment_method"] is List<int> methods)
                {
                    foreach (var method in methods)
                        payload.Add((byte)method);
                }
                else throw new ArgumentException("config 'payment_method' must be List<int>.");

                int modeCount = Convert.ToInt32(config["energy_transfer_mode_count"]);
                payload.Add((byte)modeCount);
                if (config["energy_transfer_mode"] is List<int> modes)
                {
                    foreach (var mode in modes)
                        payload.Add((byte)mode);
                }
                else throw new ArgumentException("config 'energy_transfer_mode' must be List<int>.");

                payload.AddRange(ValueToExponential(config["battery_capacity"]));

                SendQuery(0x27, 0xA0, payload.ToArray());
                Log("[V2G] EV 구성 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gEvSetConfiguration 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gSetDCChargingParameters(Dictionary<string, object> parameters)
        {
            Log("[V2G] DC 충전 파라미터 설정 시도...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");

            try
            {
                var payload = new List<byte>();

                payload.AddRange(ValueToExponential(parameters["min_voltage"]));
                payload.AddRange(ValueToExponential(parameters["min_current"]));
                payload.AddRange(ValueToExponential(parameters["min_power"]));
                payload.AddRange(ValueToExponential(parameters["max_voltage"]));
                payload.AddRange(ValueToExponential(parameters["max_current"]));
                payload.AddRange(ValueToExponential(parameters["max_power"]));
                payload.Add((byte)Convert.ToInt32(parameters["soc"]));
                payload.Add((byte)Convert.ToInt32(parameters["status"]));
                payload.AddRange(ValueToExponential(parameters["target_voltage"]));
                payload.AddRange(ValueToExponential(parameters["target_current"]));
                payload.Add((byte)Convert.ToInt32(parameters["full_soc"]));
                payload.Add((byte)Convert.ToInt32(parameters["bulk_soc"]));
                payload.AddRange(ValueToExponential(parameters["energy_request"]));
                payload.AddRange(IntTo4BytesBigEndian(parameters["departure_time"]));

                SendQuery(0x27, 0xA2, payload.ToArray());
                Log("[V2G] DC 충전 파라미터 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gSetDCChargingParameters 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gSetACChargingParameters(Dictionary<string, object> parameters)
        {
            Log("[V2G] AC 충전 파라미터 설정 시도...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");

            try
            {
                var payload = new List<byte>();

                payload.AddRange(ValueToExponential(parameters["min_voltage"]));
                payload.AddRange(ValueToExponential(parameters["min_current"]));
                payload.AddRange(ValueToExponential(parameters["min_power"]));
                payload.AddRange(ValueToExponential(parameters["max_voltage"]));
                payload.AddRange(ValueToExponential(parameters["max_current"]));
                payload.AddRange(ValueToExponential(parameters["max_power"]));
                payload.AddRange(ValueToExponential(parameters["energy_request"]));
                payload.AddRange(IntTo4BytesBigEndian(parameters["departure_time"]));

                SendQuery(0x27, 0xA5, payload.ToArray());
                Log("[V2G] AC 충전 파라미터 설정 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gSetACChargingParameters 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gStartSession()
        {
            Log("[V2G] 세션 시작 요청...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            try
            {
                SendQuery(0x27, 0xA9, null);
                Log("[V2G] 세션 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gStartSession 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gStopSession()
        {
            Log("[V2G] 세션 중지 요청...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            try
            {
                SendQuery(0x27, 0xAE, null);
                Log("[V2G] 세션 중지 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gStopSession 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gStartCableCheck()
        {
            Log("[V2G] 케이블 체크 시작 요청...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            try
            {
                SendQuery(0x27, 0xAA, null);
                Log("[V2G] 케이블 체크 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gStartCableCheck 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gStartPreCharging()
        {
            Log("[V2G] 사전 충전 시작 요청...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            try
            {
                SendQuery(0x27, 0xAB, null);
                Log("[V2G] 사전 충전 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gStartPreCharging 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gStartCharging()
        {
            Log("[V2G] 충전 시작 요청...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            try
            {
                SendQuery(0x27, 0xAC, null);
                Log("[V2G] 충전 시작 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gStartCharging 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gStopCharging(bool reneg)
        {
            Log($"[V2G] 충전 중지 요청 (Renegotiation: {reneg})...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");
            try
            {
                byte[] payload = new byte[] { (byte)(reneg ? 1 : 0) };
                SendQuery(0x27, 0xAD, payload);
                Log("[V2G] 충전 중지 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gStopCharging 실패: {ex.Message}");
                throw;
            }
        }

        public void V2gUpdateDCChargingParameters(Dictionary<string, object> parameters)
        {
            Log("[V2G] DC 충전 파라미터 업데이트 시도...");
            if (!isDeviceOpen) throw new InvalidOperationException("장치가 열려있지 않습니다.");

            try
            {
                var payload = new List<byte>();

                payload.AddRange(ValueToExponential(parameters["min_voltage"]));
                payload.AddRange(ValueToExponential(parameters["min_current"]));
                payload.AddRange(ValueToExponential(parameters["min_power"]));
                payload.AddRange(ValueToExponential(parameters["max_voltage"]));
                payload.AddRange(ValueToExponential(parameters["max_current"]));
                payload.AddRange(ValueToExponential(parameters["max_power"]));
                payload.Add((byte)Convert.ToInt32(parameters["soc"]));
                payload.Add((byte)Convert.ToInt32(parameters["status"]));
                payload.AddRange(ValueToExponential(parameters["target_voltage"]));
                payload.AddRange(ValueToExponential(parameters["target_current"]));

                SendQuery(0x27, 0xA3, payload.ToArray());
                Log("[V2G] DC 충전 파라미터 업데이트 완료 (ACK 수신).");
            }
            catch (Exception ex)
            {
                Log($"[Error] V2gUpdateDCChargingParameters 실패: {ex.Message}");
                throw;
            }
        }

        // --- 페이로드 파서 헬퍼 (Python: payloadReader...) ---
        private byte[] payloadBuffer;
        private int payloadReadIndex;
        private int payloadTotalLength;

        private void PayloadReaderInitialize(byte[] data)
        {
            this.payloadBuffer = data;
            this.payloadTotalLength = data.Length;
            this.payloadReadIndex = 0;
            Log($"[Parser] 초기화: {payloadTotalLength} 바이트.");
        }

        private int PayloadReaderReadInt(int numBytes)
        {
            if (payloadReadIndex + numBytes > payloadTotalLength)
                throw new Exception($"[Parser Error] PayloadReaderReadInt: 버퍼 오버플로우. {numBytes}바이트 읽기 시도.");
            if (numBytes > 4)
                throw new ArgumentException("PayloadReaderReadInt는 4바이트 이하만 지원합니다.");
            long value = 0;
            for (int i = 0; i < numBytes; i++)
            {
                value = (value << 8) | payloadBuffer[payloadReadIndex + i];
            }
            payloadReadIndex += numBytes;
            return (int)value;
        }

        private double PayloadReaderReadExponential()
        {
            if (payloadReadIndex + 3 > payloadTotalLength)
                throw new Exception("[Parser Error] PayloadReaderReadExponential: 버퍼 오버플로우.");

            byte[] baseBytes = { payloadBuffer[payloadReadIndex], payloadBuffer[payloadReadIndex + 1] };
            if (BitConverter.IsLittleEndian)
                Array.Reverse(baseBytes);
            short baseValue = BitConverter.ToInt16(baseBytes, 0);

            sbyte exponent = (sbyte)payloadBuffer[payloadReadIndex + 2];
            double value = baseValue * Math.Pow(10, exponent);
            payloadReadIndex += 3;
            return value;
        }

        private byte[] PayloadReaderReadBytes(int numBytes)
        {
            if (payloadReadIndex + numBytes > payloadTotalLength)
                throw new Exception($"[Parser Error] PayloadReaderReadBytes: 버퍼 오버플로우. {numBytes}바이트 읽기 시도.");

            byte[] bytes = new byte[numBytes];
            Array.Copy(payloadBuffer, payloadReadIndex, bytes, 0, numBytes);
            payloadReadIndex += numBytes;
            return bytes;
        }

        private void PayloadReaderFinalize()
        {
            if (payloadReadIndex != payloadTotalLength)
            {
                Log($"[Parser Warning] 모든 페이로드를 읽지 않았습니다. (읽은 값: {payloadReadIndex}, 전체: {payloadTotalLength})");
            }
            else
            {
                Log($"[Parser] {payloadTotalLength} 바이트 파싱 완료.");
            }
        }


        // --- 메시지 파서들 ---
        public Dictionary<string, object> V2gEvParseSessionStarted(byte[] data)
        {
            Log("[Parser] V2gEvParseSessionStarted (0xC0) 파싱 시작...");
            var message = new Dictionary<string, object>();
            PayloadReaderInitialize(data);
            try
            {
                message["protocol"] = PayloadReaderReadInt(1);
                message["session_id"] = PayloadReaderReadBytes(8);
                int evseIdLength = PayloadReaderReadInt(1);
                message["evse_id"] = PayloadReaderReadBytes(evseIdLength);
                message["payment_method"] = PayloadReaderReadInt(1);
                message["energy_transfer_mode"] = PayloadReaderReadInt(1);
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseSessionStarted 파싱 성공.");
                return message;
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseSessionStarted 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public Dictionary<string, object> V2gEvParseDCChargeParametersChanged(byte[] data)
        {
            Log("[Parser] V2gEvParseDCChargeParametersChanged (0xC1) 파싱 시작...");
            var message = new Dictionary<string, object>();
            PayloadReaderInitialize(data);
            try
            {
                message["evse_min_voltage"] = PayloadReaderReadExponential();
                message["evse_min_current"] = PayloadReaderReadExponential();
                message["evse_min_power"] = PayloadReaderReadExponential();
                message["evse_max_voltage"] = PayloadReaderReadExponential();
                message["evse_max_current"] = PayloadReaderReadExponential();
                message["evse_max_power"] = PayloadReaderReadExponential();
                message["evse_present_voltage"] = PayloadReaderReadExponential();
                message["evse_present_current"] = PayloadReaderReadExponential();
                message["evse_status"] = PayloadReaderReadInt(1);
                if (PayloadReaderReadInt(1) != 0)
                {
                    message["evse_isolation_status"] = PayloadReaderReadInt(1);
                }
                message["evse_voltage_limit_achieved"] = PayloadReaderReadInt(1);
                message["evse_current_limit_achieved"] = PayloadReaderReadInt(1);
                message["evse_power_limit_achieved"] = PayloadReaderReadInt(1);
                message["evse_peak_current_ripple"] = PayloadReaderReadExponential();
                if (PayloadReaderReadInt(1) != 0)
                {
                    message["evse_current_regulation_tolerance"] = PayloadReaderReadExponential();
                }
                if (PayloadReaderReadInt(1) != 0)
                {
                    message["evse_energy_to_be_delivered"] = PayloadReaderReadExponential();
                }
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseDCChargeParametersChanged 파싱 성공.");
                return message;
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseDCChargeParametersChanged 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public Dictionary<string, object> V2gEvParseACChargeParametersChanged(byte[] data)
        {
            Log("[Parser] V2gEvParseACChargeParametersChanged (0xC2) 파싱 시작...");
            var message = new Dictionary<string, object>();
            PayloadReaderInitialize(data);
            try
            {
                message["nominal_voltage"] = PayloadReaderReadExponential();
                message["max_current"] = PayloadReaderReadExponential();
                message["rcd"] = (PayloadReaderReadInt(1) == 1);
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseACChargeParametersChanged 파싱 성공.");
                return message;
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseACChargeParametersChanged 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public ChargingProfile V2gEvParseScheduleReceived(byte[] data)
        {
            Log("[Parser] V2gEvParseScheduleReceived (0xC3) 파싱 시작...");
            var profile = new ChargingProfile();
            PayloadReaderInitialize(data);

            try
            {
                profile.TupleCount = PayloadReaderReadInt(1);
                profile.TupleId = PayloadReaderReadInt(2);
                profile.EntriesCount = PayloadReaderReadInt(2);

                for (int i = 0; i < profile.EntriesCount; i++)
                {
                    var entry = new ChargingProfileEntry
                    {
                        Start = PayloadReaderReadInt(4),
                        Interval = PayloadReaderReadInt(4),
                        Power = PayloadReaderReadExponential()
                    };
                    profile.Entries.Add(entry);
                }

                PayloadReaderFinalize();
                Log($"[Parser] V2gEvParseScheduleReceived 파싱 성공. {profile.EntriesCount}개 항목 로드.");
                return profile;
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseScheduleReceived 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public Dictionary<string, object> V2gEvParseNotificationReceived(byte[] data)
        {
            Log("[Parser] V2gEvParseNotificationReceived (0xCC) 파싱 시작...");
            var message = new Dictionary<string, object>();
            PayloadReaderInitialize(data);
            try
            {
                message["type"] = PayloadReaderReadInt(1);
                message["max_delay"] = PayloadReaderReadInt(2);
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseNotificationReceived 파싱 성공.");
                return message;
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseNotificationReceived 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        // --- Ev.py에서 사용되는 나머지 빈 파서들 (C4~CB) ---
        public void V2gEvParseCableCheckReady(byte[] data)
        {
            Log("[Parser] V2gEvParseCableCheckReady (0xC4) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseCableCheckReady 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseCableCheckReady 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParseCableCheckFinished(byte[] data)
        {
            Log("[Parser] V2gEvParseCableCheckFinished (0xC5) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseCableCheckFinished 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseCableCheckFinished 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParsePreChargingReady(byte[] data)
        {
            Log("[Parser] V2gEvParsePreChargingReady (0xC6) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParsePreChargingReady 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParsePreChargingReady 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParseChargingReady(byte[] data)
        {
            Log("[Parser] V2gEvParseChargingReady (0xC7) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseChargingReady 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseChargingReady 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParseChargingStarted(byte[] data)
        {
            Log("[Parser] V2gEvParseChargingStarted (0xC8) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseChargingStarted 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseChargingStarted 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParseChargingStopped(byte[] data)
        {
            Log("[Parser] V2gEvParseChargingStopped (0xC9) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseChargingStopped 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseChargingStopped 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParsePostChargingReady(byte[] data)
        {
            Log("[Parser] V2gEvParsePostChargingReady (0xCA) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParsePostChargingReady 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParsePostChargingReady 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public void V2gEvParseSessionStopped(byte[] data)
        {
            Log("[Parser] V2gEvParseSessionStopped (0xCB) 파싱 시작...");
            PayloadReaderInitialize(data);
            try
            {
                PayloadReaderFinalize();
                Log("[Parser] V2gEvParseSessionStopped 파싱 성공.");
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseSessionStopped 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

        public Dictionary<string, object> V2gEvParseSessionError(byte[] data)
        {
            Log("[Parser] V2gEvParseSessionError (0xCD) 파싱 시작...");
            var message = new Dictionary<string, object>();
            PayloadReaderInitialize(data);

            try
            {
                message["code"] = PayloadReaderReadInt(1);

                PayloadReaderFinalize();
                Log("[Parser] VSuchagEvParseSessionError 파싱 성공.");
                return message;
            }
            catch (Exception ex)
            {
                Log($"[Parser Error] V2gEvParseSessionError 파싱 중 오류: {ex.Message}");
                throw;
            }
        }

    }
}