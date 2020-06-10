using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;
using System.Speech.Synthesis;
using System.Runtime.InteropServices;
using System.Deployment.Internal.CodeSigning;


namespace SmartPlug
{
    public partial class FrmMain : Form
    {

        Beacon beacon_up = new Beacon();
        Beacon beacon_down = new Beacon();

        Random r1 = new Random();

        bool anchor_OK = true;

        public FrmMain()
        {
            InitializeComponent();

            Tsb.Instance.task_tsb_rx.Start();

            Tsb_s.Instance.TSB_s_Rx += this.onTsb_s_Rx;
            Tsb_s.Instance.task_tsb_rx.Start();

            beacon_up.Beacon_Rx += this.onBeacon_Rx_up;
            beacon_down.Beacon_Rx += this.onBeacon_Rx_down;
            beacon_up.StartWok();
            beacon_down.StartWok();

            timer1.Enabled = true;
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            dGv_test_para.Rows.Add(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            combo_testCMD.SelectedIndex = 0;
            combo_testCMD.DropDownStyle = ComboBoxStyle.DropDownList;

            string[] portList = System.IO.Ports.SerialPort.GetPortNames();

            List<ComboBox> combo_PortSel = new List<ComboBox>(4);
            combo_PortSel.Add(combo_PortSel1);
            combo_PortSel.Add(combo_PortSel2);
            combo_PortSel.Add(combo_PortSel3);
            combo_PortSel.Add(combo_PortSel4);

            for (int i = 0; i < 4; i++)
            {
                combo_PortSel[i].Items.Clear();
                combo_PortSel[i].Items.AddRange(portList);
                if (combo_PortSel[i].Items.Count > 0)
                    combo_PortSel[i].SelectedIndex = 0;
            }

            tab.SelectedIndex = 2;//显示主界面

            DateTime t = DateTime.Now;
            chart1.Series[0].Points.AddXY(t.ToOADate(), 0);
            chart1.Series[1].Points.AddXY(t.ToOADate(), 0);
            chart1.Series[2].Points.AddXY(t.ToOADate(), 0);
            chart1.ChartAreas[0].AxisX.Maximum = t.AddSeconds(0).ToOADate();
            chart1.ChartAreas[0].AxisX.Minimum = t.AddSeconds(-600).ToOADate();
        }

        private void Btn_PortOpen_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;

            Beacon beacon = null;
            ComboBox combobox = null;

            if (btn.Equals(btn_PortOpen1))
            {
                beacon = beacon_up;
                combobox = combo_PortSel1;
            }
            else if(btn.Equals(btn_PortOpen2))
            {
                beacon = beacon_down;
                combobox = combo_PortSel2;
            }


            if (beacon.port_is_open())
            {
                beacon.port_close();

                btn.Text = "打开";
                btn.BackColor = Color.Gray;
            }
            else
            {
                if (combobox.Text != "")
                {
                    try
                    {
                        beacon.port_open(combobox.Text);
                        btn.Text = "关闭";
                        btn.BackColor = Color.Green;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "串口输入错误");
                    }
                }
            }
        }

