using nicorankLib.Analyze.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.output
{
    public abstract class OutputBase
    {
        /// <summary>
        /// ファイルを出力する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public abstract bool Execute(IReadOnlyList<Ranking> rankingList);
    }
}
