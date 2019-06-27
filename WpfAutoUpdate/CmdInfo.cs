using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WpfAutoUpdate
{
    public enum CmdId
    {
        NULL,
        /// <summary>
        /// 读取设备配置参数
        /// </summary>
        ReadPara,
        /// <summary>
        /// 读取动态参数
        /// </summary>
        ReadDynamicPara,
        /// <summary>
        /// 重启
        /// </summary>
        Reboot,
        /// <summary>
        /// 停止调试
        /// </summary>
        StopDebug,
        /// <summary>
        /// 停止升级
        /// </summary>
        StopUpdate,
        /// <summary>
        /// 开始升级
        /// </summary>
        StartUpdate,
        /// <summary>
        /// 修改终端配置
        /// </summary>
        SetPara,
        /// <summary>
        /// 设置模式
        /// </summary>
        State2,
        /// <summary>
        /// 正常模式
        /// </summary>
        State0,
        /// <summary>
        /// 继续升级
        /// </summary>
        ContinueUpdate,
        /// <summary>
        /// 暂停升级
        /// </summary>
        PauseUpdate,
    }

    public static class CmdInfo
    {
        /*
        HW Ver: M50_VE03, SW Ver: SWVER130
        NV Ver: UNI_STRONG,15:04:21 Jun 13 2015
        VGAP: UNI-STRONG, IMEI: 863092013877025
        GPRS APN:"ZY-DDN.BJ", UserName:"", Pwd:""
        SERVER ADDR: 10.251.65.38:7956
        VEID: 1423021432
        */


        public static readonly Dictionary<CmdId, string> CmdString = new Dictionary<CmdId, string>() {
            { CmdId.ReadPara,"*#00#" },
            { CmdId.ReadDynamicPara,"*#01#" },
            { CmdId.StopDebug,"$DIAGSTOP" },
            { CmdId.StopUpdate,"**42#" },
            { CmdId.Reboot,"**00#" },
            { CmdId.State2,"*state=2" },
            { CmdId.State0,"*state=0" },
        };

        private static readonly Dictionary<CmdId, byte[]> _cmdBytes = new Dictionary<CmdId, byte[]>();
        /// <summary>
        /// 获得命令字节数组
        /// </summary>
        /// <param name="cmdId"></param>
        /// <returns></returns>
        public static byte[] GetCmdBytes(CmdId cmdId)
        {
            string cmdStr;
            if (CmdString.TryGetValue(cmdId, out cmdStr))
            {
                byte[] buffer;
                if (!_cmdBytes.TryGetValue(cmdId, out buffer))
                {
                    buffer = Encoding.ASCII.GetBytes(cmdStr);
                    _cmdBytes[cmdId] = buffer;
                }
                return buffer;
            }
            else
                return null;
        }

        public static readonly Dictionary<CmdId, string> CmdDesc = new Dictionary<CmdId, string>() {
            {CmdId.ContinueUpdate,"继续升级(续传)" },
            {CmdId.ReadDynamicPara,"读取动态参数" },
            {CmdId.ReadPara,"读取终端信息" },
            {CmdId.Reboot,"重启设备" },
            {CmdId.SetPara,"修改终端配置" },
            {CmdId.StartUpdate,"固件升级" },
            {CmdId.State0,"进入正常模式" },
            {CmdId.State2,"进入设置模式" },
            {CmdId.StopDebug,"停止远程诊断" },
            {CmdId.StopUpdate,"停止升级" },
            {CmdId.PauseUpdate,"暂停升级" }
        };

        /// <summary>
        /// 获得命令描述
        /// </summary>
        /// <param name="cmdId"></param>
        /// <returns></returns>
        public static string GetCmdDesc(CmdId cmdId)
        {
            string cmdDesc;
            if (CmdDesc.TryGetValue(cmdId, out cmdDesc))
            {
                return cmdDesc;
            }
            return "";
        }
    }


}
