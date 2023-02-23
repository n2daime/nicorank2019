using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Official;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace nicorankLib.Analyze.Option.Basic
{
    public class HiddenMovieDelete : BasicOptionBase
    {
        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {
            try
            {
                long video_deletedCounter = 0;

                Regex regDelete = new Regex(@"/video_deleted");

                foreach (var wRank in rankingList)
                {
                    if (regDelete.IsMatch(wRank.ThumbnailURL))
                    {// 削除 or 非表示動画は除外する
                        wRank.isDelete = true;
                        video_deletedCounter++;
                    }
                }


                //対象外の動画を削除する
                rankingList = rankingList.Where(rank => !rank.isDelete).ToList();
                StatusLog.WriteLine($"{video_deletedCounter}件のvideo_deleted～データを除外しました。");
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }

            return true;
        }
    }
}
