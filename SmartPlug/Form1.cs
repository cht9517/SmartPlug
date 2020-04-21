using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartPlug
{
    public partial class FrmMain : Form
    {
        Random r1 = new Random();
        int axis_X = 0;
        double Press1 = 2;
        double Press2 = 2;

        public FrmMain()
        {
            InitializeComponent();
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            tab.SelectedIndex = 2;
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            
            Press1 += (r1.Next(50) - 25)/ 100.0;
            Press2 += (r1.Next(50) - 25)/ 100.0;

            if (Press1 < 0)
                Press1 = 0;
            if (Press2 < 0)
                Press2 = 0;
            if (Press1 > 25)
                Press1 = 25;
            if (Press2 > 25)
                Press2 = 25;

            chart1.Series[0].Points.AddXY(axis_X, Press1);
            chart1.Series[1].Points.AddXY(axis_X++, Press2);

            if (axis_X > chart1.ChartAreas[0].AxisX.Maximum)
            {
                chart1.ChartAreas[0].AxisX.Maximum += 50;
                chart1.ChartAreas[0].AxisX.Minimum = chart1.ChartAreas[0].AxisX.Maximum - 100;
            }
        }

        private void Button5_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
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

        private void Button6_Click(object sender, EventArgs e)
        {
            if (button6.Text.Contains("模拟接收ELF"))
            {
                button6.Text = "暂停接收ELF";
                timer1.Enabled = true;
                button6.BackColor = Color.Green;
            }
            else
            {
                button6.Text = "模拟接收ELF";
                timer1.Enabled = false;
                button6.BackColor = Color.Transparent;
            }
        }
    }
}
