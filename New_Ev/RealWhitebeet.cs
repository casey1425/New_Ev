using CH341;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
//using CH341;

namespace New_Ev
{
    public class ChargingProfile
    {
        public int TupleCount { get; set; }
        public int TupleId { get; set; }
        public int EntriesCount { get; set; }
        public List<ChargingProfileEntry> Entries { get; set; } = new List<ChargingProfileEntry>();
    }

    public class ChargingProfileEntry
    {
        public uint Start { get; set; }
        public uint Interval { get; set; }
        public double Power { get; set; }
    }

    public class RealWhitebeet : IDisposable
    {
        //private CH341A ch341Device;
        private CH341A ch341Device;
        private bool isDeviceOpen = false;
        private byte currentRequestId = 0;
        private Mutex _hwMutex = new Mutex();

        public event Action<string> OnLog;
        public string Version => GetVersionFromDevice();

        private const byte DATA_HEADER_1 = 0x55;
        private const byte DATA_HEADER_2 = 0x55;
        private const byte SIZE_HEADER_1 = 0xAA;
        private const byte SIZE_HEADER_2 = 0xAA;
        private const byte START_BYTE = 0xC0;
        private const byte END_BYTE = 0xC1;
        private const int CHIP_SELECT = 0x00;

        // [핀 마스크] 배선에 맞게 조정
        private const uint MASK_TX_Pending = 0x0000_0100; // ERR Pin
        private const uint MASK_RX_Ready = 0x0000_0200;   // PEMP Pin

