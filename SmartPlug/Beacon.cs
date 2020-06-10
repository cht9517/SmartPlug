using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SmartPlug
{

    public delegate void Beacon_Handler(byte[] buf);//建了个委托

    class Beacon
    {
        private Task task_beacon_rx;

        public Beacon()
        {
            task_beacon_rx = new Task(comm_rx_thd);
        }

        private  Beacon_Handler on_Beacon_rx;//委托
        public event Beacon_Handler Beacon_Rx//一个事件
        {
            add { on_Beacon_rx += new Beacon_Handler(value); }
            remove { on_Beacon_rx -= new Beacon_Handler(value); }
        }


        private System.IO.Ports.SerialPort sPort = new System.IO.Ports.SerialPort("COM1", 19200, System.IO.Ports.Parity.None, dataBits: 8);

        public byte[] rx_msg_buf = new byte[256];


        private void test_on_rx()
        {
            for (int i = 0; i < 5; i++)
                rx_msg_buf[i] = (byte)(0 & 0xFF);

            on_Beacon_rx(rx_msg_buf);
        }

        private void comm_rx_thd()
        {
            int rx_cnt_last = 0;

            while (true)
            {
                Thread.Sleep(50);

                //test_on_rx();

                if (sPort.IsOpen)
                {
                    int rx_cnt = sPort.BytesToRead;

                    if ((rx_cnt > 0) && (rx_cnt_last == rx_cnt))
                    {
                        byte[] rx_buf = new byte[rx_cnt];//存放接收到的数据
                        sPort.Read(rx_msg_buf, 0, rx_cnt);

                        on_Beacon_rx(rx_msg_buf);

                        rx_cnt_last = 0;
                    }
                    else
                    {
                        rx_cnt_last = rx_cnt;
                    }
                }
            }
        }

        public void StartWok()
        {
            task_beacon_rx.Start();
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

    }
}
