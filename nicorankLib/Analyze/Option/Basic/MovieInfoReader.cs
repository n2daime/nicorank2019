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
                        return false;
                    }
                    // DBを更新する
                    if (!api.UpdateTumbInfo(targetList, TargetTime))
                    {
                        StatusLog.WriteLine("動画情報を取得中にエラーが発生しました:UpdateTumbInfo");
                        return false;
                    }

                    // DBから値を取得する
                    if (!api.GetMovieInfo(targetList,false,true))
                    {
                        StatusLog.WriteLine("動画情報を取得中を取得中にエラーが発生しました:GetUserInfo");
                        return false;
                    }
                    api.CloseDB();
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
