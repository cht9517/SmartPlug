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
using System.Configuration;
using System.IO;

namespace SmartPlug
{
    public partial class FrmMain : Form
    {
        FileStream fs_log = null;
        StreamWriter sw_log = null;

        public const int CMD_BEACON_ON = 0x40;
        public const int CMD_BEACON_OFF = 0x00;
        public const int CMD_MEASURE = 0x80;
        public const int CMD_PLUG = 0xD0;//命令对调
        public const int CMD_PLUG_C = 0xE1;
        public const int CMD_UNPLUG = 0xE0;
        public const int CMD_UNPLUG_S = 0x88;
        public const int CMD_RING_COMPRESS = 0xC4;
        public const int CMD_RING_DECOMPRESS = 0x82;
        public const int CMD_SLEEP = 0x00;

        public byte[] Tool_Dat = new byte[15];

        Beacon beacon_up = new Beacon();
        Beacon beacon_down = new Beacon();

        Random r1 = new Random();

        bool anchor_OK = true;

        SplashScreen splash = new SplashScreen();

        System.Data.DataTable table_toolcfg = new DataTable("ToolCfgTable");


        private void ToolCfgTable_init()
        {
            table_toolcfg.Columns.Add(new DataColumn("name", System.Type.GetType("System.String")));
            table_toolcfg.Columns.Add(new DataColumn("value", System.Type.GetType("System.Int32")));
            table_toolcfg.Columns.Add(new DataColumn("remarks", System.Type.GetType("System.String")));
            table_toolcfg.Columns[0].ReadOnly = true;
            table_toolcfg.Columns[2].ReadOnly = true;

            table_toolcfg.Rows.Add("封堵最大时间", 30, "秒，范围：10~600");
            table_toolcfg.Rows.Add("解封最大时间", 30, "秒，范围：10~600");
            table_toolcfg.Rows.Add("备用解封最大时间", 30, "秒，范围：10~600");
            table_toolcfg.Rows.Add("环腔泄压最大时间", 30, "秒，范围：10~600");

            table_toolcfg.Rows.Add("环腔升压最大时间",  10, "秒，范围：10~100");
            table_toolcfg.Rows.Add("环腔升压最高压力", 100, "0.1MPa，范围：10~200");
            table_toolcfg.Rows.Add("环腔升压压力增量",  10, "0.1MPa，范围：10~200");
            table_toolcfg.Rows.Add("环腔升压保压时间",  10, "秒，范围：10~100");

            table_toolcfg.Rows.Add("油缸位移最大设定", 105, "mm，范围：1~109");
            table_toolcfg.Rows.Add("油缸位移最小设定", 5, "mm，范围：1~109");
            table_toolcfg.Rows.Add("系统溢流压力设定(暂未用)", 210, "0.1MPa，范围：10~250");
            table_toolcfg.Rows.Add("常规发射帧周期((暂未用)", 10, "秒，范围：10~30");
            table_toolcfg.Rows.Add("空闲发射帧周期((暂未用)", 30, "秒，范围：20~120");

            table_toolcfg.Rows.Add("信标停发循环次数", 20, "次，范围：20~500");
            table_toolcfg.Rows.Add("坐封步进位移", 5, "mm，范围：5~50");
            table_toolcfg.Rows.Add("溢流阀占空比设定", 65, "%，范围：0~100");

            dGv_test_Config.DataSource = table_toolcfg;
            dGv_test_Config.Columns[0].Width = 220;
            dGv_test_Config.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dGv_test_Config.Columns[1].Width = 100;
            dGv_test_Config.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dGv_test_Config.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dGv_test_Config.AllowUserToResizeColumns = false;
            dGv_test_Config.AllowUserToResizeRows = false;
        }