        private void btn_PortOpen3_Click(object sender, EventArgs e)
        {
            if (Tsb_s.Instance.port_is_open())
            {
                Tsb_s.Instance.port_close();

                btn_PortOpen3.Text = "打开";
                btn_PortOpen3.BackColor = Color.Gray;
            }
            else
            {
                if (combo_PortSel3.Text != "")
                {
                    try
                    {
                        Tsb_s.Instance.port_open(combo_PortSel3.Text);
                        btn_PortOpen3.Text = "关闭";
                        btn_PortOpen3.BackColor = Color.Green;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "串口输入错误");
                    }
                }
            }
        }

        private void btn_PortOpen4_Click(object sender, EventArgs e)
        {
            if (Tsb.Instance.port_is_open())
            {
                Tsb.Instance.port_close();

                btn_PortOpen4.Text = "打开";
                btn_PortOpen4.BackColor = Color.Gray;
            }
            else
            {
                if (combo_PortSel4.Text != "")
                {
                    try
                    {
                        Tsb.Instance.port_open(combo_PortSel4.Text);
                        btn_PortOpen4.Text = "关闭";
                        btn_PortOpen4.BackColor = Color.Green;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "串口输入错误");
                    }
                }
            }
        }



        private void Button7_Click(object sender, EventArgs e)
        {
            if(button7.Text.Contains("模拟授权"))
            {
                button7.Text = "解除授权";
                grpBoxCtrl.Enabled = true;
                button7.BackColor = Color.Red;
            }
            else
            {
                button7.Text = "模拟授权";
                grpBoxCtrl.Enabled = false;
                button7.BackColor = Color.Transparent;
            }
        }

        private void Btn_testCMDSend_Click(object sender, EventArgs e)
        {
            byte[] code_buf = {0x80, 0xE0, 0xD0, 0x88, 0xC4, 0x82, 0x00};

            int index = combo_testCMD.SelectedIndex;

            if(index >= 0)
            {
                byte cmd = code_buf[index];
                byte[] para_buf = new byte[1] { cmd };

                if (Tsb.Instance.tsb_tx_frame(0x0f, 0xAA, para_buf, null))
                {
                    MessageBox.Show("成功发送命令：" + cmd.ToString());
                }
                else
                    MessageBox.Show("命令发送失败！");
            }
            else
            {

            }




        }

        private void read_test_para_cb(int com_id)
        {
            this.Invoke(new Action(() =>
            {
                dGv_test_para.Rows[0].Cells[0].Value = string.Format("{0:X}", Tsb.rx_msg_buf[com_id, 8]);
                dGv_test_para.Rows[0].Cells[1].Value = string.Format("{0:P0}", Tsb.rx_msg_buf[com_id, 9]);
                for (int i = 2; i < 11; i++)
                {
                    byte val = Tsb.rx_msg_buf[com_id, i + 8];
                    float fval = val / 10.0f;
                    dGv_test_para.Rows[0].Cells[i].Value = string.Format("{0:f1}", fval);
                }
                dGv_test_para.Rows[0].Cells[10].Value = string.Format("{0:f1}", Tsb.rx_msg_buf[com_id, 18]*0.5);

                System.Media.SystemSounds.Beep.Play();
            }));

            Tsb.tsb_cb_buf[com_id] -= read_test_para_cb;
        }

        private void read_test_para(object source, ElapsedEventArgs e)
        {
            if (Tsb.Instance.tsb_tx_frame(0x0f, 0xDC, null, read_test_para_cb))
            {
                ;
            }
        }

        System.Timers.Timer timer_test_autoUpdate = null;
        private void Btn_testAutoUpdate_Click(object sender, EventArgs e)
        {
            if(btn_testAutoUpdate.Text == "自动读参数-开")
            {
                timer_test_autoUpdate = new System.Timers.Timer();
                timer_test_autoUpdate.Enabled = true;
                timer_test_autoUpdate.Interval = 1000; //执行间隔时间,单位为毫秒;
                timer_test_autoUpdate.Start();
                timer_test_autoUpdate.Elapsed += new System.Timers.ElapsedEventHandler(read_test_para);

                btn_testAutoUpdate.Text = "自动读参数-关";
                btn_testAutoUpdate.BackColor = Color.Green;
            }
            else
            {
                timer_test_autoUpdate.Enabled = false;

                btn_testAutoUpdate.Text = "自动读参数-开";
                btn_testAutoUpdate.BackColor = Color.Gray;
            }

        }

        private void read_test_time_cb(int com_id)
        {
            int time =   ((Tsb.rx_msg_buf[com_id, 40] & 0xff) << 24)
                       + ((Tsb.rx_msg_buf[com_id, 41] & 0xff) << 16)
                       + ((Tsb.rx_msg_buf[com_id, 42] & 0xff) << 8)
                       +  (Tsb.rx_msg_buf[com_id, 43] & 0xff);

            DateTime dt = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1)).AddSeconds(time);

            MessageBox.Show("仪器时间：" + string.Format("{0:F}", dt));
        }

        private void Btn_testTimeRead_Click(object sender, EventArgs e)
        {

            if (Tsb.Instance.tsb_tx_frame(0x0f, 0x5b, null, read_test_time_cb))//诊断
            {
                ;
            }
        }

        private void Btn_testTimeSync_Click(object sender, EventArgs e)
        {
            uint time = (uint)((DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000);

            byte[] para_buf = new byte[8];
            para_buf[4] = (byte)((time >> 24) & 0xff);
            para_buf[5] = (byte)((time >> 16) & 0xff);
            para_buf[6] = (byte)((time >>  8) & 0xff);
            para_buf[7] = (byte)((time >>  0) & 0xff);

            if (Tsb.Instance.tsb_tx_frame(0x0f, 0x55, para_buf, null))
            {
                ;
            }
            MessageBox.Show("已下发时间：" + string.Format("{0:F}", System.DateTime.Now));
        }

        private void Chart2_Click(object sender, EventArgs e)
        {

        }

        private void Btn_CMD0_Click(object sender, EventArgs e)//测量参数
        {
            int cmd = 0x80;
            if (Tsb_s.Instance.tsb_tx_frame(cmd))
            {
                MessageBox.Show("成功发送命令：" + cmd.ToString());
            }
            else
                MessageBox.Show("命令发送失败！");
        }

        private void Btn_CMD1_Click(object sender, EventArgs e)//坐封
        {
            string cmd_name = "《坐封》";
            string msg = "确定需要执行 " + cmd_name + "命令吗？";

            if (MessageBox.Show(msg, "警告", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.OK)
            {
                int cmd = 0xE0;
                if (Tsb_s.Instance.tsb_tx_frame(cmd))
                {
                    MessageBox.Show("成功发送命令：" + cmd.ToString());
                }
                else
                    MessageBox.Show("命令发送失败！");
            }
        }

        private void Btn_CMD2_Click(object sender, EventArgs e)//解封
        {

        }

        private void Btn_CMD3_Click(object sender, EventArgs e)//备用解封
        {

        }

        private void Btn_CMD4_Click(object sender, EventArgs e)//环腔打压
        {

        }

        private void Btn_CMD5_Click(object sender, EventArgs e)//环腔泄压
        {

        }

        private void Btn_CMD7_Click(object sender, EventArgs e)//仪器休眠
        {

        }

        private void sound_play(string msg)
        {
            SpeechSynthesizer ssh = new SpeechSynthesizer();
            string content = msg;
            ssh.Speak(content);
        }

        void onTsb_s_Rx(byte[] buf)
        {
            this.Invoke(new Action(() =>
            {
                textBox_S1.Text = string.Format("{0:f1}", buf[1] / 10.0);
                textBox_P1.Text = string.Format("{0:f1}", buf[2] / 10.0);
                textBox_P2.Text = string.Format("{0:f1}", buf[3] / 10.0);
                textBox_P3.Text = string.Format("{0:f1}", buf[4] / 10.0);
                //textBox_P1.Text = string.Format("{0:f1}", buf[2] / 10.0);

                DateTime t = DateTime.Now;
                chart1.Series[0].Points.AddXY(t.ToOADate(), buf[1] / 3.0);
                chart1.Series[1].Points.AddXY(t.ToOADate(), buf[2] / 7.0);
                chart1.Series[2].Points.AddXY(t.ToOADate(), buf[3] / 10.0);
                chart1.ChartAreas[0].AxisX.Maximum = t.AddSeconds(0).ToOADate();
                chart1.ChartAreas[0].AxisX.Minimum = t.AddSeconds(-600).ToOADate();

                //System.Media.SystemSounds.Beep.Play();
                Task.Run(() => sound_play("数据更新"));
            }));
        }

        void onBeacon_Rx_up(byte[] buf)
        {
            this.Invoke(new Action(() =>
            {
                anchor_OK = true;

                int rssi = (int)( ((buf[1] & 0xff) << 16) + ((buf[2] & 0xff) << 8) + (buf[3] & 0xff));

                toolStripStatusLabel1.Text = string.Format("信号强度：{0:f1}dB", rssi / 1000.0);

                toolStripProgressBar1.Value = 40;

                toolStripStatusLabel2.Text = "锚定正常";
                toolStripStatusLabel2.BackColor = Color.Chartreuse;

                Task.Run(() => sound_play("锚定正常"));

            }));
        }

        void onBeacon_Rx_down(byte[] buf)
        {
            this.Invoke(new Action(() =>
            {
                anchor_OK = false;

                int rssi = (int)(((buf[1] & 0xff) << 16) + ((buf[2] & 0xff) << 8) + (buf[3] & 0xff));

                toolStripStatusLabel1.Text = string.Format("信号强度：{0:f1}dB", rssi / 1000.0);

                toolStripProgressBar1.Value = 0;

                toolStripStatusLabel2.Text = "锚定异常";
                toolStripStatusLabel2.BackColor = Color.Red;

                Task.Run(() => sound_play("锚定异常!锚定异常!锚定异常!"));
            }));
        }
        private void Timer1_Tick(object sender, EventArgs e)
        {
            if(anchor_OK)
            {
                if (toolStripProgressBar1.Value > 0)
                {
                    toolStripProgressBar1.Value--;
                    if(toolStripProgressBar1.Value == 0)
                    {
                        toolStripStatusLabel2.BackColor = Color.DarkOrange;
                        toolStripStatusLabel2.Text = "信标丢失";

                        Task.Run(() => sound_play("信标丢失！信标丢失！信标丢失！信标丢失！信标丢失！"));
                    }
                        
                }
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            ;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            byte[] buf = new byte[5];
            buf[1] = 0;
            buf[2] = 0;
            buf[3] = 23;
            onBeacon_Rx_up(buf);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            byte[] buf = new byte[5];
            buf[1] = 0;
            buf[2] = 0;
            buf[3] = 36;
            onBeacon_Rx_down(buf);
        }
    }
}
