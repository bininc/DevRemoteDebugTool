using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

using System.Collections.ObjectModel;
using System.Diagnostics;
using NPOI.SS.Formula.Eval;

namespace WpfAutoUpdate
{
    public class ClientThread
    {
        public Action<ClientThread> ClientClosed;
        public Action<string> MessageReceived;

        public TUpdateInfo updateInfo
        {
            get
            {
                return main.dgUpdateList.FirstOrDefault(x => x.client == this);
            }
        }

        public Socket socket { get; private set; }
        private const int _recBufferSize = 2048;
        public MainWindow main { get; private set; }
        private readonly ServerThread _serverThread;

        byte[] bufUpdate;

        public string mac { get; private set; }
        public readonly DateTime ConnTime;
        private DateTime lastUpRecTime;
        private DateTime lastRecTime;
        private StringBuilder sb = new StringBuilder();
        private Timer _timer;
        private readonly byte[] _recBuffer;

        public ClientThread(ServerThread server, Socket sok, MainWindow mainw)
        {
            _recBuffer = new byte[_recBufferSize];
            _serverThread = server;
            socket = sok;
            socket.ReceiveBufferSize = _recBufferSize;
            main = mainw;
            ConnTime = DateTime.Now;
        }

        ~ClientThread()
        {
            _timer?.Dispose();
            CloseSocket();
        }
        public void Start()
        {
            ReceiveNextData(socket);

            lastRecTime = DateTime.Now;
            _timer = new Timer(state =>
            {
                if (updateInfo == null) return;
                if ((DateTime.Now - lastRecTime).TotalSeconds >= 180)
                {
                    if (string.IsNullOrWhiteSpace(updateInfo.mac))
                    {
                        updateInfo.Remove(true);
                    }
                    else
                    {
                        updateInfo.DevStatus = DevStatus.TimeOut;
                    }
                }
                else
                {
                    if (updateInfo.DevStatus == DevStatus.TimeOut)
                        updateInfo.DevStatus = DevStatus.ReConnect;
                }
            }, null, 1000, 1000);

            GetParameters();
        }

        public void Stop(bool openSocket = false)
        {
            _timer?.Dispose();
            _timer = null;

            if (!openSocket)
                CloseSocket();

            if (_serverThread.listClient.Contains(this))
                _serverThread.listClient.Remove(this);
        }

