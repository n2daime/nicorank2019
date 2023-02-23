using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Official;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace nicorankLib.Analyze.Option
{
    /// <summary>
    /// 差分データを算出するクラス
    /// </summary>
    public class SabunReader : BasicOptionBase
    {

        /// <summary>
        /// 集計基準日時
        /// </summary>
        DateTime BaseTime;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="BaseTime">集計基準日時</param>
        /// <param name="otherOption"></param>
        public SabunReader(DateTime BaseTime)
        {
            this.BaseTime = BaseTime;
        }

        /// <summary>
        /// 基準日から差分データを算出する
        /// </summary>
        /// <param name="rakingList"></param>
        /// <returns></returns>
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            StatusLog.WriteLine($"{BaseTime.ToShortDateString()}のデータを基準に差分値を取得しています...");

            try
            {

                using (var rankHistory = new RankingHistory())
                {
                    if (!rankHistory.Open())
                    {//ランキングデータが開けない
                        StatusLog.WriteLine($"差分値取得のためのDBが開けません");
                        return false;
                    }
                    long baseTimeLong = long.Parse(DateConvert.Time2String(BaseTime, false));

                    // 差分集計が必要もののリストを作成する


                    // 公式動画の場合、投稿日時だけでは判断できないので過去ログ確認する
                    // soの番号で
                    var needSoSabunList = rankingList.Where(rank =>
                            rank.Date > BaseTime.Date &&
                            rank.IsChannel &&
                            rank.CountPlay >= 1000 //1000再生もない奴は差分集計する必要なし（Nicochart節約）
                            ).ToList();


                    StatusLog.WriteLine($"{needSoSabunList.Count}件の新着公式動画が本当に新着なのか確認しています");
                    foreach (var wRank in needSoSabunList)
                    {
                        if (rankHistory.CheckSoMovieNeedSabun(wRank.ID, baseTimeLong, out var sabunRank))
                        {
                            if (sabunRank != null)
                            {
                                wRank.CountPlay -= sabunRank.CountPlay;
                                wRank.CountComment -= sabunRank.CountComment;
                                wRank.CountMyList -= sabunRank.CountMyList;
                                wRank.CountLike -= sabunRank.CountLike;
                                wRank.PointCalcReset();
                            }
                        }
                        StatusLog.Write(".");
                    }
                    StatusLog.WriteLine($"確認完了");
                    // 集計基準日時より前に投稿された動画（新着ではないものは、差分集計が必要
                    var needSabunList = rankingList.Where(rank => rank.Date < BaseTime.Date);
                    var sabunNashiDataList = new List<Ranking>();

                    long baseTimeLong2 = long.Parse(DateConvert.Time2String(BaseTime.AddDays(-7), false));

                    foreach (var wRank in needSabunList)
                    {
                        if (!rankHistory.GetRankingSabunDataLogOfficial(wRank.ID, baseTimeLong, baseTimeLong2, out var sabunRank) || sabunRank == null)
                        {
                            sabunNashiDataList.Add(wRank);
                            continue;
                        }
                        else
                        {
                            wRank.CountPlay -= sabunRank.CountPlay;
                            wRank.CountComment -= sabunRank.CountComment;
                            wRank.CountMyList -= sabunRank.CountMyList;
                            wRank.CountLike -= sabunRank.CountLike;
                            wRank.PointCalcReset();
                        }
                    }
                    var taioumaeDate = DateConvert.String2Time("20190610", false);

                    //差分が取れない場合
                    //投稿日が過去ログ対応前の場合はあきらめる。
                    //対応後の動画は、差分じゃなくてそのまま採用する
                    var taisyougaiList = sabunNashiDataList
                        .Where(rank => rank.Date < taioumaeDate)
                        .ToList();

                    //対象外設定にしてしまう
                    taisyougaiList.ForEach(rank => rank.isDelete = true);

                    //対象外の動画を削除する
                    rankingList = rankingList.Where(rank => !rank.isDelete).ToList();
                }
            }
            catch(Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }

            return true;
        }
    }
}
