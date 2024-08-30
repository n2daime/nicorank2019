using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.SnapShot
{
    public class SnapController 
    {
        public async Task<bool> GetSnapShotAsync()
        {

            bool result = true;
            await Task.Run(() =>
            {
                try
                {
                    var testObj = new SnapShotAnalyze();
                    DateTime dateTime = DateConvert.String2Time("20070306", false);
                    //DateTime dateTime = DateConvert.String2Time("20240801", false);

                    StatusLog.WriteLine($"Snapshot APIのデータを取得しています...");

                    var snapShotDB = new SnapShotDB();
                    snapShotDB.InitilizeDB();

                    var dataList = new List<SnapShotJson>(100000);
                    var addDate = new TimeSpan(15,0,0,0); //15日
                    while (dateTime < DateTime.Now.Date)
                    {
                        bool flgLimit1000 = false;
                        if ((DateTime.Now.Date - dateTime.Date).Days <= 365)
                        {
                            //直近１年前
                            flgLimit1000 = false; //1000制限なし
                        }
                        else
                        {
                            //１年よりさらに前
                            flgLimit1000 = true; //1000制限あり
                        }
                        
                        if (!testObj.AnalyzeRank(dateTime,ref addDate, ref dataList, flgLimit1000))
                        {
                            StatusLog.WriteLine($"{dateTime.ToShortDateString()}投稿動画のデータを取得中にエラー発生しました");
                            result = false;
                            break;
                        }
                        dateTime = dateTime.Add(addDate);
                        if (dataList.Count > 10000 )
                        {
                            snapShotDB.RegistDB(dataList);
                            dataList.Clear();
                        }
                        
                    }
                    if (dataList.Count > 0)
                    {
                        snapShotDB.RegistDB(dataList);
                    }
                }
                catch (Exception ex)
                {
                    ErrLog.GetInstance().Write(ex);
                }
            });
            return result;
        }
    }
}
