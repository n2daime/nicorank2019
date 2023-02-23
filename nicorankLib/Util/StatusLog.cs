using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    public class StatusLog
    {
        protected static IStatusLogWriter logWriter = null;

        public static void SetLogWriter(IStatusLogWriter logWriter)
        {
            StatusLog.logWriter = logWriter;
        }

        public static void Write(string log)
        {
            StatusLog.logWriter?.Write(log);
        }

        public static void WriteLine(string log)
        {
            StatusLog.logWriter?.Write($"{log}\n");
        }
    }
}
