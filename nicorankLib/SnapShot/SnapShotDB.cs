using nicorankLib.Analyze.model;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.SnapShot
{
    public class SnapShotDB
    {
        string DataSource;
        DateTime analyzeDay;

        protected ISQLiteCtrl _dbCtrlOverride;

        public SnapShotDB(ISQLiteCtrl dbCtrl = null)
        {
            _dbCtrlOverride = dbCtrl;
        }

        public void GetJsonData(string TargetDir, out List<SnapShotJson> dataList)
        {
            dataList = null;
            try
            {
                IEnumerable<string> files = Directory.EnumerateFiles(TargetDir, "*.json", SearchOption.AllDirectories);

                int fileLen = files.Count();
                var workDataList = new List<SnapShotJson>(fileLen);

                int GetCounter = 0;
                int CountShow = Math.Max(fileLen / 10, 10);

                StatusLog.WriteLine($"{fileLen}個のファイルを読み取ります...");

                Parallel.ForEach(files, new ParallelOptions() { MaxDegreeOfParallelism = 8 }, file =>
              {
                  TextUtil.ReadText(file, out string txt);
                  var data = SnapShotJson.FromJson(txt);
                  lock (workDataList)
                  {
                      workDataList.Add(data);
                      if (GetCounter % CountShow == 0 && GetCounter != 0)
                      {
                          StatusLog.WriteLine($"{GetCounter / (double)fileLen * 100:F0}%");
                      }
                      GetCounter++;
                  }
              }
                );
                dataList = workDataList;
                StatusLog.WriteLine($"{fileLen}個のファイルを読み取り終了...");

            }
            catch (Exception ex)
            {
                ErrLog.GetInstance().Write(ex);
            }

        }

        public bool InitilizeDB()
        {
            analyzeDay = DateTime.Today;
            DataSource = $"{DB.LOG_SNAPSHOT}_{DateConvert.Time2String(analyzeDay, false)}.db";


            if (File.Exists(DataSource))
            {
                File.Delete(DataSource);
            }

            using (ISQLiteCtrl dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl())
            {

                SQLiteConnection.CreateFile(DataSource);
                if (!dbCtrl.Open(DataSource))
                {
                    return false;
                }
                try
                {
                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {


                        aCmd.CommandText = @"CREATE TABLE ""Ranking"" (
                                        ""ID""    TEXT,
	                                    ""再生数""   INTEGER,
	                                    ""コメント数"" INTEGER,
	                                    ""マイリスト数""    INTEGER,
                                        ""いいね数""    INTEGER,
	                                    PRIMARY KEY(""ID"")
                                    )";
                        aCmd.ExecuteNonQuery();

                        aCmd.CommandText = @"CREATE TABLE ""DBVersion"" (
	                                    ""集計日""   INTEGER,
	                                    ""Ver"" TEXT
                                        )";

                        aCmd.ExecuteNonQuery();

                        aCmd.CommandText = @"INSERT INTO DBVersion(集計日,Ver)
                                            VALUES (@集計日,@Ver)";
                        aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(analyzeDay, false));
                        aCmd.Parameters.AddWithValue("@Ver", "1.0.1.0");

                        aCmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    ErrLog.GetInstance().Write(ex);
                    return false;
                }
            }
            return true;
        }

        public bool RegistDB(List<SnapShotJson> dataList)
        {
            try
            {
                using (ISQLiteCtrl dbCtrl = _dbCtrlOverride ?? new SQLiteCtrl())
                {
 
                    if (!dbCtrl.Open(DataSource))
                    {
                        return false;
                    }

                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        try
                        {
                            // トランザクションの開始
                            int commitBatch = 5000; // バッチ毎にコミットして巨大トランザクションを避ける
                            int rowCounter = 0;

                            // 動画情報が無いときだけ追加する (INSERT OR IGNORE を使用)
                            var strSQL =
                                @"INSERT OR IGNORE INTO Ranking (""ID"", ""再生数"", ""コメント数"", ""マイリスト数"", ""いいね数"") 
                                  VALUES (@ID,@再生数,@コメント数,@マイリスト数,@いいね数);";

                            aCmd.CommandText = strSQL;

                            // パラメータを一度作成して再利用する
                            aCmd.Parameters.Add(new SQLiteParameter("@ID", System.Data.DbType.String));
                            aCmd.Parameters.Add(new SQLiteParameter("@再生数", System.Data.DbType.Int64));
                            aCmd.Parameters.Add(new SQLiteParameter("@コメント数", System.Data.DbType.Int64));
                            aCmd.Parameters.Add(new SQLiteParameter("@マイリスト数", System.Data.DbType.Int64));
                            aCmd.Parameters.Add(new SQLiteParameter("@いいね数", System.Data.DbType.Int64));

                            StatusLog.WriteLine($"約{dataList.Count } * 100 件のデータを登録しています");

                            int GetCounter = 0;
                            int CountShow = Math.Max(dataList.Count / 10, 10);

                            using (var transaction = dbCtrl.Connection.BeginTransaction())
                            {
                                aCmd.Transaction = transaction;

                                foreach (var jsonList in dataList)
                                {
                                    foreach (var jsonData in jsonList.Data)
                                    {
                                        aCmd.Parameters["@ID"].Value = jsonData.ID;
                                        aCmd.Parameters["@再生数"].Value = jsonData.CountPlay;
                                        aCmd.Parameters["@コメント数"].Value = jsonData.CountComment;
                                        aCmd.Parameters["@マイリスト数"].Value = jsonData.CountMylist;
                                        aCmd.Parameters["@いいね数"].Value = jsonData.CountLike;
                                        aCmd.ExecuteNonQuery();

                                        rowCounter++;

                                        // 定期コミットして巨大トランザクションを避ける
                                        if ((rowCounter % commitBatch) == 0)
                                        {
                                            aCmd.Transaction.Commit();
                                            aCmd.Transaction = dbCtrl.Connection.BeginTransaction();
                                        }
                                    }
                                    if (GetCounter % CountShow == 0 && GetCounter != 0)
                                    {
                                        StatusLog.WriteLine($"{GetCounter / (double)dataList.Count * 100:F0}%");
                                    }
                                    GetCounter++;
                                }

                                // 最終コミット
                                aCmd.Transaction.Commit();
                            }

                            StatusLog.WriteLine($"データ登録終了");
                        }
                        catch (Exception ex)
                        {
                            try { aCmd.Transaction?.Rollback(); } catch { }
                            var errLog = ErrLog.GetInstance();
                            errLog.Write($"NicoChartTSV登録でエラー。(RankingHistory::getRankingDataLogNicoChart)");
                            errLog.Write(ex);
                            return false;
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
    }
}
