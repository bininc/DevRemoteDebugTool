using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using System.Windows;

namespace WpfAutoUpdate
{
    public class ServerThread
    {
        public MainWindow main;
        Socket server;
        Thread thread;
        public readonly List<ClientThread> listClient = new List<ClientThread>();
        private bool listening = false;
        private readonly Timer _timer;

        public ServerThread()
        {
            _timer = new Timer(TimerTick, null, 3000, 3000);
        }

        public void Start(Delegate startSuccessCallBack)
        {
            if (thread != null && thread.IsAlive) return;

            thread = new Thread(new ThreadStart(() =>
            {
                IPAddress ip;
                if (IPAddress.TryParse(main.serverIP, out ip))
                {
                    try
                    {
                        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, main.serverPort); //7920
                        server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        server.Bind(ipep);
                        server.Listen(10);
                        listening = true;
                        startSuccessCallBack?.DynamicInvoke();
                        while (listening)
                        {
                            Socket sok = server.Accept();
                            try
                            {
                                ClientThread client = new ClientThread(this, sok, main);
                                listClient.Add(client);
                                TUpdateInfo info = new TUpdateInfo(client);
                                main.dgUpdate.Dispatcher.Invoke(new Action(() => main.dgUpdateList.Add(info)));
                                client.Start();
                            }
                            catch (Exception ex)
                            {
                                if (ex is SocketException || ex is ObjectDisposedException)
                                {

                                }
                                else
                                {
                                    LogHelper.WriteError(ex, "ServerThread");
                                }
                                sok.Close();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is SocketException)
                        {
                            SocketException sex = (SocketException)ex;
                            if (sex.ErrorCode == 10004)
                            {
                                return;
                            }
                        }
                        main.Dispatcher.BeginInvoke(
                            new Action(() => MessageBox.Show(this.main, "服务启动失败，请检查端口是否被占用！\r\n错误消息：" + ex.Message, "提示",
                                MessageBoxButton.OK, MessageBoxImage.Error)), null);
                    }
                }
                else
                {
                    main.Dispatcher.BeginInvoke(
                        new Action(() => MessageBox.Show(this.main, "服务器IP地址不正确，启动失败！", "提示", MessageBoxButton.OK,
                            MessageBoxImage.Stop)), null);
                }
                listening = false;
            }));
            thread.Name = "ServerThread";
            thread.IsBackground = true;
            thread.Start();
        }

        public bool Stop()
        {
            if (listening)
            {
                listClient.ToList().ForEach(client => client.updateInfo.Remove(true));
                listening = false;
                server.Close();
            }
            return !listening;
        }

        private void TimerTick(object state)
        {
            if (!listening) return;
            lock (this)
            {
                var rmClients = listClient.Where(c => string.IsNullOrEmpty(c.mac) && (DateTime.Now - c.ConnTime).TotalMinutes > 10).ToArray();
                foreach (ClientThread client in rmClients)
                {
                    client?.updateInfo.Remove(true);
                }
            }
        }
    }
}
