using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util.Text
{
    public class CsvUtil
    {
        /// <summary>
        /// CSVを書き込む
        /// </summary>
        /// <param name="csvPath"></param>
        /// <param name="rowDataList"></param>
        /// <param name="isUnicode"></param>
        public static bool Write(string csvPath, IReadOnlyCollection<IReadOnlyCollection<object>> rowDataList, bool isUnicode = true, bool isOverwrite = true, bool isTsv = false)
        {
            
            try
            {
                using (var textUtil = new TextUtil())
                {
                    if (!textUtil.WriteOpen(csvPath, isUnicode, isOverwrite))
                    {
                        return false;
                    }
                    string delimiter = isTsv ? "\t" : ",";

                    foreach (var rowData in rowDataList)
                    {
                        var rowString = new StringBuilder();
                        int colIdx = 0;
                        foreach (object colData in rowData)
                        {
                            if (colIdx > 0)
                            {
                                rowString.Append(delimiter);
                            }

                            switch (colData)
                            {
                                case null:
                                    break;
                                case string workCol:
                                    if (isTsv)
                                    {
                                        rowString.Append(colData.ToString());
                                    }
                                    else
                                    {
                                        rowString.Append($"\"{ workCol }\"");
                                    }
                                    break;
                                default:
                                    rowString.Append(colData.ToString());
                                    break;
                            }

                            colIdx++;
                        }
                        textUtil.WriteLine(rowString.ToString());
                    }
                    textUtil.WriteClose();
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="csvPath"></param>
        /// <param name="csvDataList"></param>
        /// <param name="delimiter"></param>
        /// <param name="isUnicode"></param>
        /// <returns></returns>
        public static bool Read(string csvPath, out List<string[]> csvDataList, string delimiter = ",")
        {
            csvDataList = null;
            try
            {
                if (!TextUtil.ReadText(csvPath, out string csvText))
                {
                    return false;
                }

                // 改行毎に分割する
                string[] lines = csvText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

                // 行データを格納するリスト領域の確保
                csvDataList = new List<string[]>(lines.Length);

                foreach (var strLine in lines)
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(strLine)))
                    {
                        var parser = new TextFieldParser(stream, Encoding.UTF8);
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(delimiter);

                        // カラム毎に分割する
                        string[] cols = parser.ReadFields();

                        csvDataList.Add(cols);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }
    }
}
