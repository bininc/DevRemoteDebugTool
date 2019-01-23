using System;
using System.Configuration;

namespace WpfAutoUpdate
{
    public static class ConfigHelper
    {
        public static void Addconfig(string name, string value)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Add(name, value);
                config.Save();
                ConfigurationManager.RefreshSection("appSettings"); //重新加载新的配置文件 
            }
            catch { }
        }
        /// <summary>
        /// 返回配置文件中
        /// </summary>
        /// <param name="key">传入的信息</param>
        /// <returns></returns>
        public static string GetConfigString(string key)
        {
            try
            {
                ConfigurationManager.RefreshSection("appSettings"); //重新加载新的配置文件
                object objModel = ConfigurationManager.AppSettings[key];
                if (objModel == null)
                    return "";
                return objModel.ToString();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 获取配置文件数据库连接字符串
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetConnectionString(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try
            {
                return ConfigurationManager.ConnectionStrings[name].ConnectionString;
            }
            catch { return null; }
        }

        /// <summary>
        /// 获取用户配置信息 如果没有词配置项，则自动创建并赋默认值
        /// </summary>
        /// <param name="Key">配置项关键字</param>
        /// <param name="DefaultValue">配置项默认值</param>
        /// <param name="CreateKeyAuto">自动创建配置项</param>
        /// <returns></returns>
        public static string GetConfigString(string Key, string DefaultValue, bool CreateKeyAuto)
        {
            string val = GetConfigString(Key);
            if (val == "-1") return "";

            if (!string.IsNullOrWhiteSpace(val)) return val;

            if (CreateKeyAuto)
                Addconfig(Key, DefaultValue);
            return DefaultValue;
        }

        /// <summary>
        ///返回配置文件中
        /// </summary>
        /// <param name="key">传入的信息</param>
        /// <param name="configPath">配置文件路径</param>
        /// <returns></returns>
        public static string GetConfigString(string key, string configPath)
        {
            try
            {
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(configPath);
                object objModel = config.AppSettings.Settings[key].Value;
                return objModel.ToString();
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// 修改配置文件
        /// </summary>
        /// <param name="cname">The cname.</param>
        /// <param name="cvalue">The cvalue.</param>
        public static bool UpdateConfig(string cname, string cvalue)
        {
            try
            {
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings[cname].Value = cvalue;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");//重新加载新的配置文件 
                return true;
            }
            catch
            {
                return false;
            }

        }
        /// <summary>
        /// 修改配置文件
        /// </summary>
        /// <param name="cname">The cname.</param>
        /// <param name="cvalue">The cvalue.</param>
        public static bool UpdateConfig(string cname, string cvalue, bool CreateKeyAuto)
        {
            try
            {
                GetConfigString(cname, "", CreateKeyAuto);
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings[cname].Value = cvalue;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");//重新加载新的配置文件 
                return true;
            }
            catch
            {
                return false;
            }

        }
        /// <summary>
        /// 得到AppSettings中的配置int信息
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static int GetConfigInt(string key, int defaultValue = 0, bool createKeyAuto = false)
        {
            int result = defaultValue;
            string cfgVal = GetConfigString(key);
            if (!string.IsNullOrEmpty(cfgVal))
            {
                try
                {
                    result = int.Parse(cfgVal);
                }
                catch (FormatException)
                {
                }
            }
            else
            {
                if (createKeyAuto)
                    Addconfig(key, defaultValue.ToString());
            }

            return result;
        }

        /// <summary>
        /// 得到AppSettings中的配置uint信息
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static uint GetConfigUint(string key, uint defaultValue = 0, bool createKeyAuto = false)
        {
            uint result = defaultValue;
            string cfgVal = GetConfigString(key);
            if (!string.IsNullOrEmpty(cfgVal))
            {
                try
                {
                    result = uint.Parse(cfgVal);
                }
                catch (FormatException)
                {
                }
            }
            else
            {
                if (createKeyAuto)
                    Addconfig(key, defaultValue.ToString());
            }

            return result;
        }
        /// <summary>
        /// 得到AppSettings中的配置ushort信息
        /// </summary>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="createKeyAuto"></param>
        /// <returns></returns>
        public static ushort GetConfigUshort(string key, ushort defaultValue = 0, bool createKeyAuto = false)
        {
            ushort result = defaultValue;
            string cfgVal = GetConfigString(key);
            if (!string.IsNullOrEmpty(cfgVal))
            {
                try
                {
                    result = ushort.Parse(cfgVal);
                }
                catch (FormatException)
                {
                }
            }
            else
            {
                if (createKeyAuto)
                    Addconfig(key, defaultValue.ToString());
            }

            return result;
        }

        /// <summary>
        ///修改指定程序配置文件
        /// </summary>
        /// <param name="cname">The cname.</param>
        /// <param name="cvalue">The cvalue.</param>
        /// <param name="configPath">The config path.</param>
        public static void UpdateConfig(string cname, string cvalue, string configPath)
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(configPath);
                config.AppSettings.Settings[cname].Value = cvalue;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");//重新加载新的配置文件 
            }
            catch
            {

            }

        }

        /// <summary>
        /// 获取配置节点BOOL类型信息（1-True,else-False）
        /// </summary>
        /// <param name="key">配置节点名称</param>
        /// <returns></returns>
        public static bool GetConfigBool(string key)
        {
            string cfgVal = GetConfigString(key).Trim();
            if (string.IsNullOrWhiteSpace(cfgVal)) return false;

            return cfgVal != "0" || cfgVal.ToLower() == "true";
        }
        /// <summary>
        /// 获取用户配置信息 如果没有词配置项，则自动创建并赋默认值
        /// </summary>
        /// <param name="key">配置项关键字</param>
        /// <param name="defaultValue">配置项默认值</param>
        ///<param name="createKeyAuto">自动创建配置项</param>
        /// <returns></returns>
        public static bool GetConfigBool(string key, bool defaultValue, bool createKeyAuto)
        {
            bool result = defaultValue;
            string cfgVal = GetConfigString(key).Trim();
            if (string.IsNullOrWhiteSpace(cfgVal))
            {
                if (createKeyAuto)
                    Addconfig(key, defaultValue ? "1" : "0");
            }
            else
            {
                result = cfgVal != "0" || cfgVal.ToLower() == "true";
            }
            return result;
        }
    }
}
