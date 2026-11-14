using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.Util;

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
            // 直接ダウンロード（Issue #56）と同じ規則を参照する。名前の生成以外は変えないため、
            // queue.irv の内容は従来と1バイトも変わらない。
            return ImageFileNaming.GetMovieFileName(rank);
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
