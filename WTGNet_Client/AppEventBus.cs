
using log4net;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Collections;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using System.Reflection;

namespace WTGNet_Client
{
    public class AppEventBus {
        private static readonly ILog log = LogManager.GetLogger(typeof(AppEventBus));
        private static readonly object padlock = new object();
        private static AppEventBus eventBus;
        private int trigger_timeout = 3000;
        private Form1 appWin;
        private AppEventBus()
        {
            LoadConfig();
        }
        private void LoadConfig() {
            
            trigger_timeout = AppCfg.GetConfigInt("comunicationProps:triggerTimeout", trigger_timeout);
           
        }
        

        public static AppEventBus Inst()
        {
            if (eventBus == null)
            {
                lock (padlock)
                {
                    if (eventBus == null)
                    {
                        eventBus = new AppEventBus();
                    }
                }
            }
            return eventBus;
        }

        internal void SetWin(Form1 chWin) {
            this.appWin = chWin;
        }
       

        public void Start() {
           
        }

        public void Shutdown() {
           
        }

        /// <summary>
        ///  主入口
        /// </summary>
        /// <param name="tagId"></param>
        internal void OnTagRead(string plcId, string tagId)
        {

        }

    }
}
