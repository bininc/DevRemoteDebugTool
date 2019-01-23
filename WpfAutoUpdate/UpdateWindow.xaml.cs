using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Interop;

namespace WpfAutoUpdate
{
    /// <summary>
    /// UpdateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public UpdateWindow()
        {
            InitializeComponent();
            this.Loaded += UpdateWindow_Loaded;
            this.SizeChanged += UpdateWindow_SizeChanged;
        }

        void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartServer();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            base.OnClosing(e);
        }

        void StartServer()
        {
            this.exeName = "server.exe";
            server = new Process();
            server.StartInfo.FileName = this.exeName;
            server.StartInfo.Arguments = "pipe";
            server.StartInfo.WorkingDirectory = ".";
            server.StartInfo.Verb = "RunAs";
            server.StartInfo.UseShellExecute = false;
            server.Start();
            Thread.Sleep(500);
            appWin = server.MainWindowHandle;
            try
            {
                long oldstyle = GetWindowLong(appWin, -16);
                SetWindowLong(appWin, -16, oldstyle & (~(0x00C00000L | 0x00C0000L)));
            }
            catch { }
            SetParent(appWin, new WindowInteropHelper(this).Handle);
            MoveWindow(appWin, 0, 0, (int)this.Width, (int)this.Height, true);
            //server.StartInfo.FileName = "server.exe";
            //server.StartInfo.Arguments = "image_617.bin";
            //server.StartInfo.Verb = "runas";
            //server.StartInfo.UseShellExecute = false;
            //server.StartInfo.RedirectStandardInput = true;
            //server.StartInfo.RedirectStandardOutput = false;
            //server.StartInfo.RedirectStandardError = true;
            //server.StartInfo.CreateNoWindow = false;
            //server.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            //server.Start();
            //server.OutputDataReceived += server_OutputDataReceived;
            //server.BeginOutputReadLine();
            //server.StandardInput.WriteLine("taskkill /f /im updateserver.exe");
            //server.StandardInput.WriteLine("updateServer.exe image_617.bin");

        }

        void UpdateWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (appWin != IntPtr.Zero)
            {
                MoveWindow(appWin, 0, 0, (int)this.Width, (int)this.Height, true);
            }
        }


        public Process server = null;
        IntPtr appWin;
        private string exeName = "";

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true,
             CharSet = CharSet.Unicode, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        private static extern long GetWindowThreadProcessId(long hWnd, long lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongA", SetLastError = true)]
        private static extern long GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern long SetWindowLong(IntPtr hwnd, int nIndex, long dwNewLong);


        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetWindowPos(IntPtr hwnd, long hWndInsertAfter, long x, long y, long cx, long cy, long wFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll", EntryPoint = "PostMessageA", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hwnd, uint Msg, long wParam, long lParam);

        private const int SWP_NOOWNERZORDER = 0x200;
        private const int SWP_NOREDRAW = 0x8;
        private const int SWP_NOZORDER = 0x4;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int WS_EX_MDICHILD = 0x40;
        private const int SWP_FRAMECHANGED = 0x20;
        private const int SWP_NOACTIVATE = 0x10;
        private const int SWP_ASYNCWINDOWPOS = 0x4000;
        private const int SWP_NOMOVE = 0x2;
        private const int SWP_NOSIZE = 0x1;
        private const int GWL_STYLE = (-16);
        private const int WS_VISIBLE = 0x10000000;
        private const int WM_CLOSE = 0x10;
        private const int WS_CHILD = 0x40000000;

        public string ExeName
        {
            get
            {
                return exeName;
            }
            set
            {
                exeName = value;
            }
        }

    }
}
