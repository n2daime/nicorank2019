using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nicorankLib.Analyze.Option;

namespace nicorankLib.api
{
    public class UserInfoReader : IExtOptionBase
    {
        /// <summary>
        /// 取得数
        /// </summary>
        public int UserEnd = 0;
        DateTime? TargetTime = null;

        public UserInfoReader(int userEnd, DateTime? targetTime = null)
        {
            UserEnd = userEnd;
            TargetTime = targetTime;
        }


        public bool AnalyzeRank(List<Ranking> rankingList)
        {

            try
            {

                StatusLog.WriteLine("ユーザー情報を取得します");

                //取得対象の抽出
                // 指定順位内か、カテゴリ一位の場合は取得する
                List<Ranking> targetList = rankingList;
                if (UserEnd > 0)
                {
                    targetList =
                       rankingList.Where(wRank =>
                           (wRank.RankTotal <= this.UserEnd || wRank.RankCategory == 1)
                           && string.IsNullOrEmpty(wRank.UserName) //未取得の動画のみ対象
                        ).ToList();
                }

                using (var api = new NicoApi())
                {
                    if (!api.OpenDB())
                    {
                        //ApiXML.db は表示用キャッシュのため、開けなくても集計は続ける（Issue #40）
                        StatusLog.WriteLine("DB/ApiXML.dbを開けませんでした。ユーザー情報なしで集計を続けます");
                    }
                    else
                    {
                        // DBを更新する
                        //不足分の確保・読取に失敗しても集計は続ける。取れない動画は空欄のまま残し、除外しない
                        if (!api.UpdateTumbInfo(targetList, TargetTime))
                        {
                            StatusLog.WriteLine("ユーザー情報の更新に失敗した動画があります:UpdateTumbInfo。取得済み分で続けます");
                        }

                        // DBから値を取得する
                        if (!api.GetUserInfo(targetList))
                        {
                            StatusLog.WriteLine("ユーザー情報の取得に失敗した動画があります:GetUserInfo。取得済み分で続けます");
                        }
                        api.CloseDB();
                    }
                }
                StatusLog.WriteLine("");
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
