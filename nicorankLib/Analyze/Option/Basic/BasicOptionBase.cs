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
    /// 破棄契約を持つ。なぜ基底でIDisposableにするか: 集計後に残るDB接続を持つのは一部の派生
    /// （SnapShotSabunReader系・TagRankTotalReader）だけだが、呼び出し側（RankingAnalyze）は
    /// リストとして一括で片付けるため、個別の型を知らなくても破棄できる必要があるから。
    /// 資源を持たない派生は基底の空実装のまま何も書かなくてよい。将来資源を持つ派生が増えたら
    /// そのクラスだけDisposeをoverrideすればよく、呼び出し側の修正は不要になる（Issue #44。提案元 #42）。
    /// </summary>
    public abstract class BasicOptionBase : IDisposable
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

        /// <summary>
        /// 後片付け。資源を持たない派生はこの空実装のまま使う。資源を持つ派生はoverrideして閉じる。
        /// </summary>
        public virtual void Dispose()
        {
        }

    }
}
