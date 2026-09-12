using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.model;
using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace nicorankLib.Analyze.Option.Basic
{
    /// <summary>
    /// タグ検索のv2最新値モード（基準日DBなし）用。ライブ検索で得た4数値をそのまま集計値にする（差分なし）。
    /// SnapshotDBを開かないため、DB取得待ちなしで集計できる。
    /// 数値の出所はTagRankAnalyze.LiveCounters（工場が同一インスタンスを両者に渡す共有参照）。
    /// なぜInput参照を持つか: RankingAnalyzeはInput→Optionの順に実行されるため、AnalyzeRank実行時点ではLiveCountersが確定している。
    /// </summary>
    public class TagRankLiveTotalReader : BasicOptionBase
    {
        public DateTime AnalyzeTime { get; protected set; }

        protected readonly TagRankAnalyze Input;

        public TagRankLiveTotalReader(TagRankAnalyze input, DateTime analyzeTime)
        {
            Input = input;
            AnalyzeTime = analyzeTime;
        }

        /// <summary>
        /// 開く（DBを使わないため入力の妥当性確認のみ）
        /// </summary>
        public bool Open()
        {
            return Input != null && Input.Query != null;
        }

        /// <summary>
        /// ライブ値を累積値欄に反映する（純粋処理。対応表に無いIDはisDeleteにする）
        /// </summary>
        public static void ApplyLiveTotals(TagRankAnalyze input, List<Ranking> rankingList)
        {
            foreach (var wRank in rankingList)
            {
                if (input.LiveCounters.TryGetValue(wRank.ID, out SnapShotJson.SnapShotJsonData data))
                {
                    wRank.CountPlayTotal = data.CountPlay;
                    wRank.CountCommentTotal = data.CountComment;
                    wRank.CountMyListTotal = data.CountMylist;
                    wRank.CountLikeTotal = data.CountLike;
                }
                else
                {// ライブ結果に存在しない（取得と集計の間で非公開化した場合など）
                    wRank.isDelete = true;
                }
            }
        }

        /// <summary>
        /// ライブ値をそのまま集計値にする（差分なし）
        /// </summary>
        /// <remarks>
        /// 事前条件: RankingAnalyzeがInput→Optionの順に実行するため、Input成功後のLiveCountersが確定していること。
        /// Input失敗時は本メソッドに到達しない。単独呼び出しでLiveCountersが空なら全件isDeleteの空成功になるが、0件検索の空成功を維持するため動作は変えない。
        /// </remarks>
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            try
            {
                if (Input == null)
                {
                    return false;
                }
                ApplyLiveTotals(Input, rankingList);
                //データが取得できたものだけ抽出
                rankingList = rankingList.Where(wRank => !wRank.isDelete).ToList();

                // 動画情報を取得できていないので、取得する
                var movieInfoReader = new MovieInfoReader(this.AnalyzeTime);
                if (!movieInfoReader.AnalyzeRank(ref rankingList))
                {
                    return false;
                }

                // 差分なしのためライブ値を集計値として採用する
                foreach (var wRank in rankingList)
                {
                    wRank.CountPlay = wRank.CountPlayTotal;
                    wRank.CountComment = wRank.CountCommentTotal;
                    wRank.CountMyList = wRank.CountMyListTotal;
                    wRank.CountLike = wRank.CountLikeTotal;
                    wRank.PointCalcReset();
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }
    }
}
