using nicorankLib.Analyze.model;
using nicorankLib.Common;
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
            if (string.IsNullOrWhiteSpace(rank.UserID))
            {
                return "";
            }
            else
            {
                if(rank.IsChannel)
                {
                    return $"ch{rank.UserID}.jpg";
                }
                else
                {
                    return $"{rank.UserID}.jpg";
                }
                
            }
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
