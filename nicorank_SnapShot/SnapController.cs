using nicorankLib.SnapShot;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorank_SnapShot
{
    class SnapController
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
                    //DateTime dateTime = DateConvert.String2Time("20090324", false);

                    StatusLog.WriteLine($"Snapshot APIのデータを取得しています...");

                    var dataList = new List<SnapShotJson>(300000);
                    while (dateTime < DateTime.Now.Date)
                    {
                        StatusLog.WriteLine($"{dateTime.ToShortDateString()} 投稿動画のデータを取得しています...");
                        if (!testObj.AnalyzeRank(dateTime, ref dataList))
                        {
                            StatusLog.WriteLine($"{dateTime.ToShortDateString()}投稿動画のデータを取得中にエラー発生しました");
                            result = false;
                            break;
                        }
                        dateTime = dateTime.AddDays(1);
                    }

                    var snapShotDB = new SnapShotDB();
                    result = snapShotDB.RegistDB(dataList, DateTime.Today);
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
