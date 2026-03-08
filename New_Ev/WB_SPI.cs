using CH341;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace New_Ev
{
    internal class WB_SPI
    {

        public event EventHandler<WB_EventArgsStatus> Event_Notifier;
        //pin map
        //low byte
        //private const int SCK = 0;              //"D0", ADBUS0, TCK, CLK, SDL
        //private const int MOSI = 1;             //"D1", ADBUS1, TDI, MOSI, SDA
        //private const int MISO = 2;             //"D2", ADBUS2, TDO, MISO,
        //private const int CS = 3;               //"D3", ADBUS3, TMS, CS
                                                //"D4", ADBUS4, GPIOL0
        //high byte
        //private const int Toggle_Output = 8;    //"C0", ACBUS0,
        //private const int SPI_CS = 12;          //"C4"
        private const int MASK_RX_Ready = 0x0000_0200;        //"C5", White-beet is ready to receive, active high
        private const int MASK_TX_Pending = 0x0000_0100;      //"C6", White-beet has something to send, active high
        //private const int nRESET = 15;          //"C7"

        //Frame index
        private const int WB_IDX_FRAME_STX = 0;
        private const int WB_IDX_FRAME_MODULE_ID = 1;
        private const int WB_IDX_FRAME_SUB_ID = 2;
        private const int WB_IDX_FRAME_REQ_ID = 3;
        private const int WB_IDX_FRAME_PAYLOAD_SIZE = 4;        //size in uint16_t
        private const int WB_IDX_FRAME_PAYLOAD = 6;
        private const int WB_IDX_FRAME_CHECKSUM = -2;           //length - 2
        private const int WB_IDX_FRAME_ETX = -1;                //length - 1

        private CH341A? device = null;

        private byte[] _size_tx_frame = { 0xAA, 0xAA, 0x00, 0x00 };

        //private byte[] _last_query;

        private void notify_event(byte[]? received_message)
        {
            if (Event_Notifier != null) 
                Event_Notifier.Invoke(this, new WB_EventArgsStatus(received_message));
        }
        private void init_device()
        {
            if (device != null)
            {

            }
        }
        public WB_SPI(CH341A _device) 
        {
            device = _device;
            init_device();
        }
        public void Close()
        {

        }
        private void Delay_us(long us)
        {
            //Stopwatch 초기화 후 시간 측정 시작
            Stopwatch startNew = Stopwatch.StartNew();
            //설정한 us를 비교에 쓰일 Tick값으로 변환
            long usDelayTick = (us * Stopwatch.Frequency) / 1000000;
            //변환된 Tick값보다 클때까지 대기 
            while (startNew.ElapsedTicks < usDelayTick) ;
        }
        
        private void build_size_frame_send(byte[] tx_frame, ushort frame_size)
        {
            tx_frame[0] = 0xAA;
            tx_frame[1] = 0xAA;
            tx_frame[2] = (byte)((frame_size >> 8) & 0xFF);
            tx_frame[3] = (byte)(frame_size & 0xFF);
            send_spi(tx_frame);
        }
        private byte[] build_data_frame_send(byte[] query_frame)
        {
            byte[] tx_frame = new byte[query_frame.Length + 4];

            tx_frame[0] = 0x55;
            tx_frame[1] = 0x55;

            tx_frame[2] = 0x00;
            tx_frame[3] = 0x00;

            Buffer.BlockCopy(query_frame, 0, tx_frame, 4, query_frame.Length);
            send_spi(tx_frame);
            return tx_frame;
        }
        private byte[] build_data_frame(byte[] query_frame)
        {
            byte[] tx_frame = new byte[query_frame.Length + 4];

            tx_frame[0] = 0x55;
            tx_frame[1] = 0x55;

            tx_frame[2] = 0x00;
            tx_frame[3] = 0x00;

            Buffer.BlockCopy(query_frame, 0, tx_frame, 4, query_frame.Length);
            return tx_frame;
        }
        private void send_spi(byte[] buf)
        {
            Debug.WriteLine($"tx frame: {System.BitConverter.ToString(buf).Replace("-", " ")}");
            device.SetStream(0x82);
            //0x80: CS0
            //0x81: CS1
            //0x82: CS2
            //0x83: CS3
            device.StreamSPI4(0x80, ref buf);
            Debug.WriteLine($"tx frame echo: {System.BitConverter.ToString(buf).Replace("-", " ")}");
        }
        private void read_status(ushort size)
        {
            byte[] query = new byte[size];

            Array.Clear(query, 0, query.Length);
            var response = build_data_frame_send(query);
            notify_event(response);
            //is_valid_response(null, query);
        }
        private byte[] get_response(byte[] last_query)
        {
            byte[] rx_buf;
            if (is_tx_pending(out rx_buf))
            {
                return rx_buf;
            }
            return null;
        }
        bool is_pin_state_high(uint pin_mask)
        {
            uint gpio_input;
            if (device.GetInput(out gpio_input))
            {
                Debug.WriteLine($"io: 0x{gpio_input:X04}");
                return (gpio_input & pin_mask) > 0;
            }
            else
            {
                throw new Exception("device failed");
                return false; 
            }
        }
        bool is_tx_pending(out byte[] last_query)
        {
            ushort size;
            byte[] query_buf;

            if (is_pin_state_high(MASK_TX_Pending))
            {
                build_size_frame_send(_size_tx_frame, 0);
                size = (ushort)(_size_tx_frame[2] << 8);
                size |= (ushort)_size_tx_frame[3];
                if (size == 0)
                {
                    last_query = null;
                    return true;
                }

                //dummy frame to read data frame
                query_buf = new byte[size];
                Debug.WriteLine($"Detected tx pending: {size}");
                last_query = build_data_frame_send(query_buf);
                //                return is_valid_response(last_query, query_buf);
                return true;
            }
            last_query = null;
            return false;
        }
        bool is_valid_response(byte[] last_query, byte[] rx)
        {
            if (last_query == null)
            {
                //WB report the status
                if (rx[4 + WB_IDX_FRAME_REQ_ID] == 0xFF)
                {
                    Debug.WriteLine($"status message received: {System.BitConverter.ToString(rx).Replace("-", " ")}");
                    return true;
                }
                else 
                {
                    Debug.WriteLine($"Invalid response");
                    return false;
                }
            }
            else
            {
                if ((rx[4 + WB_IDX_FRAME_MODULE_ID] == last_query[WB_IDX_FRAME_MODULE_ID]) &&
                    (rx[4 + WB_IDX_FRAME_SUB_ID] == last_query[WB_IDX_FRAME_SUB_ID]) &&
                    (rx[4 + WB_IDX_FRAME_REQ_ID] == last_query[WB_IDX_FRAME_REQ_ID]))
                {
                    Debug.WriteLine($"response received: {0}", System.BitConverter.ToString(rx).Replace("-", " "));
                    if (rx[4 + WB_IDX_FRAME_PAYLOAD] != 0x00)
                        Debug.WriteLine($"\t-Error response");
                    return true;
                }
                else
                {
                    Debug.WriteLine("invalid response");
                    return false;
                }

            }
            return true;
        }
        public byte[] Send_Query(byte[] query)
        {
            ushort retry_count = 5;
            DateTime current_time;
            bool rx_ready;
            ushort size;

            byte[] query_frame;
            byte[] rx_buf;
            query_frame = build_data_frame(query);
            //resp_frame = new byte[query_frame.Length];

            if (is_tx_pending(out rx_buf))        //process status message
            {
                if (rx_buf is not null)
                {
                    notify_event(rx_buf);
                }
            }
            //wait for WB ready to receive a query
            rx_ready = false;
            while (!rx_ready)
            {
                rx_ready = is_pin_state_high(MASK_RX_Ready);
                if (rx_ready)
                {
                    Debug.WriteLine("Sending a qury");
                    while (true)
                    {
                        build_size_frame_send(_size_tx_frame, (ushort)query.Length);
                        size = (ushort)(_size_tx_frame[2] << 8 | _size_tx_frame[3]);
                        if (size > 0)
                        {
                            Debug.WriteLine($"Device has a message to info: size {size}");
                            read_status(size);
                        }
                        else
                        {
                            send_spi(query_frame);
                            break;
                        }
                        retry_count--;
                        if (retry_count == 0)
                        {
                            Debug.WriteLine("Query failed");
                            return null;
                        }
                    }
                    //wait for response
                    Debug.WriteLine("getting the response");

                    current_time = DateTime.Now;
                    bool result = false;
                    while ((DateTime.Now - current_time).Milliseconds < 500)
                    {
                        if (is_pin_state_high(MASK_TX_Pending))
                        {
                            query = get_response(query);
                            Debug.WriteLine($"response: {System.BitConverter.ToString(query).Replace("-", " ")}");
                            result = true;
                            break;
                        }

                    }
                    if (!result)
                        Debug.WriteLine("Timeout");
                    else
                    {
                        return query;
                    }
                 }
            }
            return null;
        }
        public void Check_Status()
        {
            byte[] rx_buf;

            if (is_tx_pending(out rx_buf))        //process status message
            {
                if (rx_buf is not null)
                {
                    notify_event(rx_buf);
                }
            }
        }
        public void Reset()
        {
            //gpio_driver.Write(nRESET, PinValue.Low);
            //Thread.Sleep(50);
            //gpio_driver.Write(nRESET, PinValue.High);
        }
    }
}
