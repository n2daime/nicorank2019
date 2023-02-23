using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    public class DateConvert
    {
        public static string Time2String(DateTime dateTime, bool isFull)
        {
            return isFull ?
                dateTime.ToString("yyyyMMddHHmmss")
                :
                dateTime.ToString("yyyyMMdd");
        }
        public static DateTime String2Time(object dateTime, bool isFull)
        {
            return isFull ?
                DateTime.ParseExact(dateTime.ToString(), "yyyyMMddHHmmss", null)
                :
                DateTime.ParseExact(dateTime.ToString(), "yyyyMMdd", null);
        }
    }
}
