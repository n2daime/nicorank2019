using System;
using System.Collections.Generic;

namespace nicorankLib.Analyze.model
{
    /// <summary>
    /// 動画IDの決定的な比較子。同点時タイブレーク用。
    /// なぜ数値認識が必要か：単純な辞書式(Ordinal)では桁数が違うIDの順序が数値順と一致しない
    /// （例：3文字目の '1' と '2' の比較で sm199 が sm20 より先になる）。IDの大小を割り当て順と
    /// 直感的に一致させるため、種別(先頭の非数字部)→数字部の順に比べる。ID体系が変わって数字化
    /// できない場合も例外にせず辞書式にフォールバックし、決定的であることを保つ。
    /// なぜComparer1個にまとめるか：ThenByの二次比較子は一次キー(ポイント等)が等しい同点ペアに
    /// 対してだけ呼ばれるため、同点時だけ分解すれば処理コストが最小になる。キー抽出をThenByで
    /// 2段に分けると全件の分解が毎回走るため採用しない。
    /// </summary>
    public sealed class RankingIdComparer : IComparer<string>
    {
        /// <summary>
        /// 使い回し用の単一インスタンス。ステートレスのため共有してよい。
        /// </summary>
        public static readonly RankingIdComparer Instance = new RankingIdComparer();

        private RankingIdComparer()
        {
        }

        /// <summary>
        /// 動画IDを比較する。nullは非nullより小さいものとして扱う。
        /// </summary>
        public int Compare(string x, string y)
        {
            // 同一参照（両方nullを含む）は等しいとする
            if (ReferenceEquals(x, y))
            {
                return 0;
            }
            if (x == null)
            {
                return -1;
            }
            if (y == null)
            {
                return 1;
            }
            // 完全一致は分解せずに返す（最速経路）
            if (string.Equals(x, y, StringComparison.Ordinal))
            {
                return 0;
            }

            SplitId(x, out string prefixX, out string numberX);
            SplitId(y, out string prefixY, out string numberY);

            // 種別（sm/so等）を先に比べる。混在時は種別順で決定的になる
            int prefixCmp = string.Compare(prefixX, prefixY, StringComparison.Ordinal);
            if (prefixCmp != 0)
            {
                return prefixCmp;
            }

            // 両方とも数字化できれば数値で比べる（sm20 と sm199 を数値順にするため）
            bool parsedX = long.TryParse(numberX, out long numX);
            bool parsedY = long.TryParse(numberY, out long numY);
            if (parsedX && parsedY)
            {
                int numCmp = numX.CompareTo(numY);
                if (numCmp != 0)
                {
                    return numCmp;
                }
                // 数値まで等しい（例：前ゼロの有無）は全体の辞書式で決定的にする
            }

            // 片方だけ数字化できない等の中途半端な状態は全体の辞書式にフォールバックする
            return string.Compare(x, y, StringComparison.Ordinal);
        }

        /// <summary>
        /// IDを先頭の非数字部（種別）と残りの数字部に分ける。
        /// 例：sm20 → ("sm", "20")、so40000000 → ("so", "40000000")。
        /// 数字部が空の場合は空文字のままとし、呼び出し側のTryParseで失敗扱いにする。
        /// </summary>
        private static void SplitId(string id, out string prefix, out string number)
        {
            int i = 0;
            while (i < id.Length && !char.IsDigit(id[i]))
            {
                i++;
            }
            prefix = id.Substring(0, i);
            number = i < id.Length ? id.Substring(i) : string.Empty;
        }
    }
}