        private void ReceiveNextData(Socket sok)
        {
            if (sok == null) return;
            try
            {
                if (sok.Connected)
                    sok.BeginReceive(_recBuffer, 0, _recBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), sok);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("recError:" + ex.Message);
                OnClientClosed("连接中断");
                updateInfo.Remove(true); //移除自己
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            Socket sok = (Socket)result.AsyncState;
            try
            {
                if (!sok.Connected) return;

                int len = sok.EndReceive(result);
                result.AsyncWaitHandle.Close();

                if (len == 0)
                {   //连接取消
                    OnClientClosed("连接断开");
                    CloseSocket();
                    return;
                }

                if (updateInfo.DevStatus == DevStatus.DisConnected)
                    updateInfo.DevStatus = DevStatus.ReConnect;

                string str = Encoding.GetEncoding("GB2312").GetString(_recBuffer, 0, len);
                lastRecTime = DateTime.Now;

                if (!string.IsNullOrEmpty(str))
                {
                    //Debug.Write($"rec({len}):{str}");
                    MessageReceived?.BeginInvoke(str, null, null); //调用接收到消息事件

                    if (updateInfo.lastCmd != CmdId.NULL)
                        sb.Append(str); //附加接收到的数据
                    else
                       if (sb.Length > 0) sb.Clear();

                    if (sb.Length > 0)
                    {
                        string sbStr = sb.ToString();
                        Match mc00 = Match.Empty;

                        if (string.IsNullOrEmpty(mac) || updateInfo.lastCmd == CmdId.ReadPara)
                        {
                            #region 识别设备
                            bool nd = (sbStr.IndexOf("Features", StringComparison.OrdinalIgnoreCase) > 0 &&
                                        sbStr.IndexOf("Parameters", StringComparison.OrdinalIgnoreCase) > 0 &&
                                        sbStr.IndexOf("Supervisor", StringComparison.OrdinalIgnoreCase) > 0 &&
                                        sbStr.IndexOf("numbers", StringComparison.OrdinalIgnoreCase) > 0 &&
                                        sbStr.IndexOf("End", StringComparison.OrdinalIgnoreCase) > 0);
                            if (nd)
                                mc00 = Regex.Match(sbStr, @"-+\s*Features\s*-+\n+(.+)\n+-+\s*Parameters\s*-+\n+(.+)\n+-+\s*Supervisor\s*numbers\s*-+\n+(.+)\n+-+\s*End\s*-+", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            if (!mc00.Success)
                            {
                                if (GetParameters())
                                {
                                    sb.Clear();
                                }
                            }
                            else
                            {
                                sb.Clear();

                                string parameters = mc00.Groups[2].Value;
                                Dictionary<string, string> dicParameters = GetDictionaryFromParameters(parameters);
                                if (dicParameters.ContainsKey("VEID"))
                                    mac = dicParameters["VEID"];
                                if (string.IsNullOrWhiteSpace(mac) && dicParameters.ContainsKey("VEID(MOBNUMB)"))
                                    mac = dicParameters["VEID(MOBNUMB)"];

                                if (string.IsNullOrWhiteSpace(mac))
                                {
                                    updateInfo.SetCmdResult(CmdId.ReadPara, false);
                                    //未提取到识别码 继续查找识别码
                                    GetParameters();
                                }
                                else
                                {
                                    updateInfo.SetCmdResult(CmdId.ReadPara, true);
                                    if (string.IsNullOrWhiteSpace(updateInfo.mac))
                                    {
                                        //当前连接未识别
                                        TUpdateInfo oldInfo = main.dgUpdateList.FirstOrDefault(x => x.mac == mac);
                                        if (oldInfo != null)
                                        {
                                            //激活历史相同客户端                                                                                       
                                            oldInfo.features = mc00.Groups[1].Value;
                                            oldInfo.parameters = parameters;
                                            oldInfo.dicParameters = dicParameters;
                                            oldInfo.supervisorNumbers = mc00.Groups[3].Value;

                                            if (dicParameters.ContainsKey("SW VER"))
                                            {
                                                string cver = dicParameters["SW VER"].TrimStart('S', 'W', 'V', 'E', 'R');
                                                oldInfo.ver = cver; //当前版本
                                            }

                                            if (oldInfo.lastCmd == CmdId.Reboot)
                                            {
                                                oldInfo.SetCmdResult(CmdId.Reboot, true);
                                            }
                                            else
                                            {
                                                //oldInfo.down = "-";
                                            }
                                            if (oldInfo.DevStatus != DevStatus.FlashRom)
                                                oldInfo.DevStatus = DevStatus.ReConnect;
                                            else
                                            {
                                                if (oldInfo.FoceUpdate)
                                                    oldInfo.FoceUpdate = false;
                                            }

                                            oldInfo.client.Stop(); //移除旧连接
                                            updateInfo.Remove(); //移除自己
                                            oldInfo.client = this; //将新连接赋值给已存在信息
                                            ReceiveNextData(sok);
                                            return;
                                        }
                                    }

                                    updateInfo.mac = mac;
                                    updateInfo.features = mc00.Groups[1].Value;
                                    updateInfo.parameters = parameters;
                                    updateInfo.dicParameters = dicParameters;
                                    updateInfo.supervisorNumbers = mc00.Groups[3].Value;

                                    if (dicParameters.ContainsKey("SW VER"))
                                    {
                                        string cver = dicParameters["SW VER"].TrimStart('S', 'W', 'V', 'E', 'R');
                                        updateInfo.ver = cver; //当前版本

                                        if (string.IsNullOrWhiteSpace(updateInfo.oldver))
                                            updateInfo.oldver = cver; //旧版本为空 也赋值为当前版本
                                    }

                                    if (updateInfo.DevStatus == DevStatus.UnKnown)
                                    {
                                        updateInfo.DevStatus = DevStatus.Connect;
                                    }
                                }
                            }
                            #endregion
                        }
                        else if (updateInfo.lastCmd == CmdId.ReadDynamicPara &&
                            sbStr.IndexOf("Dynamic", StringComparison.OrdinalIgnoreCase) > 0 &&
                            sbStr.IndexOf("parameters", StringComparison.OrdinalIgnoreCase) > 0)
                        {   //读取动态参数
                            mc00 = Regex.Match(sbStr, @"-+\s*Dynamic\s*parameters\s*-+\n+(.+?)\n+[^\s]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            if (mc00.Success)
                            {
                                sb.Clear();
                                updateInfo.dynamicParameters = mc00.Groups[1].ToString();
                                updateInfo.SetCmdResult(CmdId.ReadDynamicPara, true);
                            }
                        }
                        else if (updateInfo.lastCmd == CmdId.Reboot)
                        {   //重启指令
                            mc00 = Regex.Match(sbStr, @"Rebooting|Reboot", RegexOptions.IgnoreCase);
                            if (mc00.Success)
                            {
                                sb.Clear();
                                updateInfo.DevStatus = DevStatus.Rebooting;
                            }
                        }
                        else if (updateInfo.lastCmd == CmdId.StartUpdate || updateInfo.lastCmd == CmdId.ContinueUpdate)
                        {
                            sb.Clear();
                            string[] lines = sbStr.Split('\n');
                            int index = -1;
                            for (int i = 0; i < lines.Length; i++)
                            {
                                string line = lines[i];
                                if (string.IsNullOrEmpty(line)) continue;

                                if ((index = line.IndexOf("start_download", StringComparison.OrdinalIgnoreCase)) >= 0)
                                {
                                    updateInfo.DevStatus = DevStatus.StartDownloadRom;
                                }
                                else if ((index = line.LastIndexOf("download:", StringComparison.OrdinalIgnoreCase)) >= 0)
                                {
                                    updateInfo.DevStatus = DevStatus.DownloadingRom;
                                    line = line.Substring(index);
                                    mc00 = Regex.Match(line, @"download:\s*((\d+)\s*\(.+\))", RegexOptions.IgnoreCase); //UPD download: 119808 (57.55%)
                                    if (mc00.Success)
                                    {
                                        string down = null;
                                        if (main.ProgressMode == "P")
                                        {
                                            down = mc00.Groups[1].Value;
                                        }
                                        else if (main.ProgressMode == "T")
                                        {
                                            down = mc00.Groups[2].Value;
                                        }
                                        if (updateInfo.down != down)
                                            lastUpRecTime = DateTime.Now;
                                        updateInfo.down = down;

                                        if (!updateInfo.IsUpdating)
                                            updateInfo.IsUpdating = true;

                                        updateInfo.time = "升级中...";
                                    }

                                }
                                else if (line.Contains("image") && line.Contains("success") && line.Contains("real") && line.Contains("update"))//11,H,upd_dl.c,289,upd_dlack_handler,Download image successful, delay do real update
                                {
                                    string down = null;
                                    if (main.ProgressMode == "P")
                                    {
                                        down = main.fileStream.Length + " (100%)";
                                    }
                                    else if (main.ProgressMode == "T")
                                    {
                                        down = main.fileStream.Length.ToString();
                                    }

                                    if (updateInfo.down != down)
                                        lastUpRecTime = DateTime.Now;
                                    updateInfo.down = down;

                                    updateInfo.DevStatus = DevStatus.FlashRom;
                                }
                                else if (line.Contains("upd_task") && line.Contains("download") && line.Contains("timeout"))
                                {
                                    double lastDualSec = (DateTime.Now - lastUpRecTime).TotalSeconds;
                                    if (lastDualSec >= 1800) //距离上次发送时间超过半个小时
                                    {
                                        RebootDevice();//重启车机 超过半小时
                                        lastUpRecTime = DateTime.Now;
                                    }
                                }
                            }

                        }
                        else if (updateInfo.lastCmd == CmdId.SetPara)
                        {
                            sb.Clear();
                            mc00 = Regex.Match(sbStr, "!!!.+!!!", RegexOptions.IgnoreCase);
                            if (mc00.Success)
                            {
                                updateInfo.SetCmdResult(CmdId.SetPara, false);
                            }
                            else
                            {
                                mc00 = Regex.Match(sbStr, "reboot", RegexOptions.IgnoreCase);
                                if (mc00.Success)
                                {
                                    updateInfo.SetCmdResult(CmdId.SetPara, true);
                                    updateInfo.DevStatus = DevStatus.Rebooting;
                                }
                            }
                        }
                        else if (updateInfo.lastCmd == CmdId.StopUpdate)
                        {
                            sb.Clear();
                            mc00 = Regex.Match(sbStr, "lmt_stop_update", RegexOptions.IgnoreCase);
                            if (mc00.Success)
                            {
                                updateInfo.time = DateTime.Now.ToFormatDateTimeStr();
                                updateInfo.DevStatus = DevStatus.StopUpdate;
                                updateInfo.SetCmdResult(CmdId.StopUpdate, true);
                            }
                        }
                    }

                    CheckUpdate(str);  //每次检查更新
                    CheckErrorLink();   //检查错误连接
                    CheckCmdExecState();
                }
            }
            catch (Exception e)
            {
                if (e is SocketException)
                {
                    Debug.WriteLine("recErro:" + e.Message);
                    OnClientClosed("连接中断");
                    if (string.IsNullOrEmpty(mac))
                    {
                        updateInfo.Remove(true); //移除自己
                    }
                    return;
                }
                else if (e is ObjectDisposedException)
                {
                    OnClientClosed("连接中断");
                    return;
                }
                else
                    Debug.WriteLine(e.Message);
            }

            ReceiveNextData(sok);
        }

        private void OnClientClosed(string msg)
        {
            if (!string.IsNullOrWhiteSpace(updateInfo?.mac))
            {
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    updateInfo.DevStatus = DevStatus.DisConnected;
                    updateInfo.status = msg;

                    if (updateInfo.lastCmd == CmdId.StopDebug)
                        updateInfo.SetCmdResult(CmdId.StopDebug, true);
                }

                main.SaveExcel(updateInfo); //保存到Excel
            }

            ClientClosed?.Invoke(this);
        }

        public Dictionary<string, string> GetDictionaryFromParameters(string parameters)
        {
            Dictionary<string, string> dicParameters = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(parameters)) return dicParameters;

            string[] tmpArr = parameters.Split('\n');
            foreach (string s in tmpArr)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    if (s.StartsWith("NV", StringComparison.CurrentCultureIgnoreCase))
                    {
                        int index = s.IndexOf(':');
                        if (index > 0)
                        {
                            string key = s.Substring(0, index);
                            string val = s.Substring(index + 1);
                            dicParameters.Add(key.Trim().ToUpper(), val.Trim().Trim('"'));
                        }
                    }
                    else
                    {
                        string[] ts = s.Split(',');
                        foreach (string s1 in ts)
                        {
                            int index = s1.IndexOf(':');
                            if (index > 0)
                            {
                                string key = s1.Substring(0, index);
                                string val = s1.Substring(index + 1);
                                dicParameters.Add(key.Trim().ToUpper(), val.Trim().Trim('"'));
                            }
                        }
                    }
                }
            }

