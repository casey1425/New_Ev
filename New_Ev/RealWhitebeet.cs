using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CH341;

namespace New_Ev
{
    public class RealWhitebeet : IDisposable
    {
        private CH341A ch341Device;
        private bool isDeviceOpen = false;
        private byte currentRequestId = 0;

        public event Action<string> OnLog;
        public string Version => GetVersionFromDevice();

        private const byte DATA_HEADER_1 = 0x55;
        private const byte DATA_HEADER_2 = 0x55;
        private const byte SIZE_HEADER_1 = 0xAA;
        private const byte SIZE_HEADER_2 = 0xAA;

        // [유지] 장치를 켜는 올바른 신호
        private const int CHIP_SELECT = 0x00;

        private const uint MASK_TX_Pending = 0x0000_0100;
        private const uint MASK_RX_Ready = 0x0000_0200;

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

                    // [핵심 수정] 0x82(AA오류)와 전압은 같지만 타이밍이 반대인 0x83(Mode 3) 사용
                    // 0x80은 GPIO 먹통을 유발했으므로 사용하지 않습니다.
                    if (ch341Device.SetStream(0x83))
                    {
                        Log("[RealWhitebeet] SPI 스트림 모드(0x83) 설정 성공!");
                        ch341Device.SetDelaymS(0); // 기본 속도
                        Thread.Sleep(100); // 초기화 대기
                    }
                    else
                    {
                        throw new Exception("Failed to set CH341 stream mode (0x83).");
                    }
                }
                else
                {
                    isDeviceOpen = false;
                    Log("[RealWhitebeet Error] CH341 장치를 열 수 없습니다!");
                    throw new Exception("CH341 Device not detected.");
                }
            }
            catch (Exception ex)
            {
                Log($"[RealWhitebeet Critical Error] 초기화 중 예외: {ex.Message}");
                isDeviceOpen = false;
                throw;
            }
        }

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

        private void Log(string message) => OnLog?.Invoke(message);

        // --- SPI 전송 ---
        private bool SendSpi(ref byte[] ioBuffer)
        {
            if (!isDeviceOpen) throw new InvalidOperationException("장치 미연결");

            ch341Device.FlushBuffer();

            // [유지] 수정된 CH341.cs에 맞게 (0, CS, ...) 호출
            return ch341Device.StreamSPI4(0, CHIP_SELECT, ref ioBuffer);
        }

        // --- 나머지 로직 ---
        private byte GenerateNextRequestId() { currentRequestId++; if (currentRequestId == 0xFF) currentRequestId = 0; return currentRequestId; }

        private byte[] BuildQueryFrame(byte moduleId, byte subId, byte[] payload)
        {
            int payloadLength = payload?.Length ?? 0;
            byte[] queryFrame = new byte[3 + payloadLength];
            queryFrame[0] = moduleId;
            queryFrame[1] = subId;
            queryFrame[2] = GenerateNextRequestId();
            if (payloadLength > 0) Array.Copy(payload, 0, queryFrame, 3, payloadLength);
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

        private bool IsPinHigh(uint pinMask)
        {
            uint gpioStatus;
            if (ch341Device.GetInput(out gpioStatus)) return (gpioStatus & pinMask) > 0;
            throw new Exception("GPIO 상태 읽기 실패");
        }

        private byte[] SendQuery(byte moduleId, byte subId, byte[] payload)
        {
            if (!isDeviceOpen) throw new InvalidOperationException("장치 미연결");

            byte[] queryFrame = BuildQueryFrame(moduleId, subId, payload);
            byte[] txBuffer = BuildDataFrame(queryFrame);

            // 1. RX Ready 대기
            Log("응답 수신 준비 대기 중... (RX_Ready)");
            DateTime startTime = DateTime.Now;
            bool rxReady = false;
            while ((DateTime.Now - startTime).TotalMilliseconds < 500)
            {
                if (IsPinHigh(MASK_RX_Ready)) { rxReady = true; break; }
                Thread.Sleep(10);
            }
            if (!rxReady) throw new TimeoutException("RX_Ready 신호 없음");
            ch341Device.SetStream(0x83);
            Thread.Sleep(20);

            // 2. 명령 전송
            Log($"[TX] >> {BitConverter.ToString(txBuffer).Replace("-", " ")}");
            if (!SendSpi(ref txBuffer)) throw new Exception("SendSpi 통신 실패");
            Log($"[RX-ECHO] << {BitConverter.ToString(txBuffer).Replace("-", " ")}");

            // 3. TX Pending 대기
            Log("응답 대기 중... (TX_Pending)");
            startTime = DateTime.Now;
            bool txPending = false;
            while ((DateTime.Now - startTime).TotalMilliseconds < 500)
            {
                if (IsPinHigh(MASK_TX_Pending)) { txPending = true; break; }
                Thread.Sleep(10);
            }
            if (!txPending) throw new TimeoutException("TX_Pending 신호 없음");

            // 4. 크기 요청
            byte[] sizeTxFrame = { SIZE_HEADER_1, SIZE_HEADER_2, 0x00, 0x00 };
            if (!SendSpi(ref sizeTxFrame)) throw new Exception("SPI 크기 요청 실패");

            ushort size = (ushort)((sizeTxFrame[2] << 8) | sizeTxFrame[3]);
            Log($"수신할 데이터 크기: {size}");

            if (size == 0)
            {
                Log("데이터 크기 0 수신 (ACK).");
                return Array.Empty<byte>();
            }

            if (size == 65535)
            {
                Log("[Error] 데이터 크기 65535 (0xFFFF). 장치 응답 없음.");
                throw new Exception("Device not responding (MISO High).");
            }

            if (size > 4096)
            {
                Log($"[Warning] 비정상적인 데이터 크기({size}) 감지됨.");
                throw new Exception($"Invalid data size received: {size}");
            }

            // 5. 데이터 요청
            byte[] dataTxFrame = new byte[size + 4];
            dataTxFrame[0] = DATA_HEADER_1;
            dataTxFrame[1] = DATA_HEADER_2;
            if (!SendSpi(ref dataTxFrame)) throw new Exception("SPI 데이터 요청 실패");

            byte[] readBuffer = dataTxFrame;
            Log($"[RX-DATA] << {BitConverter.ToString(readBuffer).Replace("-", " ")}");

            // 6. 응답 검증
            if (readBuffer.Length < 8) throw new Exception("응답 너무 짧음");
            if (readBuffer[0] != DATA_HEADER_1 || readBuffer[1] != DATA_HEADER_2) throw new Exception("헤더 불일치");

            if (readBuffer[4 + 0] == moduleId && readBuffer[4 + 1] == subId && readBuffer[4 + 2] == queryFrame[2])
            {
                byte ackCode = readBuffer[4 + 6];
                if (ackCode == 0x00)
                {
                    ushort payloadLen = (ushort)((readBuffer[4 + 4] << 8) | readBuffer[4 + 5]);
                    byte[] responsePayload = new byte[payloadLen - 1];
                    if (payloadLen > 0) Array.Copy(readBuffer, 4 + 7, responsePayload, 0, responsePayload.Length);
                    return responsePayload;
                }
                else throw new Exception($"NACK/Error: {ackCode:X2}");
            }
            else throw new Exception("응답 ID 불일치");
        }

        // --- 기능 구현 ---
        private string GetVersionFromDevice()
        {
            if (!isDeviceOpen) return "N/A";
            try
            {
                byte[] res = SendQuery(0x10, 0x41, null);
                if (res.Length < 2) return "Unknown";
                int len = (res[0] << 8) | res[1];
                return System.Text.Encoding.UTF8.GetString(res, 2, len);
            }
            catch (Exception ex) { Log($"[Error] 버전 확인 실패: {ex.Message}"); return "Error"; }
        }

        private byte[] ValueToExponential(object val)
        {
            long v = Convert.ToInt64(val); sbyte e = 0;
            while (v != 0 && v % 10 == 0 && e < 3) { e++; v /= 10; }
            byte[] b = BitConverter.GetBytes((short)v);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return new byte[] { b[0], b[1], (byte)e };
        }

        private byte[] IntTo4BytesBigEndian(object val)
        {
            byte[] b = BitConverter.GetBytes(Convert.ToUInt32(val));
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return b;
        }

        // --- API ---
        public void ControlPilotSetMode(int mode) { SendQuery(0x29, 0x40, new byte[] { (byte)mode }); Log("CP 모드 설정 완료."); }
        public void ControlPilotStart() { SendQuery(0x29, 0x42, null); Log("CP 시작 완료."); }
        public void ControlPilotSetResistorValue(int value) { SendQuery(0x29, 0x46, new byte[] { (byte)value }); Log("CP 저항 설정 완료."); }
        public void SlacSetValidationConfiguration(int config) { SendQuery(0x28, 0x4B, new byte[] { (byte)config }); Log("SLAC 검증 설정 완료."); }
        public void SlacStart(int mode) { SendQuery(0x28, 0x42, new byte[] { (byte)mode }); Log("SLAC 시작 완료."); }
        public double ControlPilotGetDutyCycle() { try { byte[] res = SendQuery(0x29, 0x45, null); if (res.Length == 2) return ((res[0] << 8) | res[1]) / 10.0; } catch { } return -1.0; }

        public (int, byte[]) V2gEvReceiveRequest()
        {
            if (!isDeviceOpen) throw new Exception("Device closed");
            uint gpio; ch341Device.GetInput(out gpio);

            if ((gpio & MASK_TX_Pending) > 0)
            {
                byte[] sz = { SIZE_HEADER_1, SIZE_HEADER_2, 0, 0 };
                SendSpi(ref sz);
                ushort s = (ushort)((sz[2] << 8) | sz[3]);
                if (s == 0) throw new TimeoutException("Size 0");

                byte[] d = new byte[s + 4]; d[0] = DATA_HEADER_1; d[1] = DATA_HEADER_2;
                SendSpi(ref d);

                if (d.Length > 4 && d[4] == 0xAA)
                {
                    int l = (d[8] << 8) | d[9];
                    byte[] p = new byte[l];
                    if (l > 0) Array.Copy(d, 10, p, 0, l);
                    return (d[6], p);
                }
            }
            throw new TimeoutException("No Msg");
        }

        public void SlacStartMatching() { SendQuery(0x28, 0x44, null); Log("SLAC 매칭 시작."); }
        public bool SlacMatched()
        {
            DateTime s = DateTime.Now;
            while ((DateTime.Now - s).TotalSeconds < 60)
            {
                try
                {
                    var r = V2gEvReceiveRequest();
                    if (r.Item1 == 0x80) { Log("SLAC 매칭 성공!"); return true; }
                    if (r.Item1 == 0x81) return false;
                }
                catch (TimeoutException) { Thread.Sleep(200); }
                catch { return false; }
            }
            return false;
        }

        public void V2gSetMode(int m) => SendQuery(0x27, 0x40, new byte[] { (byte)m });
        public void V2gStartSession() => SendQuery(0x27, 0xA9, null);
        public void V2gStopSession() => SendQuery(0x27, 0xAE, null);
        public void V2gStartCableCheck() => SendQuery(0x27, 0xAA, null);
        public void V2gStartPreCharging() => SendQuery(0x27, 0xAB, null);
        public void V2gStartCharging() => SendQuery(0x27, 0xAC, null);
        public void V2gStopCharging(bool r) => SendQuery(0x27, 0xAD, new byte[] { (byte)(r ? 1 : 0) });

        public void V2gEvSetConfiguration(Dictionary<string, object> c)
        {
            var p = new List<byte>();
            p.AddRange((byte[])c["evid"]); p.Add((byte)(int)c["protocol_count"]);
            foreach (int i in (List<int>)c["protocols"]) p.Add((byte)i);
            p.Add((byte)(int)c["payment_method_count"]); foreach (int i in (List<int>)c["payment_method"]) p.Add((byte)i);
            p.Add((byte)(int)c["energy_transfer_mode_count"]); foreach (int i in (List<int>)c["energy_transfer_mode"]) p.Add((byte)i);
            p.AddRange(ValueToExponential(c["battery_capacity"]));
            SendQuery(0x27, 0xA0, p.ToArray());
            Log("EV 설정 완료.");
        }
        public void V2gSetDCChargingParameters(Dictionary<string, object> p)
        {
            var b = new List<byte>();
            b.AddRange(ValueToExponential(p["min_voltage"])); b.AddRange(ValueToExponential(p["min_current"])); b.AddRange(ValueToExponential(p["min_power"]));
            b.AddRange(ValueToExponential(p["max_voltage"])); b.AddRange(ValueToExponential(p["max_current"])); b.AddRange(ValueToExponential(p["max_power"]));
            b.Add((byte)(int)p["soc"]); b.Add((byte)(int)p["status"]); b.AddRange(ValueToExponential(p["target_voltage"])); b.AddRange(ValueToExponential(p["target_current"]));
            b.Add((byte)(int)p["full_soc"]); b.Add((byte)(int)p["bulk_soc"]); b.AddRange(ValueToExponential(p["energy_request"])); b.AddRange(IntTo4BytesBigEndian(p["departure_time"]));
            SendQuery(0x27, 0xA2, b.ToArray());
            Log("DC 파라미터 설정 완료.");
        }
        public void V2gSetACChargingParameters(Dictionary<string, object> p)
        {
            var b = new List<byte>();
            b.AddRange(ValueToExponential(p["min_voltage"])); b.AddRange(ValueToExponential(p["min_current"])); b.AddRange(ValueToExponential(p["min_power"]));
            b.AddRange(ValueToExponential(p["max_voltage"])); b.AddRange(ValueToExponential(p["max_current"])); b.AddRange(ValueToExponential(p["max_power"]));
            b.AddRange(ValueToExponential(p["energy_request"])); b.AddRange(IntTo4BytesBigEndian(p["departure_time"]));
            SendQuery(0x27, 0xA5, b.ToArray());
            Log("AC 파라미터 설정 완료.");
        }
        public void V2gUpdateDCChargingParameters(Dictionary<string, object> p)
        {
            var b = new List<byte>();
            b.AddRange(ValueToExponential(p["min_voltage"])); b.AddRange(ValueToExponential(p["min_current"])); b.AddRange(ValueToExponential(p["min_power"]));
            b.AddRange(ValueToExponential(p["max_voltage"])); b.AddRange(ValueToExponential(p["max_current"])); b.AddRange(ValueToExponential(p["max_power"]));
            b.Add((byte)(int)p["soc"]); b.Add((byte)(int)p["status"]); b.AddRange(ValueToExponential(p["target_voltage"])); b.AddRange(ValueToExponential(p["target_current"]));
            SendQuery(0x27, 0xA3, b.ToArray());
            Log("[V2G] DC 파라미터 업데이트 완료.");
        }

        // --- Parsers ---
        private byte[] pb; private int pri;
        private void PayloadReaderInitialize(byte[] d) { pb = d; pri = 0; }
        private int PayloadReaderReadInt(int n) { long v = 0; for (int i = 0; i < n; i++) v = (v << 8) | pb[pri + i]; pri += n; return (int)v; }
        private double PayloadReaderReadExponential()
        {
            byte[] b = { pb[pri], pb[pri + 1] }; if (BitConverter.IsLittleEndian) Array.Reverse(b);
            short v = BitConverter.ToInt16(b, 0); sbyte e = (sbyte)pb[pri + 2]; pri += 3; return v * Math.Pow(10, e);
        }
        private byte[] PayloadReaderReadBytes(int n) { byte[] b = new byte[n]; Array.Copy(pb, pri, b, 0, n); pri += n; return b; }
        private void PayloadReaderFinalize() { }

        public Dictionary<string, object> V2gEvParseSessionStarted(byte[] d)
        {
            PayloadReaderInitialize(d); var m = new Dictionary<string, object>();
            m["protocol"] = PayloadReaderReadInt(1); m["session_id"] = PayloadReaderReadBytes(8);
            m["evse_id"] = PayloadReaderReadBytes(PayloadReaderReadInt(1));
            m["payment_method"] = PayloadReaderReadInt(1); m["energy_transfer_mode"] = PayloadReaderReadInt(1);
            return m;
        }
        public Dictionary<string, object> V2gEvParseDCChargeParametersChanged(byte[] d)
        {
            PayloadReaderInitialize(d); var m = new Dictionary<string, object>();
            m["evse_min_voltage"] = PayloadReaderReadExponential(); m["evse_min_current"] = PayloadReaderReadExponential(); m["evse_min_power"] = PayloadReaderReadExponential();
            m["evse_max_voltage"] = PayloadReaderReadExponential(); m["evse_max_current"] = PayloadReaderReadExponential(); m["evse_max_power"] = PayloadReaderReadExponential();
            m["evse_present_voltage"] = PayloadReaderReadExponential(); m["evse_present_current"] = PayloadReaderReadExponential(); m["evse_status"] = PayloadReaderReadInt(1);
            return m;
        }
        public Dictionary<string, object> V2gEvParseACChargeParametersChanged(byte[] d)
        {
            PayloadReaderInitialize(d);
            return new Dictionary<string, object> { ["nominal_voltage"] = PayloadReaderReadExponential(), ["max_current"] = PayloadReaderReadExponential(), ["rcd"] = PayloadReaderReadInt(1) == 1 };
        }
        public ChargingProfile V2gEvParseScheduleReceived(byte[] d)
        {
            PayloadReaderInitialize(d); var p = new ChargingProfile();
            p.TupleCount = PayloadReaderReadInt(1); p.TupleId = PayloadReaderReadInt(2); p.EntriesCount = PayloadReaderReadInt(2);
            for (int i = 0; i < p.EntriesCount; i++) p.Entries.Add(new ChargingProfileEntry { Start = PayloadReaderReadInt(4), Interval = PayloadReaderReadInt(4), Power = PayloadReaderReadExponential() });
            return p;
        }
        public Dictionary<string, object> V2gEvParseNotificationReceived(byte[] d)
        {
            PayloadReaderInitialize(d); return new Dictionary<string, object> { ["type"] = PayloadReaderReadInt(1), ["max_delay"] = PayloadReaderReadInt(2) };
        }
        public Dictionary<string, object> V2gEvParseSessionError(byte[] d)
        {
            PayloadReaderInitialize(d); return new Dictionary<string, object> { ["code"] = PayloadReaderReadInt(1) };
        }
        public void V2gEvParseCableCheckReady(byte[] d) { }
        public void V2gEvParseCableCheckFinished(byte[] d) { }
        public void V2gEvParsePreChargingReady(byte[] d) { }
        public void V2gEvParseChargingReady(byte[] d) { }
        public void V2gEvParseChargingStarted(byte[] d) { }
        public void V2gEvParseChargingStopped(byte[] d) { }
        public void V2gEvParsePostChargingReady(byte[] d) { }
        public void V2gEvParseSessionStopped(byte[] d) { }
    }
}