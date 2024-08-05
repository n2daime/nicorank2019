using nicorankLib.Analyze.model;
using System;
using nicorankLib.Analyze.Input;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Analyze.Json
{
    public class JsonReaderTotal : JsonReaderBase
    {
        public JsonReaderTotal(DateTime analyzeDay) : base(analyzeDay)
        {
        }

        /// <summary>
        /// 過去ログJSONの基準URLを作成する
        /// </summary>
        /// <returns></returns>
        protected override string calcJsonTarget()
        {
            string baseURL;

            // https://example.com/path/to/old-ranking/{取得するランキングの種類}/{取得したい日付}
            // 取得するランキングの種類 = "デイリー"
            // 取得したい日付、直近の1日（当日含む） 例: 2019年6月30日の場合、 2019-06-30
            if (this.AnalyzeDay < this.URL_CheckDate1)
            {// 2024のサイバー攻撃の前
                baseURL = URL_JSON_TARGET;
            }
            else
            {// 2024のサイバー攻撃のあと
                baseURL = URL_JSON_TARGET_20240806;
            }
            var targetURL = string.Format(baseURL, "total", this.AnalyzeDay.ToString("yyyy-MM-dd"));
            return targetURL;
        }


        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            this.calcAnalyzeDay = analyzeDay;
        }
    }
}
