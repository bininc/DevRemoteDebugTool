using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfAutoUpdate
{
    public class Common
    {
        public static byte checksum(string data_ptr)
        {
            byte cs = 0;
            for (int i = 0; i < data_ptr.Length; ++i)
            {
                cs ^= (byte)data_ptr[i];
            }
            return cs;
        }

        public static string GetCheckSumString(string data)
        {
            byte cs = checksum(data);
            return cs.ToString("X2"); //校验和
        }

        public static byte CheckSumBytes(byte[] bs, int length = -1)
        {
            byte cs = 0;
            if (length == -1)
                length = bs.Length;
            for (int i = 0; i < length; i++)
            {
                cs ^= bs[i];
            }

            return cs;
        }

        public static int ReadFromBytes(byte[] bytes, ref byte[] array, int offset, int count)
        {
            if (offset >= bytes.Length) return 0;
            if (array.Length < count) throw new ArgumentOutOfRangeException("count");
            if (offset + count > bytes.Length)
                count = bytes.Length - offset;
            Array.Copy(bytes, offset, array, 0, count);
            return count;
        }

        /// <summary>
        /// 验证s是否为数字格式（只限整型）
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsNumricForNum(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return false;
            }
            foreach (char c in str)
            {
                if (!char.IsNumber(c))
                {
                    return false;
                }
            }
            return true;
        }

    }
}
