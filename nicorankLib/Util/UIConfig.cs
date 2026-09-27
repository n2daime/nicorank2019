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

        /// <summary>
        /// キー入力待ちの表示抽象。ライブラリ層にConsole.ReadLineを置くと、WinForm呼び出し・リダイレクト時にブロック等の不定振る舞いになるため、常時既定値を返す（Issue #43）。
        /// SilentMode／LocalXmlフラグ自体は互換のため温存する。現状どちらも書き換え箇所がなく常時既定値であり、削除は別タスクとする。
        /// </summary>
        public static string GetWch(string defaultChar = "" )
        {
            // SilentModeによらず既定値を返す。非SilentModeの入力待ちは行わない。
            return defaultChar;
        }
    }
}
