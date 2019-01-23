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
        public event Action<ClientThread> ClientClosed;
        public event Action<string> MessageReceived;

        public TUpdateInfo updateInfo { get; private set; }

        private Socket _socket;
        public Socket socket
        {
            get { return _socket; }
            private set
            {
                if (_socket == value) return;

                if (_socket != null)
                {
                    _socket.Close();
                }
                _socket = value;
                _socket.BeginReceive(_recBuffer, 0, _recBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback),
    socket);
            }
        }
        private const int _recBufferSize = 2048;
        public MainWindow main { get; private set; }
        private readonly ServerThread _serverThread;
        /*
        HW Ver: M50_VE03, SW Ver: SWVER130
        NV Ver: UNI_STRONG,15:04:21 Jun 13 2015
        VGAP: UNI-STRONG, IMEI: 863092013877025
        GPRS APN:"ZY-DDN.BJ", UserName:"", Pwd:""
        SERVER ADDR: 10.251.65.38:7956
        VEID: 1423021432
        */
        readonly string StrCha = "*#00#";
        readonly byte[] bufCha;
        readonly string StrCha1 = "*#01#";
        readonly byte[] bufCha1;
        readonly string StrStop = "$DIAGSTOP";
        readonly byte[] bufStop;
        byte[] bufUpdate;
        readonly string StrUpStop = "**42#"; //停止升级
        readonly byte[] bufUpStop;
        readonly string StrReboot = "**00#";
        readonly byte[] bufReboot;
        private readonly string StrState2 = "*state=2"; //编辑模式
        private readonly byte[] bufState2;
        private readonly string StrState0 = "*state=0"; //正常模式
        private readonly byte[] bufState0;

        public string mac { get; private set; }
        public readonly DateTime ConnTime;
        private DateTime lastGetParaTime;
        private DateTime lastUpRecTime;
        private DateTime lastRecTime;
        private StringBuilder sb = new StringBuilder();
        private Timer _timer;
        private readonly byte[] _recBuffer;

        public ClientThread(ServerThread server, Socket sok, MainWindow mainw)
        {
            bufCha = Encoding.ASCII.GetBytes(StrCha);
            bufCha1 = Encoding.ASCII.GetBytes(StrCha1);
            bufStop = Encoding.ASCII.GetBytes(StrStop);
            bufUpStop = Encoding.ASCII.GetBytes(StrUpStop);
            bufReboot = Encoding.ASCII.GetBytes(StrReboot);
            bufState2 = Encoding.ASCII.GetBytes(StrState2);
            bufState0 = Encoding.ASCII.GetBytes(StrState0);
            _recBuffer = new byte[_recBufferSize];
            _serverThread = server;
            _socket = sok;
            _socket.ReceiveBufferSize = _recBufferSize;
            main = mainw;
            ConnTime = DateTime.Now;
        }

        ~ClientThread()
        {
            _timer?.Dispose();
        }
        public void Start()
        {
            updateInfo = new TUpdateInfo(this);
            main.dgUpdate.Dispatcher.Invoke(new Action(() => main.dgUpdateList.Add(updateInfo)));

            socket.BeginReceive(_recBuffer, 0, _recBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback),
                socket);

            lastRecTime = DateTime.Now;
            _timer = new Timer(state =>
            {

                if ((DateTime.Now - lastRecTime).TotalSeconds >= 180)
                {
                    if (string.IsNullOrWhiteSpace(updateInfo.mac))
                    {
                        updateInfo.Remove(true);
                    }
                    else
                    {
                        updateInfo.status = "连接超时";
                    }
                }
                else
                {
                    if (updateInfo.status == "连接超时")
                        updateInfo.status = "已连接";
                }
            }, null, 1000, 1000);

            updateInfo.status = "识别连接...";
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

        private void ReceiveCallback(IAsyncResult result)
        {
            Socket sok = (Socket)result.AsyncState;
            try
            {
                int len = sok.EndReceive(result);
                result.AsyncWaitHandle.Close();

                if (len == 0)
                {   //连接取消
                    OnClientClosed("连接断开");
                    CloseSocket();
                    return;
                }

                string str = Encoding.GetEncoding("GB2312").GetString(_recBuffer, 0, len);
                lastRecTime = DateTime.Now;
                Match mc00 = Match.Empty;

                if (!string.IsNullOrEmpty(str))
                {
                    //Debug.Write($"rec({len}):{str}");
                    MessageReceived?.BeginInvoke(str, null, null); //调用接收到消息事件

                    if (string.IsNullOrEmpty(updateInfo.lastCmd))
                    {
                        mc00 = Regex.Match(str, @"upd|download", RegexOptions.IgnoreCase);
                        if (mc00.Success)
                        {
                            updateInfo.lastCmd = "update";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(updateInfo.lastCmd))
                        sb.Append(str); //附加接收到的数据
                    else
                       if (sb.Length > 0) sb.Clear();

                    if (sb.Length > 0)
                    {
                        string sbStr = sb.ToString();

                        if ((string.IsNullOrEmpty(mac) || updateInfo.lastCmd == "readP"))
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
                                if ((DateTime.Now - lastGetParaTime).TotalSeconds > 5)
                                {   //超过5秒重新查询
                                    sb.Clear();
                                    GetParameters();
                                }
                            }
                            else
                            {
                                updateInfo.lastCmd = null;
                                sb.Clear();

                                if (!string.IsNullOrEmpty(mac))
                                {
                                    updateInfo.down = "读取配置信息成功";
                                }

                                string parameters = mc00.Groups[2].Value;
                                Dictionary<string, string> dicParameters = GetDictionaryFromParameters(parameters);
                                if (dicParameters.ContainsKey("VEID"))
                                    mac = dicParameters["VEID"];
                                if (string.IsNullOrWhiteSpace(mac) && dicParameters.ContainsKey("VEID(MOBNUMB)"))
                                    mac = dicParameters["VEID(MOBNUMB)"];

                                if (string.IsNullOrWhiteSpace(mac))
                                {
                                    //未提取到识别码 继续查找识别码
                                    sb.Clear();
                                    GetParameters();
                                }
                                else
                                {
                                    if (dicParameters.ContainsKey("VGAP") && dicParameters["VGAP"].IndexOf("BUBIAO", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        mac = mac.PadLeft(12, '0');
                                    }
                                    if (string.IsNullOrWhiteSpace(updateInfo.mac))
                                    {
                                        //当前连接未识别
                                        TUpdateInfo oldInfo = main.dgUpdateList.FirstOrDefault(x => x.mac == mac && x != updateInfo);
                                        if (oldInfo != null)
                                        {
                                            //激活历史相同客户端
                                            oldInfo.client.socket = socket;
                                            oldInfo.ip_port = socket?.RemoteEndPoint.ToString();
                                            oldInfo.features = mc00.Groups[1].Value;
                                            oldInfo.parameters = parameters;
                                            oldInfo.dicParameters = dicParameters;
                                            oldInfo.supervisorNumbers = mc00.Groups[3].Value;

                                            if (dicParameters.ContainsKey("SW VER"))
                                            {
                                                string cver = dicParameters["SW VER"].TrimStart('S', 'W', 'V', 'E', 'R');
                                                oldInfo.ver = cver; //当前版本
                                            }

                                            if (oldInfo.lastCmd == "reboot")
                                            {
                                                oldInfo.lastCmd = null;
                                                oldInfo.down = "重启完成";
                                                oldInfo.status = "已连接";
                                            }
                                            else if (oldInfo.down == "刷写Flash")
                                            {
                                                oldInfo.status = "重新连接";
                                                oldInfo.down = "升级成功";
                                                oldInfo.time = DateTimeHelper.DateTimeNowStr;
                                                if (main.AutoStop)
                                                    oldInfo.client.StopDebug();
                                            }
                                            else
                                            {
                                                oldInfo.status = "重新连接";
                                                oldInfo.down = "-";
                                            }

                                            updateInfo.Remove(); //移除自己
                                            Stop(true); //移除当前客户端
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

                                    if (updateInfo.status == "识别连接...")
                                    {
                                        updateInfo.status = "已连接";
                                        updateInfo.down = "";
                                    }
                                }
                            }
                            #endregion
                        }
                        else if (updateInfo.lastCmd == "readDP" &&
                            sbStr.IndexOf("Dynamic", StringComparison.OrdinalIgnoreCase) > 0 &&
                            sbStr.IndexOf("parameters", StringComparison.OrdinalIgnoreCase) > 0)
                        {   //读取动态参数
                            mc00 = Regex.Match(sbStr, @"-+\s*Dynamic\s*parameters\s*-+\n+(.+?)\n+[^\s]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            if (mc00.Success)
                            {
                                sb.Clear();
                                updateInfo.dynamicParameters = mc00.Groups[1].ToString();
                                updateInfo.lastCmd = null;
                                updateInfo.down = "读取动态参数成功";
                            }
                        }
                        else if (updateInfo.lastCmd == "reboot")
                        {   //重启指令
                            mc00 = Regex.Match(sbStr, @"Rebooting|Reboot", RegexOptions.IgnoreCase);
                            if (mc00.Success)
                            {
                                sb.Clear();
                                updateInfo.status = "重启中";
                            }
                        }
                        else if (updateInfo.lastCmd == "update")
                        {
                            sb.Clear();
                            string[] lines = sbStr.Split('\n');
                            int index = -1;
                            for (int i = 0; i < lines.Length; i++)
                            {
                                string line = lines[i];
                                if (string.IsNullOrEmpty(line)) continue;

                                if ((index = line.IndexOf("start_download", StringComparison.CurrentCultureIgnoreCase)) >= 0)
                                {
                                    updateInfo.down = "开始下载固件";
                                }
                                else if ((index = line.LastIndexOf("download:", StringComparison.CurrentCultureIgnoreCase)) >= 0)
                                {
                                    line = line.Substring(index);
                                    mc00 = Regex.Match(line, @"download:\s*((\d+)\s*\(.+\))", RegexOptions.IgnoreCase); //UPD download: 119808 (57.55%)
                                    if (mc00.Success)
                                    {
                                        if (!isStopUp && !updateInfo.PauseUpdate)
                                        {
                                            if (main.ProgressMode == "P")
                                            {
                                                string down = mc00.Groups[1].Value;
                                                if (updateInfo.down != down)
                                                    lastUpRecTime = DateTime.Now;
                                                updateInfo.down = down;
                                            }
                                            else if (main.ProgressMode == "T")
                                            {
                                                string down = mc00.Groups[2].Value;
                                                if (updateInfo.down != down)
                                                    lastUpRecTime = DateTime.Now;
                                                updateInfo.down = down;
                                            }

                                            if (!updateInfo.IsUpdating)
                                                updateInfo.IsUpdating = true;
                                            if (!updateInfo.AutoUpdate)
                                                updateInfo.AutoUpdate = true;

                                            if (updateInfo.needUpdate)
                                            {
                                                updateInfo.time = "升级中...";
                                            }
                                        }
                                    }
                                }
                                else if (line.Contains("image") && line.Contains("success") && line.Contains("real") && line.Contains("update"))//11,H,upd_dl.c,289,upd_dlack_handler,Download image successful, delay do real update
                                {
                                    updateInfo.down = "刷写Flash";
                                    updateInfo.status = "下载完成";
                                    if (updateInfo.FoceUpdate)
                                        updateInfo.FoceUpdate = false;
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
                        else
                        {

                        }
                    }

                    CheckUpdate();  //每次检查更新
                    CheckErrorLink();   //检查错误连接
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
            try
            {
                sok.BeginReceive(_recBuffer, 0, _recBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), sok);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("recError:" + ex.Message);
                OnClientClosed("连接中断");
                updateInfo.Remove(true); //移除自己
            }
        }

        private void OnClientClosed(string msg)
        {
            if (!string.IsNullOrWhiteSpace(updateInfo?.mac))
            {
                if (!string.IsNullOrWhiteSpace(msg) && !updateInfo.Updated)
                    updateInfo.status = msg;

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

        private void CheckUpdate()
        {
            if (string.IsNullOrWhiteSpace(updateInfo?.mac)) return;

            if (!updateInfo.AutoUpdate && main.dicUpdateMac.ContainsKey(updateInfo.mac))
                updateInfo.AutoUpdate = true;

            if (main.StartUpdate)
                updateInfo.updatever = main.updateVer;

            if (!main.StartUpdate) return;

            if (!updateInfo.needUpdate && updateInfo.AutoUpdate && !updateInfo.Updated)
            {
                if (string.IsNullOrWhiteSpace(updateInfo.time))
                {
                    if (updateInfo.IsUpdating)
                        StopUpdate();

                    updateInfo.down = "无需升级";
                    updateInfo.time = DateTimeHelper.DateTimeNowStr;
                    updateInfo.lastCmd = null;
                    updateInfo.AutoUpdate = false;
                    if (main.AutoStop)
                        StopDebug();
                }
            }
            else if (updateInfo.needUpdate && updateInfo.IsUpdating && updateInfo.Updated)
            {
                if (string.IsNullOrWhiteSpace(updateInfo.time) || updateInfo.time == "升级中...")
                {
                    if (updateInfo.FoceUpdate)
                    {
                        updateInfo.FoceUpdate = false;
                    }

                    updateInfo.IsUpdating = false;
                    //updateInfo.down = "已升级";
                    //updateInfo.status = "升级成功";
                    updateInfo.lastCmd = null;
                }
            }
            else
            {
                if (updateInfo.needUpdate && updateInfo.AutoUpdate)
                {
                    if (!updateInfo.IsUpdating && !updateInfo.Updated && !updateInfo.PauseUpdate)
                        StartUpdate();
                }
            }
        }

        private void CheckErrorLink()
        {
            if (lastUpRecTime == default(DateTime) || updateInfo.lastUpSendTime == default(DateTime)) return;
            if (!string.IsNullOrWhiteSpace(updateInfo?.mac) && updateInfo.IsUpdating && !updateInfo.PauseUpdate)
            {
                double lastDualSec = (DateTime.Now - lastUpRecTime).TotalSeconds;
                double lastSendDualSec = (DateTime.Now - updateInfo.lastUpSendTime).TotalSeconds;
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

                    if (!updateInfo.Updated && lastSendDualSec >= 60)
                        StartUpdate();
                }
            }
            if (updateInfo.PauseUpdate)
                updateInfo.lastUpSendTime = DateTime.Now;
        }

        /// <summary>
        /// 查询配置参数
        /// </summary>
        public void GetParameters()
        {
            if ((DateTime.Now - lastGetParaTime).TotalSeconds < 5) return;
            try
            {
                if (socket != null)
                {
                    socket.Send(bufCha);
                    lastGetParaTime = DateTime.Now;
                    Debug.WriteLine("GetParameters");
                    if (updateInfo != null)
                    {
                        updateInfo.down = "读取配置信息...";
                        updateInfo.lastCmd = "readP";
                    }
                }
            }
            catch
            {
            }
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
                    socket.Send(bufCha1);
                    if (updateInfo != null)
                    {
                        updateInfo.down = "读取动态参数...";
                        updateInfo.lastCmd = "readDP";
                    }
                }
            }
            catch
            {
            }
        }

        private bool isStopUp = false;

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
                    isStopUp = false;
                    updateInfo.IsUpdating = true;
                    updateInfo.lastUpSendTime = DateTime.Now;
                    updateInfo.down = "开始升级";
                    updateInfo.lastCmd = "update";
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
                    socket.Send(bufUpStop);
                    isStopUp = true;
                    if (updateInfo != null)
                    {
                        updateInfo.IsUpdating = false;
                        updateInfo.down = "停止升级";
                        updateInfo.lastCmd = null;
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
                    socket.Send(bufState2);
                    socket.Send(bufReboot);
                    if (updateInfo != null)
                    {
                        updateInfo.down = "重启设备...";
                        updateInfo.lastCmd = "reboot";
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
                    socket.Send(bufStop);
                    if (updateInfo != null)
                    {
                        updateInfo.down = "停止远程诊断";
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
                    socket.Send(bufState2);
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
                socket?.Send(bufState0);
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
