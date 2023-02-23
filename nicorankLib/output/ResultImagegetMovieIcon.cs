using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Common;

namespace nicorankLib.output
{
    public class ResultImagegetMovieIcon : ResultImagegetBase
    {
        int rankED;
        public ResultImagegetMovieIcon(string outputDir, string fileName , int rankED ) : base(outputDir, fileName)
        {
            this.rankED = rankED;
        }

        /// <summary>
        /// ダウンロード場所
        /// </summary>
        /// <returns></returns>
        protected override string GetDownLoadDir()
        {
            return Config.GetInstance().StrIconDLPath;
        }

        /// <summary>
        /// ダウンロードしたときのファイル名
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        protected override string GetDownLoadName(Ranking rank)
        {
            return $"{rank.RankTotal}.jpg";
        }

        /// <summary>
        /// 何位までダウンロードするか
        /// </summary>
        /// <returns></returns>
        protected override int GetDownLoadRank(IReadOnlyList<Ranking> rankingList)
        {
            return rankED;
        }

        /// <summary>
        /// イメージのURL
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        protected override string GetImagePath(Ranking rank)
        {
            return rank.ThumbnailURL;
        }
    }
}
