using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CH341;

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
        private CH341A ch341Device;
        private bool isDeviceOpen = false;
        private byte currentRequestId = 0;
        private Mutex _hwMutex = new Mutex(); // 

        public event Action<string> OnLog;
        public string Version => GetVersionFromDevice();

        private const byte DATA_HEADER_1 = 0x55;
        private const byte DATA_HEADER_2 = 0x55;
        private const byte SIZE_HEADER_1 = 0xAA;
        private const byte SIZE_HEADER_2 = 0xAA;
        private const byte START_BYTE = 0xC0;
        private const byte END_BYTE = 0xC1;
        private const int CHIP_SELECT = 0x00;

        private const uint MASK_TX_Pending = 0x0000_0100;
        private const uint MASK_RX_Ready = 0x0000_0200;

        public RealWhitebeet(string iftype, string iface, string mac)
        {
            Log($"[RealWhitebeet] 하드웨어({mac}) 연결 초기화...");
            try
            {
                ch341Device = new CH341A(0);
                if (ch341Device.OpenDevice())
                {
                    isDeviceOpen = true;
                    if (ch341Device.SetStream(0x80))
                    {
                        ch341Device.SetDelaymS(0);
                        Thread.Sleep(100);
                        Log("[RealWhitebeet] CH341 열기 및 SPI모드(0x80) 설정 성공.");
                    }
                    else throw new Exception("SetStream(0x80) Failed");
                }
                else throw new Exception("OpenDevice Failed");
            }
            catch (Exception ex)
            {
                isDeviceOpen = false;
                Log($"[Init Error] {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            if (isDeviceOpen && ch341Device != null) ch341Device.CloseDevice();
            isDeviceOpen = false;
            _hwMutex.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Log(string msg) => OnLog?.Invoke(msg);

        // Hardware IO
        private bool TransferSpi(ref byte[] ioBuffer)
        {
            if (!isDeviceOpen) return false;
            ch341Device.FlushBuffer();
            return ch341Device.StreamSPI4(0, CHIP_SELECT, ref ioBuffer);
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

        // Protocol Logic
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

        private byte[] SendQuery(byte moduleId, byte subId, byte[] payload)
        {
            if (!isDeviceOpen) return null;
            _hwMutex.WaitOne();
            try
            {
                byte[] hciFrame = BuildHciPacket(moduleId, subId, payload);
                ushort sendLen = (ushort)hciFrame.Length;

                if (!WaitForPin(MASK_RX_Ready, true, 500)) { Log("[Timeout] RX_Ready"); return null; }

                byte[] szTx = { SIZE_HEADER_1, SIZE_HEADER_2, (byte)(sendLen >> 8), (byte)(sendLen & 0xFF) };
                TransferSpi(ref szTx);

                byte[] dTx = new byte[4 + sendLen];
                dTx[0] = DATA_HEADER_1; dTx[1] = DATA_HEADER_2;
                Array.Copy(hciFrame, 0, dTx, 4, sendLen);
                TransferSpi(ref dTx);
                Log($"[TX] Cmd(0x{subId:X2}) Sent.");

                if (!WaitForPin(MASK_TX_Pending, true, 1000)) { Log("[Timeout] TX_Pending (No Response)"); return null; }

                byte[] szRx = { SIZE_HEADER_1, SIZE_HEADER_2, 0, 0 };
                TransferSpi(ref szRx);
                ushort rxLen = (ushort)((szRx[2] << 8) | szRx[3]);

                if (rxLen == 0) { Log("[Error] Size 0 received"); return null; }
                if (rxLen > 4096) throw new Exception("Invalid Size");

                byte[] dRx = new byte[4 + rxLen];
                dRx[0] = DATA_HEADER_1; dRx[1] = DATA_HEADER_2;
                TransferSpi(ref dRx);

                byte[] resp = new byte[rxLen];
                Array.Copy(dRx, 4, resp, 0, rxLen);
                Log($"[RX] {BitConverter.ToString(resp).Replace("-", " ")}");

                return resp;
            }
            catch (Exception ex) { Log($"[Send Error] {ex.Message}"); return null; }
            finally { _hwMutex.ReleaseMutex(); }
        }

        public (int, byte[]) V2gEvReceiveRequest()
        {
            if (!isDeviceOpen) throw new Exception("Device closed");
            _hwMutex.WaitOne();
            try
            {
                if (!IsPinHigh(MASK_TX_Pending)) throw new TimeoutException("No Data");

                byte[] sz = { SIZE_HEADER_1, SIZE_HEADER_2, 0, 0 };
                TransferSpi(ref sz);
                ushort len = (ushort)((sz[2] << 8) | sz[3]);
                if (len == 0 || len > 4096) throw new TimeoutException("Size 0");

                byte[] d = new byte[4 + len];
                d[0] = DATA_HEADER_1; d[1] = DATA_HEADER_2;
                TransferSpi(ref d);

                if (d.Length > 10 && d[4] == START_BYTE)
                {
                    int payloadLen = (d[8] << 8) | d[9];
                    byte[] p = new byte[payloadLen];
                    if (d.Length >= 10 + payloadLen) Array.Copy(d, 10, p, 0, payloadLen);
                    return (d[6], p); // SubID
                }
                throw new Exception("Packet Format Error");
            }
            finally { _hwMutex.ReleaseMutex(); }
        }

        // Helpers
        private string GetVersionFromDevice() => "v1.0";
        private byte[] ValueToExponential(object v) { long val = Convert.ToInt64(v); return new byte[] { (byte)((val >> 8) & 0xFF), (byte)(val & 0xFF), 0 }; }
        private byte[] IntTo4BytesBigEndian(object v) { uint val = Convert.ToUInt32(v); return new byte[] { (byte)(val >> 24), (byte)(val >> 16), (byte)(val >> 8), (byte)(val) }; }

        // API Wrappers
        public void ControlPilotSetMode(int m) => SendQuery(0x29, 0x40, new byte[] { (byte)m });
        public void ControlPilotStart() => SendQuery(0x29, 0x42, null);
        public void ControlPilotStop() => SendQuery(0x29, 0x43, null);
        public void ControlPilotSetResistorValue(int v) => SendQuery(0x29, 0x46, new byte[] { (byte)v });
        public double ControlPilotGetDutyCycle()
        {
            var r = SendQuery(0x29, 0x45, null);
            return r != null && r.Length >= 9 ? ((r[7] << 8) | r[8]) / 10.0 : -1.0;
        }

        public void SlacStart(int m) => SendQuery(0x28, 0x42, new byte[] { (byte)m });
        public void SlacStop() => SendQuery(0x28, 0x43, null);
        public void SlacStartMatching() => SendQuery(0x28, 0x44, null);
        public void SlacSetValidationConfiguration(int c) => SendQuery(0x28, 0x4B, new byte[] { (byte)c });
        public void SlacSetAttnTxRef(byte[] val) => SendQuery(0x28, 0x48, val);

        public void V2gStartSession() => SendQuery(0x27, 0xA9, null);
        public void V2gStopSession() => SendQuery(0x27, 0xAE, null);
        public void V2gStartCableCheck() => SendQuery(0x27, 0xAA, null);
        public void V2gStartPreCharging() => SendQuery(0x27, 0xAB, null);
        public void V2gStartCharging() => SendQuery(0x27, 0xAC, null);
        public void V2gStopCharging(bool r) => SendQuery(0x27, 0xAD, new byte[] { (byte)(r ? 1 : 0) });

        public void V2gEvSetConfiguration(Dictionary<string, object> c)
        {
            var p = new List<byte>();
            if (c.ContainsKey("evid")) p.AddRange((byte[])c["evid"]);
            else p.AddRange(new byte[] { 0x02, 0, 0, 0, 0, 1 });
            p.Add(2); p.Add(0); p.Add(1);
            p.Add(1); p.Add(0);
            p.Add(1); p.Add(1);
            p.AddRange(ValueToExponential(50000));
            if (SendQuery(0x27, 0xA0, p.ToArray()) != null) Log("EV 설정 완료.");
        }

        public void V2gSetDCChargingParameters(Dictionary<string, object> p) => SendQuery(0x27, 0xA2, BuildDCParams(p));
        public void V2gUpdateDCChargingParameters(Dictionary<string, object> p) => SendQuery(0x27, 0xA3, BuildDCParams(p));
        public void V2gSetACChargingParameters(Dictionary<string, object> p) => SendQuery(0x27, 0xA5, new byte[10]);

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

        // Matching & Parsers
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

        // Payload Readers
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