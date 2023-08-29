using wz.vdl.helper;
using log4net;
using Sunny.UI;
using wz.vdl.entity;
using Sunny.UI.Win32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Collections;
using Newtonsoft.Json.Linq;
using Microsoft.VisualBasic.Logging;
using WhisperAFinal.Properties;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using HslCommunication;
using HslCommunication.Profinet.Knx;
using static Slapper.AutoMapper;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using static System.Windows.Forms.AxHost;
using System.Runtime.Intrinsics.X86;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using System.Reflection;

namespace WTGNet_Client
{
    public class AppEventBus {
        private static readonly ILog log = LogManager.GetLogger(typeof(AppEventBus));
        private static readonly object padlock = new object();
        private static AppEventBus eventBus;
        
        MySQLHelper dbClient;

        private int trigger_timeout = 3000;
        /// <summary>
        /// 回写plc结果之后,等待此时间后再次读取plc是否归零 3500ms
        /// </summary>
        private int appResetWait = 30;
        private string mes_code_exception = "mes_exception";
        private void LoadConfig() {
            dbClient = MySQLHelper.SingleInstance();
            trigger_timeout = AppCfg.GetIntVaue("comunicationProps:triggerTimeout", trigger_timeout);
            appResetWait = AppCfg.GetIntVaue("comunicationProps:appResetWait", appResetWait);
        }
        /// <summary>
        /// PLC 扫码完成,触发上位机读取信号
        /// </summary>
        private int PLC_Trigger_Value = 100;
        private int PLC_Trigger_Value_Old = 1;
        /// <summary>
        /// 设备初始化时 物料校验 触发信号
        /// </summary>
        private int PLC_Trigger_MaterialInitCheck = 1;
        /// <summary>
        /// 设备生产过程中 物料缺失 报警后校验 触发信号
        /// </summary>
        private int PLC_Trigger_MaterialEmtypCheck = 2;
        /// <summary>
        /// Main 主入口
        /// </summary>
        /// <param name="tagId"></param>
        internal void OnTagRead(string plcId,string tagId)
        {
            if (AppCfg.Inst().Debug) {
                return;
            }
            // log.DebugFormat("PLC={0},地址={1},触发",plcId,tagId);
            if (string.IsNullOrEmpty(tagId)) {
                return;
            }
            Tag tag = PlcGroup.Get().GetTag(plcId,tagId);
            if (tag == null) {
                return;
            }
            bool isNewMes = AppCfg.Inst().NewMes;
            if (plcId.Equals("st1", StringComparison.OrdinalIgnoreCase)) {
                if (!isNewMes) return;
                Station_1_Processor(plcId, tag);
            } else if (plcId.Equals("st2", StringComparison.OrdinalIgnoreCase)) {
                if (!isNewMes) return;
                Station_2_Processor(plcId, tag);
            } else if (plcId.Equals("st3", StringComparison.OrdinalIgnoreCase)) {
                if (!isNewMes) return;
                Station_3_Processor(plcId, tag);
            } else if (plcId.Equals("st4", StringComparison.OrdinalIgnoreCase)) {
                if (!isNewMes) return;
                Station_4_Processor(plcId, tag);
            } else if(plcId.Equals("st5", StringComparison.OrdinalIgnoreCase)) {
                if (!isNewMes) return;
                Station_5_Processor(plcId, tag);
            } else if (plcId.Equals("st6", StringComparison.OrdinalIgnoreCase)) {
                if (!isNewMes) return;
                Station_6_Processor(plcId, tag);
            } else if (plcId.Equals("st7", StringComparison.OrdinalIgnoreCase)) {
                Station_7_Processor(plcId, tag);
            }
        }

        #region 工位-1 逻辑处理
        private void Station_1_Processor(string plcId,Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station1HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_mat_code_trigger", StringComparison.OrdinalIgnoreCase)) {
                // 料斗码扫码,获取叠片信息
                OnMatToolCodeTrigger(plcId,tag);
            } else if (tag.Id.Equals("plc_trans_code_trigger", StringComparison.OrdinalIgnoreCase)) {
                // 流拉工装码扫码,绑定工装码
                OnTransCodeTrigger(plcId, tag);
            } else if (tag.Id.Equals("plc_lvjiao_trigger", StringComparison.OrdinalIgnoreCase)) {
                // 绿胶校验
                OnLvjiaoTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_cutter_trg", StringComparison.OrdinalIgnoreCase)) {
                // 绿胶裁刀寿命数据读取
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cutter_info");
                OnLvjiaoCaidaoTrigger(plcHelper,tag, dataTag);
            }
        }
        private int plc1State = -1;
        private short[] appAckRest = new short[1] { 0 };
        private string mes_code_suc = "0";

        
        /// <summary>
        /// 料斗码 处理逻辑进行中标记位
        /// </summary>
        private int _matToolCodeProcessing = -1;
        /// <summary>
        /// 工位-1 上料工装(料斗)码校验
        /// 
        /// </summary>
        /// <param name="tag"></param>
        public void OnMatToolCodeTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. [略过]写入上位机收到信号
                // 2. 读取 料斗码
                // 3. 通过 料斗码 查询 MES 接口,获取叠片信息
                // 4. 写会PLC校验结果;100 表示正常,200表示NG,300表示异常;
                // 5. 读取plc触发地址是否归零
                // 6. 上位机归零
                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
                if (!trigger.IsSuccess ) {
                    return;
                }
                ushort currentValue = trigger.Content[0];
                if (currentValue != PLC_Trigger_Value) return;
                if (!Need2ReadData(ref _matToolCodeProcessing, interval)) {
                    return;
                }
                log.InfoFormat("料斗码: 扫码完成,开始校验,plc触发值={0} >>>", currentValue);
                ShowMsg_ST1(string.Format("料斗码: 扫码完成,开始校验,plc触发值={0} >>>", currentValue));
                Tag appAckTag = plcG.GetTag(plcId, "app_mat_ack");
                
                // 获取批次号
                string planBatch = MesContext.getCtx().PlanBatch;
                if (string.IsNullOrEmpty(planBatch)) {
                    log.InfoFormat("错误: 料斗码扫码完成: 批次号为空,请先验证批次号");
                    // ShowMsg_ST1("错误: 料斗码扫码完成: 批次号为空,请先输入批次号 并验证通过");
                }

