using Newtonsoft.Json;
using nicorankLib.Common;
using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static nicorankLib.SnapShot.SnapShotJson;

namespace nicorankLib.SnapShot
{
    public class SnapShotAnalyze
    {
        protected class TaskOffset
        {
            public long offset_start = 0;
            public long offset_end = 0;
        }

        DateTime startDate;
        DateTime endDate;

        /// <summary>
        /// 期間指定のリクエストURLを生成する（Issue #19。string.Format 直書きの代替）
        /// </summary>
        private string CreateRequestUrl(int limit, long offset, bool flgLimit1000)
        {
            return SnapShotRequest.CreateRange(startDate, endDate, limit, offset, flgLimit1000).ToUrl();
        }

        List<SnapShotJson> dataList;

        /// <summary>
        /// ランキング情報を取得する
        /// </summary>
        /// <param name="rankings"></param>
        /// <returns></returns>
        public bool AnalyzeRank(DateTime dateTime,ref TimeSpan addDate ,ref List<SnapShotJson> dataList,bool flgLimit1000)
        {
            this.dataList = dataList;

            TimeSpan DATERANGE_MIN = new TimeSpan(1,0,0,0);

            startDate = dateTime.Date;
            SnapShotJson snapShotInfo = null;
            while (true)
            {

                endDate = dateTime.Date.Add(addDate);

                // 件数取得用のURLを計算する
                string fileURL = CreateRequestUrl(0, 0, flgLimit1000);
                

                for (int retry = 0; retry < 20; retry++)
                {

                    if (!InternetUtil.TxtDownLoad(fileURL, out string fileListJsonText))
                    {
                        //失敗
                        // 件数取得のダウンロード失敗はここで即 false になる（InternetUtil 側で20回再試行済みのため、この層では繰り返さない）。
                        // 何も残さず返すと SnapController 側も StatusLog だけで終わり、nicorankerr.log が出ず原因不明になる（Issue #48）。
                        // そのため期間・制限フラグ・失敗種別を ErrLog に残してから返す。取得の挙動自体は変えない。
                        ErrLog.GetInstance().Write($"スナップショット件数取得のダウンロードに失敗しました（期間={startDate:yyyy/MM/dd}～{endDate:yyyy/MM/dd} 1000再生制限あり={flgLimit1000}）。");
                        return false;
                    }

                    //
                    snapShotInfo = SnapShotJson.FromJson(fileListJsonText);
                    if (snapShotInfo?.Meta == null)
                    {
                        // 応答に meta がない（null・meta 欠落）場合は同じ URL の再試行で直る性質ではないため、20回の繰り返しに入れず即失敗とする。
                        // 元の実装ではここで例外終了していた経路であり、即時終了の意味は保ちつつ原因を残す（Issue #48）。
                        ErrLog.GetInstance().Write($"スナップショット件数取得の応答に meta がありませんでした（期間={startDate:yyyy/MM/dd}～{endDate:yyyy/MM/dd} 1000再生制限あり={flgLimit1000}）。");
                        return false;
                    }
                    if (snapShotInfo.Meta.Status != 200)
                    {
                        continue;
                    }
                    break;
                }
                if (snapShotInfo?.Meta.Status != 200)
                {
                    // 20回繰り返しても Status=200 が返らない場合の失敗。ダウンロード自体は通っているため、最後に見た Status を残す。
                    // Status 未取得の場合（初回から Status が返らない等）は不明として残す。理由は次回の切り分けで「通信失敗か API 異常か」を分けるため。
                    string lastStatus = snapShotInfo?.Meta?.Status.ToString() ?? "不明";
                    string lastTotal = snapShotInfo?.Meta?.TotalCount.ToString() ?? "不明";
                    ErrLog.GetInstance().Write($"スナップショット件数取得で Status=200 が返りませんでした（期間={startDate:yyyy/MM/dd}～{endDate:yyyy/MM/dd} 1000再生制限あり={flgLimit1000} 最終Status={lastStatus} TotalCount={lastTotal}）。");
                    return false;
                }
                if (snapShotInfo?.Meta.TotalCount >= 50000 && DATERANGE_MIN < addDate)
                {
                    // 10万件を超えたらアウト
                    // 自主規制で5万制限
                    addDate = addDate.Add(new TimeSpan(-1, 0, 0, 0));
                    continue;
                }
                else
                {
                    break;
                }
            }
            // 進捗はStatusLog経由で出す。直接Consoleに書くと、WinForm・コンソール・Linux CLIの3経路で受け手がばらつき、画面欠落や書式不統一になるため（Issue #43）。
            // SnapControllerの前後行は既にStatusLogであり、ここだけConsoleでは不整合になる。
            StatusLog.WriteLine($"{dateTime.ToShortDateString()} ～{dateTime.Add(addDate).ToShortDateString()} 投稿動画のデータ {snapShotInfo?.Meta.TotalCount} 件を取得しています...");
            // マルチスレッドで取得する
            int threadMax = 4;// config.ThreadMax;
            var snapShotTaskList = new List<TaskOffset>(threadMax);

            // 1スレッド毎の件数を計算する
            long snapShotMaxOffSet = (long)(Math.Ceiling(snapShotInfo.Meta.TotalCount / (double)threadMax));

            for (int offset = 0; offset < snapShotInfo.Meta.TotalCount; offset+= 100)
            {
                var taskInfo = new TaskOffset()
                {
                    offset_start = offset,
                    offset_end   = Math.Min(offset+100, snapShotInfo.Meta.TotalCount)
                };

                snapShotTaskList.Add(taskInfo);

            }


            Parallel.ForEach(snapShotTaskList,new ParallelOptions() {  MaxDegreeOfParallelism = threadMax }, (taskOffset) =>
            {
                SetRequestResult(taskOffset, flgLimit1000);
            });

            return true;
        }

