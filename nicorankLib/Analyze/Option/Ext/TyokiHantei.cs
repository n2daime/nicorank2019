using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Common;
using nicorankLib.output;
using nicorankLib.Util;
using nicorankLib.Util.Text;

namespace nicorankLib.Analyze.Option
{
    public class TyokiHantei : OutputBase, IExtOptionBase
    {
        /// <summary>
        /// 出力フォルダ
        /// </summary>
        protected string OutputDir;

        public const string FileName = "長期動画リスト.txt";

        /// <summary>
        /// 長期ランキングリスト
        /// </summary>
        public List<Ranking> tyokiRankList;

        /// <summary>
        /// ランクイン回数
        /// </summary>
        public Dictionary<string, long> tyokiRankMap;

        /// <summary>
        /// DBファイル名
        /// </summary>
        public const string DATASOURCE = DB.NiCORAN_HISTORY;

        /// <summary>
        /// 集計日
        /// </summary>
        public DateTime SyukeiBi;

        public bool AnalyzeRank(List<Ranking> rankingList)
        {

            var config = Config.GetInstance();
            try
            {
                using (var dbCtrl = new SQLiteCtrl())
                {
                    if (!dbCtrl.Open(DATASOURCE))
                    {
                        StatusLog.WriteLine($"{ DATASOURCE }が参照できません。");
                        return false;
                    }

                    //紹介する対象の動画のみを抽出する
                    var syokaiList = rankingList.Where(wRank => wRank.RankTotal <= config.Rank).ToList();
                    //長期リスト
                    this.tyokiRankList = new List<Ranking>(syokaiList.Count);
                    this.tyokiRankMap = new Dictionary<string, long>(syokaiList.Count);
                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        aCmd.CommandText =
                            @"SELECT COUNT(*) as 件数 FROM History
                              Where ID = @ID and 集計日< @集計日";

                        aCmd.Parameters.Clear();
                        aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(this.SyukeiBi, false));
                        foreach (var wRank in syokaiList)
                        {
                            aCmd.Parameters.AddWithValue("@ID", wRank.ID);
                            using (var reader = aCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    var rankCount = Convert.ToInt64(reader["件数"]);
                                    if (rankCount >= 2)
                                    {//過去2回目+今回1＝3回目以降は長期とする
                                        this.tyokiRankList.Add(wRank);
                                    }
                                    rankCount++;
                                    tyokiRankMap[wRank.ID] = rankCount;
                                }
                                else
                                {
                                    tyokiRankMap[wRank.ID] = 1;
                                }
                            }
                        }
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



        public void Set(string outputDir, DateTime syukeiBi)
        {
            OutputDir = outputDir;
            SyukeiBi = syukeiBi;
        }

        /// <summary>
        /// 長期動画.txtを出力する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {
            var config = Config.GetInstance();
            try
            {
                //紹介する対象の動画のみを抽出する
                var syokaiList = rankingList.Where(wRank => wRank.RankTotal <= config.Rank).ToList();

                Directory.CreateDirectory(this.OutputDir);
                var filepath = Path.Combine(this.OutputDir, FileName);
                using (var textUtil = new TextUtil())
                {
                    if (!textUtil.WriteOpen(filepath, true))
                    {
                        StatusLog.WriteLine($"{ FileName }の書き込みに失敗しました");
                        return false;
                    }
                    var log = new StringBuilder();

                    log.Append($"----- 長期対象の数は {this.tyokiRankList.Count}個 です。---- \r\n");
                    foreach (var wRank in this.tyokiRankList)
                    {
                        log.Append($"{tyokiRankMap[wRank.ID]:00}回目 {wRank.ID} {wRank.Title}\r\n");
                    }
                    StatusLog.Write(log.ToString());

                    log.Append($"----- 詳細 ---- \r\n");
                    foreach (var wRank in syokaiList)
                    {
                        log.Append($"{tyokiRankMap[wRank.ID]:00}回目 {wRank.ID} {wRank.Title}\r\n");
                    }

                    textUtil.WriteLine(log.ToString());
                    textUtil.WriteClose();
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
