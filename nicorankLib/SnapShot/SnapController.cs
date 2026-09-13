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
                    // 初期化失敗は例外にならないため戻り値で判定する。見落とすと空のまま取得が進み成功扱いになる（#37）。
                    if (!snapShotDB.InitilizeDB())
                    {
                        StatusLog.WriteLine("スナップショットDBの初期化に失敗しました");
                        result = false;
                        return;
                    }

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
                            // 登録失敗は例外にならないため戻り値で失敗を記録する。続行自体はやめない（残件も登録して被害を最小化するため）。
                            if (!snapShotDB.RegistDB(dataList))
                            {
                                StatusLog.WriteLine("スナップショットDBへの登録に失敗しました");
                                result = false;
                            }
                            dataList.Clear();
                        }
                        
                    }
                    if (dataList.Count > 0)
                    {
                        // 最終残件の登録失敗も成功扱いにしない（CLI の終了コードが誤るため）。
                        if (!snapShotDB.RegistDB(dataList))
                        {
                            StatusLog.WriteLine("スナップショットDBへの登録に失敗しました");
                            result = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrLog.GetInstance().Write(ex);
                    // 例外時は失敗として返す。従来は result が true のまま成功扱いになり、CLI の終了コードが誤るため修正する（#37）。
                    result = false;
                }
            });
            return result;
        }
    }
}
