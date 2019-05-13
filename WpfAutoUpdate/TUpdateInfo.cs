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

        string _mac;
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
        string _oldver;
        public string oldver { get { return _oldver; } set { _oldver = value; OnPropertyChanged("oldver"); } }
        string _ver;
        public string ver { get { return _ver; } set { _ver = value; OnPropertyChanged("ver"); } }
        string _time;
        public string time { get { return _time; } set { _time = value; OnPropertyChanged("time"); } }
        string _down;
        public string down { get { return _down; } set { _down = value; OnPropertyChanged("down"); } }

        //string _remark;
        //public string remark { get { return _remark; } set { _remark = value; OnPropertyChanged("remark"); } }

        string _updatever;
        public string updatever { get { return _updatever; } set { _updatever = value; OnPropertyChanged("updatever"); } }  //目标版本

        private string _status;
        public string status { get { return _status; } set { _status = value; OnPropertyChanged("status"); } }  //状态

        private string _ip_port;
        public string ip_port { get { return _ip_port; } set { _ip_port = value; OnPropertyChanged("ip_port"); } }

        bool foceUpdate;
        public bool FoceUpdate
        {
            get
            {
                return foceUpdate;
            }
            set
            {
                foceUpdate = value;
            }
        }
        /// <summary>
        /// 是否需要升级
        /// </summary>
        public bool needUpdate
        {
            get
            {
                if (FoceUpdate)
                {
                    return !Updated;
                }

                return !string.IsNullOrWhiteSpace(_oldver) && !string.IsNullOrWhiteSpace(_updatever) && _oldver != _updatever && _ver != _updatever;
            }
        }

        public ClientThread client
        {
            get { return _client; }
            set
            {
                if (value != null && value != _client)
                {
                    _client = value;
                    _client.MessageReceived += x =>
                    {
                        if (MessageReceived != null)
                            MessageReceived(x);
                    };
                    ip_port = _client.socket?.RemoteEndPoint.ToString();
                }
            }
        }

        private string _features;
        private string _update = "-";
        private string _parameters;

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
        public TUpdateInfo(ClientThread client)
        {
            this.client = client;
        }

        public bool IsUpdating { get; set; }
        public bool Updated { get; set; }
        public bool AutoUpdate
        {
            get { return _autoUpdate; }
            set
            {
                _autoUpdate = value;
                update = _autoUpdate ? (PauseUpdate ? "暂停" : "是") : "否";
                if (value)
                {
                    //IsUpdating = true;
                    Updated = false;
                    time = null;
                }
                else
                {
                    //IsUpdating = false;
                    Updated = false;
                    time = "升级取消";
                    FoceUpdate = false;
                }
            }
        }
        bool pauseUpdate;
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
                    update = "暂停";
                    IsUpdating = false;
                }
            }
        }

        public string lastCmd { get; set; }
        public DateTime lastUpSendTime;
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

        //事件委托
        public event PropertyChangedEventHandler PropertyChanged;
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
            if (client.main.dgUpdateList.Contains(this))
                client.main.dgUpdate.Dispatcher.Invoke(new Action(() => client.main.dgUpdateList.Remove(this)));
            if (closeSocket)
                client?.Stop();
            Removed?.Invoke(this);
        }
    }
}
