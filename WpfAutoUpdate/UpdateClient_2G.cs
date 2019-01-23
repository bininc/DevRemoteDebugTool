using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WpfAutoUpdate
{
    public class UpdateClient_2G
    {
        public event Action<UpdateClient_2G> ClientClosed;
        private readonly int _recBufferSize = 10240;
        public Socket client { get; private set; }
        public MainWindow main { get; private set; }
        private UpdateServer_2G _server_2G;
        private Thread thread;
        private byte[] pauseBytes;

        public string mac { get; private set; }

        public UpdateClient_2G(UpdateServer_2G server, Socket sok, MainWindow mainw)
        {
            _server_2G = server;
            client = sok;
            if (client != null)
            {
                client.ReceiveBufferSize = _recBufferSize;
            }
            main = mainw;
        }

        public void Start()
        {
            if (thread != null && thread.IsAlive) return;

            thread = new Thread(new ThreadStart(run));
            thread.Name = "UpdateClient_2G";
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            if (thread != null)
            {
                thread.Abort();
            }
            if (client.Connected)
            {
                client.Close();
            }
        }

        private TUpdateInfo updateInfo
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(mac))
                {
                    if (main.dgUpdateList.Any(x => x.mac == mac))
                        return main.dgUpdateList.First(x => x.mac == mac);
                }
                return null;
            }
        }

        private void run()
        {
            try
            {
                byte[] buf = new byte[_recBufferSize];
                while (true)
                {
                    int count = client.Receive(buf);
                    if (count == 0) //远程主机关闭了连接
                    {
                        client.Close();
                        OnClientClosed("升级断开");
                        return;
                    }
                    else
                    {
                        string str = Encoding.ASCII.GetString(buf, 0, count);

                        if (str.StartsWith("$UPD")) //确认是升级串
                        {
                            str = str.Split('\0')[0];   //去除重复数据
                            string[] array = str.Split('*');
                            if (array.Length > 1)   //正确串
                            {
                                string upStr = array[0];
                                string sumStr = array[1];
                                string csstr = Common.GetCheckSumString(upStr);
                                if (csstr == sumStr) //检验和正确
                                {
                                    upStr = upStr.Remove(0, 4); //移除前缀
                                    if (upStr.StartsWith("DLREQ"))
                                    {
                                        var tmpArray = upStr.Split(',');
                                        if (tmpArray.Length == 6)
                                        {
                                            if (tmpArray[1] == "U@d8k%4#jD" && Common.IsNumricForNum(tmpArray[2]) && Common.IsNumricForNum(tmpArray[3]) && Common.IsNumricForNum(tmpArray[4]))
                                            {
                                                mac = tmpArray[5];
                                                if (_server_2G.listClient.Any(x => x.mac == mac && x != this))
                                                {   //存在相同的连接
                                                    UpdateClient_2G sameClient = _server_2G.listClient.First(x => x.mac == mac && x != this);
                                                    try
                                                    {
                                                        if (sameClient.thread != null)
                                                            sameClient.thread.Abort();
                                                    }
                                                    catch { }
                                                    sameClient.OnClientClosed("重复连接");
                                                }

                                                if (updateInfo == null || !updateInfo.AutoUpdate || !updateInfo.needUpdate)
                                                {
                                                    client.Close();
                                                    OnClientClosed("");
                                                    return;
                                                }

                                                int upVer = Convert.ToInt32(tmpArray[2]);
                                                if (main.upVer == upVer || main.upVer + 1 == upVer) //升级版本一致
                                                {
                                                    int offset = Convert.ToInt32(tmpArray[3]);
                                                    int size = ConfigHelper.GetConfigInt("UpPkgSize", -1);
                                                    if (size == -1)
                                                        size = Convert.ToInt32(tmpArray[4]);
                                                    if (size <= 0)
                                                        size = 480;

                                                    int readLen = main.fileStream.Length - offset;
                                                    if (readLen > size)
                                                        readLen = size;
                                                    double rspOffset = offset + readLen;
                                                    byte[] readFileBytes =
                                                        main.fileStream
                                                            .Where((b, index) => index >= offset && index < rspOffset)
                                                            .ToArray();
                                                    int rspSize = readLen + 80;
                                                    byte[] rspBytes = new byte[rspSize];
                                                    uint crc = CRC.crc_32_calc(readFileBytes, (UInt16)(readLen * 8),
                                                        0);
                                                    string header = $"$UPDDLACK,{upVer},{offset},{readLen},";
                                                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                                                    int nlen = headerBytes.Length;
                                                    Array.Copy(headerBytes, rspBytes, nlen);
                                                    Array.Copy(readFileBytes, 0, rspBytes, nlen, readLen);
                                                    nlen += readLen;
                                                    string crcStr = crc.ToString("X8") + "\n";
                                                    byte[] crcBytes = Encoding.ASCII.GetBytes(crcStr);
                                                    Array.Copy(crcBytes, 0, rspBytes, nlen, crcBytes.Length);
                                                    nlen += crcBytes.Length;
                                                    byte cs = Common.CheckSumBytes(rspBytes, nlen);
                                                    string cumStr = "*" + (((uint)cs) & 0xFF).ToString("X2");
                                                    byte[] cumBytes = Encoding.ASCII.GetBytes(cumStr);
                                                    Array.Copy(cumBytes, 0, rspBytes, nlen, cumBytes.Length);
                                                    nlen += cumBytes.Length;

                                                    //未启动升级 或者 无需升级 或者 暂停升级 跳过升级
                                                    if (updateInfo.PauseUpdate)
                                                    {
                                                        pauseBytes = rspBytes.Take(nlen).ToArray();
                                                        continue;
                                                    }
                                                    pauseBytes = null;

                                                    client.Send(rspBytes, nlen, SocketFlags.None);

                                                    if (main.ProgressMode == "P")
                                                        updateInfo.status = string.Format("{1} ({0:P2})",
                                                            rspOffset / main.fileStream.Length,
                                                            main.fileStream.Length - rspOffset);
                                                    else if (main.ProgressMode == "T")
                                                        updateInfo.status = (main.fileStream.Length - rspOffset).ToString();

                                                    if (rspOffset >= main.fileStream.Length)
                                                    {
                                                        updateInfo.down = "下载完成";
                                                        updateInfo.time = null;
                                                        updateInfo.Updated = true;
                                                        //updateInfo.FoceUpdate = false;
                                                        //updateInfo.IsUpdating = false;
                                                    }

                                                    updateInfo.lastUpSendTime = DateTime.Now;

                                                }
                                                else
                                                {
                                                    updateInfo.status = "版本不一致";
                                                    updateInfo.time = "升级错误";
                                                }
                                            }
                                        }
                                    }
                                    else if (upStr.StartsWith("REG"))
                                    {

                                    }
                                    else if (upStr.StartsWith("UPDACK"))
                                    {
                                        var tmpArray = upStr.Split(',');
                                        if (tmpArray.Length == 3)
                                        {
                                            mac = tmpArray[1];
                                        }

                                        if (updateInfo != null)
                                        {
                                            if (pauseBytes == null)
                                                updateInfo.status = "...";
                                            else
                                            {
                                                client.Send(pauseBytes, SocketFlags.None);
                                            }
                                            updateInfo.lastUpSendTime = DateTime.Now;
                                        }
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                        }
                        else
                        {
                            client.Send(buf);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                if (ex is SocketException)
                {
                    OnClientClosed("升级断开");
                }
                else
                {
                    if (!(ex is ThreadAbortException))
                        LogHelper.WriteError(ex, "UpdateClient_2G");
                }
                try
                {
                    client?.Close();
                }
                catch
                {

                }
                //MessageBox.Show(ex.Message);
            }

        }

        private void OnClientClosed(string msg)
        {
            if (updateInfo != null && !updateInfo.Updated)
                updateInfo.status = msg;

            if (ClientClosed != null)
                ClientClosed(this);
        }
    }
}
