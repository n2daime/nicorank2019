using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.output
{
    public class ResultImagegetUserIcon:ResultImagegetBase
    {
        public ResultImagegetUserIcon(string outputDir, string fileName) : base(outputDir, fileName)
        {
        }

        /// <summary>
        /// ダウンロード場所
        /// </summary>
        /// <returns></returns>
        protected override string GetDownLoadDir()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "UserIcon");

        }

        /// <summary>
        /// ダウンロードしたときのファイル名。
        /// UserInfo.Num の範囲（総合順位が UserNum 以内、またはカテゴリ1位）だけを対象にする。
        /// なぜ絞るか: UserInfo.Num は「ユーザー情報を取りに行く動画数」の設定であり、
        /// 全件をキューに出すと数千件・数十分のDLになって設定の意図と合わない。
        /// 範囲外は空文字を返し、ResultImagegetBase.Execute 側で行ごとスキップさせる。
        /// </summary>
        protected override string GetDownLoadName(Ranking rank)
        {
            int userInfoNum = Config.GetInstance().UserNum;
            if (userInfoNum > 0
                && rank.RankTotal > userInfoNum
                && rank.RankCategory != 1)
            {
                return string.Empty;
            }

            // 直接ダウンロード（Issue #56）と同じ規則を参照する。
            return ImageFileNaming.GetUserIconFileName(rank);
        }

        /// <summary>
        /// 何位までダウンロードするか
        /// </summary>
        /// <returns></returns>
        protected override int GetDownLoadRank(IReadOnlyList<Ranking> rankingList)
        {
            return rankingList.Count;//ユーザー上があるものすべて
        }

        /// <summary>
        /// イメージのURL
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        protected override string GetImagePath(Ranking rank)
        {
            return rank.UserImageURL;
        }

    }
}
