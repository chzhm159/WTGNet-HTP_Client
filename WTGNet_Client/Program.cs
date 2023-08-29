using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WTGNet_Client
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main() {
            // https://www.lookskys.com/xywgsc/243.html
            // https://www.lookskys.com/uploads/allimg/20230323/1-230323093546349.zip
            // https://github.com/rossmann-engineering/EasyModbusTCP.NET/blob/master/EasyModbusClientExample/MainForm.cs
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