        object lockObj = new object();

		protected bool SetRequestResult(TaskOffset taskOffset, bool flgLimit1000)
        {


            for (long offset = taskOffset.offset_start; offset <= taskOffset.offset_end; offset+= 100)
            {
            //    string FileName = $@"Snap\{dateTime.ToString("yyyyMM")}\{dateTime.ToString("dd")}\{offset:00000000}.json";
            //    if (File.Exists(FileName))
            //    {
            //        continue;
            //    }


                SnapShotJson snapShotInfo = null;
                string fileListJsonText = "";

                // 件数取得時と同じ flgLimit1000 でURLを生成する（旧実装は常に1000制限URLだった不整合を解消。Issue #19）
                string fileURL = CreateRequestUrl(100, offset, flgLimit1000);

                
                while(true)//for (int retry = 0; retry < 20; retry++)
                {
                    try
                    {
                        if (!InternetUtil.TxtDownLoad(fileURL, out fileListJsonText))
                        {
                            //失敗
                            continue;
                        }
                        fileListJsonText = fileListJsonText.Replace(":null", ":0");
                        snapShotInfo = SnapShotJson.FromJson(fileListJsonText);
                        if (snapShotInfo.Meta.Status != 200)
                        {
                            continue;
                        }
                    }
					catch(Exception ex)
                    {
                        ErrLog.GetInstance().Write(ex);
                        continue;
                    }                    
                    break;
                }
                if (snapShotInfo?.Meta.Status != 200)
                {
                    continue;
                }

                lock (lockObj)
                {                    
                    var workJson = SnapShotJson.FromJson(fileListJsonText);
                    this.dataList.Add(workJson);
                    //var textUtil = new TextUtil();
                    //textUtil.WriteOpen(FileName, true);
                    //textUtil.WriteText(fileListJsonText);
                    //textUtil.WriteClose();
                }
            }
            return true;
        }

    }
}
