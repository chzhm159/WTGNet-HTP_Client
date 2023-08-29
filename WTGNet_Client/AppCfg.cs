using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WTGNet_Client
{
    public class AppCfg
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AppCfg));
        private static readonly object padlock = new object();
        private static readonly AppCfg cfg = new AppCfg();
        private bool cached = false;
        public static string DayHour() {            
            DateTime myDateTime = DateTime.Now;
            string createTime = myDateTime.ToString("yyyy_MM_dd_HH");
            return createTime;

        }
        public static int GetConfigInt(string name, int defaultValue = 0) {
            string v = ConfigurationManager.AppSettings.Get(name);
            if (!string.IsNullOrEmpty(v)) {
                return Int32.Parse(v);
            } else {
                return defaultValue;
            }
        }
        public static string GetStringVaue(string name, string defaultValue = "none") {
            string v = ConfigurationManager.AppSettings.Get(name);
            if (!string.IsNullOrEmpty(v)) {
                return v;
            } else {
                return defaultValue;
            }
            
        }
    }
}
