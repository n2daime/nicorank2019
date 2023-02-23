using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    class RegLib
    {
        public static string RegExpRep(string src, string match, string repstr)
        {
            Regex reg = new Regex(match);
            return reg.Replace(src, repstr);
        }

    }
}
