using nicorankLib.Analyze.model;
using nicorankLib.Analyze.Input;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Common;

namespace nicorankLib.Analyze.Json
{
    /// <summary>
    /// 月刊ランキング計算用 JSONリーダー
    /// </summary>
    public class JsonReaderMonthly: JsonReaderBase
    {

        public JsonReaderMonthly(DateTime analyzeDay) : base(analyzeDay)
        {
        }

        /// <summary>
        /// 過去ログJSONの基準URLを作成する
        /// </summary>
        /// <returns></returns>
        protected override string calcJsonTarget()
        {

            string baseURL = Config.GetInstance().URL_JSON_TARGET;
            // https://example.com/path/to/old-ranking/{取得するランキングの種類}/{取得したい日付}
            // 取得するランキングの種類 = "月刊"
            // 取得したい日付、直近の1日（当日含む） 例: 2019年6月30日の場合、 2019-06-30

            var targetURL = string.Format(baseURL, "monthly", this.AnalyzeDay.ToString("yyyy-MM-dd"));
            return targetURL;
        }

        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            // 1日を見つけるまでループ
            while (true)
            {
                if (analyzeDay.Day == 1)
                {
                    if (!CheckAnalyzeTime(analyzeDay))
                    {
                        //当日の0:30 前＝まだ集計されていない可能性がある
                    }
                    else
                    {
                        //集計日確定
                        break;
                    }
                }
                analyzeDay = analyzeDay.AddDays(-1);
            }
            this.calcAnalyzeDay = analyzeDay;
        }
    }
}
