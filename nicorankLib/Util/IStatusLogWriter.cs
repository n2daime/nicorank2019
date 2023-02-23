using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    public interface IStatusLogWriter
    {
        void Write(string log);
    }
}
