using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfAutoUpdate
{
    public class TUpdateInfo : INotifyPropertyChanged //继承接口INotifyPropertyChanged用于双向数据绑定
    {
        public event Action<string> MessageReceived;
        public event Action<TUpdateInfo> Removed;
        //事件委托
        public event PropertyChangedEventHandler PropertyChanged;

        private string _apn;
        private string _serverIp1;
        private string _hwVer;
        private Dictionary<string, string> _dicParameters;
        private string _serverPort1;
        private string _supervisorNumbers;
        private string _superPhone1;
        private string _superPhone2;
        private string _superPhone3;
        private string _superPhone4;
        private ClientThread _client;
        private bool _autoUpdate = false;
        private string _swVer;
        private string _features;
        private string _update = "-";
        private string _parameters;
        private DevStatus devStatus = DevStatus.UnKnown;
        private string sended1;
        private string cmdStr1;
        /// <summary>
        /// 更新数据最后发送时间
        /// </summary>
        public DateTime lastUpdSendTime;
        public readonly Dictionary<CmdId, DateTime> _cmdExecTime = new Dictionary<CmdId, DateTime>();
        private CmdId _lastCmd = CmdId.NULL;
        private bool pauseUpdate;
        private string _ip_port;
        private string _status = "识别连接...";
        private string _updatever;
        private string _down;
        private string _time;
        private string _ver;
        private string _oldver;
        private string _mac;

        public TUpdateInfo(ClientThread client)
        {
            this.client = client;
        }

        public string mac
        {
            get { return _mac; }
            set
            {
                if (value != null)
                    value = value.TrimStart('0');
                if (value != _mac)
                {
                    _mac = value;
                    OnPropertyChanged("mac");
                }
            }
        } //16	Text	车台MAC

        public string oldver { get { return _oldver; } set { _oldver = value; OnPropertyChanged("oldver"); } }
        public string ver { get { return _ver; } set { _ver = value; OnPropertyChanged("ver"); } }
        public string time { get { return _time; } set { _time = value; OnPropertyChanged("time"); } }
        public string down { get { return _down; } set { _down = value; OnPropertyChanged("down"); } }

        //string _remark;
        //public string remark { get { return _remark; } set { _remark = value; OnPropertyChanged("remark"); } }

        public string updatever { get { return _updatever; } set { _updatever = value; OnPropertyChanged("updatever"); } }  //目标版本

        public string status { get { return _status; } set { _status = value; OnPropertyChanged("status"); } }  //状态

        public string ip_port { get { return _ip_port; } private set { _ip_port = value; OnPropertyChanged("ip_port"); } }

        public bool FoceUpdate { get; set; }
        /// <summary>
        /// 是否需要升级
        /// </summary>
        public bool needUpdate
        {
            get
            {
                if (FoceUpdate)
                {
                    return true;
                }

                return !string.IsNullOrWhiteSpace(_oldver) && !string.IsNullOrWhiteSpace(_updatever) && _oldver != _updatever && _ver != _updatever;
            }
        }

        public ClientThread client
        {
            get
            {
                return _client;
            }
            set
            {
                if (value != null && value != _client)
                {
                    if (_client != null)
                    {
                        _client.MessageReceived = null;
                    }
                    _client = value;
                    _client.MessageReceived = OnMessageReceived;
                    ip_port = _client.socket?.RemoteEndPoint.ToString();
                }
            }
        }

        private void OnMessageReceived(string msg)
        {
            MessageReceived?.Invoke(msg);
        }

        public string features
        {
            get { return _features; }
            set { _features = value; hasFeatures = !string.IsNullOrWhiteSpace(_features); OnPropertyChanged("features"); }
        }
        public bool hasFeatures { get; private set; }

        public string update
        {
            get { return _update; }
            set { _update = value; OnPropertyChanged("update"); }
        }

        public string parameters
        {
            get { return _parameters; }
            set
            {
                _parameters = value;
                hasParameters = !string.IsNullOrWhiteSpace(_parameters);
                OnPropertyChanged("parameters");
            }
        }
        public bool hasParameters { get; private set; }

        public Dictionary<string, string> dicParameters
        {
            get { return _dicParameters; }
            set
            {
                _dicParameters = value;
                if (_dicParameters != null)
                {
                    if (_dicParameters.ContainsKey("VEID"))
                        mac = _dicParameters["VEID"];
                    if (string.IsNullOrWhiteSpace(mac) && dicParameters.ContainsKey("VEID(MOBNUMB)"))
                        mac = dicParameters["VEID(MOBNUMB)"];
                    if (_dicParameters.ContainsKey("SW VER"))
                        swVer = _dicParameters["SW VER"];
                    if (_dicParameters.ContainsKey("GPRS APN"))
                        apn = _dicParameters["GPRS APN"];
                    if (_dicParameters.ContainsKey("SERVER ADDR"))
                        serverIP1 = _dicParameters["SERVER ADDR"];
                    else if (_dicParameters.ContainsKey("SERVER IP"))
                        serverIP1 = _dicParameters["SERVER IP"];
                    if (_dicParameters.ContainsKey("HW VER"))
                        hwVer = _dicParameters["HW VER"];
                }
            }
        }

        public string dynamicParameters { get; set; }

        public string apn
        {
            get { return _apn; }
            set { _apn = value; OnPropertyChanged("apn"); }
        }

        public string serverIP1
        {
            get { return _serverIp1; }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var tmp = value.Split(':');
                    _serverIp1 = tmp[0];
                    if (tmp.Length > 1)
                        serverPort1 = tmp[1];
                    OnPropertyChanged("serverIP1");
                }
            }
        }

        public string serverPort1
        {
            get { return _serverPort1; }
            set { _serverPort1 = value; OnPropertyChanged("serverPort1"); }
        }

        public string hwVer
        {
            get { return _hwVer; }
            set
            {
                _hwVer = value; OnPropertyChanged("hwVer");
            }
        }

        public string swVer
        {
            get { return _swVer; }
            set { _swVer = value; OnPropertyChanged("swVer"); }
        }

        public int xlsIndex { get; set; }

        public bool IsUpdating { get; set; }
        public bool AutoUpdate
        {
            get { return _autoUpdate; }
            set
            {
                _autoUpdate = value;
                update = _autoUpdate ? "是" : "否";
                if (value)
                {
                    if (pauseUpdate)
                        PauseUpdate = false;
                    else
                    {
                        time = sended = down = "";
                    }

                    if (FoceUpdate)
                        update += "(强制)";
                }
                else
                {
                    FoceUpdate = false;
                    IsUpdating = false;
                }
            }
        }

        /// <summary>
        /// 暂停升级
        /// </summary>
        public bool PauseUpdate
        {
            get
            {
                return pauseUpdate;
            }
            set
            {
                pauseUpdate = value;
                if (pauseUpdate)
                {
                    lastCmd = CmdId.PauseUpdate;
                    update = "暂停";
                    IsUpdating = false;
                    time = DateTime.Now.ToFormatDateTimeStr();
                    DevStatus = DevStatus.PauseUpdate;
                }
                else
                {
                    lastCmd = CmdId.ContinueUpdate;
                    time = "";
                    DevStatus = DevStatus.ContinueUpdate;
                }
            }
        }

        public CmdId lastCmd
        {
            get => _lastCmd;
            set
            {
                _lastCmd = value;
                cmdStr = CmdInfo.GetCmdDesc(_lastCmd) + "...";
                if (_lastCmd != CmdId.StartUpdate && _lastCmd != CmdId.ContinueUpdate && _lastCmd != CmdId.PauseUpdate)
                    _cmdExecTime[_lastCmd] = DateTime.Now;
            }
        }

        public void SetCmdResult(CmdId cmd, bool? suc)
        {
            string cmdDesc = CmdInfo.GetCmdDesc(cmd);
            if (suc == null)
            {
                cmdStr = cmdDesc;
            }
            else
            {
                cmdStr = cmdDesc + (suc.Value ? "成功" : "失败");
            }

            _cmdExecTime.Remove(cmd);

            if (cmd == _lastCmd)
                _lastCmd = CmdId.NULL;
        }


        public string supervisorNumbers
        {
            get { return _supervisorNumbers; }
            set
            {
                _supervisorNumbers = value;
                if (!string.IsNullOrWhiteSpace(_supervisorNumbers))
                {
                    var tmp = _supervisorNumbers.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Where(
                        t =>
                        {
                            t = t.Trim();
                            return Common.IsNumricForNum(t);
                        }).ToArray();

                    if (tmp.Length <= 1)
                        superPhone1 = tmp[0].Trim();
                    else if (tmp.Length <= 2)
                    {
                        superPhone1 = tmp[0].Trim();
                        superPhone2 = tmp[1].Trim();
                    }
                    else if (tmp.Length <= 3)
                    {
                        superPhone1 = tmp[0].Trim();
                        superPhone2 = tmp[1].Trim();
                        superPhone3 = tmp[2].Trim();
                    }
                    else if (tmp.Length <= 4)
                    {
                        superPhone1 = tmp[0].Trim();
                        superPhone2 = tmp[1].Trim();
                        superPhone3 = tmp[2].Trim();
                        superPhone4 = tmp[3].Trim();
                    }
                    else if (tmp.Length > 4)
                    {
                        int len = tmp.Length;
                        superPhone1 = tmp[len - 4].Trim();
                        superPhone2 = tmp[len - 3].Trim();
                        superPhone3 = tmp[len - 2].Trim();
                        superPhone4 = tmp[len - 1].Trim();
                    }
                }
            }
        }

        public string superPhone1
        {
            get { return _superPhone1; }
            set { _superPhone1 = value; OnPropertyChanged("superPhone1"); }
        }

        public string superPhone2
        {
            get { return _superPhone2; }
            set { _superPhone2 = value; OnPropertyChanged("superPhone2"); }
        }

        public string superPhone3
        {
            get { return _superPhone3; }
            set { _superPhone3 = value; OnPropertyChanged("superPhone3"); }
        }

        public string superPhone4
        {
            get { return _superPhone4; }
            set { _superPhone4 = value; OnPropertyChanged("superPhone4"); }
        }

        public string sended { get => sended1; set { sended1 = value; OnPropertyChanged(nameof(sended)); } }
        public string cmdStr { get => cmdStr1; set { cmdStr1 = value; OnPropertyChanged(nameof(cmdStr)); } }

        public DevStatus DevStatus
        {
            get => devStatus; set
            {
                devStatus = value;
                switch (devStatus)
                {
                    case DevStatus.UnKnown:
                        status = "识别连接...";
                        break;
                    case DevStatus.TimeOut:
                        status = "连接超时";
                        break;
                    case DevStatus.Connect:
                        status = "已连接";
                        break;
                    case DevStatus.ReConnect:
                        status = "重新连接";
                        break;
                    case DevStatus.FlashRom:
                        status = "刷写Flash...";
                        break;
                    case DevStatus.Rebooting:
                        status = "重启中...";
                        break;
                    case DevStatus.StartDownloadRom:
                        status = "开始下载固件";
                        break;
                    case DevStatus.DownloadingRom:
                        status = "固件下载中...";
                        break;
                    case DevStatus.DownloadRomError:
                        status = "固件下载错误";
                        break;
                    case DevStatus.DownloadRomSuccess:
                        status = "固件下载完成";
                        break;
                    case DevStatus.UpdateSuccess:
                        status = "升级成功";
                        break;
                    case DevStatus.DisConnected:
                        status = "连接断开";
                        break;
                    case DevStatus.PauseUpdate:
                        status = "升级暂停";
                        break;
                    case DevStatus.StopUpdate:
                        status = "升级停止";
                        break;
                    case DevStatus.ContinueUpdate:
                        status = "升级继续";
                        break;
                    default:
                        break;
                }
            }
        }

        //实现接口INotifyPropertyChanged定义函数
        private void OnPropertyChanged(string propertyName)
        {
            try
            {

                PropertyChangedEventHandler handler = PropertyChanged;
                if (null != handler)
                {
                    handler.Invoke(this, new PropertyChangedEventArgs(propertyName));
                }
            }
            catch (Exception ex) { }
        }

        public void Remove(bool closeSocket = false)
        {
            if (closeSocket)
                client.Stop();
            if (client.main.dgUpdateList.Contains(this))
                client.main.dgUpdate.Dispatcher.Invoke(new Action(() => client.main.dgUpdateList.Remove(this)));
            Removed?.Invoke(this);
        }
    }

    public enum DevStatus
    {
        UnKnown,
        TimeOut,
        Connect,
        ReConnect,
        FlashRom,
        Rebooting,
        StartDownloadRom,
        DownloadingRom,
        DownloadRomError,
        DownloadRomSuccess,
        UpdateSuccess,
        DisConnected,
        PauseUpdate,
        StopUpdate,
        ContinueUpdate
    }
}
