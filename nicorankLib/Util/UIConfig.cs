using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Util
{
    public class UIConfig
    {
        protected static UIConfig Instance = new UIConfig();

        public bool SilentMode;
        public bool LocalXml;

        protected UIConfig()
        {
            SilentMode = true;
        }

        public static UIConfig GetInstance()
        {
            return Instance;
        }

        public static string GetWch(string defaultChar = "" )
        {
            var uiConfig = UIConfig.GetInstance();
            if(uiConfig.SilentMode)
            {
                return defaultChar;
            }
            else
            {
                return Console.ReadLine();
            }
        }
    }
}