        public RealWhitebeet(string iftype, string iface, string mac)
        {
            Log($"[RealWhitebeet] 연결 시도...");
            try
            {
                ch341Device = new CH341A(0);
                if (ch341Device.OpenDevice())
                {
                    isDeviceOpen = true;
                    // 안정성을 위해 속도 1로 설정
                    if (ch341Device.SetStream(0x80))
                    {
                        ch341Device.SetDelaymS(1);
                        Thread.Sleep(100);
                        Log("[성공] CH341A 열기 및 SPI Mode 0 설정.");
                    }
                    else throw new Exception("SetStream(0x80) Failed");
                }
                else throw new Exception("OpenDevice Failed");
            }
            catch (Exception ex)
            {
                isDeviceOpen = false;
                Log($"[초기화 오류] {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            if (isDeviceOpen) ch341Device.CloseDevice();
            isDeviceOpen = false;
            _hwMutex.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Log(string msg) => OnLog?.Invoke(msg);

        // Hardware IO
        private bool TransferSpi(ref byte[] ioBuffer)
        {
            if (!isDeviceOpen) return false;
            //2025.12.09
            //ch341Device.FlushBuffer();
            //return ch341Device.StreamSPI4(0, CHIP_SELECT, ref ioBuffer);
            ch341Device.SetStream(0x82);
            return ch341Device.StreamSPI4(0x80, ref ioBuffer);
        }

        private bool IsPinHigh(uint mask)
        {
            if (!isDeviceOpen) return false;
            uint s;
            return ch341Device.GetInput(out s) && (s & mask) > 0;
        }

        private bool WaitForPin(uint mask, bool state, int timeout)
        {
            DateTime start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeout)
            {
                if (IsPinHigh(mask) == state) return true;
                Thread.Sleep(5);
            }
            return false;
        }

        private ushort ReadWhitebeetMessage(out byte[] rx_buf)
        {
            byte[] szRx = { SIZE_HEADER_1, SIZE_HEADER_2, 0, 0 };
            byte[] dRx;
            ushort rxLen;
            ushort received = 0;
            
            rx_buf = null;

            //device에서 보낼 메시지가 없으면 false return
            if (IsPinHigh(MASK_TX_Pending))
            {
                //1. 먼저 device가 보낼 data size를 확인
                if (!TransferSpi(ref szRx)) throw new Exception("SPI Read Size Failed");
                //SIZE frame이 맞는 지 확인
                if (szRx[0] != 0xAA || szRx[1] != 0xAA)
                {
                    Log($"[SPI Error] 헤더 깨짐: {BitConverter.ToString(szRx)}");
                    return received;
                }
                //보낼 SIZE확인
                rxLen = (ushort)((szRx[2] << 8) | szRx[3]);

                //메시지를 수신하기 위해 Dummy frame생성
                dRx = new byte[rxLen + 4];    //헤더 (0x55, 0x55, 0x00, 0x00) 포함
                szRx = new byte[4 + rxLen];
                dRx[0] = DATA_HEADER_1; dRx[1] = DATA_HEADER_2;

                //메시지 수신
                if (!TransferSpi(ref szRx)) throw new Exception("SPI Read Size Failed");

                if (rxLen == 0) { Log("[Error] Size 0 received"); return received; }
                if (rxLen > 4096) throw new Exception($"Invalid Size: {rxLen}");

                if (!TransferSpi(ref dRx)) throw new Exception("SPI Read Data Failed");

                received = (ushort)(rxLen - 4);
                rx_buf = new byte[received];
                Array.Copy(dRx, 4, rx_buf, 0, received);
            }
            return received;
        }
        // Protocol Logic
//2025.12.09
        private void RxMessageProcess(byte[] rx_message)
        {
            //STX, ModuleID, SubID, ReqID, Payload Length[2], Payload[n], (CRC[2]), ETX
            if (rx_message[3] == 0xFF)
                Log($"[Status Message] Module ID = 0x{rx_message[1]:X2}, Sub ID = 0x{rx_message[2]}");
            else
                Log($"[Response] Module ID = 0x{rx_message[1]}, Sub ID = 0x{rx_message[2]}");
        }
        private byte GenerateNextRequestId() => ++currentRequestId == 0xFF ? (byte)0 : currentRequestId;

        private byte[] BuildHciPacket(byte moduleId, byte subId, byte[] payload)
        {
            int pLen = payload?.Length ?? 0;
            List<byte> pkt = new List<byte> { START_BYTE, moduleId, subId, GenerateNextRequestId(), (byte)(pLen >> 8), (byte)(pLen & 0xFF) };
            if (pLen > 0) pkt.AddRange(payload);
            pkt.Add(0x00); pkt.Add(END_BYTE);
            byte[] arr = pkt.ToArray();
            arr[arr.Length - 2] = CalculateChecksum(arr);
            return arr;
        }

        private byte CalculateChecksum(byte[] data)
        {
            uint sum = 0;
            foreach (byte b in data) sum += b;
            sum = (sum & 0xFFFF) + (sum >> 16);
            sum = (sum & 0xFF) + (sum >> 8);
            byte res = (byte)~sum;
            return res == 0 ? (byte)0xFF : res;
        }
        //응답을 대기하려면에서 receive_required true로 설정
        private byte[] SendQuery(byte moduleId, byte subId, byte[] payload, bool receive_required = false)
        {
            byte[] response = null;

            if (!isDeviceOpen) return null;
            _hwMutex.WaitOne();
            try
            {
                byte[] szRx;
                byte[] hciFrame = BuildHciPacket(moduleId, subId, payload);
                ushort sendLen = (ushort)hciFrame.Length;

                byte[] rxMessage;

                //1. 수신할 메시지 있는 지 확인
                while(true)
                {
                    if (ReadWhitebeetMessage(out rxMessage) > 0) RxMessageProcess(rxMessage);

                    //2. 송신할 메시지 Size 전송
                    szRx = new byte[]{ SIZE_HEADER_1, SIZE_HEADER_2, (byte)((sendLen >> 8) & 0xFF), (byte)(sendLen & 0xFF) };

                    if (!TransferSpi(ref szRx)) throw new Exception("SPI Read Size Failed");

                    if (szRx[0] != 0xAA || szRx[1] != 0xAA)
                    {
                        Log($"[SPI Error] 헤더 깨짐: {BitConverter.ToString(szRx)}");
                        return null;
                    }
                    if ((szRx[2] | szRx[3]) == 0)
                        break;
                }

//                int txLen = 4 + Math.Max(sendLen, slaveLen);
                byte[] dTx = new byte[sendLen + 4];
                dTx[0] = DATA_HEADER_1; dTx[1] = DATA_HEADER_2; dTx[2] = 0; dTx[3] = 0;
                Array.Copy(hciFrame, 0, dTx, 4, sendLen);

                if (!TransferSpi(ref dTx)) throw new Exception("SPI Write Data Failed");
                Log($"[TX] Cmd(0x{subId:X2}) Sent.");

                if (receive_required)
                {
                    while (true)
                    {
                        Thread.Sleep(50);
                        if (ReadWhitebeetMessage(out response) > 0)
                        {
                            //STX, ModuleID, SubID, ReqID, Payload Length[2], Payload[n], (CRC[2]), ETX
                            RxMessageProcess(response);
                            if ((response[1] == moduleId) && (response[2] == subId))
                                break;
                        }
                    }
                }
                else
                {
                    response = new byte[1];
                }

            }
            catch (Exception ex) { Log($"[Send Error] {ex.Message}"); return null; }
            finally { _hwMutex.ReleaseMutex(); }
            return response;
        }

        public (int, byte[]) V2gEvReceiveRequest()
        {
            if (!isDeviceOpen) throw new Exception("Device closed");
            _hwMutex.WaitOne();
            try
            {
                if (!IsPinHigh(MASK_TX_Pending)) throw new TimeoutException("No Data");

                byte[] szReq = { SIZE_HEADER_1, SIZE_HEADER_2, 0, 0 };
                TransferSpi(ref szReq);

                if (szReq[0] != 0xAA || szReq[1] != 0xAA) throw new TimeoutException("Invalid Header");

                ushort len = (ushort)((szReq[2] << 8) | szReq[3]);
                if (len == 0 || len > 4096) throw new TimeoutException("Invalid Size");

                byte[] dReq = new byte[4 + len];
                dReq[0] = DATA_HEADER_1; dReq[1] = DATA_HEADER_2;
                TransferSpi(ref dReq);

                if (dReq.Length > 10 && dReq[4] == START_BYTE)
                {
                    int subId = dReq[6];
                    int payloadLen = (dReq[8] << 8) | dReq[9];
                    byte[] payload = new byte[payloadLen];
                    if (dReq.Length >= 10 + payloadLen) Array.Copy(dReq, 10, payload, 0, payloadLen);
                    return (subId, payload);
                }
                throw new Exception("Packet Format Error");
            }
            finally { _hwMutex.ReleaseMutex(); }
        }

        private string GetVersionFromDevice() => "v1.0";
        private byte[] ValueToExponential(object v) { long val = Convert.ToInt64(v); return new byte[] { (byte)((val >> 8) & 0xFF), (byte)(val & 0xFF), 0 }; }
        private byte[] IntTo4BytesBigEndian(object v) { uint val = Convert.ToUInt32(v); return new byte[] { (byte)(val >> 24), (byte)(val >> 16), (byte)(val >> 8), (byte)(val) }; }

        public bool ControlPilotSetMode(int m) => SendQuery(0x29, 0x40, new byte[] { (byte)m }) != null;
        public bool ControlPilotStart() => SendQuery(0x29, 0x42, null) != null;
        public bool ControlPilotStop() => SendQuery(0x29, 0x43, null) != null;
        public bool ControlPilotSetResistorValue(int v) => SendQuery(0x29, 0x46, new byte[] { (byte)v }) != null;
        public double ControlPilotGetDutyCycle()
        {
            var r = SendQuery(0x29, 0x45, null);
            return r != null && r.Length >= 9 ? ((r[7] << 8) | r[8]) / 10.0 : -1.0;
        }

        public bool SlacStart(int m) => SendQuery(0x28, 0x42, new byte[] { (byte)m }) != null;
        public bool SlacStop() => SendQuery(0x28, 0x43, null) != null;
        public bool SlacStartMatching() => SendQuery(0x28, 0x44, null) != null;
        public bool SlacSetAttnTxRef(byte[] val) => SendQuery(0x28, 0x48, val) != null;
        public bool SlacSetValidationConfiguration(int c) => SendQuery(0x28, 0x4B, new byte[] { (byte)c }) != null;

        public bool V2gStartSession() => SendQuery(0x27, 0xA9, null) != null;
        public bool V2gStopSession() => SendQuery(0x27, 0xAE, null) != null;
        public bool V2gStartCableCheck() => SendQuery(0x27, 0xAA, null) != null;
        public bool V2gStartPreCharging() => SendQuery(0x27, 0xAB, null) != null;
        public bool V2gStartCharging() => SendQuery(0x27, 0xAC, null) != null;
        public bool V2gStopCharging(bool r) => SendQuery(0x27, 0xAD, new byte[] { (byte)(r ? 1 : 0) }) != null;

        public bool V2gEvSetConfiguration(Dictionary<string, object> c)
        {
            var p = new List<byte>();
            if (c.ContainsKey("evid")) p.AddRange((byte[])c["evid"]);
            else p.AddRange(new byte[] { 0x02, 0, 0, 0, 0, 1 });
            p.Add(2); p.Add(0); p.Add(1);
            p.Add(1); p.Add(0);
            p.Add(1); p.Add(1);
            p.AddRange(ValueToExponential(50000));
            return SendQuery(0x27, 0xA0, p.ToArray()) != null;
        }

        public bool V2gSetDCChargingParameters(Dictionary<string, object> p) => SendQuery(0x27, 0xA2, BuildDCParams(p)) != null;
        public bool V2gUpdateDCChargingParameters(Dictionary<string, object> p) => SendQuery(0x27, 0xA3, BuildDCParams(p)) != null;
        public bool V2gSetACChargingParameters(Dictionary<string, object> p) => SendQuery(0x27, 0xA5, new byte[10]) != null;

        private byte[] BuildDCParams(Dictionary<string, object> p)
        {
            var b = new List<byte>();
            b.AddRange(ValueToExponential(p["min_voltage"])); b.AddRange(ValueToExponential(p["min_current"])); b.AddRange(ValueToExponential(p["min_power"]));
            b.AddRange(ValueToExponential(p["max_voltage"])); b.AddRange(ValueToExponential(p["max_current"])); b.AddRange(ValueToExponential(p["max_power"]));
            b.Add((byte)Convert.ToInt32(p["soc"])); b.Add((byte)Convert.ToInt32(p["status"])); b.AddRange(ValueToExponential(p["target_voltage"])); b.AddRange(ValueToExponential(p["target_current"]));
            if (p.ContainsKey("full_soc")) b.Add((byte)Convert.ToInt32(p["full_soc"]));
            if (p.ContainsKey("bulk_soc")) b.Add((byte)Convert.ToInt32(p["bulk_soc"]));
            b.AddRange(ValueToExponential(p.ContainsKey("energy_request") ? p["energy_request"] : 0));
            b.AddRange(IntTo4BytesBigEndian(p.ContainsKey("departure_time") ? p["departure_time"] : 0));
            return b.ToArray();
        }

        private byte[] pb; private int pri;
        private void PayloadReaderInitialize(byte[] d) { pb = d; pri = 0; }
        private int PayloadReaderReadInt(int n) { long v = 0; for (int i = 0; i < n; i++) v = (v << 8) | pb[pri + i]; pri += n; return (int)v; }
        private double PayloadReaderReadExponential()
        {
            byte[] b = { pb[pri], pb[pri + 1] }; if (BitConverter.IsLittleEndian) Array.Reverse(b);
            short v = BitConverter.ToInt16(b, 0); sbyte e = (sbyte)pb[pri + 2]; pri += 3; return v * Math.Pow(10, e);
        }
        private byte[] PayloadReaderReadBytes(int n) { byte[] b = new byte[n]; Array.Copy(pb, pri, b, 0, n); pri += n; return b; }

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
            for (int i = 0; i < p.EntriesCount; i++)
                p.Entries.Add(new ChargingProfileEntry { Start = (uint)PayloadReaderReadInt(4), Interval = (uint)PayloadReaderReadInt(4), Power = PayloadReaderReadExponential() });
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

        public bool SlacMatched()
        {
            Log("SLAC 매칭 대기 중...");
            DateTime s = DateTime.Now;
            while ((DateTime.Now - s).TotalSeconds < 60)
            {
                try
                {
                    var r = V2gEvReceiveRequest();
                    if (r.Item1 == 0x80) { Log("SLAC 매칭 성공!"); return true; }
                    if (r.Item1 == 0x81) { Log("SLAC 매칭 실패."); return false; }
                }
                catch (TimeoutException) { Thread.Sleep(200); }
                catch { return false; }
            }
            return false;
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