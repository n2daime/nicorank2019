using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace nicorankLib.Analyze.Input
{
    public class SPAnalyze : InputBase
    {
        public string MovieListFile { get; protected set; }
        public DateTime AnalyzeTime { get; protected set; }

        public string GenreSQL = @"人気のタグ LIKE '%""演奏してみた""%'";

        /// <summary>
        /// 仮集計用の動画ID列。nullなら従来どおりファイルから読む（Issue #54）。
        /// なぜフラグを別に持たないか: null と「空のリスト」を区別する必要があるのは
        /// 入力経路の判定だけで、空リストはそのまま空の集計対象として扱えばよいためである。
        /// </summary>
        protected IReadOnlyList<string> MovieIds { get; set; }

        public SPAnalyze(DateTime analyzeTime, string movieListFile)
        {
            AnalyzeTime = analyzeTime;
            MovieListFile = movieListFile;
        }

        /// <summary>
        /// 動画ID列を直接受け取る（仮集計・Issue #54）。
        /// なぜファイル経由にしないか: 仮集計のIDは NicoranHistory.db から作るため、
        /// 途中でテキストファイルに書き出すと余計な中間ファイルと後始末が増えるだけだから。
        /// </summary>
        public SPAnalyze(DateTime analyzeTime, IEnumerable<string> movieIds)
        {
            AnalyzeTime = analyzeTime;
            MovieIds = new List<string>(movieIds ?? new List<string>());
        }


        public override bool AnalyzeRank(out List<Ranking> rakingList)
        {
            rakingList = new List<Ranking>();

            if (MovieIds != null)
            {
                // 仮集計のID列。ファイル読みと同じ正規化（前後空白除去・空行除外）を通す
                rakingList = ToRankingList(MovieIds);
                return true;
            }

            if (!TextUtil.ReadText(MovieListFile, out string csvText))
            {
                return false;
            }

            // 改行毎に分割する
            string[] lines = csvText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            rakingList = ToRankingList(lines);

            return true;
        }

        /// <summary>
        /// 動画ID列を Ranking 列にする。ファイル経路とID列経路で同じ扱いにするため共通化する
        /// （前後空白を除き、空のIDは捨てる）。
        /// </summary>
        private static List<Ranking> ToRankingList(IEnumerable<string> ids)
        {
            var rankingList = new List<Ranking>();
            foreach (var id in ids)
            {
                var workId = id?.Trim();
                if (!string.IsNullOrEmpty(workId))
                {
                    var wRank = new Ranking();
                    wRank.ID = workId;
                    rankingList.Add(wRank);
                }
            }
            return rankingList;
        }

        public override DateTime getAnalyzeDay()
        {
            return AnalyzeTime;
        }

        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            AnalyzeTime = analyzeDay;
        }
    }
}