                // 料斗码(料斗码)
                Tag coceTad = plcG.GetTag(plcId, "plc_mat_code");
                HslCommunication.OperateResult<string> matCode = plcHelper.ReadString(coceTad.AddrName, (ushort)coceTad.Count);
                string vehicleNo = string.IsNullOrEmpty(matCode.Content) ? string.Empty : matCode.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "");

                if (!matCode.IsSuccess || string.IsNullOrEmpty(vehicleNo)) {
                    string msg = string.Format("错误: 料斗码 读取失败 或者 料斗码为空.");
                    log.Error(msg);
                    ShowMsg_ST1(msg);
                }
                              
                MatList matCheck = MesHelper.CheckMatToolCode(planBatch, vehicleNo);               
                string checkMsg = string.Format("料斗码:[{0}],MES校验code=:{1},msg={2} ", vehicleNo, matCheck.code, matCheck.rawResp);
                log.Info(checkMsg);
                ShowMsg_ST1(checkMsg);

                if (matCheck != null && matCheck.data != null && matCheck.data.Count != 4) {
                    string err = string.Format("错误: 料斗码:[{0}],绑定叠片数量不足4个!,此料斗NG排出", vehicleNo);
                    log.Info(err);
                    ShowMsg_ST1(err);
                }
                // 生成数据记录,离线模式下需要构造1条虚拟记录,以便后续方便差错
                bool dateValid = AddNewProduct(vehicleNo, matCheck);

                short[] _matToolCodeCheckResult = new short[1] { 100 };
                short appResponseCode = 100;
                // 如果是在线模式下,按照mes返回值写入plc 100:正常,200:异常
                bool mesSuc = mes_code_suc.Equals(matCheck.code, StringComparison.OrdinalIgnoreCase);
                bool mesDataNumSuc = (matCheck.data!=null && matCheck.data.Count==4);
                if (AppCfg.Inst().MesisOnline && mesSuc && mesDataNumSuc && dateValid) {
                    // 在线模式下,mes返回成功结果,数量4个,表示正常
                    appResponseCode = 100;
                } else if (AppCfg.Inst().MesisOnline && (!mesSuc || !mesDataNumSuc || !dateValid)) {
                    // 在线模式下,mes返回不成功,或者数量小于4个,表示异常NG 排出,如果存在还未上传解绑的数据,需要ng排除
                    appResponseCode = 200;
                } else if (!AppCfg.Inst().MesisOnline) {
                    // 离线模式下按照ok处理
                    appResponseCode = 100;
                }
                _matToolCodeCheckResult[0] = appResponseCode;
                HslCommunication.OperateResult appResponse = plcHelper.WriteShort(appAckTag.AddrName, _matToolCodeCheckResult,true);
                Thread.Sleep(appResetWait);
                FinishAck(plcHelper, addr, count, 0, appAckTag.AddrName, appAckRest);              
                
                this.appWin.OnMatToolCode(vehicleNo, matCheck);
                log.InfoFormat("料斗码: 料斗码校验完成,回写plc结果值={0},是否成功:{1} <<<", appResponseCode, appResponse.IsSuccess);
                Interlocked.Exchange(ref _matToolCodeProcessing, -1);
                ShowMsg_ST1(string.Format("料斗码校验完成,回写PLC结果值:{0},是否成功:{1} <<<", appResponseCode, appResponse.IsSuccess));
            } catch (Exception  e) {
                log.InfoFormat("料斗码: 异常 !!! {0},{1}", e.Message,e.StackTrace);
                Interlocked.Exchange(ref _matToolCodeProcessing, -1);
            }
        }
        /// <summary>
        /// 流拉工装码 处理流程开始标记
        /// </summary>
        private int _transToolCodeProcessing = -1;
        public void OnTransCodeTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 读取 流拉工装码,并完成绑定
                // 2. 写会PLC校验结果;100 表示正常,200表示NG,300表示异常;
                // 5. 读取plc触发地址是否归零
                // 6. 上位机归零

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
                
                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (currentValue != PLC_Trigger_Value) return;
                if (!Need2ReadData(ref _transToolCodeProcessing, interval)) return;

                // 工装扫码绑定完成
                log.InfoFormat("流拉工装: 扫码完成,plc触发值={0} >>>", currentValue);
                Tag appAckTag = plcG.GetTag(plcId, "app_trans_ack");
                ShowMsg_ST1(string.Format("流拉工装: 扫码完成,plc触发值={0} >>>", currentValue));
                // 料斗码
                Tag coceTad = plcG.GetTag(plcId, "plc_tmat_code");
                HslCommunication.OperateResult<string> matCode = plcHelper.ReadString(coceTad.AddrName, (ushort)coceTad.Count,true);
                string matCodeStr= string.IsNullOrEmpty(matCode.Content) ? string.Empty : matCode.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "");
                
                bool matCodeReadSuc = true;
                if (!matCode.IsSuccess || string.IsNullOrEmpty(matCodeStr)) {
                    matCodeReadSuc = false;
                    string msg =  string.Format("流拉工装: 料斗码 为空,或者读取失败! 通信={0}", matCode.IsSuccess);
                    log.Error(msg);
                    ShowMsg_ST1(msg);
                }
                
                // 流拉工装码
                Tag transCoedTad = plcG.GetTag(plcId, "plc_trans_code");
                HslCommunication.OperateResult<string> transCodeRet = plcHelper.ReadString(transCoedTad.AddrName, (ushort)transCoedTad.Count, true);
                string transCode = string.IsNullOrEmpty(transCodeRet.Content) ? string.Empty : transCodeRet.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "");

                bool transCodeReadSuc = true;
                if (!transCodeRet.IsSuccess || string.IsNullOrEmpty(transCode)) {
                    transCodeReadSuc = false;
                    string msg = string.Format("流拉工装: 流拉工装码为空,或者读取失败! 通信={0}", transCodeRet.IsSuccess);
                    log.Error(msg);
                    ShowMsg_ST1(msg);
                }
                // 叠片在工装码中的位置
                Tag cIdx = plcG.GetTag(plcId, "plc_trans_code_idx");
                HslCommunication.OperateResult<ushort[]> cIdxRet = plcHelper.ReadUInt16(cIdx.AddrName, (ushort)cIdx.Count,true);
                ushort idxValue = cIdxRet.Content[0];
                if (!trigger.IsSuccess) {
                    string msg = string.Format("流拉工装: 工装码位置信息读取失败!");
                    log.Error(msg);
                    ShowMsg_ST1(msg);
                }
                // 流拉工装码绑定到对应的2个产品上
                List<MesEntity> bindProd = BindTransToolCode(transCode, matCodeStr, idxValue);

                // 写回结束状态
                // 100 2个码都读到了,
                // 101 仅读到了 料斗码
                // 102 仅读到了 流拉工装码
                // 103 料斗码与工装码 都没读到
                short[] transCodeBindFinish = new short[1] { 100 };
                if(matCodeReadSuc && transCodeReadSuc) {
                    // 料斗码,工装码都读取成功,且不为空
                    transCodeBindFinish[0] = 100;
                } else if (matCodeReadSuc && !transCodeReadSuc) {
                    // 料斗码成功,工装码失败
                    transCodeBindFinish[0] = 101;
                } else if (!matCodeReadSuc && transCodeReadSuc) {
                    // 料斗码成功,工装码失败
                    transCodeBindFinish[0] = 102;
                } else if (!matCodeReadSuc && !transCodeReadSuc) {
                    // 料斗码成功,工装码失败
                    transCodeBindFinish[0] = 103;
                }

                OperateResult ackRet = plcHelper.WriteShort(appAckTag.AddrName, transCodeBindFinish, true);
                Thread.Sleep(appResetWait);
                // 完成归零
                FinishAck(plcHelper, addr, count, 0, appAckTag.AddrName, appAckRest);
                // 更新界面
                string ackMsg = string.Format("流拉工装:[{0}]绑定料斗:[{1}],叠片位置:[{2}],回写plc结果值:{3},是否成功:{4} <<<", transCode, matCodeStr, idxValue, transCodeBindFinish[0], ackRet.IsSuccess);

                ShowMsg_ST1(ackMsg);
                // this.appWin.OnTransToolCode(bindProd);
                log.Info(ackMsg);
                Interlocked.Exchange(ref _transToolCodeProcessing, -1);
                
            } catch(Exception e) {
                log.InfoFormat("流拉工装: 异常{0},{1}!!!", e.Message,e.StackTrace);
                Interlocked.Exchange(ref _transToolCodeProcessing, -1);
            }
        }
        /// <summary>
        /// 设备初始化时 物料校验 触发值 =1 
        /// </summary>
        private ushort InitMaterialCheck = 1;
        /// <summary>
        /// 物料缺料时 物料校验 触发值 =2
        /// </summary>
        private ushort MaterialEmptyCheck = 2;
        private int _lvjiaoChecker = -1;
        public void OnLvjiaoTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 绿胶 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!( (currentValue == InitMaterialCheck ) || (currentValue== MaterialEmptyCheck) )) return;

                if (!Need2ReadData(ref _lvjiaoChecker, interval)) {
                    return;
                }
                OnQueryLvjiao();

                log.InfoFormat("物料: [绿胶] MES校验 完成 >>>");
                Interlocked.Exchange(ref _lvjiaoChecker, -1);
            } catch (Exception e) {
                log.InfoFormat("物料: [绿胶] MES校验 : 异常{0},{1}!!!", e.Message, e.StackTrace);
                Interlocked.Exchange(ref _lvjiaoChecker, -1);
            }
        }
        private int lvjiaocaidao = -1;
        public void OnLvjiaoCaidaoTrigger(PlcHelper plcHelper, Tag trg, Tag data) {
            try {
                // 1. 绿胶 裁刀寿命校验
                // 2. 上位机 读取信息,上抛 MES  即可.

                OperateResult<ushort[]> trgRet = plcHelper.ReadUInt16(trg.AddrName, (ushort)trg.Count);
                OperateResult<int[]> dataRet = plcHelper.ReadInt32(data.AddrName, (ushort)data.Count);
                if (!trgRet.IsSuccess || !dataRet.IsSuccess) return;

                ushort trgValue = trgRet.Content[0];
                int used = dataRet.Content[0];
                // int warning = dataRet.Content[1];

                int endLine = dataRet.Content[1];
                // log.InfoFormat("工夹具: [绿胶裁刀] 使用数据: 实际使用={0},寿命线={1}", used, endLine);
                MesContext mctx = MesContext.getCtx();
                BaseResponse resp = mctx.UploadFixtureData("lvjiaodaopian", used);
                // log.InfoFormat("工夹具: [绿胶裁刀] MES 上传: code={0},msg={1}",resp.code,resp.msg);

                lvjiaocaidao = FixtureEndlife(trgValue, endLine, used, lvjiaocaidao, "lvjiaodaopian");
                //if ((trgValue == 1) || ((endLine - used) < 1)) {
                //    if(lvjiaocaidao == -1) {
                //        lvjiaocaidao = 1;
                //    } else {
                //        lvjiaocaidao = 9;
                //    }
                //    FixtureEndlife(trgValue,endLine,used, lvjiaocaidao,"lvjiaodaopian");
                //} else {
                //    lvjiaocaidao = -1;
                //}
            } catch (Exception e) {
                log.InfoFormat("工夹具: [绿胶裁刀] 寿命处理异常{0},{1}!!!", e.Message, e.StackTrace);
                
            }
        }
        /// <summary>
        /// 触发值,是否报警,寿命线,使用数值,是否已经触发弹框的标记,工夹具标识符
        /// </summary>
        /// <param name="trgV"></param>
        /// <param name="endLine"></param>
        /// <param name="used"></param>
        /// <param name="flag"></param>
        /// <param name="fixTag"></param>
        /// <returns></returns>
        private int FixtureEndlife(int trgV,int endLine,int used,int flag,string fixTag) {
            if ((trgV == 1) || ((endLine - used) < 1)) {
                if (flag == -1) {
                    flag = 1;
                } else {
                    flag = 9;
                }

                this.toolsWin.BeginInvoke(this.toolsWin.OnFixtureEndLife, fixTag, flag);
            } else {
                flag = -1;
            }
            return flag;
        }
        #region 产品信息绑定功能
        /// <summary>
        /// 在7单元,每次初始化校验的时候,自动读取一次
        /// </summary>
        public void ReadDevWeight() {
            try { 
                string plcId = "st7";
                Tag minTag1 = PlcGroup.Get().GetTag(plcId, "weight1_std_min");
                Tag maxTag1 = PlcGroup.Get().GetTag(plcId, "weight1_std_max");

                Tag minTag2 = PlcGroup.Get().GetTag(plcId, "weight2_std_min");
                Tag maxTag2 = PlcGroup.Get().GetTag(plcId, "weight2_std_max");


                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                OperateResult<float[]> stdMinRet1 = plcHelper.ReadFloat(minTag1.AddrName,(ushort)minTag1.Count);
                OperateResult<float[]> stdMaxRet1 = plcHelper.ReadFloat(maxTag1.AddrName, (ushort)maxTag1.Count);

                OperateResult<float[]> stdMinRet2 = plcHelper.ReadFloat(minTag2.AddrName, (ushort)minTag2.Count);
                OperateResult<float[]> stdMaxRet2 = plcHelper.ReadFloat(maxTag2.AddrName, (ushort)maxTag2.Count);

                MesContext mesCtx = MesContext.getCtx();
                ProdProperty pp1 = new ProdProperty();
                pp1.status = "-1";
                string devStdCompany = AppCfg.GetStringVaue("stdCompany", "g"); 
                if (stdMinRet1.IsSuccess) {
                    pp1.min = stdMinRet1.Content[0];
                    pp1.company = devStdCompany;
                    pp1.status = "0";
                    string msg = string.Format("设备端,产品1,重量下限: {0} {1}", pp1.min, devStdCompany);
                    
                    ShowMsg_Line(msg);
                    log.Info(msg);
                }
                if (stdMaxRet1.IsSuccess) {
                    pp1.max = stdMaxRet1.Content[0];
                    pp1.company = devStdCompany;
                    pp1.status = "0";
                    string msg = string.Format("设备端,产品1,重量上限: {0} {1}", pp1.max, devStdCompany);

                    ShowMsg_Line(msg);
                    log.Info(msg);
                }
                
                ProdProperty pp2 = new ProdProperty();
                pp2.status = "-1";
                
                if (stdMinRet2.IsSuccess) {
                    pp2.min = stdMinRet2.Content[0];
                    pp2.company = devStdCompany;
                    pp2.status = "0";
                    string msg = string.Format("设备端,产品2,重量下限:{0} {1}", pp2.min, devStdCompany);
                    ShowMsg_Line(msg);
                    log.Info(msg);
                }
                if (stdMaxRet2.IsSuccess) {
                    pp2.max = stdMaxRet2.Content[0];
                    pp2.company = devStdCompany;
                    pp2.status = "0";
                    string msg = string.Format("设备端,产品2,重量上限: {0} {1}", pp2.max, devStdCompany);
                    ShowMsg_Line(msg);
                    log.Info(msg);
                }
                mesCtx.DevWeightStand1 = pp1;
                mesCtx.DevWeightStand2 = pp2;
                this.appWin.UpdateDevProdWeightStand();
            } catch (Exception ex) {
                log.InfoFormat("设备端重量上下限读取异常:{0},{1}",ex.Message,ex.StackTrace);
            }
        }
        // 防止中间断电后, 产品绑定信息丢失,更新后就通过文件保存
        string bingdingDataCachePath = Path.Combine("bingding.json");
        
        public void Save2File(string data) {
            try {
                lock (cacheFileLock) {
                    using (FileStream fs = new FileStream(
                   path: bingdingDataCachePath,
                   mode: FileMode.OpenOrCreate,
                   access: FileAccess.ReadWrite,
                   share: FileShare.None,
                   bufferSize: 4096,
                   useAsync: false)) {

                        var bits = Encoding.UTF8.GetBytes(data);
                        fs.Write(bits, 0, bits.Length);
                    }
                }
            } catch(Exception e) {
                log.InfoFormat("绑定数据缓存写入文件异常: {0},{1}",e.Message
                    ,e.StackTrace);
            }
        }
        public void LoadProducCache(string cachePath) {
            lock (cacheFileLock) {
                if(File.Exists(cachePath)) {
                    using (StreamReader file = File.OpenText(cachePath)) {
                        JsonSerializer serializer = new JsonSerializer();
                        JsonReader reader = new JsonTextReader(file);
                        List<MesEntity> localCache = serializer.Deserialize<List<MesEntity>>(reader);
                        if (localCache != null) {
                            prodCace = localCache;
                        } else {
                            log.Warn("产品缓存文件数据为空.");
                        }
                    }
                }
            }
        }
        public  List<MesEntity> prodCace = new List<MesEntity>();
        private void RemoveCacheItem(MesEntity item) {
            // 在此动作之后,就不需要工装码了.完全通过 barcode (产品码/钢壳码) 来绑定
            lock (cacheLock) {
                // 尝试删除3次,防止内存不断的增长.
                if(item == null) {
                    log.InfoFormat("缓存中未能找到对应的绑定记录,无法删除");
                    return;
                }
                log.InfoFormat("删除缓存中绑定记录: 料斗码:[{0}],流拉工装码:[{1}],产品码:[{2}]", item.vehicleNo, item.transCode,item.barcode);
                if (prodCace != null && !prodCace.Remove(item)) {
                    if (!prodCace.Remove(item)) {
                        prodCace.Remove(item);
                    }
                }
            }
           
        }
        private int ReleaseInvalidData(string vehicleNo) {
            int invalidNum = prodCace.RemoveAll(item => {
                bool matchd = string.Equals(vehicleNo, item.vehicleNo);
                if (matchd) {
                    string msg = string.Format("料斗码:[{0}] 未解绑,重复扫码,本条记录忽略:叠片Id:{1},对应料斗:{2}.", vehicleNo, item.id, item.vehicleNo);
                    log.Warn(msg);
                }        
                return matchd;
            });
            log.InfoFormat("料斗码绑定重复数据数量:{0}", invalidNum);
            return invalidNum;
        }
        private static readonly object cacheLock = new object();
        private static readonly object cacheFileLock = new object();
        /// <summary>
        /// 扫描上料工装,创建新产品记录
        /// </summary>
        /// <param name="planBatch"></param>
        /// <param name="vehicleNo"></param>
        /// <param name="matList"></param>
        public bool AddNewProduct(string vehicleNo, MatList matList) {
            // prodCace 此时 上料工装重复流转回来后,如果还保留原有数据会导致,一个上料工装绑定超过4个产品.
            // 例如 v:1,v:2,v:3,v:4,v:1,v:2. 那么下一步流拉工装码绑定时就会找到超过2条的数据.所以此时应该先清除此上料工装的历史数据
            // 也就是解绑
            // 0 是
            bool state = false;
            lock (cacheLock) {
                bool hasReuse = (ReleaseInvalidData(vehicleNo)>0);
                if (hasReuse) {
                    log.ErrorFormat("存在未解绑的料斗:[{0}],NG排出", vehicleNo);
                    // 此料斗存在未解绑的数据.
                    return state=false;
                }
                if (matList.data !=null && matList.data.Count ==4) {
                    MesContext mesCtx = MesContext.getCtx();
                    string planBatch = mesCtx.PlanBatch;
                    string model = mesCtx.Model;
                    string resourceNo = mesCtx.DeviceNo;
                    string User = mesCtx.User;
                    string createSql = "insert into  products " +
                        "(myId, planBatch,model,resourceNo,createBy,vehicleNo,positon,id,upload,online,paperPn,paperPnPlanBatch,cPn,cPlanBatch,aPn,aPlanBatch,paperWidePn,paperWidePnPlanBatch,paperNarrowPn,paperNarrowPnPlanBatch,loamCakePn,loamCakePnPlanBatch,cotsPn,cotsPlanBatch,downCakePn,downCakePnPlanBatch,mesStdMin,mesStdMax,mesStdCompany,devStdMin,devStdMax,devStdCompany,devStdMin2,devStdMax2,cansCodeValid,cansCodeValidMsg,updateTime,createTime) values " +
                        "(@myId,@planBatch,@model,@resourceNo,@createBy,@vehicleNo,@positon,@id,0,@online,@paperPn,@paperPnPlanBatch,@cPn,@cPlanBatch,@aPn,@aPlanBatch,@paperWidePn,@paperWidePnPlanBatch,@paperNarrowPn,@paperNarrowPnPlanBatch,@loamCakePn,@loamCakePnPlanBatch,@cotsPn,@cotsPlanBatch,@downCakePn,@downCakePnPlanBatch,@mesStdMin,@mesStdMax,@mesStdCompany,@devStdMin,@devStdMax,@devStdCompany,@devStdMin2,@devStdMax2,@cansCodeValid,@cansCodeValidMsg,@updateTime,@createTime);";
                    matList.data.ForEach((Action<Mat>)(item => {

                        item.vehicleNo = vehicleNo;
                        MesEntity p = new MesEntity();
                        // 数据是在线还是离线模式下生成
                        if (AppCfg.Inst().MesisOnline) {
                            p.online = 1;
                        }
                        // 本地主键,后续依靠此id,查找绑定数据
                        p.myId = Guid.NewGuid().ToString(); 
                        // 批次号
                        p.planBatch = planBatch;
                        // 料号
                        p.model = model;
                        // 设备编号
                        p.resourceNo = resourceNo;
                        // 生产人
                        p.createBy = User;
                        // 料斗码(料斗码)
                        p.vehicleNo = vehicleNo;

                        p.positon = item.position;
                        p.id = item.id;
                       
                        // 绿胶 数据
                        p.paperPn = GetMaterialCode(mesCtx.Lvjiao);
                        p.paperPnPlanBatch = GetMaterialCode(mesCtx.Lvjiao);
                        // 正极耳
                        p.cPn = GetMaterialCode(mesCtx.Zhengjier);
                        p.cPlanBatch = GetMaterialCode(mesCtx.Zhengjier);
                        // 负极耳
                        p.aPn = GetMaterialCode(mesCtx.Fujier);
                        p.aPlanBatch = GetMaterialCode(mesCtx.Fujier);
                        // 茶胶-宽
                        p.paperWidePn = GetMaterialCode(mesCtx.ChajiaoK);
                        p.paperWidePnPlanBatch = GetMaterialCode(mesCtx.ChajiaoK);
                        // 茶胶-窄
                        p.paperNarrowPn = GetMaterialCode(mesCtx.ChajiaoZ);
                        p.paperNarrowPnPlanBatch = GetMaterialCode(mesCtx.ChajiaoZ);
                        // 上盖
                        p.loamCakePn = GetMaterialCode(mesCtx.Shanggai);
                        p.loamCakePnPlanBatch = GetMaterialCode(mesCtx.Shanggai);
                        // 密封圈
                        p.cotsPn = GetMaterialCode(mesCtx.Mifengquan);
                        p.cotsPlanBatch = GetMaterialCode(mesCtx.Mifengquan);
                        // 下盖
                        p.downCakePn = GetMaterialCode(mesCtx.Xiagai);
                        p.downCakePnPlanBatch = GetMaterialCode(mesCtx.Xiagai);
                        // 绑定Mes端重量上下限
                        if (mesCtx.WeightStand != null) {
                            p.mesStdMin = mesCtx.WeightStand.min.ToString();
                            p.mesStdMax = mesCtx.WeightStand.max.ToString();
                            p.mesStdCompany = mesCtx.WeightStand.company;
                        }
                        // 绑定设备端,产品1 ,重量上下限配置
                        if(mesCtx.DevWeightStand1!=null && "0".Equals(mesCtx.DevWeightStand1.status)) {
                            p.devStdMin = mesCtx.DevWeightStand1.min.ToString();
                            p.devStdMax = mesCtx.DevWeightStand1.max.ToString();
                            p.devStdCompany = mesCtx.DevWeightStand1.company;
                        }
                        // 绑定设备端,产品2 ,重量上下限配置
                        if (mesCtx.DevWeightStand2 != null && "0".Equals(mesCtx.DevWeightStand2.status)) {
                            p.devStdMin2 = mesCtx.DevWeightStand2.min.ToString();
                            p.devStdMax2 = mesCtx.DevWeightStand2.max.ToString();
                        }
                        string now = AppCfg.Now();
                        // 上传MES是需要的参数
                        // p.updateDate = now;
                        p.updateTime = now;
                        p.createTime = now;
                        
                        p.cansCodeValidMsg = matList.rawResp;
                        if(string.Equals(mes_code_suc, matList.code)) {
                            // mes 校验通过
                            p.cansCodeValid= 10;
                        } else if(string.Equals(mes_code_exception, matList.code)){
                            // mes 异常
                            p.cansCodeValid = -11;
                        } else {
                            // 明确失败了
                            p.cansCodeValid = -10;
                        }
                        log.InfoFormat("料斗码:[{0}]绑定: 叠片 Id:[{1}],位置:[{2}],MES校验:{3}", vehicleNo, p.id, p.positon, matList.rawResp);
                        // 如果料斗没有绑定信息,并且校验通过才可以生成记录,否则ng排除
                        if(!hasReuse && string.Equals(mes_code_suc, matList.code) ) {
                            // 如果这里校验不通过,返回给设备,是当做ng排出
                            prodCace.Add(p);
                            // 完成通过之后,才生成记录
                            dbClient.Insert(createSql, p);
                        } else {
                            
                        }
                    }));
                    if (string.Equals(mes_code_suc, matList.code)) {
                        state = true;
                        // 如果异常,仅是在数据库中记录数据,文件缓存不在记录
                        string bindingData = JsonConvert.SerializeObject(prodCace);
                        Save2File(bindingData);
                    }
                }
                return state;
            }
        }
        /// <summary>
        /// 返回物料编码
        /// </summary>
        /// <param name="matInfo"></param>
        /// <returns></returns>
        private string GetMaterialCode(MaterialBatchModel matInfo) {
            string code = string.Empty;
            if (matInfo != null && matInfo.data != null) {
                code = matInfo.data.materialNo;
            }
            return code;
        }
       
        
        /// <summary>
        /// 产品绑定流拉工装码(通过料斗码的位置信息,将2个产品绑定到一个流拉工装码上)
        /// </summary>
        /// <param name="transCode"></param>
        /// <param name="vehicleNo"></param>
        /// <param name="pos"></param>
        public List<MesEntity> BindTransToolCode(string transCode, string vehicleNo, int pos) {
            // string queryTpl = "SELECT * FROM `products` where vehicleNo='ab' and (positon=1 or positon=3)";
            
            List<MesEntity> prodCached = prodCace.FindAll(prod => {
                if (pos == 13) {
                    return (prod.vehicleNo == vehicleNo) && (prod.positon == 1 || prod.positon == 3);
                } else {
                    return (prod.vehicleNo == vehicleNo) && (prod.positon == 2 || prod.positon == 4);
                }
            });
            if (prodCached.IsNullOrEmpty()) {
                bool online = AppCfg.Inst().MesisOnline;
                if (online) {
                    string err = string.Format("流拉工装码:[{0}], 料斗码:[{1}],位置:[{2}] 缓存中未能找到对应的产品信息!", transCode, vehicleNo, pos);
                    log.InfoFormat(err);
                    ShowMsg_ST1(err);
                }
                return Array.Empty<MesEntity>().ToList();
            }
            List<MesEntity> myids = new List<MesEntity>();
            // 正常情况下只能有 2条.
            prodCached.ForEach(p => {
                // 绑定 流拉工装码信息
                p.transCode = transCode;
                myids.Add(p);
            });
            // myid 自动生成的为 uuid,不可能为 -1,所以,如果找不到缓存,就不会更新任何数据
            string myid1 = "-1", myid2 = "-1";
            if (myids.Count==2) {
                myid1 = myids[0].myId;
                myid2 = myids[1].myId;
            }else if(myids.Count == 1) {
                myid1 = myids[0].myId;
            } else if(myids.Count>2) {
                // 正常情况下,找到多个情况都不应该出现.如果超过了2个,则意味着之前的未能正常处理.需要进一步纠正后处理
                // 例如 1工位上料4个产品,分为了2个流拉工装,但是未能流转到6工位(删除),但是之后又重复流转到1工位.
                // 就会导致 一个工装码,绑定了多个产品,需要把之前的放弃掉.
                myids.Sort((a, b) => {
                    return DateTime.Compare(DateTime.Parse(b.createTime), DateTime.Parse(a.createTime));
                });
                string err = string.Format("[异常]: 流拉工装码:[{0}], 料斗码:[{1}],位置:[{2}] 找到多条绑定记录.", transCode, vehicleNo, pos);
                log.Error(err);
                ShowMsg_ST1(err);
                int c = myids.Count;
                for (int idx=0;idx<c; idx++) {
                    if (idx==0) {
                        myid1 = myids[0].myId;
                    } else if (idx == 1) {
                        myid2 = myids[1].myId;
                    } else {
                        string err1 = string.Format("[警告]: 忽略异常数据:流拉工装码:[{0}],叠片码:[{1}]", transCode, myids[idx].id);
                        log.Error(err1);
                        ShowMsg_ST1(err1);
                        RemoveCacheItem(myids[idx]);
                    }
                }
            }
            string bindingData = JsonConvert.SerializeObject(prodCace);
            Save2File(bindingData);

            string updateTpl = "update products  set transCode=@transCode,updateTime=@updateTime,updateDate=@updateDate   where myid in (@myid1,@myid2)";
            string updateTime = AppCfg.Now();
            string updateDate = updateTime;
            var param = new { transCode, updateTime, updateDate, myid1, myid2 };

            int rows = dbClient.Update(updateTpl, param);

            if (rows < 1) {
                string err = string.Format("[异常]: 流拉工装码:[{0}], 料斗码:[{1}],位置:[{2}] 数据库中未能找到对应的产品信息!", transCode, vehicleNo, pos);
                log.InfoFormat(err);
                ShowMsg_ST1(err);
            }
            return prodCached;
        }
        
        /// <summary>
        /// 产品绑定电芯二维码(plc会在两个产品码都扫完之后,给出工装码+2个产品码,通过条码地址来区分是此工装对应的哪个),
        /// 在线模式下 并做 MES 校验
        /// </summary>
        /// <param name="transCode"></param>
        /// <param name="vehicleNo"></param>
        /// <param name="pos"></param>
        public MesEntity BindBarCode(string transCode, string barcode, int pos, BaseResponse check) {
            
            MesEntity prodCached = prodCace.Find(prod => {
                bool match = false;
                if (prod.transCode == transCode) {
                    if (pos == 1) {
                        match = (prod.positon == 1 || prod.positon == 2);
                    } else if (pos == 2) {
                        match = (prod.positon == 3 || prod.positon == 4);
                    }
                }
                return match;
            });
            
            if (prodCached == null) {
                string err = string.Format("[异常]: 钢壳码:[{0}], 产品-{1},缓存中未能找到与工装码:[{2}]对应的产品信息!", barcode, pos, transCode);
                log.InfoFormat(err);
                ShowMsg_ST6(err);
                return null;
            }
            prodCached.barcode = barcode;

            // 更新数据库
            string barcodeValidMsg = check.rawResp;
            string updateTime = AppCfg.Now();
            int barcodeValid = -20;
            if (string.Equals(mes_code_suc, check.code)) {
                // 料斗码校验通过
                barcodeValid = 20;
            }else {
                // if (string.Equals(mes_code_exception, check.code)) 
                // 前提认定为MES正常. 如果返回了非 offline,非code_exception
                barcodeValid = -21;
            }

            string updateTpl = "update products set barcode=@barcode,barcodeValid=@barcodeValid,barcodeValidMsg=@barcodeValidMsg,updateTime=@updateTime where myId=@myId";
            string myId = prodCached.myId;
            
            var param = new { barcode, barcodeValid,barcodeValidMsg, updateTime, myId };
            int rows = dbClient.Update(updateTpl, param);
            if (rows < 1) {
                string err = string.Format("[异常]: 钢壳码:[{0}], 产品-{1},数据库中未能找与工装码:[{2}]对应的产品信息!", barcode, pos, transCode);
                log.Error(err);
                ShowMsg_ST6(err);
            }
            // 在此动作之后,就不需要工装码了.完全通过 barcode (产品码/钢壳码) 来绑定
            RemoveCacheItem(prodCached);
            return prodCached;
        }
        /// <summary>
        /// 装配物料,上传完成
        /// </summary>
        /// <param name="item"></param>
        /// <param name="upRet"></param>
        private void FirstUploadFinish(MesEntity item, BaseResponse upRet) {
            if (item == null || string.IsNullOrEmpty(item.myId) || upRet == null || string.IsNullOrEmpty(upRet.code)) {
                log.InfoFormat("钢壳码扫码完成,校验,上传完成,但异常数据,无法更新对应数据,或者无法判断是否上传成功.");
                return;
            }
            string updateTpl = "update products set materialUpload=@materialUpload,materialUploadMsg=@materialUploadMsg,updateTime=@updateTime where myid =@myid";
            string updateTime = AppCfg.Now();
            int materialUpload = -30;
            if (string.Equals(mes_code_suc, upRet.code)) {
                // 料斗码校验通过,或者MES异常不知道具体结果,所以可以重试 
                materialUpload = 30;
            } else if (string.Equals(mes_code_exception, upRet.code)) {
                materialUpload = -31;
            }
            string materialUploadMsg = upRet.rawResp;
            string myid = item.myId;
            var param = new { materialUpload, materialUploadMsg, updateTime, myid };
            int rows = dbClient.Update(updateTpl, param);
            string msg = string.Format("钢壳码:[{0}]_叠片码:[{1}]上传成功,影响行数: {2}", item.barcode, item.id, rows);
            log.InfoFormat(msg);
        }
        /// <summary>
        /// 品绑定重量信息,(PLC触发时只会有电芯二维码+重量信息+结果)
        /// </summary>
        /// <param name="prodBarcode">产品码</param>
        /// <param name="weight">重量</param>
        /// <param name="result">结果</param>
        /// <returns></returns>
        public MesEntity BindWeight(string prodBarcode, float weight,int resultb,string resultByMes) {           
            string updateSql = "update products set antecedentWeight=@antecedentWeight, resultb=@resultb, resultByMes=@resultByMes, updateTime=@updateTime where barcode=@barcode;";
            string updateTime = AppCfg.Now();
            string antecedentWeight = weight.ToString();
            // string resultb = //设备端依据设备自身的上下限设置而给出的 ok/ng 结果;
            
            string barcode = prodBarcode;
            var param = new { antecedentWeight, updateTime, resultb, resultByMes, barcode };
            int rows = dbClient.Update(updateSql, param);
            string err = string.Format("产品:[{0}]称重: 重量[{1}],结果:[{2}],MES标准:[{4}],更新数据库数量:{3}!", prodBarcode, antecedentWeight, resultb, rows, resultByMes);
            log.InfoFormat(err);
            if (rows > 0) {
                string query = "select * from products where barcode=@barcode LIMIT 1; ";
                var queryP = new { barcode };
                IEnumerable<MesEntity> items = dbClient.Query<MesEntity>(query, queryP);
                if (items==null || items.Count()<1) {
                    return null;
                } else {
                    MesEntity dbItem = items.ToList()[0];                    
                    return dbItem;
                }
            } else {
                return null;
            }
            //return prodCached;
        }
        private void UpdateWeightUploadState(MesEntity item, BaseResponse mecCheck) {
            if (item == null || mecCheck == null) {
                log.InfoFormat("称重数据上抛完成,更新本地数据库状态,数据异常");
                return;
            }

            int weightUpload = -40;
            if (string.Equals(mes_code_suc, mecCheck.code) ) {
                // 料斗码校验通过,
                weightUpload = 40;
            } else if(string.Equals(mes_code_exception, mecCheck.code)){
                // 或者MES异常不知道具体结果,所以可以重试 
                weightUpload = -41;
            }
            string updateSql = "update products set weightUpload=@weightUpload,weightUploadMsg=@weightUploadMsg,updateTime=@updateTime where myid=@myid;";
            string updateTime = AppCfg.Now();
            string myid = item.myId;
            string weightUploadMsg = !string.IsNullOrEmpty(mecCheck.rawResp) ? mecCheck.rawResp : mecCheck.msg;

            var param = new { weightUpload, weightUploadMsg, updateTime, myid };
            int rows = dbClient.Update(updateSql, param);
            string err = string.Format("更新重量信息上传状态,myid:{0},数量:{1}", myid, rows);
            log.Info(err);
        }
        private string  ValidWeightByMesStand(float weight) {
            string result = "unknow";
            ProdProperty mesStand = MesContext.getCtx().WeightStand;
            if (mesStand == null) {
                
                return result;
            }
            if(weight> mesStand.min && weight < mesStand.max) {
                result = "OK";
            } else {
                result = "NG";
            }
            return result;
        }
        /// <summary>
        /// 产品码+下料盘码绑定: (PLC每下一个料,就会触发一次,触发值为100,如果触发值为300,表示结束了.可以上抛MES了),100
        /// </summary>
        /// <param name="transCode"></param>
        /// <param name="vehicleNo"></param>
        /// <param name="pos"></param>
        public void BindUnloadingToolCode(string unloadToolCode, string barcode) {
            if (string.IsNullOrEmpty(barcode)) {
                log.ErrorFormat("下料盘绑定: 下料盘[{0}],产品码为空,不进行绑定!", unloadToolCode);
                return;
            }
            string updateSql = "update products  set unloadToolCode=@unloadToolCode,updateTime=@updateTime where barcode=@barcode;";
            string updateTime = AppCfg.Now();
            var param = new { unloadToolCode, updateTime,barcode };
            int rows = dbClient.Update(updateSql, param);
            string err = string.Format("下料盘绑定: 下料盘[{0}],产品码:[{1}],绑定数量:{2}!", unloadToolCode, barcode, rows);
            log.InfoFormat(err);
            // ShowMsg(err);

        }
        #endregion

        private bool Need2ReadData(ref int flag, int interval,int timeout=-1) {
            bool need = false;
            if (flag == -1) {
                need = true;
                // 第一次执行
                Interlocked.Exchange(ref flag, interval);
            } else {
                // 第二次之后,开始累计
                Interlocked.Exchange(ref flag, (flag += interval));
                need = false;
            }
            int timeDelay = trigger_timeout;
            if (timeout > 0) {
                timeDelay = timeout;
            }
            // 第二次进入之后,但是未到阈值
            if (flag > interval && flag < timeDelay) {
                need = false;
            } else if (flag > timeDelay) {
                need = false;
                Interlocked.Exchange(ref flag, -1);
            }
            
            return need;
        }
        private void FinishAck(PlcHelper plcHelper,string plcAddr,ushort plcAddrCount, ushort plcAckValue, string appAddr, short[] appRestValue) {
            HslCommunication.OperateResult<ushort[]> plcAck = plcHelper.ReadUInt16(plcAddr, plcAddrCount,true);
            ushort plcValue = plcAck.Content[0];
            if (plcAck.IsSuccess && plcValue == plcAckValue) {
                HslCommunication.OperateResult reset = plcHelper.WriteShort(appAddr, appRestValue,true);
                log.InfoFormat("PLC归零成功,地址:[{0}],上位机归零完成,地址:[{1}],suc={2}", plcAddr, appAddr, reset.IsSuccess);
            } else {
                log.InfoFormat("PLC未归零,地址:[{0}],上位机无法归零,地址:[{1}]",plcAddr,appAddr);
            }
        }
        #endregion

        #region 工位-2 逻辑处理
        int fujierchongqiemuj = -1;
        int fujiercaidao = -1;
        int fujihantou_1 = -1;
        int fujihantou_2 = -1;
        private void Station_2_Processor(string plcId, Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station2HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_fujier_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnFujierTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_cqmj_trg", StringComparison.OrdinalIgnoreCase)) {
                // 负极耳冲切模具 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cqmj_info");
                fujierchongqiemuj = OnCommonTriggerWithWarning(plcHelper, tag, dataTag, "fujierchongqiemuju", "负极耳冲切模具", fujierchongqiemuj);
            } else if (tag.Id.Equals("fix_cd_trg", StringComparison.OrdinalIgnoreCase)) {
                // 负极耳裁刀 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cd_info");
                fujiercaidao = OnCommonTriggerWithWarning(plcHelper, tag, dataTag, "fujiercaidao", "负极耳裁刀", fujiercaidao);
            } else if (tag.Id.Equals("fix_ht1_trg", StringComparison.OrdinalIgnoreCase)) {
                // 负极焊头-1 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag usedTag = plcG.GetTag(plcId, "fix_ht1_info");
                Tag dataTag = plcG.GetTag(plcId, "fix_ht2_info");
                fujihantou_1= OnHantouTrigger(plcHelper, tag, usedTag, dataTag, "fujihantou_1", "负极焊头-1", fujihantou_1);
            }
            else if (tag.Id.Equals("fix_ht2_trg", StringComparison.OrdinalIgnoreCase)) {
                // 负极焊头-2 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag usedTag = plcG.GetTag(plcId, "fix_ht2_info");
                Tag dataTag = plcG.GetTag(plcId, "fix_ht2_info");
                fujihantou_2 = OnHantouTrigger(plcHelper, tag, usedTag, dataTag, "fujihantou_2", "负极焊头-2", fujihantou_2);
            }
        }
        /// <summary>
        /// 带预警线的工夹具校验
        /// </summary>
        /// <param name="plcHelper"></param>
        /// <param name="trg"></param>
        /// <param name="data"></param>
        /// <param name="fixTag"></param>
        /// <param name="name"></param>
        public int OnCommonTriggerWithWarning(PlcHelper plcHelper, Tag trg, Tag data, string fixTag,string name,int flag) {
            try {                
                OperateResult<ushort[]> trgRet = plcHelper.ReadUInt16(trg.AddrName, (ushort)trg.Count);
                OperateResult<int[]> dataRet = plcHelper.ReadInt32(data.AddrName, (ushort)data.Count);
                if (!trgRet.IsSuccess || !dataRet.IsSuccess) return flag;

                ushort trgValue = trgRet.Content[0];
                int used = dataRet.Content[0];
                int warning = dataRet.Content[1];
                int endLine = dataRet.Content[2];
                // log.InfoFormat("工夹具: [{3}] 使用数据: 实际使用={0},预警线={1},寿命线={2}", used, warning, endLine, name);
                if (!(trgValue == 1) && ((endLine - used) < 1)) return flag;
                MesContext mctx = MesContext.getCtx();
                BaseResponse resp = mctx.UploadFixtureData(fixTag, used);
                // log.InfoFormat("工夹具: [{2}] MES 上传: code={0},msg={1}", resp.code, resp.msg, name);

                flag = FixtureEndlife(trgValue, endLine, used, flag, fixTag);
            } catch (Exception e) {
                log.InfoFormat("工夹具: [{2}] 寿命处理异常{0},{1}!!!", e.Message, e.StackTrace, name);
                flag = -1;
            }
            return flag;
        }
        /// <summary>
        /// 仅仅存在寿命线的
        /// </summary>
        /// <param name="plcHelper"></param>
        /// <param name="trg"></param>
        /// <param name="data"></param>
        /// <param name="fixTag">appsettings.json中工夹具标识符</param>
        /// <param name="name"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public int  OnCommonTriggerOnlyAlarmLine(PlcHelper plcHelper, Tag trg, Tag data, string fixTag, string name,int flag) {
            try {
                OperateResult<ushort[]> trgRet = plcHelper.ReadUInt16(trg.AddrName, (ushort)trg.Count);
                OperateResult<int[]> dataRet = plcHelper.ReadInt32(data.AddrName, (ushort)data.Count);
                if (!trgRet.IsSuccess || !dataRet.IsSuccess) return flag;

                ushort trgValue = trgRet.Content[0];
                int used = dataRet.Content[0];
                // int warning = dataRet.Content[1];
                int endLine = dataRet.Content[1];
                // log.InfoFormat("工夹具: [{2}] 使用数据: 实际使用={0},寿命线={1}", used,endLine, name);
                if (!(trgValue == 1) && ((endLine - used) < 1)) return flag;
                MesContext mctx = MesContext.getCtx();
                BaseResponse resp = mctx.UploadFixtureData(fixTag, used);
                // log.InfoFormat("工夹具: [{2}] MES 上传: code={0},msg={1}", resp.code, resp.msg, name);
                flag = FixtureEndlife(trgValue, endLine, used, flag, fixTag);
            } catch (Exception e) {
                log.InfoFormat("工夹具: [{2}] 寿命处理异常{0},{1}!!!", e.Message, e.StackTrace, name);
                flag = -1;
            }
            return flag;
        }
        public int OnHantouTrigger(PlcHelper plcHelper, Tag trg, Tag usedT, Tag data, string fixTag, string name,int flag) {
            try {
                OperateResult<ushort[]> trgRet = plcHelper.ReadUInt16(trg.AddrName, (ushort)trg.Count);
                OperateResult<int[]> usedRet = plcHelper.ReadInt32(usedT.AddrName, (ushort)usedT.Count);
                OperateResult<int[]> dataRet = plcHelper.ReadInt32(data.AddrName, (ushort)data.Count);
                if (!trgRet.IsSuccess || !dataRet.IsSuccess || !usedRet.IsSuccess) return flag;

                ushort trgValue = trgRet.Content[0];
                int used = usedRet.Content[0];
                int warning = dataRet.Content[2];
                int endLine = dataRet.Content[1];
                // log.InfoFormat("工夹具: [{3}] 使用数据: 实际使用={0},预警线={1},寿命线={2}", used, warning, endLine, name);
                if (!(trgValue == 1) && ((endLine - used) < 1)) return flag;
                MesContext mctx = MesContext.getCtx();
                BaseResponse resp = mctx.UploadFixtureData(fixTag, used);
                //log.InfoFormat("工夹具: [{2}] MES 上传: code={0},msg={1}", resp.code, resp.msg, name);
                flag = FixtureEndlife(trgValue, endLine, used, flag, fixTag);

            } catch (Exception e) {
                log.InfoFormat("工夹具: [{2}] 寿命处理异常{0},{1}!!!", e.Message, e.StackTrace, name);
                flag = -1;
            }
            return flag;
        }
        private int _fujierChecker = -1;
        public void OnFujierTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 负极耳 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _fujierChecker, interval)) {
                    return;
                }
                OnQueryFujier();

                log.InfoFormat("物料: [负极耳] MES校验 完成 >>>");
                Interlocked.Exchange(ref _fujierChecker, -1);
            } catch (Exception e) {
                log.InfoFormat("物料: [负极耳] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
                Interlocked.Exchange(ref _fujierChecker, -1);
            }
        }
        #endregion 工位-2 逻辑处理

        #region 工位-3 逻辑处理
        int zhengjierchongqiemujuFlag = -1;
        int zhengjiercaidaoState = -1;
        int zhengjihantou_1 = -1;
        int zhengjihantou_2 = -1;
        private void Station_3_Processor(string plcId, Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station3HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_zhengjier_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnZhengjierTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_cqmj_trg", StringComparison.OrdinalIgnoreCase)) {
                // 正极耳冲切模具 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cqmj_info");
                zhengjierchongqiemujuFlag =  OnCommonTriggerWithWarning(plcHelper, tag, dataTag, "zhengjierchongqiemuju", "正极耳冲切模具", zhengjierchongqiemujuFlag);
            } else if (tag.Id.Equals("fix_cd_trg", StringComparison.OrdinalIgnoreCase)) {
                // 正极耳裁刀 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cd_info");
                zhengjiercaidaoState = OnCommonTriggerWithWarning(plcHelper, tag, dataTag, "zhengjiercaidao", "正极耳裁刀", zhengjiercaidaoState);
            } else if (tag.Id.Equals("fix_ht1_trg", StringComparison.OrdinalIgnoreCase)) {
                // 正极焊头-1 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag usedTag = plcG.GetTag(plcId, "fix_ht1_info");
                Tag dataTag = plcG.GetTag(plcId, "fix_ht2_info");
                zhengjihantou_1 = OnHantouTrigger(plcHelper, tag, usedTag, dataTag, "zhengjihantou_1", "正极焊头-1", zhengjihantou_1);
            } else if (tag.Id.Equals("fix_ht2_trg", StringComparison.OrdinalIgnoreCase)) {
                // 正极焊头-2 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag usedTag = plcG.GetTag(plcId, "fix_ht2_info");
                Tag dataTag = plcG.GetTag(plcId, "fix_ht2_info");
                zhengjihantou_2 =OnHantouTrigger(plcHelper, tag, usedTag, dataTag, "zhengjihantou_2", "正极焊头-2", zhengjihantou_2);
            }
        }
        private int _zhengjierChecker = -1;
        public void OnZhengjierTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 正极耳 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _zhengjierChecker, interval)) {
                    return;
                }
                OnQueryZhengjier();

                log.InfoFormat("物料: [负极耳] MES校验 完成 >>>");
                Interlocked.Exchange(ref _zhengjierChecker, -1);
            } catch (Exception e) {
                log.InfoFormat("物料: [负极耳] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
                Interlocked.Exchange(ref _zhengjierChecker, -1);
            }
        }
        #endregion 工位-3 逻辑处理


        #region 工位-4 逻辑处理
        int zhengjihuangjiaoqiedaoState = -1;
        private void Station_4_Processor(string plcId, Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station4HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_chajiaoK_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnchajiaoKTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_cd_trg", StringComparison.OrdinalIgnoreCase)) {
                // 正极黄胶切刀 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cd_info");
                zhengjihuangjiaoqiedaoState = OnCommonTriggerOnlyAlarmLine(plcHelper, tag, dataTag, "zhengjihuangjiaoqiedao", "正极黄胶切刀", zhengjihuangjiaoqiedaoState);
            }
        }
        private int _chajiaoKChecker = -1;
        public void OnchajiaoKTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 茶胶(宽) 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _chajiaoKChecker, interval)) {
                    return;
                }
                OnQueryChajiaoK();

                log.InfoFormat("物料: [茶胶(宽)] MES校验 完成 >>>");
                Interlocked.Exchange(ref _chajiaoKChecker, -1);
            } catch (Exception e) {
                log.InfoFormat("物料: [茶胶(宽] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
                Interlocked.Exchange(ref _chajiaoKChecker, -1);
            }
        }
        #endregion 工位-4 逻辑处理

        #region 工位-5 逻辑处理
        int fujihuangjiaoqiedaoState = -1;
        private void Station_5_Processor(string plcId, Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station5HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_chajiaoZ_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnchajiaoZTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_cd_trg", StringComparison.OrdinalIgnoreCase)) {
                // 负极黄胶切刀 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_cd_info");
                fujihuangjiaoqiedaoState = OnCommonTriggerOnlyAlarmLine(plcHelper, tag, dataTag, "fujihuangjiaoqiedao", "负极黄胶切刀", fujihuangjiaoqiedaoState);
            }
        }
        private int _chajiaoZChecker = -1;
        public void OnchajiaoZTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _chajiaoZChecker, interval)) {
                    return;
                }
                OnQueryChajiaoZ();

                log.InfoFormat("物料: [茶胶(窄)] MES校验 完成 >>>");
                Interlocked.Exchange(ref _chajiaoZChecker, -1);
            } catch (Exception e) {
                Interlocked.Exchange(ref _chajiaoZChecker, -1);
                log.InfoFormat("物料: [茶胶(窄)] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }
        #endregion 工位-5 逻辑处理

        #region 工位-6 逻辑处理
        int fujiguanghanState = -1;
        private void Station_6_Processor(string plcId, Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station6HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_barcode_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnBarcodeTrigger(plcId, tag);
            } else if (tag.Id.Equals("plc_shanggai_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnShanggaiTrigger(plcId, tag);
            } else if (tag.Id.Equals("plc_mifengquan_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnMifengquanTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_jgh_trg", StringComparison.OrdinalIgnoreCase)) {
                // 负激光焊 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_jgh_info");
                fujiguanghanState  = OnCommonTriggerOnlyAlarmLine(plcHelper, tag, dataTag, "fujiguanghan", "负激光焊", fujiguanghanState);
            }
        }
        private int _shanggaiChecker = -1;
        public void OnShanggaiTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _shanggaiChecker, interval)) {
                    return;
                }
                OnQueryShanggai();

                log.InfoFormat("物料: [上端盖] MES校验 完成 >>>");
                Interlocked.Exchange(ref _shanggaiChecker, -1);
            } catch (Exception e) {
                Interlocked.Exchange(ref _shanggaiChecker, -1);
                log.InfoFormat("物料: [上端盖] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }
        private int _mifengquanChecker = -1;
        public void OnMifengquanTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _mifengquanChecker, interval)) {
                    return;
                }
                OnQueryMifengquan();

                log.InfoFormat("物料: [密封圈] MES校验 完成 >>>");
                Interlocked.Exchange(ref _mifengquanChecker, -1);
            } catch (Exception e) {
                Interlocked.Exchange(ref _mifengquanChecker, -1);
                log.InfoFormat("物料: [密封圈] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }
        private int _OnBarcodeTrigger = -1;
        public void OnBarcodeTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 读取 流拉工装码+2个工装码,并完成绑定
                // 2. 与MES交互,做校验
                // 3. 写会PLC校验结果;100 表示正常,200表示NG,300表示异常;
                // 4. 读取plc触发地址是否归零
                // 6. 上位机归零

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;


                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
               
                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                
                if (currentValue != PLC_Trigger_Value) return;

                if (!Need2ReadData(ref _OnBarcodeTrigger, interval)) return;

                log.InfoFormat("钢壳码: 扫码完成,plc触发值={0} >>>", currentValue);
                ShowMsg_ST6(string.Format("钢壳码: 扫码完成,plc触发值={0} >>>", currentValue));
                Tag appAckTag = plcG.GetTag(plcId, "app_barcode_ack");
               
                Tag barcode1Tad = plcG.GetTag(plcId, "plc_barcode_1");
                HslCommunication.OperateResult<string> code1Ret = plcHelper.ReadString(barcode1Tad.AddrName, (ushort)barcode1Tad.Count);
                string barcode1 = string.IsNullOrEmpty(code1Ret.Content) ? string.Empty : code1Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "");
                barcode1 = barcode1.Trim();
                if (!code1Ret.IsSuccess || string.IsNullOrEmpty(barcode1)) {
                    string msg =string.Format("钢壳码: 钢壳码-1 为空,或者读取失败! ");
                    log.Error(msg);
                    ShowMsg_ST6(msg);
                }


                Tag barcode2Tad = plcG.GetTag(plcId, "plc_barcode_2");
                HslCommunication.OperateResult<string> code2Ret = plcHelper.ReadString(barcode2Tad.AddrName, (ushort)barcode2Tad.Count);
                string barcode2 = string.IsNullOrEmpty(code2Ret.Content) ? string.Empty : code2Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "");
                barcode2 = barcode2.Trim();
                if (!code2Ret.IsSuccess || string.IsNullOrEmpty(barcode2)) {
                    string msg = string.Format("钢壳码: 钢壳码-2 为空,或者读取失败!");
                    log.Error(msg);
                    ShowMsg_ST6(msg);
                }

                Tag transcode1Tad = plcG.GetTag(plcId, "plc_transcode");
                HslCommunication.OperateResult<string> transcodeRet = plcHelper.ReadString(transcode1Tad.AddrName, (ushort)transcode1Tad.Count);
                string transCode = string.IsNullOrEmpty(transcodeRet.Content) ? string.Empty : transcodeRet.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "");
                transCode = transCode.Trim();
                if (!transcodeRet.IsSuccess || string.IsNullOrEmpty(transCode)) {
                    string msg = string.Format("钢壳码: 流拉工装码 为空,或者读取失败!");
                    log.Error(msg);
                    ShowMsg_ST6(msg);
                }
                MesContext mesCtx = MesContext.getCtx();
                BaseResponse checkRet1 = MesHelper.CheckMesCansDataSliceFrontZp(mesCtx.PlanBatch, barcode1);
                string msg1 = string.Format("钢壳码: 产品-1 钢壳码[{0}],MES 校验:{1}", barcode1, checkRet1.rawResp);
                log.Info(msg1);
                
                // 钢壳码校
                BaseResponse checkRet2 = MesHelper.CheckMesCansDataSliceFrontZp(mesCtx.PlanBatch, barcode2);
                string msg2 = string.Format("钢壳码: 产品-2 钢壳码[{0}],MES 校验:{1}", barcode2, checkRet2.rawResp);
                log.Info(msg2);
                // 钢壳码校验不通过如何处理? 
                MesEntity bindProd = BindBarCode(transCode, barcode1, 1, checkRet1);
                MesEntity bindProd2 = BindBarCode(transCode, barcode2, 2, checkRet2);
                
                // 产品物料信息上传 
                BaseResponse upLoadedRet1 = MesHelper.UploadMesCansDataSliceFrontZp(bindProd, checkRet1);
                string msg3 = string.Format("钢壳码: 产品-1 钢壳码[{0}],MES校验:{1},上传结果:{2}", barcode1, checkRet1.rawResp, upLoadedRet1.rawResp);
                log.Info(msg3);
                ShowMsg_ST6(msg3);

                BaseResponse upLoadedRet2 = MesHelper.UploadMesCansDataSliceFrontZp(bindProd2, checkRet2);
                string msg4 = string.Format("钢壳码: 产品-2 钢壳码[{0}],MES校验:{1},上传结果:{2}", barcode2, checkRet2.rawResp, upLoadedRet2.rawResp);
                log.Info(msg4);
                ShowMsg_ST6(msg4);

                // 如果产品在这一步校验失败是不会向下流转的,所以后续的 40,50,肯定为空,那么如何判断是否上传失败?
                FirstUploadFinish(bindProd, upLoadedRet1);
                FirstUploadFinish(bindProd2, upLoadedRet2);
                // 100 表示两个产品都 OK
                // 200 表示 产品1 NG, 产品2 OK
                // 300 表示 产品1 OK, 产品2 NG
                // 500 表示 产品1 NG, 产品2  NG,
                short[] barcodeCheckCode = new short[1] { 100 };
                bool prod_1_Ok = (string.Equals("0", checkRet1.code) && string.Equals("0", upLoadedRet2.code));
                bool prod_2_Ok = (string.Equals("0", checkRet2.code) && string.Equals("0", upLoadedRet2.code));
                if (AppCfg.Inst().MesisOnline && prod_1_Ok && prod_2_Ok) {
                    //在线模式下, 产品1 ,产品2 都校验成功
                    barcodeCheckCode[0] = 100;
                } else if(AppCfg.Inst().MesisOnline && !prod_1_Ok && prod_2_Ok) {
                    //在线模式下, 产品1 NG,产品2 OK
                    barcodeCheckCode[0] = 200;
                   
                } else if (AppCfg.Inst().MesisOnline && prod_1_Ok && prod_2_Ok) {
                    //在线模式下, 产品1 OK,产品2 NG
                    barcodeCheckCode[0] = 300;

                } else if (AppCfg.Inst().MesisOnline&& !prod_1_Ok && !prod_2_Ok) {
                    // 在线模式下,产品1 NG,产品2 NG
                    barcodeCheckCode[0] = 500;
                } else if (!AppCfg.Inst().MesisOnline) {
                    // 离线模式下,按照都OK处理
                    barcodeCheckCode[0] = 100;
                }

                // 校验结果通知plc;
                OperateResult ackRet = plcHelper.WriteShort(appAckTag.AddrName, barcodeCheckCode);
                Thread.Sleep(appResetWait);
                // 完成归零
                FinishAck(plcHelper, addr, count, 0, appAckTag.AddrName, appAckRest);               
                log.InfoFormat("钢壳码: 绑定完成,回写PLC结果值:{0},是否成功:{1}<<<", barcodeCheckCode[0], ackRet.IsSuccess);
                ShowMsg_ST6(string.Format("钢壳码: 绑定完成,回写PLC结果值:{0},是否成功:{1}<<<", barcodeCheckCode[0], ackRet.IsSuccess));
                // 更新缓存文件
                string bindingData = JsonConvert.SerializeObject(prodCace);
                Save2File(bindingData);

                Interlocked.Exchange(ref _OnBarcodeTrigger, -1);
            } catch (Exception e) {
                Interlocked.Exchange(ref _OnBarcodeTrigger, -1);
                log.InfoFormat("钢壳码: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }

        #endregion

        #region 工位-7 逻辑处理
        int zhengjiguanghanState = -1;
        private void Station_7_Processor(string plcId, Tag tag) {
            if (tag.Id.Equals("HeartBeat", StringComparison.OrdinalIgnoreCase)) {
                Station7HeartBeat(plcId, tag);
            } else if (tag.Id.Equals("plc_weight_trigger", StringComparison.OrdinalIgnoreCase)) {
                // 产品 称重结果 触发
                // OnWeightFinish(plcId, tag);
                if (AppCfg.Inst().NewMes) {
                    OnWeightFinish(plcId, tag);
                } else {
                    OnWeightFinishOld(plcId, tag);
                }
            }  else if (tag.Id.Equals("plc_unload_trigger", StringComparison.OrdinalIgnoreCase)) {
                UnloadBinding(plcId, tag);
            }else if (tag.Id.Equals("plc_unload_finish_trigger", StringComparison.OrdinalIgnoreCase)) {
                UnloadBindingFinish(plcId, tag);
            } else if (tag.Id.Equals("plc_xiagai_trigger", StringComparison.OrdinalIgnoreCase)) {
                OnXiagaiTrigger(plcId, tag);
            } else if (tag.Id.Equals("fix_jgh_trg", StringComparison.OrdinalIgnoreCase)) {
                // 正激光焊 到达寿命
                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag dataTag = plcG.GetTag(plcId, "fix_jgh_info");
                zhengjiguanghanState = OnCommonTriggerOnlyAlarmLine(plcHelper, tag, dataTag, "zhengjiguanghan", "正激光焊", zhengjiguanghanState);
            }

        }
        private int _xiagaiChecker = -1;
        public void OnXiagaiTrigger(string plcId, Tag triggerTag) {
            try {
                // 1. 物料校验
                // 2. 上位机 查询MES 
                // 5. 查询结果 回写PLC 

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);

                if (!trigger.IsSuccess) return;
                ushort currentValue = trigger.Content[0];
                if (!((currentValue == InitMaterialCheck) || (currentValue == MaterialEmptyCheck))) return;

                if (!Need2ReadData(ref _xiagaiChecker, interval)) {
                    return;
                }
                OnQueryXiagai();
                // 加载设备端重量信息
                ReadDevWeight();
                log.InfoFormat("物料: [下盖] MES校验 完成 >>>");

            } catch (Exception e) {
                log.InfoFormat("物料: [密封圈] MES校验: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }

        private int _OnWeightFinish = -1;
        private void OnWeightFinish(string plcId, Tag trgTag) {
            try {
                string addr = trgTag.AddrName;
                ushort count = (ushort)trgTag.Count;
                int interval = trgTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag ackTag = plcG.GetTag(plcId, "app_weight_ack");

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
                if (!trigger.IsSuccess) return;
                ushort trgValue = trigger.Content[0];
                ushort ackValue = trigger.Content[1];
                if (!(trgValue == 100 || trgValue == 200 || trgValue == 300 && ackValue==0)) return;
                if (!Need2ReadData(ref _OnWeightFinish, interval)) return;

                log.InfoFormat("产品称重完成: 开始处理. plc触发数值=[{0}] >>>", trgValue);
                ShowMsg(string.Format("产品称重完成: 开始处理. plc触发数值=[{0}] >>>", trgValue));
                // 100,200,300为触发条件: 分别代表 产品1触发,产品2触发,2个产品都触发
                // 返回结果定义:
                // 100: 都ok
                // 200: 1 NG,2 OK
                // 300: 1 OK,2 NG
                // 500: 2个都NG
                ushort[] final = new ushort[1] { 99};
                string idx = "1";
                if (trgValue == 100) {
                    idx = "1";
                    final = OnWeightFinish1(plcId, plcHelper, trgTag, ackTag,300,500);
                
                } else if (trgValue == 200) {
                    idx = "2";
                    final = OnWeightFinish2(plcId, plcHelper, trgTag, ackTag,200,500);
               
                } else if (trgValue == 300) {
                    idx = "1和2";
                    ushort[] ret1 = OnWeightFinish1(plcId, plcHelper, trgTag, ackTag,1,2);
                    ushort[] ret2 = OnWeightFinish2(plcId, plcHelper, trgTag, ackTag,1,2);
                    if (ret1[0]==1 && ret2[0] == 1) {
                        final[0]= 100;
                    } else if (ret1[0] == 2 && ret2[0] == 2) {
                        final[0] = 500;
                    } else if (ret1[0] == 1 && ret2[0] == 2) {
                        final[0] = 300;
                    } else if (ret1[0] == 2 && ret2[0] == 1) {
                        final[0] = 200;
                    }
                }
                OperateResult errRet = plcHelper.WriteUInt16(ackTag.AddrName, final, true);
                log.InfoFormat("产品-{0} 称重: 回写PLC校验值:[{1}],回写plc是否成功:[{2}]", idx, final[0].ToString(), errRet.IsSuccess);
                // 处理完之后需要完成归零
                // 等待
                Thread.Sleep(appResetWait);
                // 完成归零
                FinishAck(plcHelper, addr, count, 0, ackTag.AddrName, appAckRest);
                log.InfoFormat("产品-{0} 称重: 处理结束,回写PLC状态值{1},是否成功:{2} <<<", idx, final[0], errRet);
                Interlocked.Exchange(ref _OnWeightFinish, -1);
                ShowMsg(string.Format("产品称重结果处理完成,回写PLC状态值{0},是否成功:{1} <<<", final[0], errRet));
            } catch (Exception e) {
                Interlocked.Exchange(ref _OnWeightFinish, -1);
                log.InfoFormat("产品-{0} 称重: 处理异常:{0},{1} ", e.Message,e.StackTrace);
                ShowMsg(string.Format("称重数据处理发生异常: {0},{1}",e.Message,e.StackTrace));
            }
        }

        /// <summary>
        /// 产品称重结束
        /// </summary>
        /// <param name="plcId"></param>
        /// <param name="tag"></param>
        private ushort[] OnWeightFinish1(string plcId,PlcHelper plcHelper, Tag triggerTag, Tag ackTag,ushort ok,ushort ng) {
            // 默认 0 是正常. 如果mes校验不通过,统统返回 6
            ushort[] WeightCheckFinishd = new ushort[1] { ok };
            try {
                PlcGroup plcG = PlcGroup.Get();

                // 产品-1 ================== 处理流程
                log.InfoFormat("产品-1 称重: 称重完成开始处理...");
                
                // 读取 产品码
                Tag barcodeTag = plcG.GetTag(plcId, "plc_barcode_1");
                HslCommunication.OperateResult<string> code1Ret = plcHelper.ReadString(barcodeTag.AddrName, (ushort)barcodeTag.Count);
                string barcode = string.IsNullOrEmpty(code1Ret.Content) ? string.Empty : code1Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                if (!code1Ret.IsSuccess || string.IsNullOrEmpty(barcode)) {
                    string err1 = string.Format("产品-1 称重: 产品码为空,或者读取失败!", barcode);
                    log.Error(err1);
                    ShowMsg(err1);
                }
                // 读取 产品称重信息
                Tag weightTag = plcG.GetTag(plcId, "plc_weight_1");
                HslCommunication.OperateResult<float[]> weightRet = plcHelper.ReadFloat(weightTag.AddrName, (ushort)weightTag.Count);
                if(!weightRet.IsSuccess) {
                    string err2 = string.Format("产品-1 称重: 产品-1:[{0}] 重量数值失败!", barcode, weightRet.Content);
                    log.Error(err2);
                    ShowMsg(err2);
                }
                float weight = weightRet.Content[0];
                
                // 读取 产品称重判断结果
                Tag resultTag = plcG.GetTag(plcId, "plc_weight_result_1");
                HslCommunication.OperateResult<ushort[]> resultRet = plcHelper.ReadUInt16(resultTag.AddrName, (ushort)resultTag.Count);
                
                if (!resultRet.IsSuccess) {
                    string err3 = string.Format("产品-1 称重: 产品-1:[{0}] 称重结果读取失败!", barcode, resultRet.Content);
                    log.Error(err3);
                    ShowMsg(err3);
                }
                ushort result = resultRet.Content[0];
                
                string resultByMes = ValidWeightByMesStand(weight); ;// 依据mes端 设置的上下限而得出的 ok/ng 结果
                // 先本地数据库保存
                MesEntity item = BindWeight(barcode, weight, result, resultByMes);
                
                MesContext m = MesContext.getCtx();
                string ok_ng = (result == 1) ? "OK" : "NG";
                BaseResponse mecCheck = MesHelper.UploadMesCansDataSliceZpTwo( barcode,weight, ok_ng);
               
                
                // 更新本地记录状态
                UpdateWeightUploadState(item, mecCheck);
                if (AppCfg.Inst().MesisOnline && (!string.Equals(mecCheck.code, "0") && !string.Equals(mecCheck.code, "offline"))) {
                    // 与mes校验失败,固定写6
                    WeightCheckFinishd[0] = ng;
                }
                this.appWin.OnWeightData_1(barcode, weight, ok_ng, resultByMes);
                string msg = string.Format("产品-1:[{0}],重量[{1}],结果:[{2}],MES标准:[{3}],MES校验结果: code={4},msg={5}", barcode, weight, ok_ng, resultByMes, mecCheck.code, mecCheck.msg);
                log.Info(msg);
                ShowMsg(msg);
            } catch (Exception e) {
                WeightCheckFinishd[0] = ng;
                string err =string.Format("产品-1 称重: 异常{0},{1}!!!", e.Message, e.StackTrace);
                log.Error(err); 
                ShowMsg(err);
            }
            return WeightCheckFinishd;
        }
        private ushort[] OnWeightFinish2(string plcId, PlcHelper plcHelper, Tag triggerTag, Tag ackTag, ushort ok, ushort ng) {
            // 默认 0 是正常. 如果mes校验不通过,统统返回 6
            ushort[] WeightCheckFinishd = new ushort[1] { ok };
            try {
                PlcGroup plcG = PlcGroup.Get();

                // 产品-2 ================== 处理流程
                log.InfoFormat("产品-2 称重: 称重完成开始处理...");

                // 读取 产品码
                Tag barcodeTag = plcG.GetTag(plcId, "plc_barcode_2");
                HslCommunication.OperateResult<string> code1Ret = plcHelper.ReadString(barcodeTag.AddrName, (ushort)barcodeTag.Count);
                string barcode = string.IsNullOrEmpty(code1Ret.Content) ? string.Empty : code1Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                if (!code1Ret.IsSuccess || string.IsNullOrEmpty(barcode)) {
                    string err1 = string.Format("产品-2 称重: 产品码为空,或者读取失败!", barcode);
                    log.Error(err1);
                    ShowMsg(err1);
                }
                // 读取 产品称重信息
                Tag weightTag = plcG.GetTag(plcId, "plc_weight_2");
                HslCommunication.OperateResult<float[]> weightRet = plcHelper.ReadFloat(weightTag.AddrName, (ushort)weightTag.Count);
                if (!weightRet.IsSuccess) {
                    string err2 = string.Format("产品-2 称重: 产品-2:[{0}] 重量数值失败!", barcode, weightRet.Content);
                    log.Error(err2);
                    ShowMsg(err2);
                }
                float weight = weightRet.Content[0];

                // 读取 产品称重判断结果
                Tag resultTag = plcG.GetTag(plcId, "plc_weight_result_2");
                HslCommunication.OperateResult<ushort[]> resultRet = plcHelper.ReadUInt16(resultTag.AddrName, (ushort)resultTag.Count);

                if (!resultRet.IsSuccess) {
                    string err3 = string.Format("产品-2 称重: 产品-2:[{0}] 称重结果读取失败!", barcode, resultRet.Content);
                    log.Error(err3);
                    ShowMsg(err3);
                }
                ushort result = resultRet.Content[0];
                // 先本地保存
                string resultByMes = ValidWeightByMesStand(weight); ;// 依据mes端 设置的上下限而得出的 ok/ng 结果
                MesEntity item = BindWeight(barcode, weight, result, resultByMes);

                
                string ok_ng = (result == 1) ? "OK" : "NG";
                BaseResponse mecCheck = MesHelper.UploadMesCansDataSliceZpTwo(barcode, weight, ok_ng);
                
                // 更新本地记录状态
                UpdateWeightUploadState(item, mecCheck);

                if (AppCfg.Inst().MesisOnline && (!string.Equals(mecCheck.code, "0") && !string.Equals(mecCheck.code, "offline"))) {
                    // 与mes校验失败,固定写6
                    WeightCheckFinishd[0] = ng;
                }
                this.appWin.OnWeightData_2(barcode, weight, ok_ng, resultByMes);
                ShowMsg(string.Format("产品-2:[{0}],重量[{1}],结果:[{2}],MES标准:[{3}],MES校验结果: code={4},msg={5}", barcode, weight, ok_ng, resultByMes, mecCheck.code, mecCheck.msg));
            } catch (Exception e) {
                WeightCheckFinishd[0] = ng;
                string err = string.Format("产品-1 称重: 异常{0},{1}!!!", e.Message, e.StackTrace);
                log.Error(err);
                ShowMsg(err);
            }
            return WeightCheckFinishd;
        }
        
        private int _UnloadBinding = -1;
        private void UnloadBinding(string plcId, Tag triggerTag) {
            try {
                // 1. 读取 1个下料工装码+2个产品码并完成绑定                
                // 3. 写会PLC校验结果;100 表示正常,200表示NG,300表示异常;
                // 4. 读取plc触发地址是否归零
                // 6. 上位机归零

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;


                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
                if (!trigger.IsSuccess) return;
                ushort trgV = trigger.Content[0];
                if (trgV != PLC_Trigger_Value_Old) return;
                if (!Need2ReadData(ref _UnloadBinding, interval)) return;

                string trgMsg = string.Format("产品下料盘绑定: 下料完成开始处理 plc触发:{0} >>>", trgV);
                log.Info(trgMsg);
                ShowMsg(trgMsg);
                Tag appAckTag = plcG.GetTag(plcId, "app_unload_ack");

                Tag unloadTag = plcG.GetTag(plcId, "plc_unload_code");
                HslCommunication.OperateResult<string> unloadRet = plcHelper.ReadString(unloadTag.AddrName, (ushort)unloadTag.Count, true);
                string unloadCode = string.IsNullOrEmpty(unloadRet.Content) ? string.Empty : unloadRet.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                bool unloadCodeEmpty = false;
                if (!unloadRet.IsSuccess || string.IsNullOrEmpty(unloadCode)) {
                    string err1 = string.Format("产品与下料盘绑定: 下料盘码 为空,或者读取失败! ");
                    log.Error(err1);
                    ShowMsg(err1);
                    unloadCodeEmpty = true;
                }

                Tag pCodeTag1 = plcG.GetTag(plcId, "plc_unload_pncode_1");
                HslCommunication.OperateResult<string> pCode1Ret = plcHelper.ReadString(pCodeTag1.AddrName, (ushort)pCodeTag1.Count, true);
                string pCode1 = string.IsNullOrEmpty(pCode1Ret.Content) ? string.Empty : pCode1Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();

                if (!pCode1Ret.IsSuccess || string.IsNullOrEmpty(pCode1)) {
                    string err2 = string.Format("产品与下料盘绑定: 产品-1 码为空,或者读取失败!");
                    log.Error(err2);
                    ShowMsg(err2);

                }
                BindUnloadingToolCode(unloadCode, pCode1);

                Tag pCodeTag2 = plcG.GetTag(plcId, "plc_unload_pncode_2");
                HslCommunication.OperateResult<string> pCode2Ret = plcHelper.ReadString(pCodeTag2.AddrName, (ushort)pCodeTag2.Count,true);
                string pCode2 = string.IsNullOrEmpty(pCode2Ret.Content) ? string.Empty : pCode2Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();

                if (!pCode2Ret.IsSuccess || string.IsNullOrEmpty(pCode2)) {
                    string err3 = string.Format("产品与下料盘绑定: 产品-2 码为空,或者读取失败!");
                    log.Error(err3);
                    ShowMsg(err3);
                }

                BindUnloadingToolCode(unloadCode, pCode2);

                short[] ackV = new short[1] { 1 };

                if (unloadCodeEmpty) {
                    ackV[0] = 2;
                }
                // 写回结束状态
                OperateResult ackRet = plcHelper.WriteShort(appAckTag.AddrName, ackV, true);
                // 等待
                Thread.Sleep(appResetWait);
                // 完成归零
                FinishAck(plcHelper, addr, count, 0, appAckTag.AddrName, appAckRest);
                
                string ackMsg = string.Format("产品下料盘绑定: 绑定完成,下料盘:[{0}]-产品-1:[{1}]-产品-2:[{2}]. 回写PLC状态码:{3},是否成功:{4} <<<", unloadCode, pCode1, pCode2, ackV[0], ackRet.IsSuccess);
                log.Info(ackMsg);
                ShowMsg(ackMsg);
                Interlocked.Exchange(ref _UnloadBinding, -1);
            } catch (Exception e) {
                Interlocked.Exchange(ref _UnloadBinding, -1);
                log.InfoFormat("产品与下料盘绑定: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }
        private int _UnloadBindingFinish = -1;
        /// <summary>
        /// 下料完成,数据上抛MES
        /// </summary>
        /// <param name="plcId"></param>
        /// <param name="triggerTag"></param>
        public void UnloadBindingFinish(string plcId, Tag triggerTag) {
            try {

                string addr = triggerTag.AddrName;
                ushort count = (ushort)triggerTag.Count;
                int interval = triggerTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
                if (!trigger.IsSuccess) return;
                ushort trgValue = trigger.Content[0];
                if (trgValue != PLC_Trigger_Value_Old) return;
                if (!Need2ReadData(ref _UnloadBindingFinish, interval)) return;

                string trgMsg = string.Format("下料完成: 下料完成开始处理, plc触发:{0} >>>",trgValue);
                log.Info(trgMsg);
                ShowMsg(trgMsg);

                Tag appAckTag = plcG.GetTag(plcId, "app_unload_finish_trigger_ack");

                Tag unloadTag = plcG.GetTag(plcId, "plc_unload_code");
                HslCommunication.OperateResult<string> unloadRet = plcHelper.ReadString(unloadTag.AddrName, (ushort)unloadTag.Count,true);
                string unloadCode = string.IsNullOrEmpty(unloadRet.Content) ? string.Empty : unloadRet.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                bool codeEmpty = false;
                if (!unloadRet.IsSuccess || string.IsNullOrEmpty(unloadCode)) {
                    string err1 = string.Format("下料完成: 下料盘码 为空,或者读取失败!,无法触发MES上抛 ");
                    log.Error(err1);
                    ShowMsg(err1);
                    codeEmpty = true;
                }
                // unloadCode = "VDLKD012173";
                
                // 处理时间计时
                Stopwatch sw = new Stopwatch();
                sw.Start();
                // 先查询下料盘,对应的未上传的数据
                List<MesEntity> dbItem = UploadUnloadInfo(unloadCode);
                // 下料绑定
                int upCount = UploadAndUpdateInfo(unloadCode,dbItem);
                sw.Stop();
                TimeSpan ts2 = sw.Elapsed;
                log.WarnFormat("下料完成: 数据处理共计耗时: {0} ms.\r\n", ts2.TotalMilliseconds);
                // 计时结束

                // 写回结束状态,正常写 1
                short[] UnLoadFinish = new short[1] { 1 };
                if (codeEmpty) {
                    // 如果下料盘码读到空,写2
                    UnLoadFinish[0] = 2;
                }
                OperateResult finish = plcHelper.WriteShort(appAckTag.AddrName, UnLoadFinish, true);
                // 等待
                Thread.Sleep(appResetWait);
                // 完成归零
                FinishAck(plcHelper, addr, count, 0, appAckTag.AddrName, appAckRest);

                string ackMsg = string.Format("下料完成: 绑定完成,更新数据:[{2}]条,回写plc状态码:{0},是否写入成功:{1} <<<", UnLoadFinish[0], finish.IsSuccess, upCount);
                Interlocked.Exchange(ref _UnloadBinding, -1);                
                log.Info(ackMsg);
                ShowMsg(ackMsg);
            } catch (Exception e) {
                Interlocked.Exchange(ref _UnloadBinding, -1);
                log.InfoFormat("下料完成: 异常{0},{1}!!!", e.Message, e.StackTrace);
            }
        }
        private List<MesEntity> UploadUnloadInfo(string unloadToolCode) {
            // 查询工装码对应的数据,且未上传过的,防止重复上传
            string query = "select * from products where unloadToolCode=@unloadToolCode and unloadBinding is null";
            var queryP = new { unloadToolCode };
            IEnumerable<MesEntity> items = dbClient.Query<MesEntity>(query, queryP);
            if (items == null || items.Count() < 1) {
                return Array.Empty<MesEntity>().ToList();
            } else {
                List<MesEntity> dbItem = items.ToList();
                return dbItem;
            }
        }
        /// <summary>
        /// 单个帮盘数据上传
        /// </summary>
        /// <param name="unloadCode"></param>
        /// <param name="dbItem"></param>
        /// <returns></returns>
        private int UploadAndUpdateInfo(string unloadCode,List<MesEntity> dbItem) {
            // 获取全局参数
            MesContext mesCtx = MesContext.getCtx();
            string planBatch = mesCtx.PlanBatch;
            string model = mesCtx.Model;
            string devNo = mesCtx.DeviceNo;
            int count = 0;
            if (dbItem == null) {
                log.ErrorFormat("下料完成: 下料盘:[{0}] 未能找到需要更新的数据记录");
                return 0;
            }
            dbItem.ForEach(item => {
                try {
                    BaseResponse ret = MesHelper.UploadMesCansDataSliceBindingListLuJiaSigle(planBatch, model, devNo, item);
                    string resp = string.IsNullOrEmpty(ret.rawResp) ? ret.rawResp : ret.msg;
                    log.InfoFormat("下料盘数据绑定MES上传接口返回值: {0}", resp);
                    // 更新本地数据上传状态
                    int upCount = UpdateUnloadInfoSingle(item, ret);
                    count = count + upCount;
                } catch(Exception e) {
                    log.ErrorFormat("下料完成数据上抛异常: {0},{1}",e.Message,e.StackTrace);
                }
                
            });
            return count;
        }

        private int UpdateUnloadInfoSingle(MesEntity dbItem, BaseResponse mesRet) {
            if (dbItem == null ) {
                log.ErrorFormat("下料完成: 下料盘:[{0}] 未能找到需要更新的数据记录");
                return 0;
            }
            string updateTime = AppCfg.Now();
            int unloadBinding = -50;
            // 如果当前的 钢壳码,已经上传过了,认为成功
            bool repeat =!string.IsNullOrEmpty(mesRet.rawResp) && mesRet.rawResp.Contains("已经存在数据") && mesRet.rawResp.Contains(dbItem.barcode);
            if (string.Equals(mes_code_suc, mesRet.code) || repeat) {
                // 料斗码校验通过,或者MES异常不知道具体结果,所以可以重试 
                unloadBinding = 50;
            } else if (string.Equals(mes_code_exception, mesRet.code)) {
                unloadBinding = -51;
            }
            string unloadBindingMsg = string.IsNullOrEmpty(mesRet.rawResp) ? mesRet.msg : mesRet.rawResp;
            string upsql = "update  products set unloadBinding=@unloadBinding,unloadBindingMsg=@unloadBindingMsg,updateTime=@updateTime where myid=@myid;";
            int upCount = 0;
            string myid = dbItem.myId;
            var p = new { unloadBinding, unloadBindingMsg, updateTime, myid };
            int num = dbClient.Update(upsql, p);
            upCount = upCount + num;
            return upCount;
        }
        private  int UpdateUnloadInfo(string unloadToolCode, List<MesEntity> dbItem,BaseResponse uploadInfo) {           
            if(dbItem==null || dbItem.Count < 1) {
                log.ErrorFormat("下料完成: 下料盘:[{0}] 未能找到需要更新的数据记录");
                return 0 ;
            }
            string updateTime = AppCfg.Now();
            int unloadBinding = -50;
            if (string.Equals(mes_code_suc, uploadInfo.code)) {
                // 料斗码校验通过,或者MES异常不知道具体结果,所以可以重试 
                unloadBinding = 50;
            } else if (string.Equals(mes_code_exception, uploadInfo.code)) {
                unloadBinding = -51;
            }
            string unloadBindingMsg = string.IsNullOrEmpty(uploadInfo.rawResp) ? uploadInfo.msg : uploadInfo.rawResp;
            string upsql = "update  products set unloadBinding=@unloadBinding,unloadBindingMsg=@unloadBindingMsg,updateTime=@updateTime where myid=@myid;";
            int upCount = 0;
            dbItem.ForEach(item => {
                string myid = item.myId;
                var p = new { unloadBinding, unloadBindingMsg, updateTime, myid };
                int num = dbClient.Update(upsql, p);
                upCount = upCount + num;
            });
            return upCount;
            // string update = "update  * from products where unloadToolCode=@unloadToolCode;";
            // update products  set transCode=@transCode
            //string update = "update  products set unloadBinding=@unloadBinding,unloadBindingMsg=@unloadBindingMsg,updateTime=@updateTime where unloadToolCode=@unloadToolCode;";
            //string updateTime = AppCfg.Now();
            //int unloadBinding = -50;
            //if (string.Equals(mes_code_suc, mesRet.code) ) {
            //    // 料斗码校验通过,或者MES异常不知道具体结果,所以可以重试 
            //    unloadBinding = 50;
            //} else if(string.Equals(mes_code_exception, mesRet.code)) {
            //    unloadBinding = -51;
            //}
            //string unloadBindingMsg = string.IsNullOrEmpty(mesRet.rawResp) ? mesRet.msg : mesRet.rawResp;
            //var param = new { unloadBinding, unloadBindingMsg, updateTime,unloadToolCode };
            //int items = dbClient.Update(update, param);
            // return items;
        }
        #endregion

        #region PLC心跳包处理
        /// <summary>
        /// 工位-1心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station1HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc1State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc1State = tmp;
                }
            }
        }
        private int plc2State = -1;
        /// <summary>
        /// 工位-2心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station2HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc2State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc2State = tmp;
                }
            }
        }
        private int plc3State = -1;
        /// <summary>
        /// 工位-3心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station3HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc3State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc3State = tmp;
                }
            }
        }
        private int plc4State = -1;
        /// <summary>
        /// 工位-2心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station4HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc4State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc4State = tmp;
                }
            }
        }
        private int plc5State = -1;
        /// <summary>
        /// 工位-5心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station5HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc5State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc5State = tmp;
                }
            }
        }
        private int plc6State = -1;
        /// <summary>
        /// 工位-6心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station6HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;
            //(UInt16)(DateTime.Now.Second)

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc6State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc6State = tmp;
                }
            }
        }
        private int plc7State = -1;
        /// <summary>
        /// 工位-7心跳处理
        /// </summary>
        /// <param name="tag"></param>
        private void Station7HeartBeat(string plcId, Tag tag) {
            string addr = tag.AddrName;

            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);

            HslCommunication.OperateResult ret = plcHelper.WriteUInt16(addr, heartBeat);
            int tmp = -1;
            if (ret.IsSuccess) {
                tmp = 1;
            } else {
                tmp = 2;
            }
            if (this.appWin != null) {
                if (plc7State != tmp) {
                    this.appWin.OnPlcConnectionEvent(plcId, tmp);
                    plc7State = tmp;
                }
            }
        }
        #endregion PLC心跳包处理

        
        #region demo

        
        #endregion
        private ushort[] heartBeat = new ushort[1] { 1};

        #region 物料校验功能区
        MaterialDetails matWin;
        internal void SetMaterialWin(MaterialDetails matWin) {
            this.matWin = matWin;
        }
        // 当更新完批次号之后,自动调出物料校验功能,需要人工确认
        public void PlanBatchChanged() {
            if (this.matWin != null) {
                this.matWin.OnPlanBatchChanged();
            }
        }
        // 设备初始化时物料到位情况查询
        private short[] MatOk = new short[1] { 1 };
        private short[] MatNg = new short[1] { 0 };
        // 设备初始化,绿胶,物料校验查询
        public void OnQueryLvjiao() {
            bool suc = this.appWin.CheckLvjiao();
            // bool suc = matWin.CheckLvjiao();
            if (suc) {
                MaterialCheckResponse("st1", "app_lvjiao_ack", MatOk);
            } 
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st1", "app_lvjiao_ack", MatNg);
            //}
        }

        public void OnQueryZhengjier() {
            bool suc = this.appWin.CheckZhengjier();
            // bool suc = matWin.CheckZhengjier();
            if (suc) {
                MaterialCheckResponse("st3", "app_zhengjier_ack", MatOk);
            }
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st3", "app_zhengjier_ack", MatNg);
            //}
        }
        public void OnQueryFujier() {
            bool suc = this.appWin.CheckFujier();
            if (suc) {
                MaterialCheckResponse("st2", "app_fujier_ack", MatOk);
            }
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st2", "plc_fujier_trigger", MatNg);
            //}
        }
        public void OnQueryChajiaoK() {

            bool suc = appWin.CheckChajiaoK();
            if (suc) {
                MaterialCheckResponse("st4", "app_chajiaoK_ack", MatOk);
            }
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st4", "app_chajiaoK_ack", MatNg);
            //}
        }
        public void OnQueryChajiaoZ() {
            bool suc = appWin.CheckChajiaoZ();
            if (suc) {
                MaterialCheckResponse("st5", "app_chajiaoZ_ack", MatOk);
            } 
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st5", "app_chajiaoZ_ack", MatNg);
            //}
        }
        public void OnQueryShanggai() {
            bool suc = appWin.CheckShanggai();
            if (suc) {
                MaterialCheckResponse("st6", "app_shanggai_ack", MatOk);
            }
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st6", "app_shanggai_ack", MatNg);
            //}
        }
        public void OnQueryMifengquan() {
            bool suc = appWin.CheckMifengquan();
            if (suc) {
                MaterialCheckResponse("st6", "app_mifengquan_ack", MatOk);
            }
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st6", "app_mifengquan_ack", MatNg);
            //}
        }
        public void OnQueryXiagai() {
            bool suc = appWin.CheckXiagai();
            if (suc) {
                MaterialCheckResponse("st7", "app_xiagai_ack", MatOk);
            }
            //else {
            //    // 写入 PLC 物料 NG 信号
            //    MaterialCheckResponse("st7", "app_xiagai_ack", MatNg);
            //}
        }
        public bool MaterialCheckResponse(string plcId, string tagId, short[] ret) {
            PlcGroup plcG = PlcGroup.Get();
            PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
            Tag appAckTag = plcG.GetTag(plcId, tagId);
            string addr = appAckTag.AddrName;
            ushort count = (ushort)appAckTag.Count;
            return plcHelper.WriteShort(addr, ret, true).IsSuccess;
        }
        #endregion
        //public void ReadStrTest() {
        //    PlcGroup plcG = PlcGroup.Get();
        //    PlcHelper plcHelper = plcG.GetPlcHelper("st1");
        //    Tag testTag = plcG.GetTag("st1", "code_test");
        //    string addr = testTag.AddrName;
        //    ushort count = (ushort)testTag.Count;
        //    HslCommunication.OperateResult<string> ret = plcHelper.ReadString(addr, count);
        //    if(ret.IsSuccess) {
        //        this.appWin.ShowMsg(string.Format("读取到条码数据: {0}",ret.Content));
        //    } else {
        //        this.appWin.ShowMsg(string.Format("读取到条码数据失败: {0}", addr));
        //    }
        //}

        private Main appWin;
        private AppEventBus() {
            LoadConfig();
        }


        public static AppEventBus Inst() {
            if (eventBus == null) {
                lock (padlock) {
                    if (eventBus == null) {
                        eventBus = new AppEventBus();
                    }
                }
            }
            return eventBus;
        }
        internal void SetWin(Main chWin) {
            this.appWin = chWin;
        }
        private ToolsLifetime toolsWin;
        internal void SetFixtureWin(ToolsLifetime twin) {
            this.toolsWin = twin;
        }


        public void Start() {
            PlcGroup pg = PlcGroup.Get();
            pg.StartAll();
            // 数据库连接
            dbClient.Connect();
            LoadProducCache(bingdingDataCachePath);
        }

        public void Shutdown() {
            PlcGroup pg = PlcGroup.Get();
            pg.StopAll();
        }

        //public void MaterialCheckResult(bool ok) {
        //    if (this.appWin != null) {
        //        this.appWin.ChangeMatState(ok);
        //    }
        //}
        /// <summary>
        /// 7工位内容显示
        /// </summary>
        /// <param name="msg"></param>
        public void ShowMsg(string msg) {
            if (this.appWin != null) {
                this.appWin.ShowMsg_ST7(msg);
            }
        }
        public void ShowMsg_ST1(string msg) {
            if (this.appWin != null) {
                this.appWin.ShowMsg_ST1(msg);
            }
        }
        public void ShowMsg_ST6(string msg) {
            if (this.appWin != null) {
                this.appWin.ShowMsg_ST6(msg);
            }
        }
        public void ShowMsg_Line(string msg) {
            if (this.appWin != null) {
                this.appWin.ShowMsg_Line(msg);
            }
        }

        internal string GetFixtureBarcode(string fixtureName) {
            if (string.IsNullOrEmpty(fixtureName)|| this.toolsWin==null) {
                return null;
            }
            return this.toolsWin.GetFixtureBarcode(fixtureName);
        }
        #region 称重数据上传老接口
        private int _OnWeightFinishOld = -1;
        private void OnWeightFinishOld(string plcId, Tag trgTag) {
            try {
                string addr = trgTag.AddrName;
                ushort count = (ushort)trgTag.Count;
                int interval = trgTag.Interval;

                PlcGroup plcG = PlcGroup.Get();
                PlcHelper plcHelper = plcG.GetPlcHelper(plcId);
                Tag ackTag = plcG.GetTag(plcId, "app_weight_ack");

                HslCommunication.OperateResult<ushort[]> trigger = plcHelper.ReadUInt16(addr, count);
                if (!trigger.IsSuccess) return;
                ushort trgValue = trigger.Content[0];
                ushort ackValue = trigger.Content[1];
                if (!(trgValue == 100 || trgValue == 200 || trgValue == 300 && ackValue == 0)) return;
                if (!Need2ReadData(ref _OnWeightFinishOld, 1500)) return;

                log.InfoFormat("产品 称重: 开始处理 PLC触发数值=[{0}] >>>",trgValue);
                ShowMsg("称重完成: 开始处理......"+ trgValue);
                // 100,200,300为触发条件: 分别代表 产品1触发,产品2触发,2个产品都触发
                // 返回结果定义:
                // 100: 都ok
                // 200: 1 NG,2 OK
                // 300: 1 OK,2 NG
                // 500: 2个都NG
                ushort[] final = new ushort[1] { 99 };
                string idx = "1";
                if (trgValue == 100) {
                    idx = "1";
                    final = OnWeightFinish1_Old(plcId, plcHelper, trgTag, ackTag, 300, 500);

                } else if (trgValue == 200) {
                    idx = "2";
                    final = OnWeightFinish2_Old(plcId, plcHelper, trgTag, ackTag, 200, 500);

                } else if (trgValue == 300) {
                    idx = "1和2";
                    ushort[] ret1 = OnWeightFinish1_Old(plcId, plcHelper, trgTag, ackTag, 1, 2);
                    ushort[] ret2 = OnWeightFinish2_Old(plcId, plcHelper, trgTag, ackTag, 1, 2);
                    if (ret1[0] == 1 && ret2[0] == 1) {
                        final[0] = 100;
                    } else if (ret1[0] == 2 && ret2[0] == 2) {
                        final[0] = 500;
                    } else if (ret1[0] == 1 && ret2[0] == 2) {
                        final[0] = 300;
                    } else if (ret1[0] == 2 && ret2[0] == 1) {
                        final[0] = 200;
                    }
                }
                OperateResult errRet = plcHelper.WriteUInt16(ackTag.AddrName, final, true);
                log.InfoFormat("产品-{0} 称重:回写PLC状态码:{1},回写plc是否成功:{2}", idx, final[0].ToString(), errRet.IsSuccess);
                // 处理完之后需要完成归零
                // 等待
                Thread.Sleep(appResetWait);
                // 完成归零
                FinishAck(plcHelper, addr, count, 0, ackTag.AddrName, appAckRest);
                log.InfoFormat("产品-{0} 称重: 处理结束,回写plc结果: {1},是否成功:{2}<<<", idx, final[0], errRet.IsSuccess);
                Interlocked.Exchange(ref _OnWeightFinishOld, -1);
                ShowMsg(string.Format("称重数据处理完成,回写plc结果:{0},是否成功 {1} <<<", final[0], errRet.IsSuccess));
            } catch (Exception e) {
                Interlocked.Exchange(ref _OnWeightFinishOld, -1);
                log.InfoFormat("产品-{0} 称重: 处理异常:{0},{1} ", e.Message, e.StackTrace);
                ShowMsg(string.Format("称重数据处理发生异常: {0},{1}", e.Message, e.StackTrace));
            }
        }
        /// <summary>
        /// 产品称重结束
        /// </summary>
        /// <param name="plcId"></param>
        /// <param name="tag"></param>
        private ushort[] OnWeightFinish1_Old(string plcId, PlcHelper plcHelper, Tag triggerTag, Tag ackTag, ushort ok, ushort ng) {
            // 默认 0 是正常. 如果mes校验不通过,统统返回 6
            ushort[] WeightCheckFinishd = new ushort[1] { ok };
            try {
                PlcGroup plcG = PlcGroup.Get();

                // 产品-1 ================== 处理流程
                log.InfoFormat("产品-1 称重: 称重完成开始处理 >>>");

                // 读取 产品码
                Tag barcodeTag = plcG.GetTag(plcId, "plc_barcode_1");
                HslCommunication.OperateResult<string> code1Ret = plcHelper.ReadString(barcodeTag.AddrName, (ushort)barcodeTag.Count);
                string barcode = string.IsNullOrEmpty(code1Ret.Content) ? string.Empty : code1Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                if (!code1Ret.IsSuccess || string.IsNullOrEmpty(barcode)) {
                    string err1 = string.Format("产品-1 称重: 产品码为空,或者读取失败!", barcode);
                    log.Error(err1);
                    ShowMsg(err1);
                }
                // 读取 产品称重信息
                Tag weightTag = plcG.GetTag(plcId, "plc_weight_1");
                HslCommunication.OperateResult<float[]> weightRet = plcHelper.ReadFloat(weightTag.AddrName, (ushort)weightTag.Count);
                if (!weightRet.IsSuccess) {
                    string err2 = string.Format("产品-1 称重: 产品-1:[{0}] 重量数值失败!", barcode, weightRet.Content);
                    log.Error(err2);
                    ShowMsg(err2);
                }
                float weight = weightRet.Content[0];

                // 读取 产品称重判断结果
                Tag resultTag = plcG.GetTag(plcId, "plc_weight_result_1");
                HslCommunication.OperateResult<ushort[]> resultRet = plcHelper.ReadUInt16(resultTag.AddrName, (ushort)resultTag.Count);

                if (!resultRet.IsSuccess) {
                    string err3 = string.Format("产品-1 称重: 产品-1:[{0}] 称重结果读取失败!", barcode, resultRet.Content);
                    log.Error(err3);
                    ShowMsg(err3);
                }
                ushort result = resultRet.Content[0];

                string resultByMes = ValidWeightByMesStand(weight); ;// 依据mes端 设置的上下限而得出的 ok/ng 结果
               
                string ok_ng = (result == 1) ? "OK" : "NG";
                BaseResponse mecCheck = MesHelper.UploadWeightOld(barcode, weight, ok_ng);
                string msg = string.Format("产品-1:[{0}],重量[{1}],结果:[{2}],MES标准:[{3}],MES校验结果: code={4},msg={5}", barcode, weight, ok_ng, resultByMes, mecCheck.code, mecCheck.msg);
                log.Info(msg);
                ShowMsg(msg);

                if (AppCfg.Inst().MesisOnline && (!string.Equals(mecCheck.code, "0"))) {
                    // 与mes校验失败,固定写6
                    WeightCheckFinishd[0] = ng;
                } else if(string.Equals(mecCheck.code, "offline")) {
                    WeightCheckFinishd[0] = ok;
                }
                this.appWin.OnWeightData_1(barcode,weight, ok_ng, resultByMes);
                log.InfoFormat("产品-1 称重: 绑定完成<<<");

            } catch (Exception e) {
                WeightCheckFinishd[0] = ng;
                string err = string.Format("产品-1 称重: 异常{0},{1}!!!", e.Message, e.StackTrace);
                log.Error(err);
                ShowMsg(err);
            }
            return WeightCheckFinishd;
        }
        private ushort[] OnWeightFinish2_Old(string plcId, PlcHelper plcHelper, Tag triggerTag, Tag ackTag, ushort ok, ushort ng) {
            // 默认 0 是正常. 如果mes校验不通过,统统返回 6
            ushort[] WeightCheckFinishd = new ushort[1] { ok };
            try {
                PlcGroup plcG = PlcGroup.Get();

                // 产品-2 ================== 处理流程
                log.InfoFormat("产品-2 称重: 称重完成开始处理 >>>");

                // 读取 产品码
                Tag barcodeTag = plcG.GetTag(plcId, "plc_barcode_2");
                HslCommunication.OperateResult<string> code1Ret = plcHelper.ReadString(barcodeTag.AddrName, (ushort)barcodeTag.Count);
                string barcode = string.IsNullOrEmpty(code1Ret.Content) ? string.Empty : code1Ret.Content.Replace("\0", "").Replace("\r", "").Replace("\n", "").Trim();
                if (!code1Ret.IsSuccess || string.IsNullOrEmpty(barcode)) {
                    string err1 = string.Format("产品-2 称重: 产品码为空,或者读取失败!", barcode);
                    log.Error(err1);
                    ShowMsg(err1);
                }
                // 读取 产品称重信息
                Tag weightTag = plcG.GetTag(plcId, "plc_weight_2");
                HslCommunication.OperateResult<float[]> weightRet = plcHelper.ReadFloat(weightTag.AddrName, (ushort)weightTag.Count);
                if (!weightRet.IsSuccess) {
                    string err2 = string.Format("产品-2 称重: 产品-2:[{0}] 重量数值失败!", barcode, weightRet.Content);
                    log.Error(err2);
                    ShowMsg(err2);
                }
                float weight = weightRet.Content[0];

                // 读取 产品称重判断结果
                Tag resultTag = plcG.GetTag(plcId, "plc_weight_result_2");
                HslCommunication.OperateResult<ushort[]> resultRet = plcHelper.ReadUInt16(resultTag.AddrName, (ushort)resultTag.Count);

                if (!resultRet.IsSuccess) {
                    string err3 = string.Format("产品-2 称重: 产品-2:[{0}] 称重结果读取失败!", barcode, resultRet.Content);
                    log.Error(err3);
                    ShowMsg(err3);
                }
                ushort result = resultRet.Content[0];
                // 先本地保存
                string resultByMes = ValidWeightByMesStand(weight); ;// 依据mes端 设置的上下限而得出的 ok/ng 结果
                // MesEntity item = BindWeight(barcode, weight, result, resultByMes);


                string ok_ng = (result == 1) ? "OK" : "NG";
                BaseResponse mecCheck = MesHelper.UploadWeightOld(barcode, weight, ok_ng);
                ShowMsg(string.Format("产品-2:[{0}],重量[{1}],结果:[{2}],MES标准:[{3}],MES校验结果: code={4},msg={5}", barcode, weight, ok_ng, resultByMes, mecCheck.code, mecCheck.msg));

                if (AppCfg.Inst().MesisOnline && (!string.Equals(mecCheck.code, "0"))) {
                    // 与mes校验失败,固定写6
                    WeightCheckFinishd[0] = ng;
                } else if (string.Equals(mecCheck.code, "offline")) {
                    // 离线模式下写正常,保存本地后可以后续上传
                    WeightCheckFinishd[0] = ok;
                }

                this.appWin.OnWeightData_2(barcode, weight, ok_ng, resultByMes);
                log.InfoFormat("产品-2 称重: 绑定完成<<<");
            } catch (Exception e) {
                WeightCheckFinishd[0] = ng;
                string err = string.Format("产品-1 称重: 异常{0},{1}!!!", e.Message, e.StackTrace);
                log.Error(err);
                ShowMsg(err);
            }
            return WeightCheckFinishd;
        }

        #endregion
    }
}
