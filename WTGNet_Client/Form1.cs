using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WTGNet_Client.utils;

namespace WTGNet_Client
{
    public partial class Form1 : Form
    {
        Action<string> msgHandler;
        ModbusTCPHelper mth = new ModbusTCPHelper();
        public Form1() {
            InitializeComponent();
        }
        private void LoadConfig()
        {
            msgHandler = ShowMsghandler;
        }
        private void button1_Click(object sender, EventArgs e) {

            
            bool suc = mth.Connect();
            if (suc) {
                ShowMsg("链接成功");
                this.app_lb_state1.Text= "正常";
                this.app_lb_state1.BackColor= Color.ForestGreen;
            } else{
                ShowMsg("链接失败");
                this.app_lb_state1.Text = "失败";
                this.app_lb_state1.BackColor = Color.Red;
            }
        }
        public void ShowMsg(string msg)
        {
            if(this.InvokeRequired){
                this.BeginInvoke(msgHandler, msg);
            }
            else
            {
                ShowMsghandler(msg);
            }

        }
        public void ShowMsghandler(string msg)
        {
            this.console_1.AppendText(string.Format("{0}\r\n", msg));
            console_1.Select(console_1.TextLength - 1, 0);
            //滚动到控件光标处  
            console_1.ScrollToCaret();
        }

        private void app_btn_read_Click(object sender, EventArgs e) {
            string idxStr = textBox1.Text;
            string numStr = textBox4.Text;
            try {
                int idx = int.Parse(idxStr);
                int num = int.Parse(numStr);
                int[] data =mth.Read(idx, num);
                if (data != null) {
                    string dataStr = string.Join("-", data);
                    ShowMsg(string.Format("读取结果: {0}",dataStr));
                }
            }
            catch(Exception ex) { }

            
        }
    }
}