        public FrmMain()
        {
            InitializeComponent();

            splash.Show();
            //System.Threading.Thread.Sleep(3000);
            splash.Close();

            ToolCfgTable_init();

            Tsb.Instance.task_tsb_rx.Start();

            Tsb_s.Instance.TSB_s_Rx += this.onTsb_s_Rx;
            Tsb_s.Instance.task_tsb_rx.Start();

            beacon_up.Beacon_Rx += this.onBeacon_Rx_up;
            beacon_down.Beacon_Rx += this.onBeacon_Rx_down;
            beacon_up.StartWok();
            beacon_down.StartWok();

            timer1.Enabled = true;

            string last_proj = ConfigurationManager.AppSettings["Last_Proj_Dir"];
            
            if(!Directory.Exists(last_proj))
            {
                textBox_lastProj.Text = "";
                MessageBox.Show("工程文件导入失败，请手动选择打开工程或创建新工程！");
            }
            else
            {
                textBox_lastProj.Text = last_proj;

                fs_log = new FileStream(last_proj + "/proj.log", FileMode.Append);
                sw_log = new StreamWriter(fs_log, Encoding.Default);

                sw_log.WriteLine("####################################################################");
                log_info("打开工程");
            }


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

        private void btn_OpenProj_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择文件路径";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox_OpenProj.Text = dialog.SelectedPath;

                var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None); //打开配置文件
                cfg.AppSettings.Settings["Last_Proj_Dir"].Value = dialog.SelectedPath; ; //修改配置节
                cfg.Save(); //保存
                ConfigurationManager.RefreshSection("appSettings"); //更新缓存

