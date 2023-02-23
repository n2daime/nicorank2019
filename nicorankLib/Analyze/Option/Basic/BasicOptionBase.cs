using nicorankLib.Analyze.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Option
{
    /// <summary>
    /// ランキング情報に情報を付与する(ユーザー情報を追加する、差分を計算する、、など）
    /// </summary>
    public abstract class BasicOptionBase
    {

        /// <summary>
        /// 拡張オプション付きコンストラクタ
        /// </summary>
        /// <param name="otherOption"></param>
        public BasicOptionBase()
        {
        }

        /// <summary>
        /// ランキングに情報を付与する
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        public abstract bool AnalyzeRank(ref List<Ranking> rankingList);

    }
}
