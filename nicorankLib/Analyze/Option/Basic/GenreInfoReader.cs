using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nicorankLib.Analyze.Option;
using nicorankLib.api;

namespace nicorankLib.Analyze.Option
{
    public class GenreInfoReader : BasicOptionBase
    {
        DateTime? TargetTime = null;

        public GenreInfoReader(DateTime? targetTime = null)
        {
            TargetTime = targetTime;
        }

        public override bool AnalyzeRank(ref List<Ranking> rankingList)
        {

            try
            {

                

                //取得対象の抽出
                // 指定順位内か、カテゴリ一位の場合は取得する
                List<Ranking> targetList = rankingList;

                targetList =
                    rankingList.Where(wRank =>
                        string.IsNullOrEmpty(wRank.Category) //カテゴリ不明のものが対象
                    ).ToList();

                StatusLog.WriteLine($"ジャンル不明の動画 {targetList.Count} 件について情報を取得します");

                using (var api = new NicoApi())
                {
                    if (!api.OpenDB())
                    {
                        return false;
                    }
                    // DBを更新する
                    if (!api.UpdateTumbInfo(targetList, TargetTime))
                    {
                        StatusLog.WriteLine("ジャンルを取得中にエラーが発生しました:UpdateTumbInfo");
                        return false;
                    }

                    // DBから値を取得する
                    if (!api.GetUserInfo(targetList))
                    {
                        StatusLog.WriteLine("ジャンルを取得中にエラーが発生しました:GetUserInfo");
                        return false;
                    }
                    api.CloseDB();
                }
                StatusLog.WriteLine("");
                StatusLog.WriteLine("ジャンルを取得終了");

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
