using System;
using System.Collections.Generic;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace WpfAutoUpdate
{
    /// <summary>
    /// Excel 助手类。
    /// </summary>
    public static class ExcelHelper
    {
        public static MemoryStream WriteToStream(XSSFWorkbook xssfworkbook)
        {
            //Write the stream data of workbook to the root directory
            MemoryStream file = new MemoryStream();
            xssfworkbook.Write(file);
            return file;
        }

        /// <summary>
        /// 读excel内容到list列表里
        /// </summary>
        /// <param name="buf">excel文件的字节数组形式</param>
        /// <returns>List<List<string>></returns>
        public static List<List<string>> ReadExcel(byte[] buf)
        {
            List<List<string>> r = new List<List<string>>();
            XSSFWorkbook workbook = new XSSFWorkbook(new MemoryStream(buf));
            ISheet sheet = workbook.GetSheetAt(0);
            int rowCount = sheet.LastRowNum;
            for (int i = 0; i <= rowCount; i++)
            {
                List<string> list = new List<string>();
                r.Add(list);
                IRow row = sheet.GetRow(i);
                for (int j = 0; j < row.Cells.Count; j++)
                {
                    ICell cell = row.GetCell(j);
                    string cellStr = cell == null ? String.Empty : cell.ToString();
                    list.Add(cellStr);
                }
            }
            return r;
        }

        public static byte[] FillData(List<List<string>> data, byte[] fileBytes)
        {
            if (data == null || fileBytes == null) return null;
            XSSFWorkbook workbook = new XSSFWorkbook(new MemoryStream(fileBytes));
            ISheet sheet = workbook.GetSheetAt(0);
            for (var i = 0; i < data.Count; i++)
            {
                IRow row = sheet.GetRow(i);
                List<string> rowdata = data[i];
                for (var j = 0; j < rowdata.Count; j++)
                {
                    ICell cell = row.GetCell(j);
                    if (cell == null)
                        cell = row.CreateCell(j, CellType.String);
                    cell.SetCellValue(rowdata[j]);
                }
            }

            return WriteToStream(workbook).GetBuffer();
        }
    }
}

