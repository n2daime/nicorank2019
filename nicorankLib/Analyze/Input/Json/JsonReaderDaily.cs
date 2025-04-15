using System;
using nicorankLib.Analyze.Input;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nicorankLib.Analyze.model;
using nicorankLib.Common;

namespace nicorankLib.Analyze.Json
{
    public class JsonReaderDaily : JsonReaderBase
    {

        public JsonReaderDaily(DateTime analyzeDay) : base(analyzeDay)
        {
        }

        public override void setAnalyzeDay(DateTime analyzeDay)
        {
            this.calcAnalyzeDay = analyzeDay;
        }

        /// <summary>
        /// 過去ログJSONの基準URLを作成する
        /// </summary>
        /// <returns></returns>
        protected override string calcJsonTarget()
        {
            string baseURL= Config.GetInstance().URL_JSON_TARGET;

            // https://2daime.myds.me/old-ranking/{取得するランキングの種類}/{取得したい日付}
            var targetURL = string.Format(baseURL, "daily", this.AnalyzeDay.ToString("yyyy-MM-dd"));
            return targetURL;
        }
    }
}
