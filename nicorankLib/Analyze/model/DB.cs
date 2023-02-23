using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.model
{
    /// <summary>
    /// データベースのリソースを管理するクラス
    /// </summary>
    public class DB
    {
        /// <summary>
        /// 公式過去ランキングデータを格納したDB
        /// </summary>
        public const string LOG_OFFICEIAL = "DB/LogOfficial.db";

        /// <summary>
        /// ニコチャートのデータを格納したDB
        /// </summary>
        public const string LOG_NICOCHART = "DB/LogNicoChart.db";

        /// <summary>
        /// ニコランのランキングデータを格納したDB
        /// </summary>
        public const string NiCORAN_HISTORY = "DB/NicoranHistory.db";

        /// <summary>
        /// ニコランのランキングデータを格納したDB
        /// </summary>
        public const string LOG_SNAPSHOT = "LogSnapshot";
    }
}
