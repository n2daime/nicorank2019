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
    public class ResultCsv : OutputBase
    {
        /// <summary>
        /// 出力フォルダ
        /// </summary>
        protected string outFolder;

        /// <summary>
        /// CSVの名前
        /// </summary>
        protected IReadOnlyList<CsvConfig> csvConfigList;

        /// <summary>
        /// CSVの出力設定
        /// </summary>
        public class CsvConfig
        {
            /// <summary>
            /// 出力ファイル名
            /// </summary>
            public string csvName;
            /// <summary>
            /// Unicodeかどうか
            /// </summary>
            public bool isUnicode = true;

            /// <summary>
            /// 上書きか？（falseで追記)
            /// </summary>
            public bool isOverwrite = true;
        }


        /// <summary>
        /// 出力の設定をする
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="fileName"></param>
        public void SetOutput(string folder, IReadOnlyList<CsvConfig> csvConfig)
        {
            this.outFolder = folder;
            this.csvConfigList = csvConfig;
        }


        /// <summary>
        /// 出力する
        /// </summary>
        /// <param name="rankingList"></param>
        /// <returns></returns>
        public override bool Execute(IReadOnlyList<Ranking> rankingList)
        {
            try
            {
                Directory.CreateDirectory(outFolder);

                //CSVに書き込むためのデータリスト
                var csvDataList = new List<List<object>>(rankingList.Count);

                //ヘッダー
                var headerData = new List<object>() {
                    "ID","投稿日","タイトル","再生時間",
                    "総合ランク","ポイント","カテゴリランク","カテゴリ","再生ランク","再生数",
                    "コメントランク","コメント数","マイリストランク","登録数","いいねランク","いいね数","前回ランク","前回ポイント",
                    "コメント補正","コメントポイント","運営ポイントランク","運営ポイント",
                    "ユーザーID","ユーザー名","ユーザーアイコン","マイリスト補正","マイリストポイント",
                    "再生補正","再生ポイント","いいねポイント"
                };
                //headerData.Add("人気のタグ");
                csvDataList.Add(headerData);

                //動画の数だけループ
                foreach (var wRank in rankingList)
                {
                    try
                    {
                        // 列データ
                        var rowData = new List<object>(headerData.Count);

                        //"ID","投稿日","タイトル","再生時間",
                        rowData.Add(wRank.ID);
                        rowData.Add(wRank.Date.ToString("yyyy年MM月dd日 HH：mm：ss 投稿"));
                        rowData.Add( getTitle( wRank ) );
                        rowData.Add(wRank.PlayTime);

                        //"総合ランク","ポイント","カテゴリランク","カテゴリ","再生ランク","再生数"
                        rowData.Add(wRank.RankTotal);
                        rowData.Add(wRank.PointTotal);
                        rowData.Add(wRank.RankCategory);
                        rowData.Add(wRank.Category == "全ジャンル" ? "カテゴリ無し" : wRank.Category);
                        rowData.Add(wRank.RankPlay);
                        rowData.Add(wRank.CountPlay);
                        rowData.Add(wRank.RankComment);
                        rowData.Add(wRank.CountComment);

                        //"マイリストランク","登録数"                       
                        rowData.Add(wRank.RankMyList);
                        rowData.Add(wRank.CountMyList);

                        //"いいねランク","いいね数"
                        rowData.Add(wRank.RankLike);
                        rowData.Add(wRank.CountLike);

                        //"前回ランク","前回ポイント",
                        if (wRank.LastRank == 0)
                        {
                            rowData.Add(null);
                            rowData.Add(null);
                        }
                        else
                        {
                            rowData.Add($"第{wRank.LastRank}位");
                            rowData.Add(wRank.LastPoint);
                        }
                        //"コメント補正","コメントポイント",
                        if (wRank.CountComment == 0)
                        {
                            rowData.Add("1.000000");
                            rowData.Add(0);
                        }
                        else
                        {
                            rowData.Add($"{wRank.HoseiComment:F6}");
                            rowData.Add(wRank.PointComment);
                        }

                        //"運営ポイントランク","運営ポイント",
                        rowData.Add(null);//未対応
                        rowData.Add(null);//未対応

                        //"ユーザーID","ユーザー名","ユーザーアイコン",
                        rowData.Add(wRank.UserID);
                        rowData.Add(getUserName( wRank ));
                        if (string.IsNullOrWhiteSpace(wRank.UserID))
                        {
                            rowData.Add("");
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
                        //"マイリスト補正","マイリストポイント",
                        if (wRank.CountMyList == 0)
                        {
                            rowData.Add("1.000000");
                            rowData.Add(0);
                        }
                        else
                        {
                            rowData.Add($"{wRank.HoseiMylist:F6}");
                            rowData.Add(wRank.PointMyList);
                        }

                        //"再生補正","再生ポイント"
                        if (wRank.CountPlay == 0)
                        {
                            rowData.Add("1.000000");
                            rowData.Add(0);
                        }
                        else
                        {
                            rowData.Add($"{wRank.HoseiPlay:F6}");
                            rowData.Add(wRank.PointPlay);
                        }
                        //"いいねポイント
                        rowData.Add(wRank.PointLike);


                        csvDataList.Add(rowData);

                    }
                    catch (Exception ex)
                    {
                        var pLog = ErrLog.GetInstance();
                        pLog.Write($"{wRank.ID}書き出し中にエラー発生(Result_csv::Execute)");
                        pLog.Write(ex);
                        return false;
                    }
                }
                foreach (var csvConfig in this.csvConfigList)
                {
                    var csvPath = Path.Combine(this.outFolder, csvConfig.csvName);

                    var writeData = csvDataList;
                    if(!csvConfig.isOverwrite)
                    {//追記の時はヘッダーを削除する
                        writeData.Remove(headerData);
                    }
                    if (!CsvUtil.Write(csvPath, writeData, csvConfig.isUnicode))
                    {
                        ErrLog.GetInstance().Write($"{csvConfig.csvName} を書き込み用に開けませんでした。");
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

        /// <summary>
        /// タイトルを取得する
        /// </summary>
        /// <param name="ranking"></param>
        /// <returns></returns>
        protected virtual string getTitle(Ranking ranking)
        {
            return editEscape(ranking.Title); 
        }

        /// <summary>
        /// ユーザー名を取得する
        /// </summary>
        /// <param name="ranking"></param>
        /// <returns></returns>
        protected virtual string getUserName(Ranking ranking)
        {
            return editEscape(ranking.UserName);
        }

        private string editEscape(string str)
        {
            if (str != null)
            {
                return RegLib.RegExpRep(str, "\"", "”");
            }
            else
            {
                return "";
            }
        }
    }
}
