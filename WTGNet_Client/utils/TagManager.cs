using log4net;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Quartz;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WTGNet_Client.utils
{
    /// <summary>
    /// 螺丝机工位点位的定时管理
    /// </summary>
    public class TagManager
    {

        private static readonly ILog log = LogManager.GetLogger(typeof(TagManager));
        private static readonly object padlock = new object();
        private static TagManager op40TagManager;

        private IScheduler scheduler;
        private TriggerBuilder triggerBuilder;
        public TagHelper tagHelper { get; set; }
        // 定时器已经开启的判断,
        private bool moniting = false;

        public TagManager(){
            LoadConfig();
        }

        /// <summary>
        /// 加载当前所需要的配置信息
        /// </summary>
        private void LoadConfig()
        {
            // string appCfg = AppCfg.GetStringVaue("plc:ip", "空");
        }

        public static TagManager Inst()
        {
            if (op40TagManager == null)
            {
                lock (padlock)
                {
                    if (op40TagManager == null)
                    {
                        op40TagManager = new TagManager();
                    }
                }
            }
            return op40TagManager;
        }
        public void Start()
        {
            if (scheduler == null || scheduler != null && (!scheduler.IsStarted || scheduler.IsShutdown))
            {
                InitScheduler();
                BeginMonitor();
                scheduler.Start();
                moniting = true;
            }
        }

        public void Stop()
        {
            if (scheduler != null && scheduler.IsStarted)
            {
                scheduler.Clear();
                scheduler.Shutdown(false);
                triggerBuilder = null;
                scheduler = null;
                moniting = false;
            }
        }
        Random rand = new Random();
        /// <summary>
        /// </summary>
        /// <param name="threadCount"></param>
        /// <param name="maxConcurrency"></param>
        /// <param name="misfire"></param>
        private void InitScheduler(string threadCount = "2", string maxConcurrency = "2", string misfire = "30000")
        {
            string schName = "sch_" + Guid.NewGuid().ToString();
            string schThreadName = "schThreadName" + DateTime.Now.ToLongTimeString();
            NameValueCollection schedulerConfig = new NameValueCollection {
                {"quartz.threadPool.threadCount", threadCount},
                {"quartz.threadPool.maxConcurrency", maxConcurrency},
                {"quartz.scheduler.threadName", schThreadName},
                {"quartz.scheduler.instanceName", schName},
                {"org.quartz.jobStore.misfireThreshold",misfire }, // 任务延迟判断阈值
                {"org.quartz.scheduler.makeSchedulerThreadDaemon","true" }
            };
            scheduler = SchedulerBuilder.Create(schedulerConfig).BuildScheduler().Result;
            triggerBuilder = TriggerBuilder.Create();
        }

        private void BeginMonitor()
        {
            if (moniting)
            {
                return;
            }
            ControlTags taglist = tagHelper.TagList;
            string plcId = taglist.Plc.Id;
            foreach (Tag tag in taglist.Tags)
            {
                if (tag.Interval > 0)
                {
                    // log.InfoFormat("点位监听: AddrName={0},Count={1}", tag.AddrName, tag.Count);
                    RegisterRepeatJob(plcId, tag);
                }
            }
        }
        private void RegisterRepeatJob(string devId,Tag tag, string jobGroup = "p_", string triggerGroup = "g_")
        {
            JobBuilder jobBuilder = JobBuilder.Create<TagReadJob>();
            jobBuilder.WithIdentity(tag.Id, (jobGroup+ devId));
            IJobDetail job = jobBuilder.Build();
            job.JobDataMap.Add("PlcId", devId);
            job.JobDataMap.Add("TagId", tag.Id);
            ITrigger trigger = triggerBuilder.ForJob(job.Key)
            .WithIdentity("jid" + tag.Id, (triggerGroup+devId))
            .WithSimpleSchedule(x => x
            .WithMisfireHandlingInstructionNextWithRemainingCount()
            .WithInterval(TimeSpan.FromMilliseconds(tag.Interval))
            .RepeatForever())
            .Build();

            scheduler.ScheduleJob(job, trigger);
        }

    }

    [DisallowConcurrentExecutionAttribute]
    public class TagReadJob : IJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TagReadJob));
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                AppEventBus bus = AppEventBus.Inst();
                JobKey key = context.JobDetail.Key;
                JobDataMap dataMap = context.JobDetail.JobDataMap;
                string plcId = dataMap.GetString("PlcId");
                string tagId = dataMap.GetString("TagId");
                bus.OnTagRead(plcId ,tagId);
                // log.InfoFormat("Job-id={0},tagId={1}",key,tagId);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult("fail");
            }
        }
    }
}
