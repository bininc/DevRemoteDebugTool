using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WpfAutoUpdate
{
    public class UpdateServer_2G
    {
        public MainWindow main;
        Socket server;
        Thread thread;
        public readonly List<UpdateClient_2G> listClient = new List<UpdateClient_2G>();
        private bool listening = false;
        public int MaxClient { get; private set; }

        public UpdateServer_2G()
        {
            MaxClient = 1000; //同时最多有1000个在线升级
        }

        public void Start(Delegate startSuccessCallBack)
        {
            if (thread != null && thread.IsAlive) return;

            thread = new Thread(() =>
            {
                IPAddress ip;
                if (IPAddress.TryParse(main.serverIP, out ip))
                {
                    try
                    {
                        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, main.updatePort); //4518
                        server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        server.Bind(ipep);
                        server.Listen(10);
                        listening = true;
                        startSuccessCallBack?.DynamicInvoke();
                        while (listening)
                        {
                            try
                            {
                                Socket sok = server.Accept();
                                if (OnlineClients > MaxClient)
                                    sok.Close(); //连接数过多 阻止
                                else
                                {
                                    UpdateClient_2G client = new UpdateClient_2G(this, sok, main);
                                    client.ClientClosed += client_ClientClosed;
                                    listClient.Add(client);
                                    client.Start();
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ex is SocketException || ex is ObjectDisposedException)
                                {

                                }
                                else
                                {
                                    LogHelper.WriteError(ex, "UpdateServer_2G");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        listening = false;
                        main.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(this.main, "升级服务启动失败，请检查端口是否被占用！\r\n错误消息：" + ex.Message, "提示",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }), null);
                    }
                }
                else
                {
                    listening = false;
                    main.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(this.main, "升级服务器IP地址不正确，启动失败！", "提示", MessageBoxButton.OK,
                            MessageBoxImage.Stop);
                    }), null);
                }
                listening = false;
            });
            thread.Name = "UpdateServer_2G";
            thread.IsBackground = true;
            thread.Start();
        }

        private void client_ClientClosed(UpdateClient_2G obj)
        {
            if (listClient.Contains(obj))
                listClient.Remove(obj);
        }

        public int OnlineClients { get { return listClient.Count; } }

        public bool Stop()
        {
            if (listening)
            {
                listClient.ForEach(uc => uc.Stop());
                listClient.Clear();
                listening = false;
                server.Close();
            }
            return !listening;
        }
    }
}
