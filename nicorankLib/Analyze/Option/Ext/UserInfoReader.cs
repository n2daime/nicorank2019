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


        public  bool AnalyzeRank(List<Ranking> rankingList)
        {

            try
            {

                StatusLog.WriteLine("ユーザー情報を取得します");

                //取得対象の抽出
                // 指定順位内か、カテゴリ一位の場合は取得する
                List < Ranking > targetList = rankingList;
                if (UserEnd > 0)
                {
                     targetList =
                        rankingList.Where(wRank => wRank.RankTotal <= this.UserEnd || wRank.RankCategory == 1)
                        .ToList();
                }

                using (var api = new NicoApi())
                {
                    if(!api.OpenDB())
                    {
                        return false;
                    }
                    // DBを更新する
                    if(!api.UpdateTumbInfo(targetList, TargetTime))
                    {
                        StatusLog.WriteLine("ユーザー情報を取得中にエラーが発生しました:UpdateTumbInfo");
                        return false;
                    }

                    // DBから値を取得する
                    if (!api.GetUserInfo(targetList))
                    {
                        StatusLog.WriteLine("ユーザー情報を取得中にエラーが発生しました:GetUserInfo");
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
