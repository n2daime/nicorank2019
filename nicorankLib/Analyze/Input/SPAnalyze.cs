using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace nicorankLib.Analyze.Input
{
    public class SPAnalyze : InputBase
    {
        public string MovieListFile { get; protected set; }
        public DateTime AnalyzeTime { get; protected set; }

        public string GenreSQL = @"人気のタグ LIKE '%""演奏してみた""%'";

        public SPAnalyze(DateTime analyzeTime,string movieListFile)
        {
            AnalyzeTime = analyzeTime;
            MovieListFile = movieListFile;
        }


        public override bool AnalyzeRank(out List<Ranking> rakingList)
        {
            rakingList = new List<Ranking>();

            if (!TextUtil.ReadText(MovieListFile, out string csvText))
            {
                return false;
            }

            // 改行毎に分割する
            string[] lines = csvText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var id in lines)
            {
                var workId = id.Trim();
                if (workId != string.Empty)
                {
                    var wRank = new Ranking();
                    wRank.ID = workId;
                    rakingList.Add(wRank);
                }
            }

            return true;
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
