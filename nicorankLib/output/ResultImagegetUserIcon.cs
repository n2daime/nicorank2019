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
        /// ダウンロードしたときのファイル名
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        protected override string GetDownLoadName(Ranking rank)
        {
            // 直接ダウンロード（Issue #56）と同じ規則を参照する。名前の生成以外は変えないため、
            // queue_UserIcon.irv の内容は従来と1バイトも変わらない。
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
