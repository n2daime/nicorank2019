﻿using Newtonsoft.Json;
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

        /// <summary>
        /// リクエストURL
        /// </summary>
        const string REQUEST_URL =
            @"https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search?q=&_sort=-viewCounter&fields=contentId,commentCounter,viewCounter,mylistCounter,likeCounter&filters[startTime][gte]={0}T00:00:00%2B09:00&filters[startTime][lt]={1}T00:00:00%2B09:00&_limit={2}&_offset={3}&filters[viewCounter][gte]=1000";

        const string REQUEST_URL_LAST_1YEAR =
            @"https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search?q=&_sort=-viewCounter&fields=contentId,commentCounter,viewCounter,mylistCounter,likeCounter&filters[startTime][gte]={0}T00:00:00%2B09:00&filters[startTime][lt]={1}T00:00:00%2B09:00&_limit={2}&_offset={3}";

        string startDay = "";
        string endDay = "";

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

            startDay = dateTime.Date.ToString("yyyy-MM-dd"); ;
            SnapShotJson snapShotInfo = null;
            while (true)
            {

                endDay = dateTime.Date.Add(addDate).ToString("yyyy-MM-dd");

                // 件数取得用のURLを計算する
                string fileURL;
                if (flgLimit1000)
                {
                    fileURL = string.Format(REQUEST_URL, startDay, endDay, 0, 0);
                }
                else
                {
                    fileURL = string.Format(REQUEST_URL_LAST_1YEAR, startDay, endDay, 0, 0);
                }
                

                for (int retry = 0; retry < 20; retry++)
                {

                    if (!InternetUtil.TxtDownLoad(fileURL, out string fileListJsonText))
                    {
                        //失敗
                        return false;
                    }

                    //
                    snapShotInfo = SnapShotJson.FromJson(fileListJsonText);
                    if (snapShotInfo.Meta.Status != 200)
                    {
                        continue;
                    }
                    break;
                }
                if (snapShotInfo?.Meta.Status != 200)
                {
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
            Console.WriteLine($"{dateTime.ToShortDateString()} ～{dateTime.Add(addDate).ToShortDateString()} 投稿動画のデータ {snapShotInfo?.Meta.TotalCount} 件を取得しています...");
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
                SetRequestResult(taskOffset,dateTime);
            });

            return true;
        }

        object lockObj = new object();

		protected bool SetRequestResult(TaskOffset taskOffset,DateTime dateTime)
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

                string fileURL = string.Format(REQUEST_URL, startDay,endDay , 100, offset);

                
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
