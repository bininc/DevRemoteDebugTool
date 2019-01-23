using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

using System.IO;
using System.Windows;

namespace WpfAutoUpdate
{
    class WriteExcel
    {
        Thread thread;
        public MainWindow main;
        public void Start()
        {
            thread = new Thread(new ThreadStart(run));
            thread.IsBackground = true;
            thread.Start();
        }
        byte[] buf;
        private void run()
        {

            while (true)
            {
                if (!string.IsNullOrWhiteSpace(main.excelFilePath) && main.StartUpdate)
                {
                    try
                    {
                        string[] strs = File.ReadAllLines(main.excelFilePath);
                        for (int i = 0; i < strs.Length; i++)
                        {
                            string[] str = strs[i].Split('\t');
                            if (str.Length < 1) //跳过空行
                                continue;
                            if (!string.IsNullOrWhiteSpace(str[0]))
                            {
                                string mac = str[0].Trim();
                                if (main.dgUpdateList.Any(x => x.mac == mac))
                                {
                                    var upinfo = main.dgUpdateList.First(x => x.mac == mac);
                                    if (upinfo.down == "已升级")
                                    {
                                        if (str.Last() != "已升级")
                                            strs[i] += "\t已升级";
                                    }
                                    else if (upinfo.down == "无需更新")
                                    {
                                        if (str.Last() != "无需更新")
                                            strs[i] += "\t无需更新";
                                    }
                                }
                            }
                        }
                        File.WriteAllLines(main.excelFilePath, strs, Encoding.Unicode);
                    }
                    catch (Exception ex)
                    {
                        //MessageBox.Show(ex.Message);
                    }
                }
                Thread.Sleep(2000); //每隔2秒扫描一次
            }

        }
    }
}
