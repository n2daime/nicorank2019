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
    /// (ランキング順位計算後前提用のオプション）
    /// </summary>
    public interface IExtOptionBase
    {

        /// <summary>
        /// ランキングに情報を付与する
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        bool AnalyzeRank(List<Ranking> rankingList);
    }
}
