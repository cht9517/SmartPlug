using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPlug
{
    public delegate void TSB_s_Handler(byte[] buf);//建了个委托

    public class Tsb_s
    {
        private static Tsb_s instance = new Tsb_s();
        private Tsb_s() { }
        public static Tsb_s Instance { get { return instance; } }

        private static TSB_s_Handler on_TSB_s_rx;//委托
        public event TSB_s_Handler TSB_s_Rx//一个事件
        {
            add { on_TSB_s_rx += new TSB_s_Handler(value); }
            remove { on_TSB_s_rx -= new TSB_s_Handler(value); }
        }

        public Task task_tsb_rx = new Task(tsb_rx_thd);

        private static System.IO.Ports.SerialPort sPort = new System.IO.Ports.SerialPort("COM1", 19200, System.IO.Ports.Parity.None, dataBits: 8);
        private static byte[] rx_buf_o = new Byte[1050];
        private byte[] tx_buf_o = new Byte[1050];
        private byte[] tx_buf_e = new Byte[2196];

        public static byte[] rx_msg_buf = new byte[1050];

        private static byte rx_state = 0;
        private static bool rx_escape = false;
        
        private static int rx_len = 0;
        private static int rx_msg_len = 0;

        public static bool tsb_rx_byte(byte dat)
        {
            if (rx_state == 0)
            {
                if (dat == (byte)(0xFB))
                {
                    rx_state = 1;
                    rx_len = 1;
                    rx_escape = false;
                }
            }
            else
            {
                if (dat == (byte)(0xFB))
                {
                    rx_state = 1;
                    rx_len = 1;
                    rx_escape = false;
                }
                else
                {
                    if (dat == (byte)(0xFE))//帧结束
                    {
                        rx_state = 0;
                        rx_msg_len = rx_buf_o[1] & 0xff;
                        if (rx_msg_len + 4 == rx_len)//包中长度信息正确
                        {
                            int crc = CRC16(rx_buf_o, 2, rx_msg_len);

                            int rx_crc = ((rx_buf_o[rx_len - 2] & 0xff) << 8) + (rx_buf_o[rx_len - 1] & 0xff);
                            if (crc == rx_crc)
                            {
                                //rx_cmd_id = ((rx_buf_o[3] & 0xff) << 8) + (rx_buf_o[4] & 0xff);
                                return true;
                            }
                        }

                    }
                    else if (dat == (byte)(0xF0))//转义标志
                    {
                        rx_escape = true;
                    }
                    else
                    {
                        if (rx_escape)//转义字符
                        {
                            if ((dat == 0x00) || (dat == 0x0B) || (dat == 0x0E))
                            {
                                rx_escape = false;
                                rx_buf_o[rx_len++] = (byte)(dat | 0xF0);
                            }
                            else
                                rx_state = 0;
                        }
                        else//正常接收
                            rx_buf_o[rx_len++] = dat;

                        //超长判断
                        if (rx_len > 1030)
                            rx_state = 0;
                    }

                }
            }

            return false;
        }


        public bool tsb_tx_frame(int cmd_id)
        {
            int len = 1; 
            tx_buf_o[1] = (byte)(len & 0xff);

            tx_buf_o[2] = (byte)(cmd_id & 0xff);

            //计算CRC
            int tx_crc = CRC16(tx_buf_o, 2, len);
            tx_buf_o[3] = (byte)((tx_crc >> 8) & 0xff);
            tx_buf_o[4] = (byte)(tx_crc & 0xff);

            //数据转义
            tx_buf_e[0] = (byte)(0xFB);
            int pos = 1;
            for (int i = 1; i < (len + 4); i++)
            {
                if ((tx_buf_o[i] == 0xF0) || (tx_buf_o[i] == 0xFB) || (tx_buf_o[i] == 0xFE))
                {
                    tx_buf_e[pos++] = (byte)(0xF0);
                    tx_buf_e[pos++] = (byte)(tx_buf_o[i] & 0x0F);
                }
                else
                    tx_buf_e[pos++] = tx_buf_o[i];
            }
            tx_buf_e[pos++] = (byte)(0xFE);

            //写入到通信接口
            if (tsb_tx_write(tx_buf_e, pos))
            {
                return true;
            }

            return false;
        }

        private bool tsb_tx_write(byte[] buf, int len)
        {
            if(port_is_open())
            {
                sPort.Write(buf, 0, len);
                return true;
            }
                
            return false;
        }

        private static int id_cnt = 0;
        private static void test_on_rx()
        {
            Thread.Sleep(5000);

            for (int i = 0; i < 10; i++)
                rx_msg_buf[i] = (byte)(id_cnt++ & 0xFF);

            id_cnt -= 9;
            rx_msg_buf[2] = 0x80;
            rx_msg_buf[9] = (byte)(rx_msg_buf[8] - 8);
            rx_msg_buf[10] = 0;
            rx_msg_buf[11] = 252;
            rx_msg_buf[12] = 52;


            on_TSB_s_rx(rx_msg_buf);
        }

        protected static void tsb_rx_thd()
        {
            while (true)
            {
                Thread.Sleep(50);

                //test_on_rx();

                if (sPort.IsOpen)
                {
                    int rx_cnt = sPort.BytesToRead;
                    byte[] rx_buf = new byte[rx_cnt];//存放接收到的数据
                    sPort.Read(rx_buf, 0, rx_cnt);

                    int id = 0;
                    while (id < rx_cnt)
                    {
                        if (tsb_rx_byte(rx_buf[id++]))//成功接收到一帧
                        {
                            for (int i = 0; i < rx_len; i++)
                                rx_msg_buf[i] = rx_buf_o[i];

                            on_TSB_s_rx(rx_msg_buf);
                        }
                    }
                }
            }
        }


        

        public void port_open(string portName)
        {
            if (port_is_open())
                sPort.Close();

            sPort.PortName = portName;
            sPort.Open();
        }
        public void portNameSetting(string portName)
        {
            sPort.PortName = portName;
        }

        public void port_close()
        {
            if (port_is_open())
            {
                sPort.Close();
            }
                
        }

        public bool port_is_open()
        {
            return sPort.IsOpen;
        }

        

        private static int[] CRC16CcittTable =
        {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
            0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
            0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
            0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
            0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
            0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
            0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
            0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
            0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
            0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
            0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
            0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
            0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
            0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
            0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
            0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
            0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
            0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
            0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
            0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
            0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
            0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
            0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
            0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
            0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
            0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0
        };
        private static int CRC16(byte[] buf, int offset, int len)
        {
            int crc, i;

            crc = 0xffff;//暂存校验字节
            for (i = offset; i < offset+len; i++)
            {
                crc = CRC16CcittTable[((crc >> 8 & 0xff) ^ (buf[i])) & 0xff] ^ (crc << 8 & 0xff00);
            }
            return crc;
        }
    }
}
