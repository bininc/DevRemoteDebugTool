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
using System.Windows.Threading;

namespace WpfAutoUpdate
{
    /// <summary>
    /// SetParameters.xaml 的交互逻辑
    /// </summary>
    public partial class SetParameters : Window
    {
        private TUpdateInfo _info;
        public SetParameters(TUpdateInfo info)
        {
            _info = info;
            InitializeComponent();
            if (_info != null)
            {
                txtServerIP_R.DataContext = _info;
                txtServerPort_R.DataContext = _info;
                txtApn_R.DataContext = _info;
                txtMac_R.DataContext = _info;
                txtMac.Text = _info.mac;
                txtSuperPhone1_R.DataContext = _info;
                txtSuperPhone2_R.DataContext = _info;
                txtSuperPhone3_R.DataContext = _info;
                txtSuperPhone4_R.DataContext = _info;
            }
        }

        private void ButtonSet_OnClick(object sender, RoutedEventArgs e)
        {
            if (_info != null && _info.client != null)
            {
                StringBuilder sb = new StringBuilder("**99*");
                string apn = txtApn.Text.Trim();
                string serverip = txtServerIP.Text.Trim();
                string serverport = txtServerPort.Text.Trim();
                string mac = txtMac.Text.Trim();
                string superphone1 = txtSuperPhone1.Text.Trim();
                string superphone2 = txtSuperPhone2.Text.Trim();
                string superphone3 = txtSuperPhone3.Text.Trim();
                string superphone4 = txtSuperPhone4.Text.Trim();
                if (chkApn.IsChecked == true)
                {
                    if (apn == "")
                    {
                        MessageBox.Show("接入点不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtApn.Focus();
                        return;
                    }
                    sb.AppendFormat("{0},", apn);
                }
                else
                {
                    sb.Append(",");
                }

                if (chkServerIP.IsChecked == true)
                {
                    if (serverip == "")
                    {
                        MessageBox.Show("服务器IP不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtServerIP.Focus();
                        return;
                    }
                    if (chkServerPort.IsChecked == true)
                    {
                        if (serverport == "")
                        {
                            MessageBox.Show("服务器端口不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            txtServerPort.Focus();
                            return;
                        }
                        sb.AppendFormat("{0}:{1},", serverip, serverport);
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(_info.serverPort1))
                        {
                            MessageBox.Show("如果不想更改服务器端口，请读取原来的端口！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        sb.AppendFormat("{0}:{1},", serverip, _info.serverPort1);
                    }
                }
                else
                {
                    if (chkServerPort.IsChecked == true)
                    {
                        if (serverport == "")
                        {
                            MessageBox.Show("服务器端口不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            txtServerPort.Focus();
                            return;
                        }
                        if (string.IsNullOrWhiteSpace(_info.serverIP1))
                        {
                            MessageBox.Show("如果不想更改服务器IP，请读取原来的IP！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        sb.AppendFormat("{0}:{1},", _info.serverIP1, serverport);
                    }
                    else
                    {
                        sb.Append(",");
                    }
                }

                if (chkMac.IsChecked == true)
                {
                    if (mac == "")
                    {
                        MessageBox.Show("终端识别码不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        txtMac.Focus();
                        return;
                    }
                    sb.AppendFormat("{0}", mac);
                }

                if (chkSuperPhone.IsChecked == true)
                {
                    if (superphone1 != "")
                        sb.AppendFormat(",{0}", superphone1);
                    if (superphone2 != "")
                        sb.AppendFormat(",{0}", superphone2);
                    if (superphone3 != "")
                        sb.AppendFormat(",{0}", superphone3);
                    if (superphone4 != "")
                        sb.AppendFormat(",{0}", superphone4);
                }

                sb.Append("#");

                string setStr = sb.ToString();
                if (setStr == "**99*,,#")
                {
                    MessageBox.Show("未选中任何设置项！", "提示", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                _info.client.EnterSetMode(); //进入设置模式
                _info.client.SendString(setStr); //设置参数
                _info.lastCmd = "reboot";
                this.DialogResult = true;
            }
        }

        private void ButtonRead_OnClick(object sender, RoutedEventArgs e)
        {
            if (_info != null && _info.client != null)
            {
                _info.client.GetParameters();
                Button btn = (Button)sender;
                DispatcherTimer timer = new DispatcherTimer(new TimeSpan(0, 0, 20), DispatcherPriority.Normal, (o, args) =>
                {
                    DispatcherTimer t = (DispatcherTimer)o;
                    t.Stop();
                    btn.IsEnabled = true;
                }, this.Dispatcher);

                btn.IsEnabled = false;
                timer.Start();
            }
        }

        private void ButtonCancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ChkMac_OnChecked(object sender, RoutedEventArgs e)
        {
            if (chkMac.IsChecked != true)
                chkMac.IsChecked = true;
        }
    }
}
