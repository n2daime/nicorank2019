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
        public int Rank { get { if (UseTagRank) { return xml.TAGRANK.RANK.Num; } return IsSP ? xml.SP.RANK.Num : xml.RANK.Num; } set { } }

        /// <summary>
        /// rankEDに掲載する動画数 例 週間は120 SPは400
        /// </summary>
        public int RankED { get { if (UseTagRank) { return xml.TAGRANK.RANKED.Num; } return IsSP ? xml.SP.RANKED.Num : xml.RANKED.Num; } set { } }

        /// <summary>
        /// UserInfo/Iconを取得する動画数。長期は考慮しないので単純指定
        /// </summary>
        public int UserNum { get { if (UseTagRank) { return xml.TAGRANK.UserInfo.Num; } return IsSP ? xml.SP.UserInfo.Num : xml.UserInfo.Num; } set { if (UseTagRank) { xml.TAGRANK.UserInfo.Num = value; } else if (IsSP) { xml.SP.UserInfo.Num = value; } else { xml.UserInfo.Num = value; } } }

        /// <summary>
        /// Tyouki=1 History.csvで長期動画補正あり
        /// </summary>
        public bool IsTyouki { get { if (UseTagRank) { return xml.TAGRANK.RANK.Tyouki; } return IsSP ? xml.SP.RANK.Tyouki : xml.RANK.Tyouki; } set { } }

        /// <summary>
        /// lastresultSP.csvチェック用。前回SPの”集計日”を指定すること
        /// </summary>
        public string CheckDateOver { get { if (UseTagRank && xml.TAGRANK.CheckDateOver != null) { return xml.TAGRANK.CheckDateOver; } return xml.SP.CheckDateOver; } set { } }

        /// <summary>
        /// ED用アイコンのDL先指定
        /// </summary>
        public string StrIconDLPath { get { return xml.ICONDL_PATH; } set { } }

        /// <summary>
        /// SP用の集計かどうか
        /// </summary>
        public bool IsSP = false;

        /// <summary>
        /// タグ検索用の集計かどうか（TAGRANK節がなければ週間設定を使う）
        /// </summary>
        public bool IsTagRank = false;

        /// <summary>
        /// TAGRANK節を使うか（節単位切替。節なし・項目欠落があれば週間設定にフォールバックする）
        /// </summary>
        private bool UseTagRank
        {
            get
            {
                return IsTagRank && xml != null && xml.TAGRANK != null
                    && xml.TAGRANK.RANK != null && xml.TAGRANK.RANKED != null
                    && xml.TAGRANK.POINT != null && xml.TAGRANK.UserInfo != null;
            }
        }

        /// <summary>
        /// マイリストの倍率
        /// </summary>
        public double CalcMyList { get { if (UseTagRank) { return xml.TAGRANK.POINT.CALC_MYLIST; } return IsSP ? xml.SP.POINT.CALC_MYLIST : xml.POINT.CALC_MYLIST; } set { if (UseTagRank) { xml.TAGRANK.POINT.CALC_MYLIST = value; } else if (IsSP) { xml.SP.POINT.CALC_MYLIST = value; } else { xml.POINT.CALC_MYLIST = value; } } }

        /// <summary>
        /// コメントの倍率
        /// </summary>
        public double CalcComment { get { if (UseTagRank) { return xml.TAGRANK.POINT.CALC_COMMENT; } return IsSP ? xml.SP.POINT.CALC_COMMENT : xml.POINT.CALC_COMMENT; } set { if (UseTagRank) { xml.TAGRANK.POINT.CALC_COMMENT = value; } else if (IsSP) { xml.SP.POINT.CALC_COMMENT = value; } else { xml.POINT.CALC_COMMENT = value; } } }

        /// <summary>
        /// 再生の倍率
        /// </summary>
        public double CalcPlay { get { if (UseTagRank) { return xml.TAGRANK.POINT.CALC_PLAY; } return IsSP ? xml.SP.POINT.CALC_PLAY : xml.POINT.CALC_PLAY; } set { if (UseTagRank) { xml.TAGRANK.POINT.CALC_PLAY = value; } else if (IsSP) { xml.SP.POINT.CALC_PLAY = value; } else { xml.POINT.CALC_PLAY = value; } } }

        /// <summary>
        /// いいねの倍率
        /// </summary>
        public double CalcLike { get { if (UseTagRank) { return xml.TAGRANK.POINT.CALC_LIKE; } return IsSP ? xml.SP.POINT.CALC_LIKE : xml.POINT.CALC_LIKE; } set { if (UseTagRank) { xml.TAGRANK.POINT.CALC_LIKE = value; } else if (IsSP) { xml.SP.POINT.CALC_LIKE = value; } else { xml.POINT.CALC_LIKE = value; } } }

        /// <summary>
        /// コメントポイント補正を行うか？
        /// Issue #39でSP／TAGRANK節別化した。読み取りは節内に対応要素があれば節内値を、なければ共通を使う（項目単位フォールバック）。
        /// なぜ節単位（UseTagRankのような全部必須）にしないか：1項目だけ変えたいときに4項目全部書かせるのは手間であり、
        /// 既存nicorank.xml（節内OFFSETなし）との互換を保ちつつ段階的に移行するためである。
        /// 書き込みは読み取りと異なり、モード別の節へ書く（なければ生成する）。共通へのフォールバック書き込みはしない。
        /// なぜ生成するか：節内要素なしのまま共通へ書くと、タグ用のつもりが週間共通値を書き換え、
        /// Issue #39が解消しようとしたモード間の波及が再発するためである。
        /// </summary>
        public int CalcCommentKind { get { return EffectiveCommentOffset.Mode; } set { CommentOffsetForWrite.Mode = value; } }

        /// <summary>
        /// ポイント全体補正を行うか？（Issue #39でSP／TAGRANK節別化。読みはフォールバック・書きは節内生成）
        /// </summary>
        public int CalcPointAllKind { get { return EffectivePointAllOffset.Mode; } set { PointAllOffsetForWrite.Mode = value; } }

        /// <summary>
        /// コメントポイント補正の下限値（COMMENT_OFFSET節に付随するためCOMMENT_OFFSETと同一のフォールバック・書込先に従う）
        /// </summary>
        public double CalcCommentUnderLimit{ get { return EffectiveCommentOffset.UnderLimit ; } set { CommentOffsetForWrite.UnderLimit = value; } }

        /// <summary>
        /// マイリストポイント補正を行うか？（Issue #39でSP／TAGRANK節別化。読みはフォールバック・書きは節内生成）
        /// </summary>
        public int CalcMyListKind { get { return EffectiveMylistOffset.Mode; } set { MylistOffsetForWrite.Mode = value; } }

        /// <summary>
        /// 再生ポイント補正を行うか？（Issue #39でSP／TAGRANK節別化。読みはフォールバック・書きは節内生成）
        /// </summary>
        public int CalcPlayKind { get { return EffectivePlayOffset.Mode; } set { PlayOffsetForWrite.Mode = value; } }

        /// <summary>
        /// 有効なCOMMENT_OFFSETを返す（タグ検索節→SP節→共通の優先順）。節内要素なしは共通にフォールバックする。
        /// タグ検索とSPは同時に立たない運用（SelectTagMode／SelectSyukeiModeで排他）のため、タグ検索を先に見る。
        /// </summary>
        private COMMENT_OFFSET EffectiveCommentOffset
        {
            get
            {
                if (IsTagRank && xml != null && xml.TAGRANK != null && xml.TAGRANK.COMMENT_OFFSET != null) { return xml.TAGRANK.COMMENT_OFFSET; }
                if (IsSP && xml != null && xml.SP != null && xml.SP.COMMENT_OFFSET != null) { return xml.SP.COMMENT_OFFSET; }
                return xml.COMMENT_OFFSET;
            }
        }

        /// <summary>有効なMYLIST_OFFSETを返す（優先順はCOMMENT_OFFSETと同一）</summary>
        private MYLIST_OFFSET EffectiveMylistOffset
        {
            get
            {
                if (IsTagRank && xml != null && xml.TAGRANK != null && xml.TAGRANK.MYLIST_OFFSET != null) { return xml.TAGRANK.MYLIST_OFFSET; }
                if (IsSP && xml != null && xml.SP != null && xml.SP.MYLIST_OFFSET != null) { return xml.SP.MYLIST_OFFSET; }
                return xml.MYLIST_OFFSET;
            }
        }

        /// <summary>有効なPLAY_OFFSETを返す（優先順はCOMMENT_OFFSETと同一）</summary>
        private PLAY_OFFSET EffectivePlayOffset
        {
            get
            {
                if (IsTagRank && xml != null && xml.TAGRANK != null && xml.TAGRANK.PLAY_OFFSET != null) { return xml.TAGRANK.PLAY_OFFSET; }
                if (IsSP && xml != null && xml.SP != null && xml.SP.PLAY_OFFSET != null) { return xml.SP.PLAY_OFFSET; }
                return xml.PLAY_OFFSET;
            }
        }

        /// <summary>有効なPOINTALL_OFFSETを返す（優先順はCOMMENT_OFFSETと同一）</summary>
        private POINTALL_OFFSET EffectivePointAllOffset
        {
            get
            {
                if (IsTagRank && xml != null && xml.TAGRANK != null && xml.TAGRANK.POINTALL_OFFSET != null) { return xml.TAGRANK.POINTALL_OFFSET; }
                if (IsSP && xml != null && xml.SP != null && xml.SP.POINTALL_OFFSET != null) { return xml.SP.POINTALL_OFFSET; }
                return xml.POINTALL_OFFSET;
            }
        }

        /// <summary>
        /// 書き込み先のCOMMENT_OFFSETを返す。モード別の節へ書くため、節・要素がなければ生成する。
        /// 生成時の初期値は共通の現在値を引き継ぐ。共通自体がない場合（最小XML）は現行既定値を使う。
        /// なぜ引き継ぐか：生成直後の読み値が従来の有効値と変わらず、パネル保存の前後で表示値が跳ねないようにするためである。
        /// </summary>
        private COMMENT_OFFSET CommentOffsetForWrite
        {
            get
            {
                if (IsTagRank)
                {
                    if (xml.TAGRANK == null) { xml.TAGRANK = new TAGRANK(); }
                    if (xml.TAGRANK.COMMENT_OFFSET == null)
                    {
                        var fallback = xml.COMMENT_OFFSET;
                        xml.TAGRANK.COMMENT_OFFSET = new COMMENT_OFFSET()
                        {
                            Mode = fallback != null ? fallback.Mode : DefaultCommentOffsetMode,
                            UnderLimit = fallback != null ? fallback.UnderLimit : DefaultCommentUnderLimit
                        };
                    }
                    return xml.TAGRANK.COMMENT_OFFSET;
                }
                if (IsSP)
                {
                    if (xml.SP == null) { xml.SP = new SP(); }
                    if (xml.SP.COMMENT_OFFSET == null)
                    {
                        var fallback = xml.COMMENT_OFFSET;
                        xml.SP.COMMENT_OFFSET = new COMMENT_OFFSET()
                        {
                            Mode = fallback != null ? fallback.Mode : DefaultCommentOffsetMode,
                            UnderLimit = fallback != null ? fallback.UnderLimit : DefaultCommentUnderLimit
                        };
                    }
                    return xml.SP.COMMENT_OFFSET;
                }
                if (xml.COMMENT_OFFSET == null) { xml.COMMENT_OFFSET = new COMMENT_OFFSET() { Mode = DefaultCommentOffsetMode, UnderLimit = DefaultCommentUnderLimit }; }
                return xml.COMMENT_OFFSET;
            }
        }

        /// <summary>書き込み先のMYLIST_OFFSETを返す（生成方針はCOMMENT_OFFSETと同一）</summary>
        private MYLIST_OFFSET MylistOffsetForWrite
        {
            get
            {
                if (IsTagRank)
                {
                    if (xml.TAGRANK == null) { xml.TAGRANK = new TAGRANK(); }
                    if (xml.TAGRANK.MYLIST_OFFSET == null)
                    {
                        var fallback = xml.MYLIST_OFFSET;
                        xml.TAGRANK.MYLIST_OFFSET = new MYLIST_OFFSET() { Mode = fallback != null ? fallback.Mode : DefaultMylistOffsetMode };
                    }
                    return xml.TAGRANK.MYLIST_OFFSET;
                }
                if (IsSP)
                {
                    if (xml.SP == null) { xml.SP = new SP(); }
                    if (xml.SP.MYLIST_OFFSET == null)
                    {
                        var fallback = xml.MYLIST_OFFSET;
                        xml.SP.MYLIST_OFFSET = new MYLIST_OFFSET() { Mode = fallback != null ? fallback.Mode : DefaultMylistOffsetMode };
                    }
                    return xml.SP.MYLIST_OFFSET;
                }
                if (xml.MYLIST_OFFSET == null) { xml.MYLIST_OFFSET = new MYLIST_OFFSET() { Mode = DefaultMylistOffsetMode }; }
                return xml.MYLIST_OFFSET;
            }
        }

        /// <summary>書き込み先のPLAY_OFFSETを返す（生成方針はCOMMENT_OFFSETと同一）</summary>
        private PLAY_OFFSET PlayOffsetForWrite
        {
            get
            {
                if (IsTagRank)
                {
                    if (xml.TAGRANK == null) { xml.TAGRANK = new TAGRANK(); }
                    if (xml.TAGRANK.PLAY_OFFSET == null)
                    {
                        var fallback = xml.PLAY_OFFSET;
                        xml.TAGRANK.PLAY_OFFSET = new PLAY_OFFSET() { Mode = fallback != null ? fallback.Mode : DefaultPlayOffsetMode };
                    }
                    return xml.TAGRANK.PLAY_OFFSET;
                }
                if (IsSP)
                {
                    if (xml.SP == null) { xml.SP = new SP(); }
                    if (xml.SP.PLAY_OFFSET == null)
                    {
                        var fallback = xml.PLAY_OFFSET;
                        xml.SP.PLAY_OFFSET = new PLAY_OFFSET() { Mode = fallback != null ? fallback.Mode : DefaultPlayOffsetMode };
                    }
                    return xml.SP.PLAY_OFFSET;
                }
                if (xml.PLAY_OFFSET == null) { xml.PLAY_OFFSET = new PLAY_OFFSET() { Mode = DefaultPlayOffsetMode }; }
                return xml.PLAY_OFFSET;
            }
        }

        /// <summary>書き込み先のPOINTALL_OFFSETを返す（生成方針はCOMMENT_OFFSETと同一）</summary>
        private POINTALL_OFFSET PointAllOffsetForWrite
        {
            get
            {
                if (IsTagRank)
                {
                    if (xml.TAGRANK == null) { xml.TAGRANK = new TAGRANK(); }
                    if (xml.TAGRANK.POINTALL_OFFSET == null)
                    {
                        var fallback = xml.POINTALL_OFFSET;
                        xml.TAGRANK.POINTALL_OFFSET = new POINTALL_OFFSET() { Mode = fallback != null ? fallback.Mode : DefaultPointAllOffsetMode };
                    }
                    return xml.TAGRANK.POINTALL_OFFSET;
                }
                if (IsSP)
                {
                    if (xml.SP == null) { xml.SP = new SP(); }
                    if (xml.SP.POINTALL_OFFSET == null)
                    {
                        var fallback = xml.POINTALL_OFFSET;
                        xml.SP.POINTALL_OFFSET = new POINTALL_OFFSET() { Mode = fallback != null ? fallback.Mode : DefaultPointAllOffsetMode };
                    }
                    return xml.SP.POINTALL_OFFSET;
                }
                if (xml.POINTALL_OFFSET == null) { xml.POINTALL_OFFSET = new POINTALL_OFFSET() { Mode = DefaultPointAllOffsetMode }; }
                return xml.POINTALL_OFFSET;
            }
        }

        /// <summary>
        /// result.csv系について Unicodeで出力
        /// </summary>
        public bool IsResultCsvUnicode { get { return xml.SYSTEM.ResultCsv.IsUnicode; } set { } }

        /// <summary>
        /// マルチスレッドが有効な処理での最大のスレッド数
        /// </summary>
        public int ThreadMax { get { return xml.SYSTEM.Thread.Max; } set { } }

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

        // OFFSET系の既定値（現行共通値）。Initilizeの欠落補完と*ForWriteの生成初期値で共用する。
        // なぜ定数化するか：4箇所×4種に散らすと将来の既定値変更で修正漏れが起きるためである（AGENTS.md §1のマジックナンバー抑止）。
        private const int DefaultCommentOffsetMode = 2;
        private const double DefaultCommentUnderLimit = 0.01;
        private const int DefaultMylistOffsetMode = 1;
        private const int DefaultPlayOffsetMode = 2;
        private const int DefaultPointAllOffsetMode = 0;

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
        protected virtual void Initilize()
        {
            bool isOpened = TextUtil.ReadText("nicorank.xml", out string strXml);
            if (!isOpened)
            {
                return;
            }
            this.xml = XmlSerializerUtil.Deserialize<NicoRankXml>(strXml);
            if (this.xml.SYSTEM.Download.NicoAPI == null)
            {//設定がない場合のデフォルトは20
                this.xml.SYSTEM.Download.NicoAPI = new NicoAPIXML() { Retry = 20 };
            }
            if (this.xml.SYSTEM.Download.UserIcon == null)
            {//設定がない場合のデフォルトは20
                this.xml.SYSTEM.Download.UserIcon = new UserIcon() { Retry = 20 };
            }
            if (this.xml.COMMENT_OFFSET == null)
            {//設定がない場合の既定は現行共通値。欠落XMLでのNullReferenceを防ぐ
                this.xml.COMMENT_OFFSET = new COMMENT_OFFSET() { Mode = DefaultCommentOffsetMode, UnderLimit = DefaultCommentUnderLimit };
            }
            if (this.xml.MYLIST_OFFSET == null)
            {//設定がない場合の既定は現行共通値
                this.xml.MYLIST_OFFSET = new MYLIST_OFFSET() { Mode = DefaultMylistOffsetMode };
            }
            if (this.xml.PLAY_OFFSET == null)
            {//設定がない場合の既定は現行共通値
                this.xml.PLAY_OFFSET = new PLAY_OFFSET() { Mode = DefaultPlayOffsetMode };
            }
            if (this.xml.POINTALL_OFFSET == null)
            {//設定がない場合 補正なしにする
                this.xml.POINTALL_OFFSET = new POINTALL_OFFSET() { Mode = DefaultPointAllOffsetMode };
            }
            if (this.xml.SYSTEM.URL_JSON_TARGET == null)
            {//設定がない場合
                this.xml.SYSTEM.URL_JSON_TARGET = new URL_JSON_TARGET() { Url = @"https://2daime.myds.me/old-ranking/{0}/{1}/" };
            }
        }
    }
}
