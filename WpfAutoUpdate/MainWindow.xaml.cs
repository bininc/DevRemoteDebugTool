using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Windows.Input;
using System.Windows.Interop;

namespace WpfAutoUpdate
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ServerThread MainThread;
        private UpdateServer_2G updateServer_2G;

        public ObservableCollection<TUpdateInfo> dgUpdateList = new ObservableCollection<TUpdateInfo>();
        public Dictionary<string, int> dicUpdateMac = new Dictionary<string, int>();
        public bool StartUpdate { get; private set; } //开始更新
        public string strUpdatefmt { get; private set; }
        public string strUpdate(bool sameVer = false) //更新字符串
        {
            if (string.IsNullOrWhiteSpace(strUpdatefmt)) return null;

            string tmp = string.Format(strUpdatefmt, updateVer);
            if (sameVer)
            {
                tmp = string.Format(strUpdatefmt, upVer + 1);
            }

            byte checkSum = Common.checksum(tmp);
            tmp += "*" + checkSum.ToString("X2"); //校验和
            return tmp;
        }
        public string serverIP { get; private set; } //服务器IP
        ushort serverPort1;
        public ushort serverPort
        {
            get
            {
                return serverPort1;
            }
            private set
            {
                serverPort1 = value;
            }
        }
        public string updateVer { get; private set; } //当前升级版本
        public int upVer { get; private set; }
        ushort updatePort1;
        public ushort updatePort
        {
            get
            {
                return updatePort1;
            }
            private set
            {
                updatePort1 = value;
            }
        }
        public string fileName { get; private set; } //升级文件名
        public string filePath { get; private set; } //升级文件路径
        public byte[] fileStream { get; private set; } //升级文件流
        private List<List<string>> excelData;
        private byte[] excelBytes;
        public string excelFilePath { get; private set; } //xls文档路径
        private bool startDebug; //开启远程调试
        public bool AutoStop { get; private set; } //自动停止远程调试

        public string ProgressMode
        {
            get { return ConfigHelper.GetConfigString("Pmode", "P", false); }
            set { ConfigHelper.UpdateConfig("Pmode", value, true); }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.btnSelectUpdateFile.Click += btnSelectUpdateFile_Click;
            this.txtUpdateCarMac.PreviewMouseDown += txtUpdateCarMac_PreviewMouseDown;
            this.txtUpdateCarMac.LostFocus += txtUpdateCarMac_LostFocus;
            this.btnStart.Click += btnStart_Click;
            this.btnSelectUpdateCarFile.Click += btnSelectUpdateCarFile_Click;
            this.txtServerIP.PreviewMouseDown += txtServerIP_PreviewMouseDown;
            this.txtServerIP.LostFocus += txtServerIP_LostFocus;
            this.Title = "车载终端远程调试工具 V" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.btnDownExcel.Click += btnDownExcel_Click;
            this.btnStartUpdate.Click += btnStartUpdate_Click;
            this.dgUpdate.MouseDoubleClick += dgUpdate_MouseDoubleClick;
            this.chkAutoClose.Checked += chkAutoClose_Checked;
            this.dgUpdate.LoadingRow += DgUpdate_LoadingRow;
            this.btnFOTA_TOOL.Click += BtnFOTA_TOOL_Click;
            this.rb2g.Checked += Rb2g_Checked;
            this.rb4g.Checked += Rb4g_Checked;
        }

        private void Rb4g_Checked(object sender, RoutedEventArgs e)
        {
            txtUpdateFilePath.Text = "点击右侧浏览按钮选择文件";
        }

        private void Rb2g_Checked(object sender, RoutedEventArgs e)
        {
            txtUpdateFilePath.Text = "点击右侧浏览按钮选择文件";
        }

        private Process otaProcess = null;
        private void BtnFOTA_TOOL_Click(object sender, RoutedEventArgs e)
        {
            if (otaProcess != null)
            {
                if (!otaProcess.HasExited)
                {
                    User32.ShowWindow(otaProcess.MainWindowHandle, User32.SW_RESTORE);
                    User32.SetForegroundWindow(otaProcess.MainWindowHandle);
                    return;
                }
            }
            var fts = Process.GetProcessesByName("FOTA_TOOL");
            if (fts.Length > 0)
            {
                fts[0].Kill();
                Thread.Sleep(100);
            }
            string runPath = Path.GetTempPath() + "\\FOTA_TOOL.exe";
            File.WriteAllBytes(runPath, Properties.Resources.FOTA_TOOL);
            otaProcess = Process.Start(runPath);
            otaProcess.EnableRaisingEvents = true;
            otaProcess.Exited += (object s1, EventArgs e1) =>
            {
                File.Delete(runPath);
            };
        }


        private void DgUpdate_LoadingRow(object sender, System.Windows.Controls.DataGridRowEventArgs e)
        {
            e.Row.Header = e.Row.GetIndex() + 1;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource != null)
                hwndSource.AddHook(new HwndSourceHook(WindowProc));
        }


        private bool ctrlPress = false;

        protected virtual IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x100:
                    if (wParam.ToInt32() == 17)   //按下Ctrl按键
                        ctrlPress = true;
                    else if (wParam.ToInt32() == 84) //按下T键
                    {
                        if (ctrlPress)
                            ProgressMode = "T";
                    }
                    else if (wParam.ToInt32() == 80) //按下P键
                    {
                        if (ctrlPress)
                            ProgressMode = "P";
                    }
                    break;
                case 0x101:
                    if (wParam.ToInt32() == 17)
                        ctrlPress = false;  //释放Ctrl按键
                    break;
            }
            return IntPtr.Zero;
        }

        private void chkAutoClose_Checked(object sender, RoutedEventArgs e)
        {
            if (chkAutoClose.IsChecked == true)
                AutoStop = true;
            else
                AutoStop = false;
        }

        private void btnStartUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!StartUpdate)
            {
                if (!startDebug)
                {
                    MessageBox.Show(this, "请先启动远程诊断服务！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }
                if (string.IsNullOrWhiteSpace(this.txtUpdateFilePath.Text) ||
                    this.txtUpdateFilePath.Text.Trim() == "点击右侧浏览按钮选择文件")
                {
                    MessageBox.Show(this, "还没有选择升级固件！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (string.IsNullOrWhiteSpace(this.txtUpdatePort.Text))
                {
                    MessageBox.Show(this, "还没有输入更新服务器端口号！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                else
                {
                    if (Regex.IsMatch(this.txtUpdatePort.Text.Trim(), @"^\d+$")) //确认是数字
                    {
                        bool suc = ushort.TryParse(this.txtUpdatePort.Text.Trim(), out updatePort1);
                        if (!suc)
                        {
                            MessageBox.Show(this, "输入的更新服务器端口号范围不正确！", "温馨提示", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show(this, "输入的更新服务器端口号不正确！", "温馨提示", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        this.txtUpdatePort.Text = "4518";
                        return;
                    }
                }

                if (string.IsNullOrEmpty(updateVer))
                {
                    MessageBox.Show(this, "更新版本号解析错误！请检查升级文件！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                try
                {
                    strUpdatefmt = "$UPDUPD,U@d8k%4#jD,"; //头
                    strUpdatefmt += "{0},0,"; //版本号

                    FileInfo fileInfo = new FileInfo(filePath);
                    FileStream fs = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                    long fileSize = fs.Length;
                    fs.Position = 0;
                    fileStream = new byte[fileSize];
                    fs.Read(fileStream, 0, fileStream.Length);
                    fs.Close();
                    strUpdatefmt += fileSize + ","; //升级文件大小
                    strUpdatefmt += fileInfo.LastWriteTime.ToString("HH:mm:ss MM-dd-yyyy,"); //文件修改时间

                    UInt32 Crc32 = 0;
                    int readBufSize = 512;
                    int position = 0;
                    do
                    {
                        int readCount = (int)fileSize - position;
                        readCount = readCount > readBufSize ? readBufSize : readCount;
                        byte[] fileBuf = fileStream.Where((b, index) => index >= position && index < (position + readCount))
                            .ToArray();
                        Crc32 = CRC.crc_32_calc(fileBuf, (UInt16)(readCount * 8), Crc32);
                        position += readCount;
                    } while (position < fileSize);
                    strUpdatefmt += Crc32.ToString("X") + ",1,"; //CRC校验
                    strUpdatefmt += serverIP + ":" + updatePort; //ip和端口
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "升级文件打开错误，请检查升级文件！\r\n错误信息：" + ex.Message, "启动失败", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                if (string.IsNullOrWhiteSpace(strUpdatefmt))
                {
                    MessageBox.Show(this, "获取\"更新SMS串\"出错，请检查升级文件是否正确！", "温馨提示", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                this.txtSMS.Text = strUpdate();
                btnEnterMac_Click(null, null);


                if (updateServer_2G == null)
                {
                    updateServer_2G = new UpdateServer_2G();
                    updateServer_2G.main = this;
                }
                updateServer_2G.Start(new Action(() =>
                {
                    this.btnStartUpdate.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.StartUpdate = true;
                        this.btnStartUpdate.Content = "停止服务\n(已启动)";
                        this.MenuItemStartUpdate.IsEnabled = this.MenuItemStopUpdate.IsEnabled =
                        this.MenuItemForceUpdate.IsEnabled = this.MenuItemPauseUpdate.IsEnabled = true;
                        this.btnSelectUpdateCarFile.IsEnabled = this.btnSelectUpdateFile.IsEnabled = false;
                    }));
                })); //启动更新服务器
            }
            else
            {
                if (updateServer_2G.Stop())
                {
                    this.StartUpdate = false;
                    this.btnStartUpdate.Content = "启动升级";
                    this.MenuItemStartUpdate.IsEnabled = this.MenuItemStopUpdate.IsEnabled =
                    this.MenuItemForceUpdate.IsEnabled = this.MenuItemPauseUpdate.IsEnabled = false;
                    this.btnSelectUpdateCarFile.IsEnabled = this.btnSelectUpdateFile.IsEnabled = true;
                }
            }
        }

        private void btnDownExcel_Click(object sender, RoutedEventArgs e)
        {
            string filetype = "Excel表格(*.xlsx)|*.xlsx";
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.OverwritePrompt = true;
            sfd.Filter = filetype;
            sfd.FileName = "批量升级模板";
            if (sfd.ShowDialog(this) == true)
            {
                string filePath = sfd.FileName;
                try
                {
                    Stream fileStream = sfd.OpenFile();
                    byte[] file = Properties.Resources.升级模板;
                    fileStream.Write(file, 0, file.Length);
                    fileStream.Close();
                    MessageBox.Show("模板下载成功，请查看！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    //string dirpath= Path.GetDirectoryName(filePath);
                    // string path = TmoShare.GetRootPath() + @"\Log";
                    if (File.Exists(filePath))
                        Process.Start(filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("模板下载出错，请重试！\n" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        void txtServerIP_LostFocus(object sender, RoutedEventArgs e)
        {
            if (txtServerIP.Text.Trim() == "")
                txtServerIP.Text = "默认自动获取公网地址";
        }

        void txtServerIP_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (txtServerIP.Text.Trim() == "默认自动获取公网地址")
                txtServerIP.Text = "";
        }

        void btnSelectUpdateCarFile_Click(object sender, RoutedEventArgs e)
        {
            string filetype = "Excel表格(*.xlsx)|*.xlsx";
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = filetype;
            if (ofd.ShowDialog(this) == true)
            {
                string filePath = ofd.FileName;
                try
                {
                    excelBytes = File.ReadAllBytes(filePath);
                    excelData = ExcelHelper.ReadExcel(excelBytes);
                    if (excelData.Count < 1)
                        throw new Exception("程序无法识别，请使用模板Excel文件。");
                    if (excelData[0].Count != 6)
                        throw new Exception("程序无法识别，请使用模板Excel文件。");
                    if (excelData[0][0] != "车机号(MAC)[必填]" || excelData[0][1] != "备注信息[选填]" || excelData[0][2] != "原版本[勿填]" || excelData[0][3] != "升级版本[勿填]" || excelData[0][4] != "升级结果[勿填]" || excelData[0][5] != "升级时间[勿填]")
                        throw new Exception("程序无法识别，请使用模板Excel文件。");

                    dicUpdateMac.Clear();
                    this.txtUpdateCarMac.Text = null;
                    for (int i = 0; i < excelData.Count; i++)
                    {
                        List<string> str = excelData[i];
                        if (str.Count == 0) //跳过空行
                            continue;
                        if (!string.IsNullOrWhiteSpace(str[0])) //mac不为空
                        {
                            string mac = str[0].Trim();
                            if (Regex.IsMatch(mac, @"^\d+$")) //确认是数字
                            {
                                //string remark = null;
                                //if (str.Count > 1)  //存在备注
                                //    remark = str[1].Trim();
                                //if (str.Count > 5)
                                //{
                                //    if (!string.IsNullOrWhiteSpace(str[5]))
                                //        continue;
                                //}
                                this.txtUpdateCarMac.Text += mac + ";";
                                dicUpdateMac.Add(mac, i);
                            }
                        }
                    }
                    if (dicUpdateMac.Count == 0)
                        MessageBox.Show("未从文件中读取到可升级设备！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    excelFilePath = filePath;
                }
                catch (Exception ex)
                {
                    excelBytes = null;
                    excelData = null;
                    MessageBox.Show("读取文件出错，请检查！\n" + ex.Message, "读取Excel出错", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
            }
        }

        public delegate void ThreadExceptionEventHandler(Exception ex);
        public static ThreadExceptionEventHandler exceptionHappened;
        void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!startDebug)
            {
                if (string.IsNullOrWhiteSpace(this.txtServerPort.Text))
                {
                    MessageBox.Show(this, "还没有输入短信服务器端口号！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                else
                {
                    if (Regex.IsMatch(this.txtServerPort.Text.Trim(), @"^\d+$")) //确认是数字
                    {
                        bool suc = ushort.TryParse(this.txtServerPort.Text.Trim(), out serverPort1);
                        if (!suc)
                        {
                            MessageBox.Show(this, "输入的短信服务器端口号范围不正确！", "温馨提示", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show(this, "输入的短信服务器端口号不正确！", "温馨提示", MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        this.txtServerPort.Text = "10000";
                        serverPort = 10000;
                        return;
                    }
                }

                serverIP = this.txtServerIP.Text.Trim();
                IPAddress ip;
                if (!IPAddress.TryParse(serverIP, out ip))
                {
                    try
                    {
                        WebClient wc = new WebClient();
                        wc.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                        wc.Encoding = Encoding.GetEncoding("gb2312");
                        string s = wc.DownloadString("http://2019.ip138.com/ic.asp");
                        serverIP = Regex.Match(s, "<center>.*\\[(.+)\\].*</center>").Groups[1].Value;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "获取IP地址失败，请检查网络连接！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    serverIP = ip.ToString();
                }


                this.txtServerIP.Text = serverIP;
                this.txtSendSMS.Text = string.Format("$DIAGSTART,{0}:{1}", serverIP, serverPort);

                if (MainThread == null)
                {
                    MainThread = new ServerThread();
                    MainThread.main = this;
                }
                MainThread.Start(new Action(() =>
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.btnStart.Content = "停止服务\n(已启动)";
                        this.txtServerIP.IsReadOnly = this.txtServerPort.IsReadOnly = true;
                        startDebug = true;
                    }));
                })); //启动服务器
            }
            else
            {
                if (StartUpdate)
                {
                    btnStartUpdate_Click(null, null);
                }
                if (MainThread.Stop())
                {
                    this.btnStart.Content = "启动服务";
                    this.txtServerIP.IsReadOnly = this.txtServerPort.IsReadOnly = false;
                    startDebug = false;
                }
            }
        }

        void btnEnterMac_Click(object sender, RoutedEventArgs e)
        {
            string macs = txtUpdateCarMac.Text.Trim();
            if (string.IsNullOrWhiteSpace(macs)) return;

            if (sender == null)
                dicUpdateMac.Keys.ToList().ForEach(x =>
                {
                    if (dicUpdateMac[x] == -1)
                        dicUpdateMac.Remove(x);
                });

            string[] ms = macs.Split(';');
            for (int i = 0; i < ms.Length; i++)
            {
                string mac = ms[i].Trim();
                if (Regex.IsMatch(mac, @"^\d+$")) //确认是数字
                {
                    if (!dicUpdateMac.ContainsKey(mac))
                    {
                        dicUpdateMac.Add(mac, -1);
                    }
                }
            }

            foreach (KeyValuePair<string, int> item in dicUpdateMac)
            {
                string key = item.Key;
                int val = item.Value;
                if (dgUpdateList.Any(y => y.mac == key)) //标记为升级
                {
                    var info = dgUpdateList.First(y => y.mac == key);
                    info.AutoUpdate = true;
                    info.xlsIndex = val;
                }
            }
        }

        void txtUpdateCarMac_LostFocus(object sender, RoutedEventArgs e)
        {
            if (txtUpdateCarMac.Text.Trim() == "")
                txtUpdateCarMac.Text = "请输入识别码(若有多个请用;号分割)";
        }

        void txtUpdateCarMac_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (txtUpdateCarMac.Text.Trim() == "请输入识别码(若有多个请用;号分割)")
                txtUpdateCarMac.Text = "";
        }

        void btnSelectUpdateFile_Click(object sender, RoutedEventArgs e)
        {
            string filetype = "升级固件(*.bin)|*.bin";
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = filetype;
            if (ofd.ShowDialog(this) == true)
            {
                this.txtUpdateFilePath.Text = string.Empty;
                filePath = ofd.FileName;
                fileName = Path.GetFileName(filePath);
                string patternStr = @"_v(\d+)";
                if (rb2g.IsChecked == true)
                {
                    if (!fileName.EndsWith("_Upgrade_Package.bin"))
                    {
                        MessageBox.Show(this, "升级固件未加壳，请先对程序进行加壳！\r\n注：加壳后请勿修改文件名", "温馨提示", MessageBoxButton.OK,
                            MessageBoxImage.Stop);
                        return;
                    }
                    patternStr += "_Upgrade_Package";
                }
                patternStr += @"\.bin$";
                if (!Regex.IsMatch(fileName, patternStr, RegexOptions.IgnoreCase))
                {
                    MessageBox.Show(this, "读取升级固件信息失败，请勿修改文件名！", "温馨提示", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                Match match = Regex.Match(fileName, patternStr, RegexOptions.IgnoreCase);
                updateVer = match.Groups[1].Value;
                upVer = Convert.ToInt32(updateVer);

                this.txtUpdateFilePath.Text = filePath;
                //if (Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "\\" + fileName != filePath)
                //    File.Copy(filePath, fileName, true);
            }
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            txtServerIP.Text = ConfigurationManager.AppSettings["ServerIP"];
            if (string.IsNullOrWhiteSpace(txtServerIP.Text))
            {
                txtServerIP.Text = "默认自动获取公网地址";
            }

            txtServerPort.Text = ConfigurationManager.AppSettings["SMSPort"];
            if (string.IsNullOrWhiteSpace(txtServerPort.Text))
                txtServerPort.Text = "10000";

            txtUpdatePort.Text = ConfigurationManager.AppSettings["UpdatePort"];
            if (string.IsNullOrWhiteSpace(txtUpdatePort.Text))
                txtUpdatePort.Text = "4518";

            dgUpdate.ItemsSource = dgUpdateList;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (StartUpdate)
            {
                if (updateServer_2G != null && updateServer_2G.OnlineClients > 0)
                {
                    MessageBoxResult mbr = MessageBox.Show(this, "确定要退出程序？\n当前有车机正在升级可能会造成不可预料的后果！！！", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (mbr == MessageBoxResult.Yes)
                    {
                        if (MessageBox.Show(this, "警告：当前有车机正在升级可能会造成不可预料的后果！！！", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                        {
                            e.Cancel = true;
                        }
                    }
                    else
                        e.Cancel = true;
                }
                if (e.Cancel == false)
                    SaveExcel();
            }
            if (e.Cancel == false)
            {
                if (otaProcess != null && !otaProcess.HasExited)
                {//没有退出
                    otaProcess?.CloseMainWindow();
                    otaProcess?.WaitForExit();
                }
            }
            base.OnClosing(e);
        }

        public void SaveExcel(TUpdateInfo info = null)
        {
            if (excelData == null || excelBytes == null || excelFilePath == null) return;

            if (info == null)
            {
                foreach (TUpdateInfo upinfo in dgUpdateList)
                {
                    UpdateRowData(upinfo);
                }
            }
            else
                UpdateRowData(info);

            try
            {
                byte[] fileBytes = ExcelHelper.FillData(excelData, excelBytes);
                File.WriteAllBytes(excelFilePath, fileBytes); //更新Excel文件
            }
            catch (Exception ex)
            {
                MessageBox.Show("写Excel文件出错，将无法保存更新记录！（不影响升级）\n" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
        }

        void UpdateRowData(TUpdateInfo info)
        {
            if (info != null && info.xlsIndex != -1 && info.xlsIndex != 0)
            {
                List<string> row = excelData[info.xlsIndex];
                if (row.Count == 1)
                    row.AddRange(new[] { String.Empty, String.Empty, String.Empty, String.Empty, String.Empty });
                else if (row.Count == 2)
                    row.AddRange(new[] { String.Empty, String.Empty, String.Empty, String.Empty });
                else if (row.Count == 3)
                    row.AddRange(new[] { String.Empty, String.Empty, String.Empty });
                else if (row.Count == 4)
                    row.AddRange(new[] { String.Empty, String.Empty });
                else if (row.Count == 5)
                    row.AddRange(new[] { String.Empty });
                row[2] = info.oldver;
                row[3] = info.updatever;
                row[4] = info.down;
                row[5] = info.time;
            }
        }

        private TUpdateInfo GetSelectedItem()
        {
            var cell = dgUpdate.CurrentCell;
            TUpdateInfo item = cell.Item as TUpdateInfo;
            return item;
        }

        private void MenuItemFeatures_OnClick(object sender, RoutedEventArgs e)
        {   //版本特性信息
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (item.hasFeatures)
                {
                    MessageBox.Show(this, item.features, "版本特征-" + item.mac, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    item.client.GetParameters();
                    MessageBox.Show(this, "正在查询相关信息，请稍后查看！", "请稍后", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
        }

        private void MenuItemParameters_OnClick(object sender, RoutedEventArgs e)
        {   //配置信息
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (item.hasFeatures)
                {
                    ParameterInfo pi = new ParameterInfo("配置信息-" + item.mac);
                    pi.DicData = item.dicParameters;
                    pi.ShowDialog();
                }
                else
                {
                    item.client.GetParameters();
                    MessageBox.Show(this, "正在查询相关信息，请稍后查看！", "请稍后", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
        }

        private void MenuItemDynamicParameters_OnClick(object sender, RoutedEventArgs e)
        {   //动态参数信息
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (!string.IsNullOrWhiteSpace(item.dynamicParameters))
                    MessageBox.Show(this, item.dynamicParameters, "动态参数-" + item.mac, MessageBoxButton.OK, MessageBoxImage.None);
                else
                {
                    item.client.GetDynamicParameters();
                    MessageBox.Show(this, "正在查询相关信息，请稍后查看！", "请稍后", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
        }

        private void MenuItemReadParameters_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                item.client.GetParameters();
            }
        }

        private void MenuItemReadDynamicParameters_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                item.client.GetDynamicParameters();
            }
        }

        private void MenuItemReboot_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (MessageBox.Show(this, "确定要重启设备？", "请选择", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
                    item.client.RebootDevice();
            }
        }

        private void MenuItemStopDebug_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (MessageBox.Show(this, "确定要断开远程远程诊断？", "请选择", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.OK)
                    item.client?.StopDebug();
            }
        }

        private void MenuItemSetParameters_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                new SetParameters(item).ShowDialog();
            }
        }

        Dictionary<TUpdateInfo, RealTimeData> dicWidows = new Dictionary<TUpdateInfo, RealTimeData>();

        private void ShowRealTimeDataWindow(TUpdateInfo info)
        {
            if (info == null) return;

            RealTimeData window = null;
            if (dicWidows.ContainsKey(info))
            {
                window = dicWidows[info];
            }
            else
            {
                window = new RealTimeData(info);
                window.Closed += (sender, e) => { dicWidows.Remove(((RealTimeData)sender).Info); };
                dicWidows.Add(info, window);
            }
            window.Owner = this;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Show();
            window.Activate();
        }

        private void dgUpdate_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (item.hasFeatures)
                {
                    MessageBox.Show(this, item.features, "版本特征-" + item.mac, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    item.client.GetParameters();
                    MessageBox.Show(this, "正在查询相关信息，请稍后查看！", "请稍后", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
        }

        private void MenuItemViewRealData_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            ShowRealTimeDataWindow(item);
        }

        private void MenuItemStopUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (!item.AutoUpdate)
                {
                    MessageBox.Show(this, "升级已经停止，请勿重复指令！", "注意", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                if (item.client != null)
                {
                    item.AutoUpdate = false;
                    item.client.StopUpdate();
                }
            }
        }

        private void MenuItemStartUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (item.AutoUpdate)
                {
                    if (item.PauseUpdate)
                    {
                        item.PauseUpdate = false;
                        item.AutoUpdate = true;
                    }
                    else
                        MessageBox.Show(this, "升级已经开始，请勿重复指令！", "注意", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    return;
                }

                if (string.IsNullOrWhiteSpace(strUpdatefmt))
                {
                    MessageBox.Show(this, "升级服务还未启动，无法升级！", "注意", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                item.AutoUpdate = true;
            }
        }

        private void MenuItemForceUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                if (item.FoceUpdate)
                {
                    if (item.PauseUpdate)
                    {
                        item.PauseUpdate = false;
                        item.AutoUpdate = true;
                    }
                    else
                        MessageBox.Show(this, "升级已经开始，请勿重复指令！", "注意", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    return;
                }

                if (string.IsNullOrWhiteSpace(strUpdatefmt))
                {
                    MessageBox.Show(this, "升级服务还未启动，无法升级！", "注意", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                item.FoceUpdate = true;
                item.AutoUpdate = true;
            }
        }

        private void MenuItemRemove_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                item.Remove(true);
            }
        }

        private void MenuItemPauseUpdate_OnClick(object sender, RoutedEventArgs e)
        {
            TUpdateInfo item = GetSelectedItem();
            if (item != null)
            {
                item.PauseUpdate = true;
            }
        }
    }
}
