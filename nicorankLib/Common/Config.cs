using nicorankLib.Util;
using nicorankLib.Util.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nicorankLib.Common
{
    public class Config
    {
        #region 設定項目

        /// <summary>
        /// ランキングは何位から紹介するか
        /// </summary>
        public int Rank { get { return IsSP? xml.SP.RANK.Num : xml.RANK.Num;  } set { } }

        /// <summary>
        /// rankEDに掲載する動画数 例 週間は120 SPは400
        /// </summary>
        public int RankED { get { return IsSP ? xml.SP.RANKED.Num : xml.RANKED.Num; } set { } }

        /// <summary>
        /// UserInfo/Iconを取得する動画数。長期は考慮しないので単純指定
        /// </summary>
        public int UserNum { get { return IsSP ? xml.SP.UserInfo.Num : xml.UserInfo.Num; } set { if (IsSP) { xml.SP.UserInfo.Num = value; } else { xml.UserInfo.Num = value; } } }

        /// <summary>
        /// Tyouki=1 History.csvで長期動画補正あり
        /// </summary>
        public bool IsTyouki { get { return IsSP ? xml.SP.RANK.Tyouki : xml.RANK.Tyouki; } set { } }

        /// <summary>
        /// lastresultSP.csvチェック用。前回SPの”集計日”を指定すること
        /// </summary>
        public string CheckDateOver { get { return xml.SP.CheckDateOver; } set { } }

        /// <summary>
        /// ED用アイコンのDL先指定
        /// </summary>
        public string StrIconDLPath { get { return xml.ICONDL_PATH; } set { } }

        /// <summary>
        /// SP用の集計かどうか
        /// </summary>
        public bool IsSP = false;

        /// <summary>
        /// マイリストの倍率
        /// </summary>
        public double CalcMyList { get { return IsSP ? xml.SP.POINT.CALC_MYLIST : xml.POINT.CALC_MYLIST; } set { if (IsSP) { xml.SP.POINT.CALC_MYLIST = value; } else { xml.POINT.CALC_MYLIST = value; } } }

        /// <summary>
        /// コメントの倍率
        /// </summary>
        public double CalcComment { get { return IsSP ? xml.SP.POINT.CALC_COMMENT : xml.POINT.CALC_COMMENT; } set { if (IsSP) { xml.SP.POINT.CALC_COMMENT = value; } else { xml.POINT.CALC_COMMENT = value; } } }

        /// <summary>
        /// 再生の倍率
        /// </summary>
        public double CalcPlay { get { return IsSP ? xml.SP.POINT.CALC_PLAY : xml.POINT.CALC_PLAY; } set { if (IsSP) { xml.SP.POINT.CALC_PLAY = value; } else { xml.POINT.CALC_PLAY = value; } } }

        /// <summary>
        /// いいねの倍率
        /// </summary>
        public double CalcLike { get { return IsSP ? xml.SP.POINT.CALC_LIKE : xml.POINT.CALC_LIKE; } set { if (IsSP) { xml.SP.POINT.CALC_LIKE = value; } else { xml.POINT.CALC_LIKE = value; } } }

        /// <summary>
        /// コメントポイント補正を行うか？
        /// </summary>
        public int CalcCommentKind { get { return xml.COMMENT_OFFSET.Mode; } set { xml.COMMENT_OFFSET.Mode = value; } }

        /// <summary>
        /// ポイント全体補正を行うか？
        /// </summary>
        public int CalcPointAllKind { get { return xml.POINTALL_OFFSET.Mode; } set { xml.POINTALL_OFFSET.Mode = value; } }

        /// <summary>
        /// コメントポイント補正の下限値
        /// </summary>
        public double CalcCommentUnderLimit{ get { return xml.COMMENT_OFFSET.UnderLimit ; } set { xml.COMMENT_OFFSET.UnderLimit = value; } }

        /// <summary>
        /// マイリストポイント補正を行うか？
        /// </summary>
        public int CalcMyListKind { get { return xml.MYLIST_OFFSET.Mode; } set { xml.MYLIST_OFFSET.Mode = value; } }

        /// <summary>
        /// 再生ポイント補正を行うか？
        /// </summary>
        public int CalcPlayKind { get { return xml.PLAY_OFFSET.Mode; } set { xml.PLAY_OFFSET.Mode = value; } }

        /// <summary>
        /// result.csv系について Unicodeで出力
        /// </summary>
        public bool IsResultCsvUnicode { get { return xml.SYSTEM.ResultCsv.IsUnicode; } set { } }

        /// <summary>
        /// マルチスレッドが有効な処理での最大のスレッド数
        /// </summary>
        public int ThreadMax { get { return xml.SYSTEM.Thread.Max; } set { } }

        /// <summary>
        /// ニコチャート設定。Mode：0（取得しない） 1(取得する）
        /// </summary>
        public bool IsGetNicoChart { get { return xml.SYSTEM.NicoChart.Mode; } set { } }

        /// <summary>
        /// NicoAPIに対しての最大リトライ回数
        /// </summary>
        public int RetryNicoAPI { get { return xml.SYSTEM.Download.NicoAPI.Retry; } set { } }

        /// <summary>
        /// UserIcon取得の最大リトライ回数
        /// </summary>
        public int RetryUserIcon { get { return xml.SYSTEM.Download.UserIcon.Retry; } set { } }

        /// <summary>
        /// ランキング取得用のURL
        /// </summary>
        public string URL_JSON_TARGET { get { return xml.SYSTEM.URL_JSON_TARGET.Url; } set { } }

        #endregion

        /// <summary>
        /// 唯一のインスタンス
        /// </summary>
        protected static Config Instance = null;

        protected NicoRankXml xml = null;


        public string GetXMLString()
        {
            return XmlSerializerUtil.Serialize<NicoRankXml>(xml);
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        protected Config()
        {
        }

        /// <summary>
        /// インスタンスを取得する
        /// </summary>
        /// <returns></returns>
        public static Config GetInstance()
        {
            if (Config.Instance == null)
            {
                Config.Instance = new Config();
                Config.Instance.Initilize();
            }
            return Config.Instance;
        }

        /// <summary>
        /// XMLの読み込み
        /// </summary>
        protected void Initilize()
        {
            bool isOpened = TextUtil.ReadText("nicorank.xml", out string strXml);
            if (!isOpened)
            {
                return;
            }
            this.xml = XmlSerializerUtil.Deserialize<NicoRankXml>(strXml);
            if( this.xml.SYSTEM.NicoChart == null )
            {//設定がない場合のデフォルトは取得する
                this.xml.SYSTEM.NicoChart = new NicoChart() { Mode = true };
            }
            if (this.xml.SYSTEM.Download.NicoAPI == null)
            {//設定がない場合のデフォルトは20
                this.xml.SYSTEM.Download.NicoAPI = new NicoAPIXML() { Retry = 20 };
            }
            if (this.xml.SYSTEM.Download.UserIcon == null)
            {//設定がない場合のデフォルトは20
                this.xml.SYSTEM.Download.UserIcon = new UserIcon() { Retry = 20 };
            }
            if (this.xml.POINTALL_OFFSET == null)
            {//設定がない場合 補正なしにする
                this.xml.POINTALL_OFFSET = new POINTALL_OFFSET() { Mode = 0 };
            }
            if (this.xml.SYSTEM.URL_JSON_TARGET == null)
            {//設定がない場合
                this.xml.SYSTEM.URL_JSON_TARGET = new URL_JSON_TARGET() { Url = @"https://2daime.myds.me/old-ranking/{0}/{1}/" };
            }
        }
    }
}
