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

        public bool RegistDB(List<SnapShotJson> dataList, DateTime dateTime)
        {
            string DataSource = $"{DB.LOG_SNAPSHOT}_{DateConvert.Time2String(dateTime, false)}.db";
            try
            {

                using (var dbCtrl = new SQLiteCtrl())
                {
 

                    bool isNew = false;
                    if (File.Exists(DataSource))
                    {
                        File.Delete(DataSource);
                    }
                    SQLiteConnection.CreateFile(DataSource);
                    isNew = true;
                    if (!dbCtrl.Open(DataSource))
                    {
                        return false;
                    }

                    if (isNew)
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
                            aCmd.Parameters.AddWithValue("@集計日", DateConvert.Time2String(dateTime, false));
                            aCmd.Parameters.AddWithValue("@Ver", "1.0.1.0");

                            aCmd.ExecuteNonQuery();
                        }
                    }

                    using (var aCmd = new SQLiteCommand(dbCtrl.Connection))
                    {
                        try
                        {
                            //トランザクションの開始
                            aCmd.Transaction = dbCtrl.Connection.BeginTransaction();


                            //動画情報が無いときだけ追加する
                            var strSQL =
                                @"  INSERT INTO Ranking( ID, '再生数','コメント数','マイリスト数','いいね数')
                                    SELECT @ID,@再生数,@コメント数,@マイリスト数,@いいね数
                                    WHERE NOT EXISTS (SELECT * FROM Ranking WHERE ID=@ID)";

                            aCmd.CommandText = strSQL;

                            StatusLog.WriteLine($"約{dataList.Count } * 100 件のデータを登録しています");

                            int GetCounter = 0;
                            int CountShow = Math.Max(dataList.Count / 10, 10);

                            foreach (var jsonList in dataList)
                            {
                                foreach (var jsonData in jsonList.Data)
                                {
                                    aCmd.Parameters.AddWithValue("@ID", jsonData.ID);
                                    aCmd.Parameters.AddWithValue("@再生数", jsonData.CountPlay);
                                    aCmd.Parameters.AddWithValue("@コメント数", jsonData.CountComment);
                                    aCmd.Parameters.AddWithValue("@マイリスト数", jsonData.CountMylist);
                                    aCmd.Parameters.AddWithValue("@いいね数", jsonData.CountLike);
                                    aCmd.ExecuteNonQuery();
                                }
                                if (GetCounter % CountShow == 0 && GetCounter != 0)
                                {
                                    StatusLog.WriteLine($"{GetCounter / (double)dataList.Count * 100:F0}%");
                                }
                                GetCounter++;
                            }
                            aCmd.Transaction.Commit();
                            StatusLog.WriteLine($"データ登録終了");
                        }
                        catch (Exception ex)
                        {
                            aCmd.Transaction.Rollback();
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
