using nicorankLib.Analyze;
using nicorankLib.Analyze.Input;
using nicorankLib.Analyze.Json;
using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Option;
using nicorankLib.api;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;
using System;
using System.Collections.Generic;

namespace nicorankLib.Analyze.Option.Basic
{
    public class MovieInfoReader : BasicOptionBase
    {
        DateTime? TargetTime = null;

        public MovieInfoReader(DateTime? targetTime = null)
        {
            TargetTime = targetTime;
        }

        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            try
            {

                StatusLog.WriteLine($"動画情報を取得します {rankingList.Count}件");
                List<Ranking> targetList = rankingList;


                using (var api = new NicoApi())
                {
                    if (!api.OpenDB())
                    {
                        //ApiXML.db は表示用キャッシュのため、開けなくても集計は続ける（Issue #40）
                        StatusLog.WriteLine("DB/ApiXML.dbを開けませんでした。動画情報なしで集計を続けます");
                    }
                    else
                    {
                        // DBを更新する
                        //不足分の確保・読取に失敗しても集計は続ける。取れない動画は空欄のまま残し、除外しない
                        if (!api.UpdateTumbInfo(targetList, TargetTime))
                        {
                            StatusLog.WriteLine("動画情報の更新に失敗した動画があります:UpdateTumbInfo。取得済み分で続けます");
                        }

                        // DBから値を取得する
                        if (!api.GetMovieInfo(targetList,false,true))
                        {
                            StatusLog.WriteLine("動画情報の取得に失敗した動画があります:GetMovieInfo。取得済み分で続けます");
                        }
                        api.CloseDB();
                    }
                }
                StatusLog.WriteLine("ユーザー情報を取得終了。アイコンは別途ダウンロードしてください");

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
