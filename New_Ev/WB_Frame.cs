using CH341;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Ev
{
    internal class WB_Frame
    {

        public event EventHandler<WB_EventArgsStatus> Event_Notifier;

        private CH341A? _device;
        private WB_SPI? white_beet = null;

        private const byte STX = 0xC0;
        private const byte ETX = 0xC1;

        private byte _module_id = 0;
        private byte _sub_id = 0;
        private byte _req_id = 0;
        private ushort _payload_len = 0;
        private byte[]? _payload = null;
        private byte _crc = 0;
        private byte _stx = STX;
        private byte _etx = ETX;

        private byte[]? _last_query = null;

        private string _last_error = "";

        public byte Module_ID { get { return _module_id; } }
        public byte Sub_ID{ get { return _sub_id; } }
        public byte Req_ID { get { return _req_id; } }
        public ushort Payload_Len { get { return _payload_len; } }
        public byte CRC { get { return _crc; } }
        public string Last_Error { get { return _last_error; } }

        
        public WB_Frame(CH341A? device)
        {
            _device = device;
            white_beet = new WB_SPI(device);
            white_beet.Event_Notifier += OnReceivedStatus;
        }
        private void OnReceivedStatus(object sender, WB_EventArgsStatus e)
        {
            if (Event_Notifier is not null)
                Event_Notifier.Invoke(this, e);
        }
        public void Close()
        {
            if (white_beet != null)
            {
                white_beet.Close();
            }
            white_beet = null;
            _device = null;
        }
        public byte[] Get_Data_Frame()
        {
            byte[] frame_id = { 0x55, 0x55, 0x00, 0x00 };
            ushort len = (ushort)(_payload_len + frame_id.Length + 3)/*STX, CRC, ETX*/;
            byte tmp_len = 0;

            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(frame_id);
            bw.Write(_stx);
            bw.Write(_module_id);
            bw.Write(_sub_id);
            bw.Write(_req_id);
            //big endoan
            bw.Write((byte)((_payload_len >> 8) & 0xFF));
            bw.Write((byte)(_payload_len & 0xFF));

            bw.Write(_payload);
            bw.Write(_crc);
            bw.Write(_etx);

            _last_query = ms.ToArray();
            bw.Close();
            ms.Close();
            //Set_CRC(_last_query);
            return _last_query;
        }
        public byte[] build_query()
        {
            ushort len = (ushort)(_payload_len  + 5)/*STX, payload_len, CRC, ETX*/;
            byte tmp_len = 0;

            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(_stx);
            bw.Write(_module_id);
            bw.Write(_sub_id);
            bw.Write(_req_id);
            //big endoan
            bw.Write((byte)((_payload_len >> 8) & 0xFF));
            bw.Write((byte)(_payload_len & 0xFF));

            if (_payload != null)
                bw.Write(_payload);
            bw.Write(_crc);
            bw.Write(_etx);

            _last_query = ms.ToArray();
            bw.Close();
            ms.Close();
            //Set_CRC(_last_query);
            return _last_query;
        }
        public byte Calc_CRC(byte[] frame)
        {
            if (frame == null)
                throw new InvalidOperationException("Frame has nt been establised");
            //excluding frame identifier
            uint check_sum = 0;
            for (ushort i = 4; i < frame.Length; ++i)
            { check_sum += frame[i]; }

            check_sum = ((check_sum & 0xFFFF) + (check_sum >> 16));
            check_sum = ((check_sum & 0xFF) + (check_sum >> 8));
            check_sum = ((check_sum & 0xFF) + (check_sum >> 8));

            if (check_sum != 0xFF)
                check_sum = ~check_sum;

            return (byte)check_sum;
        }
        public void Set_CRC(byte[] frame)
        {
            frame[frame.Length-2] = Calc_CRC(frame);
        }
        public bool Set_Frame(byte[] received_frame)
        {
            byte[] frame_id = { 0, 0, 0, 0 };
            byte check_sum;
            MemoryStream ms = new MemoryStream();
            BinaryReader br = new BinaryReader(ms);

            frame_id = br.ReadBytes(frame_id.Length);
            if (br.ReadByte() != STX)
            {
                _last_error = $"Frame error: no STX found";
                return false;
            }
            _module_id = br.ReadByte();
            _sub_id = br.ReadByte();
            _req_id = br.ReadByte();
            _payload_len = (ushort)(br.ReadByte() << 8);
            _payload_len += br.ReadByte();
            _payload = br.ReadBytes(_payload_len);
            _crc = br.ReadByte();
            if (br.ReadByte() != ETX)
            {
                _last_error = $"Frame error: no ETX found";
                return false;
            }

            br.Close();
            ms.Close();
            check_sum = Calc_CRC(ms.ToArray());
            if (check_sum != _crc)
            {
                _last_error = $"Invalid CRC - rx: 0x{_crc:X2}, calc: 0x{check_sum:X2}";
                return false ;
            }
            _last_error = "";
            return true;
        }
        //public bool Is_Valid_Frame(WB_Frame last_query)
        //{
            
        //}
        public byte[] Send_Query(WB_Query query, byte[]? payload)
        {
            _module_id = query.module_id;
            _sub_id = query.sub_id;
            _req_id = query.req_id;
            if (payload == null)
            {
                _payload_len = 0;
                payload = new byte[1];
                payload[0] = 0x00;
            }
            else
            {
                _payload_len = (ushort)payload.Length;
                _payload = new byte[_payload_len];
                Buffer.BlockCopy(payload, 0, _payload, 0, _payload_len);
            }
            _crc = 0;
            return white_beet.Send_Query(build_query());
        }
        public void Check_Status()
        {
            white_beet.Check_Status();
        }
        public void Reset()
        {
            white_beet.Reset();
        }
    }
}
