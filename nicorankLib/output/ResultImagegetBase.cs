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
    public abstract class ResultImagegetBase : OutputBase
    {
        protected string OutputDir;
        protected string FileName;

        public ResultImagegetBase(string outputDir, string fileName)
        {
            OutputDir = outputDir;
            FileName = fileName;
        }

        /// <summary>
        /// 何位までダウンロードするか
        /// </summary>
        /// <returns></returns>
        protected abstract int GetDownLoadRank(IReadOnlyList<Ranking> rankingList);

        /// <summary>
        /// イメージファイルのURL
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        protected abstract string GetImagePath(Ranking rank);

        /// <summary>
        /// イメージファイルのダウンロードディレクトリ
        /// </summary>
        /// <returns></returns>
        protected abstract string GetDownLoadDir();

        /// <summary>
        /// ダウンロードしたときのファイル名
        /// </summary>
        /// <param name="rank"></param>
        /// <returns></returns>
        protected abstract string GetDownLoadName(Ranking rank);

        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {

            try
            {
                Directory.CreateDirectory(OutputDir);

                int endRank = Math.Min(GetDownLoadRank(rankingList), rankingList.Count);
                var tsvDataList = new List<List<object>>((int)endRank);

                string strDownPath = GetDownLoadDir();
                for (int i = 0; i < endRank; i++)
                {
                    var wRank = rankingList[i];

                    string strFileName = GetDownLoadName(wRank);
                    if(string.IsNullOrWhiteSpace(strFileName))
                    {
                        continue;
                    }
                    var rowData = new List<object>(15);

                    rowData.Add(GetImagePath(wRank));
                    rowData.Add(strDownPath);
                    rowData.Add(strFileName);
                    rowData.Add(null);
                    rowData.Add(null);
                    rowData.Add(null);
                    rowData.Add("39316.8175424884");
                    rowData.Add(0);
                    rowData.Add(null);
                    rowData.Add(0);
                    rowData.Add("4429");
                    rowData.Add("0");
                    rowData.Add(Path.Combine( strDownPath , strFileName));
                    rowData.Add("0");
                    rowData.Add(null);

                    tsvDataList.Add(rowData);
                }

                if (tsvDataList.Count > 0)
                {
                    var tsvPath = Path.Combine(this.OutputDir, this.FileName);

                    if (!CsvUtil.Write(tsvPath, tsvDataList, false, true, true))
                    {
                        ErrLog.GetInstance().Write($"{this.FileName} を書き込み用に開けませんでした。");
                    }
                }
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
