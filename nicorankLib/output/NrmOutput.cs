using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;

namespace nicorankLib.output
{
    public class NrmOutput : OutputBase
    {
        protected string OutputDir;
        protected string FileName;

        protected int RankStart;
        protected int RankEnd;
        protected bool isED;

        public NrmOutput()
        {
        }


        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);

                long end = Math.Min(this.RankEnd, rankingList.Count);

                var tsvDataList = new List<List<object>>((int)end);

                for (int i = RankStart; i < end; i++)
                {
                    var wRank = rankingList[i];
                    var rowData = new List<object>();


                    rowData.Add(wRank.ID);
                    rowData.Add(wRank.Date.ToString("yyyy年MM月dd日 HH:mm:ss"));
                    rowData.Add(wRank.Title);
                    rowData.Add(wRank.PlayTime);
                    rowData.Add(wRank.RankTotal);
                    rowData.Add(Ranking.GetCommaValue(wRank.PointTotal));
                    rowData.Add(wRank.RankCategory);

                    rowData.Add(string.IsNullOrWhiteSpace(wRank.Category) ? "カテゴリ無し" : wRank.Category);

                    rowData.Add(wRank.RankPlay);
                    rowData.Add(Ranking.GetCommaValue(wRank.CountPlay));
                    rowData.Add(wRank.RankComment);
                    rowData.Add(Ranking.GetCommaValue(wRank.CountComment));
                    rowData.Add(wRank.RankMyList);
                    rowData.Add(Ranking.GetCommaValue(wRank.CountMyList));
                    rowData.Add(wRank.RankLike);
                    rowData.Add(Ranking.GetCommaValue(wRank.CountLike));

                    if (wRank.LastRank == 0)
                    {
                        rowData.Add(null);
                        rowData.Add(null);
                        rowData.Add("NEW");
                    }
                    else
                    {
                        rowData.Add(wRank.LastRank);
                        rowData.Add(Ranking.GetCommaValue(wRank.LastPoint));

                        string yazirusi = "-";
                        if (wRank.RankTotal < wRank.LastRank)
                        {
                            yazirusi = "↑";
                        }
                        else if (wRank.RankTotal > wRank.LastRank)
                        {
                            yazirusi = "↓";
                        }
                        else
                        {
                            yazirusi = "→";
                        }
                        rowData.Add(yazirusi);
                    }

                    if (wRank.CountComment == 0)
                    {
                        rowData.Add("1.00");
                    }
                    else
                    {
                        rowData.Add($"{wRank.HoseiComment:F2}");
                    }

                    rowData.Add(wRank.UserID);
                    rowData.Add(wRank.UserName);
                    if (wRank.UserID.Length < 3)
                    {
                        rowData.Add("NoImage.jpg");
                    }
                    else
                    {
                        if (wRank.IsChannel)
                        {
                            rowData.Add($"ch{wRank.UserID}.jpg");
                        }
                        else
                        {
                            rowData.Add($"{wRank.UserID}.jpg");
                        }

                    }

                    if (wRank.CountMyList == 0)
                    {
                        rowData.Add("1.00");
                    }
                    else
                    {
                        rowData.Add($"{wRank.HoseiMylist:F2}");
                    }
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (var tag in wRank.FavoriteTags)
                    {
                        if (stringBuilder.Length > 0)
                        {
                            stringBuilder.Append(",");
                        }
                        stringBuilder.Append(tag);
                    }
                    rowData.Add(stringBuilder);

                    if (wRank.CountPlay == 0)
                    {
                        rowData.Add("1.00");
                    }
                    else
                    {
                        rowData.Add($"{wRank.HoseiPlay:F2}");
                    }    
                    tsvDataList.Add(rowData);

                }
                var tsvPath = Path.Combine(this.OutputDir, this.FileName);

                if (!CsvUtil.Write(tsvPath, tsvDataList, true, true, true))
                {
                    ErrLog.GetInstance().Write($"{this.FileName} を書き込み用に開けませんでした。");
                }
            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
                return false;
            }
            return true;
        }

        public void Set(string outputDir, string fileName, int rankStart, int rankEnd, bool isED = false)
        {
            OutputDir = outputDir;
            FileName = fileName;
            RankStart = rankStart;
            RankEnd = rankEnd;
            this.isED = isED;
        }
    }
}
