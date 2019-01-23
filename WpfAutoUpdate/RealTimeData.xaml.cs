using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfAutoUpdate
{
    /// <summary>
    /// RealTimeData.xaml 的交互逻辑
    /// </summary>
    public partial class RealTimeData : Window
    {
        private TUpdateInfo _info;
        public TUpdateInfo Info => _info;
        private bool _isPause;
        public RealTimeData(TUpdateInfo info)
        {
            _info = info;
            InitializeComponent();
            if (_info != null)
            {
                _info.MessageReceived += _info_MessageReceived;
                _info.PropertyChanged += _info_PropertyChanged;
                _info.Removed += _info_Removed;
            }
            txtRec.MaxLines = 500;
            txtRec.IsReadOnly = true;
            Title = "在线维护[" + _info?.mac + "] " + _info?.ip_port;
            AppendText("开始监听实时数据...");
        }

        private void _info_Removed(TUpdateInfo obj)
        {
            this.Dispatcher.BeginInvoke(new Action(Close));
        }

        private void _info_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ip_port" || e.PropertyName == "mac")
                this.Dispatcher.Invoke(new Action(() => { Title = "在线维护[" + _info?.mac + "] " + _info?.ip_port; }));
        }

        private void _info_MessageReceived(string obj)
        {
            AppendText(obj);
        }

        private void AppendText(string text)
        {
            if (_isPause) return;

            txtRec.Dispatcher.Invoke(new Action(() =>
            {
                txtRec.AppendText($"{Environment.NewLine}[{DateTime.Now.ToString("HH:mm:ss.fff")}]↓↓↓{Environment.NewLine}");
                txtRec.AppendText(text);
                txtRec.ScrollToEnd();
            }));
        }

        private List<string> listHistory = new List<string>();
        private int hisIndex = -1;
        private void BtnSend_OnClick(object sender, RoutedEventArgs e)
        {
            string text = txtSend.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            listHistory.Add(text);
            txtSend.Clear();
            hisIndex = -1;
            if (_info != null && _info.client != null)
            {
                AppendText(string.Format("send:{0}", text));
                _info.client.SendString(text);
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (txtSend.IsFocused)
            {
                if (e.Key == Key.Up)
                {
                    if (hisIndex == -1)
                    {
                        txtSend.Text = listHistory.LastOrDefault();
                        hisIndex = listHistory.Count - 1;
                    }
                    else
                    {
                        hisIndex--;
                        if (hisIndex < 0)
                        {
                            hisIndex = 0;
                            return;
                        }
                        if (listHistory.Any())
                        {
                            txtSend.Text = listHistory[hisIndex];
                        }
                    }
                }
                if (e.Key == Key.Down)
                {
                    if (hisIndex == -1) return;
                    hisIndex++;
                    if (hisIndex > listHistory.Count)
                        hisIndex = listHistory.Count;
                    if (hisIndex == listHistory.Count)
                    {
                        txtSend.Clear();
                        return;
                    }
                    txtSend.Text = listHistory[hisIndex];
                }
            }
            base.OnKeyUp(e);
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (btnPause.Tag == null)
            {
                _isPause = true;
                btnPause.Tag = "pause";
                btnPause.Content = "开始滚屏";
            }
            else
            {
                _isPause = false;
                btnPause.Tag = null;
                btnPause.Content = "暂停滚屏";
            }
        }

        private void btnSetMode_Click(object sender, RoutedEventArgs e)
        {
            if (_info != null && _info.client != null)
            {
                _info.client.SendString("*state=2");
            }
        }
    }
}