                fs_log = new FileStream(dialog.SelectedPath + "/proj.log", FileMode.Append);
                sw_log = new StreamWriter(fs_log, Encoding.Default);
                sw_log.WriteLine("####################################################################");
                log_info("打开工程");
            }
        }

        private void log_info(string info)
        {
            sw_log.WriteLine(string.Format("{0:G}", System.DateTime.Now) + "  " + info);

            sw_log.Flush();
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



        private void Btn_testCMDSend_Click(object sender, EventArgs e)
        {
            byte[] code_buf = {0x80, 0xE0, 0xD0, 0x88, 0xC4, 0x82, 0x00, 0xF0, 0x40};

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
                dGv_test_para.Rows[0].Cells[1].Value = string.Format("{0:f1}", Tsb.rx_msg_buf[com_id, 9]*1.0);
                for (int i = 2; i < 10; i++)
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

            DateTime dt = (new DateTime(1970, 1, 1)).AddSeconds(time);

            MessageBox.Show("仪器时间：" + string.Format("{0:F}", dt));
        }

        private void Btn_testTimeRead_Click(object sender, EventArgs e)//读时间
        {

            if (Tsb.Instance.tsb_tx_frame(0x0f, 0x5b, null, read_test_time_cb))//诊断
            {
                ;
            }
        }

        private void Btn_testTimeSync_Click(object sender, EventArgs e)
        {
            uint time = (uint)((DateTime.Now.ToLocalTime().Ticks - 621355968000000000) / 10000000);

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
        
        private void Read_test_cfg_cb(int com_id)
        {
            for(int i=0; i<16; i++)
            {
                int val = ((Tsb.rx_msg_buf[com_id, 8 + 2 * i] & 0xff) << 8)
                            + (Tsb.rx_msg_buf[com_id, 9 + 2 * i] & 0xff);
                table_toolcfg.Rows[i].SetField("value", val);
            }

            System.Media.SystemSounds.Beep.Play();
        }

        private void Btn_testCfgRead_Click(object sender, EventArgs e)//读取配置参数
        {
            if (Tsb.Instance.tsb_tx_frame(0x0f, 0x5b, null, Read_test_cfg_cb))//诊断
            {
                ;
            }
        }
        private void Write_test_cfg_cb(int com_id)
        {
            MessageBox.Show("已成功写入配置！");
        }

            private void Btn_testCfgWrite_Click(object sender, EventArgs e)//写入配置参数
        {
            byte[] para_buf = new byte[32];

            for(int i=0; i<16; i++)
            {
                int val = (int)(table_toolcfg.Rows[i]["value"]);
                para_buf[2 * i + 0] = (byte)(val >> 8);
                para_buf[2 * i + 1] = (byte)(val & 0xFF);
            }

            if (Tsb.Instance.tsb_tx_frame(0x0f, 0xad, para_buf, Write_test_cfg_cb))
            {
            }
            else
            {
                MessageBox.Show("下发命令失败！");
            }
        }

        private void CMD_Perform(int cmd, string cmd_name)
        {
            string msg = "确定需要执行 " + cmd_name + "命令吗？";

            if (MessageBox.Show(msg, "警告", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.OK)
            {
                log_info("执行命令：" + cmd_name);

                if (Tsb_s.Instance.tsb_tx_frame(cmd))
                {
                    MessageBox.Show("成功发送命令：" + cmd_name);

                    Status_Indicator_Switch(cmd);

                    log_info("命令发送成功！");
                }
                else
                {
                    MessageBox.Show("命令发送失败！");

                    log_info("命令发送失败！");
                }
            }
        }

        private void Btn_CMD0_Click(object sender, EventArgs e)//测量参数
        {
            CMD_Perform(CMD_MEASURE, "《测量参数》");
        }

        private void Btn_CMD1_Click(object sender, EventArgs e)//坐封
        {
            CMD_Perform(CMD_PLUG, "《坐封》");
        }

        private void Btn_CMD2_Click(object sender, EventArgs e)//解封
        {
            CMD_Perform(CMD_UNPLUG, "《解封》");
        }

        private void Btn_CMD3_Click(object sender, EventArgs e)//备用解封
        {
            CMD_Perform(CMD_UNPLUG_S, "《备用解封》");
        }

        private void Btn_CMD4_Click(object sender, EventArgs e)//环腔打压
        {
            CMD_Perform(CMD_RING_COMPRESS, "《环腔打压》");
        }

        private void Btn_CMD5_Click(object sender, EventArgs e)//环腔泄压
        {
            CMD_Perform(CMD_RING_DECOMPRESS, "《环腔泄压》");
        }

        private void Btn_CMD7_Click(object sender, EventArgs e)//仪器休眠
        {
            CMD_Perform(CMD_SLEEP, "《仪器休眠》");
        }

        private void Status_Indicator_Switch(int cmd)
        {
            Lb_SI1.BackColor = Color.White;
            Lb_SI2.BackColor = Color.White;
            Lb_SI3.BackColor = Color.White;
            Lb_SI4.BackColor = Color.White;
            Lb_SI5.BackColor = Color.White;

            if (cmd == CMD_PLUG_C)//坐封完成
            {
                combo_CheckType.Visible = true;
                btn_PlugCheck.Visible = true;
            }
            else
            {
                combo_CheckType.Visible = false;
                btn_PlugCheck.Visible = false;
            }

            int Lb_pos_Y = Lb_SI1.Location.Y;
            int Lb_pos_X = Lb_SI1.Location.X;

            if (cmd == CMD_PLUG)
            {
                Lb_SI1.Text = "坐封指令";
                Lb_SI2.Text = "收到指令";
                Lb_SI3.Text = "系统压力建立";
                Lb_SI4.Text = "执行完毕";
                Lb_SI1.Location = new Point(Lb_pos_X, Lb_pos_Y);
                Lb_SI2.Location = new Point(Lb_SI1.Location.X + Lb_SI1.Size.Width + 1, Lb_pos_Y);
                Lb_SI3.Location = new Point(Lb_SI2.Location.X + Lb_SI2.Size.Width + 1, Lb_pos_Y);
                Lb_SI4.Location = new Point(Lb_SI3.Location.X + Lb_SI3.Size.Width + 1, Lb_pos_Y);
                Lb_SI1.BackColor = Color.Lime;
                Lb_SI2.BackColor = Color.Silver;
                Lb_SI3.BackColor = Color.Silver;
                Lb_SI4.BackColor = Color.Silver;
            }
            else if(cmd == CMD_PLUG_C)//坐封完成、验封开始
            {
                Lb_SI1.Text = "直接验封法：";
                Lb_SI2.Text = "P3>P1=P2";
                Lb_SI3.Text = "等待5分钟";
                Lb_SI4.Text = "P3>P1=P2";
                Lb_SI1.Location = new Point(Lb_pos_X, Lb_pos_Y);
                Lb_SI2.Location = new Point(Lb_SI1.Location.X + Lb_SI1.Size.Width + 1, Lb_pos_Y);
                Lb_SI3.Location = new Point(Lb_SI2.Location.X + Lb_SI2.Size.Width + 1, Lb_pos_Y);
                Lb_SI4.Location = new Point(Lb_SI3.Location.X + Lb_SI3.Size.Width + 1, Lb_pos_Y);
                Lb_SI1.BackColor = Color.Lime;
                if((Tool_Dat[6] > Tool_Dat[7]) && (Tool_Dat[6] > Tool_Dat[8]))
                    Lb_SI2.BackColor = Color.Lime;
                else
                    Lb_SI2.BackColor = Color.Red;
                Lb_SI3.BackColor = Color.Silver;
                Lb_SI4.BackColor = Color.Silver;
            }
            else if(cmd == CMD_UNPLUG)
            {
                Lb_SI1.Text = "解封指令";
                Lb_SI2.Text = "收到指令";
                Lb_SI3.Text = "系统压力建立";
                Lb_SI4.Text = "执行完毕";
                Lb_SI1.Location = new Point(Lb_pos_X, Lb_pos_Y);
                Lb_SI2.Location = new Point(Lb_SI1.Location.X + Lb_SI1.Size.Width + 1, Lb_pos_Y);
                Lb_SI3.Location = new Point(Lb_SI2.Location.X + Lb_SI2.Size.Width + 1, Lb_pos_Y);
                Lb_SI4.Location = new Point(Lb_SI3.Location.X + Lb_SI3.Size.Width + 1, Lb_pos_Y);
                Lb_SI1.BackColor = Color.Lime;
                Lb_SI2.BackColor = Color.Silver;
                Lb_SI3.BackColor = Color.Silver;
                Lb_SI4.BackColor = Color.Silver;
            }
            else if (cmd == CMD_UNPLUG_S)
            {

            }
            else if (cmd == CMD_RING_COMPRESS)
            {

            }
            else if (cmd == CMD_RING_DECOMPRESS)
            {

            }
            else if (cmd == CMD_SLEEP)
            {

            }
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
                //buf[2]:Mode
                toolStripStatusLabel5.Text = "仪器状态码：0x" + String.Format("{0:X}", buf[2]);

                textBox_S1.Text = string.Format("{0:f1}", buf[3] / 1.0);

                
                textBox_P1.Text = string.Format("{0:f1}", buf[4] / 10.0);
                textBox_P2.Text = string.Format("{0:f1}", buf[5] / 10.0);
                textBox_P3.Text = string.Format("{0:f1}", buf[6] / 10.0);
                textBox_P4.Text = string.Format("{0:f1}", buf[7] / 10.0);
                textBox_P5.Text = string.Format("{0:f1}", buf[8] / 10.0);
                textBox_P6.Text = string.Format("{0:f1}", buf[9] / 10.0);
                

                if ((buf[2] & 0xFE) == 0x80)//11bytes
                {
                    textBox_P7.Text = string.Format("{0:f1}", buf[10] / 10.0);
                    textBox_VBAT.Text = string.Format("{0:f1}", buf[11] / 10.0);
                    textBox_Temp.Text = string.Format("{0:f1}", buf[12] * 0.5);
                }
                else
                {
                    ;
                }
                
                DateTime t = DateTime.Now;
                chart1.Series[0].Points.AddXY(t.ToOADate(), buf[7] / 10.0);
                chart1.Series[1].Points.AddXY(t.ToOADate(), buf[8] / 10.0);
                chart1.Series[2].Points.AddXY(t.ToOADate(), buf[9] / 10.0);
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

                int rssi = (int)(buf[2] & 0xff);

                toolStripStatusLabel1.Text = string.Format("信号强度：{0:f1}%", rssi * 100.0 / 256 );

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

                int rssi = (int)(buf[2] & 0xff);

                toolStripStatusLabel1.Text = string.Format("信号强度：{0:f1}%", rssi * 100.0 / 256);

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
        private void button1_Click(object sender, EventArgs e)
        {
            CMD_Perform(CMD_BEACON_ON, "《信标开启》");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            byte[] buf = new byte[5];
            buf[1] = 1;
            buf[2] = 32;
            buf[3] = 30;
            onBeacon_Rx_up(buf);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            byte[] buf = new byte[5];
            buf[1] = 1;
            buf[2] = 10;
            buf[3] = 36;
            onBeacon_Rx_down(buf);
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            Status_Indicator_Switch(CMD_PLUG_C);
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("确定需要关闭软件吗？", "警告", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.OK)
            {
                if(fs_log != null)
                {
                    sw_log.Close();
                    fs_log.Close();
                }
            }
            else
            {
                e.Cancel = true;
            }
        }


    }
}