            return dicParameters;
        }

        private void CheckUpdate(string readStr = null)
        {
            if (string.IsNullOrWhiteSpace(updateInfo?.mac)) return;

            if (!main.StartUpdate) return;

            if (updateInfo.lastCmd == CmdId.NULL && updateInfo.DevStatus == DevStatus.Connect)
            {
                Match mc00 = Regex.Match(readStr, @"upd|download", RegexOptions.IgnoreCase);
                if (mc00.Success)
                {
                    updateInfo.AutoUpdate = true;
                    updateInfo.IsUpdating = true;
                    updateInfo.updatever = main.updateVer;
                    if (updateInfo.updatever != updateInfo.ver) //跳过版本号相同的情况
                        updateInfo.lastCmd = CmdId.ContinueUpdate;
                }
            }

            if (!updateInfo.AutoUpdate)
            {
                if (updateInfo.IsUpdating)
                {
                    updateInfo.IsUpdating = false;
                    StopUpdate();
                }
                return;
            }

            if (!updateInfo.needUpdate)
            {   //检测到版本号一致，无需升级
                if (updateInfo.lastCmd != CmdId.StartUpdate && updateInfo.lastCmd != CmdId.ContinueUpdate)
                {
                    updateInfo.sended = "无需升级";

                    if (updateInfo.IsUpdating)
                        StopUpdate();

                    updateInfo.AutoUpdate = false;

                    if (main.AutoStop)
                        StopDebug();
                }
                else
                {   //固件下载完成
                    if (updateInfo.DevStatus == DevStatus.FlashRom)
                    { //固件刷写完成
                        if (updateInfo.lastCmd == CmdId.StartUpdate)
                            updateInfo.SetCmdResult(CmdId.StartUpdate, true);
                        else if (updateInfo.lastCmd == CmdId.ContinueUpdate)
                            updateInfo.SetCmdResult(CmdId.ContinueUpdate, true);

                        updateInfo.time = DateTimeHelper.DateTimeNowStr;
                        updateInfo.DevStatus = DevStatus.UpdateSuccess;
                        updateInfo.IsUpdating = false;
                        updateInfo.AutoUpdate = false;
                        if (main.AutoStop)
                            StopDebug();
                    }
                }
            }
            else
            {
                if (updateInfo.lastCmd != CmdId.StartUpdate && updateInfo.lastCmd != CmdId.ContinueUpdate && updateInfo.lastCmd != CmdId.PauseUpdate)
                    StartUpdate();
            }
        }

        private void CheckErrorLink()
        {
            if (string.IsNullOrWhiteSpace(updateInfo?.mac)) return;
            if (lastUpRecTime == default(DateTime) || updateInfo.lastUpdSendTime == default(DateTime)) return;
            if (!updateInfo.AutoUpdate) return;

            if (updateInfo.PauseUpdate)
            {
                updateInfo.lastUpdSendTime = DateTime.Now;
                return;
            }

            if (updateInfo.DevStatus == DevStatus.StartDownloadRom || updateInfo.DevStatus == DevStatus.DownloadingRom)
            {
                double lastDualSec = (DateTime.Now - lastUpRecTime).TotalSeconds;
                double lastSendDualSec = (DateTime.Now - updateInfo.lastUpdSendTime).TotalSeconds;
                if (lastDualSec >= 40) //距离上次接收更新数据超过40秒
                {
                    if (lastDualSec > 180) //距离上次接收到更新数据超过180秒
                    {
                        if (lastSendDualSec >= 60)
                        {
                            //重新启动升级
                            StopUpdate();
                            Thread.Sleep(1000);
                            StartUpdate();
                        }
                    }

                    if (lastSendDualSec >= 60)
                        StartUpdate();
                }
            }

        }


        private void CheckCmdExecState()
        {
            foreach (var item in updateInfo._cmdExecTime.ToArray())
            {
                if ((DateTime.Now - item.Value).TotalSeconds > 60)
                {   //超过60秒 命令没有反馈 判定为超时
                    updateInfo.SetCmdResult(item.Key, false);
                }
            }
        }

        /// <summary>
        /// 查询终端信息
        /// </summary>
        public bool GetParameters()
        {
            if (updateInfo._cmdExecTime.ContainsKey(CmdId.ReadPara))
                if ((DateTime.Now - updateInfo._cmdExecTime[CmdId.ReadPara]).TotalSeconds < 5) return false;
            try
            {
                if (socket != null)
                {
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.ReadPara));
                    if (updateInfo != null)
                    {
                        updateInfo.lastCmd = CmdId.ReadPara;
                    }
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// 查询动态参数
        /// </summary>
        public void GetDynamicParameters()
        {
            try
            {
                if (socket != null)
                {
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.ReadDynamicPara));
                    if (updateInfo != null)
                    {
                        updateInfo.lastCmd = CmdId.ReadDynamicPara;
                    }
                }
            }
            catch
            {
            }
        }


        /// <summary>
        /// 开始升级
        /// </summary>
        private void StartUpdate()
        {
            try
            {
                if (updateInfo == null) return;

                bool same = updateInfo.FoceUpdate;
                if (!string.IsNullOrWhiteSpace(main.strUpdatefmt))
                {
                    string upStr = main.strUpdate(same);
                    bufUpdate = Encoding.ASCII.GetBytes(upStr);
                }


                if (socket != null && bufUpdate != null)
                {
                    socket.Send(bufUpdate);
                    updateInfo.lastUpdSendTime = DateTime.Now;
                    updateInfo.lastCmd = CmdId.StartUpdate;
                }
            }
            catch
            {
            }
        }


        /// <summary>
        /// 停止升级
        /// </summary>
        public void StopUpdate()
        {
            try
            {
                if (socket != null)
                {
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.StopUpdate));
                    if (updateInfo != null)
                    {
                        updateInfo.lastCmd = CmdId.StopUpdate;
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 重启设备
        /// </summary>
        public void RebootDevice()
        {
            try
            {
                if (socket != null)
                {
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.State2));
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.Reboot));
                    if (updateInfo != null)
                    {
                        updateInfo.lastCmd = CmdId.Reboot;
                    }
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// 停止调试
        /// </summary>
        public void StopDebug()
        {
            try
            {
                if (socket != null)
                {
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.StopDebug));
                    if (updateInfo != null)
                    {
                        updateInfo.lastCmd = CmdId.StopDebug;
                    }
                }
            }
            catch
            {

            }
        }

        /// <summary>
        /// 关闭Socket
        /// </summary>
        public void CloseSocket()
        {
            if (socket != null)
            {
                socket.Close();
            }
        }

        /// <summary>
        /// 进入设置模式
        /// </summary>
        public void EnterSetMode()
        {
            try
            {
                if (socket != null)
                    socket.Send(CmdInfo.GetCmdBytes(CmdId.State2));
            }
            catch
            {

            }
        }
        /// <summary>
        /// 退出设置模式
        /// </summary>
        public void ExitSetMode()
        {
            try
            {
                socket?.Send(CmdInfo.GetCmdBytes(CmdId.State0));
            }
            catch
            {

            }
        }

        /// <summary>
        /// 发送字符串
        /// </summary>
        /// <param name="str"></param>
        public void SendString(string str)
        {
            if (str == null) return;
            try
            {
                byte[] buffer = Encoding.ASCII.GetBytes(str);
                if (socket != null)
                    socket.Send(buffer);
            }
            catch
            {

            }
        }
    }


}
