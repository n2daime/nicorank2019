using nicorankLib.Common;
using nicorankLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace nicorankLib.Analyze.model
{
    public class Ranking
    {
        public Ranking()
        {
            clear();
        }

        const int HOSEI_NASHI = 0;
        const int HOSEI_ARI = 1;
        const int HOSEI_ARI_TOUKA = 2;
        const int HOSEI_ARI_SQRT = 3;
        const int HOSEI_ARI_SQRT2 = 4;

        const int HOSEI_MYLIST_NASHI = 0;
        const int HOSEI_MYLIST_YOWAI = 1;
        const int HOSEI_MYLIST_TUYOI = 2;

        const int HOSEI_PLAY_NASHI = 0;
        const int HOSEI_PLAY_ARI = 1;
        const int HOSEI_PLAY_ARI_LIKE = 2;//いいね対応

        /// <summary>
        /// ポイント全体補正用
        /// </summary>
        enum HOSEI_POINT_ALL
        {
            NASHI = 0,
            VCOLE2023 = 1 //ボカコレ補正用
        }

        /// <summary>
        /// 投稿日
        /// </summary>        
        [JsonProperty("Date")]
        public DateTime Date;

        /// <summary>
        /// 動画ID
        /// </summary>
        [JsonProperty("ID")]
        public string ID = string.Empty;

        /// <summary>
        /// 動画名
        /// </summary>
        [JsonProperty("Title")]
        public string Title = string.Empty;

        /// <summary>
        /// 再生時間
        /// </summary>
        [JsonProperty("PlayTime")]
        public string PlayTime = string.Empty;

        /// <summary>
        /// 前回の順位
        /// </summary>
        [JsonProperty("LastRank")]
        public long LastRank = 0;

        /// <summary>
        /// 前回のポイント
        /// </summary>
        [JsonProperty("LastPoint")]
        public long LastPoint = 0;

        //チャンネル動画かどうか判断する
        public bool IsChannel { get { return ID.StartsWith("so"); } set { } }

        /// <summary>
        /// イメージパス
        /// </summary>
        [JsonProperty("ThumbnailURL")]
        public string ThumbnailURL = string.Empty;

        /// <summary>
        /// マイリスト数
        /// </summary>
        [JsonProperty("CountMyList")]
        public long CountMyList = 0;

        /// <summary>
        /// コメント数
        /// </summary>
        [JsonProperty("CountComment")]
        public long CountComment = 0;

        /// <summary>
        /// 再生数
        /// </summary>
        [JsonProperty("CountPlay")]
        public long CountPlay = 0;

        /// <summary>
        /// いいね数
        /// </summary>
        [JsonProperty("CountLike")]
        public long CountLike = 0;

        /// <summary>
        /// マイリスト数（差分なし）
        /// </summary>
        [JsonProperty("CountMyListTotal")]
        public long CountMyListTotal = 0;

        /// <summary>
        /// コメント数（差分なし）
        /// </summary>
        [JsonProperty("CountCommentTotal")]
        public long CountCommentTotal = 0;

        /// <summary>
        /// 再生数（差分なし）
        /// </summary>
        [JsonProperty("CountPlayTotal")]
        public long CountPlayTotal = 0;

        /// <summary>
        /// いいね数（差分なし）
        /// </summary>
        [JsonProperty("CountLikeTotal")]
        public long CountLikeTotal = 0;

        /// <summary>
        /// マイリストランク
        /// </summary>
        [JsonProperty("RankMyList")]
        public long RankMyList = 0;

        /// <summary>
        /// コメントランク
        /// </summary>
        [JsonProperty("RankComment")]
        public long RankComment = 0;

        /// <summary>
        /// 再生数ランク
        /// </summary>
        [JsonProperty("RankPlay")]
        public long RankPlay = 0;

        /// <summary>
        /// いいね数ランク
        /// </summary>
        [JsonProperty("RankLike")]
        public long RankLike = 0;

        /// <summary>
        /// カテゴリ/ジャンル
        /// </summary>
        [JsonProperty("Category")]
        public string Category = string.Empty;

        /// <summary>
        /// 人気のタグ
        /// </summary>
        [JsonProperty("FavoriteTags")]
        public HashSet<string> FavoriteTags;

        /// <summary>
        /// カテゴリランク
        /// </summary>
        [JsonProperty("RankCategory")]
        public long RankCategory = 0;

        /// <summary>
        /// コメント補正
        /// </summary>
        [JsonProperty("HoseiComment")]
        public double HoseiComment = 1;

        /// <summary>
        /// マイリスト補正
        /// </summary>
        [JsonProperty("HoseiMylist")]
        public double HoseiMylist = 1;

        /// <summary>
        /// 再生補正
        /// </summary>
        [JsonProperty("HoseiPlay")]
        public double HoseiPlay = 1;

        /// <summary>
        /// ポイント全体への補正
        /// </summary>
        [JsonProperty("HoseiAllPoint")]
        public double HoseiAllPoint = 1;

        protected long? workPointTotal = null;

        /// <summary>
        /// ポイント数
        /// </summary>
        [JsonProperty("PointTotal")]
        public long PointTotal { get { return CalcPoint(); } set { workPointTotal = value; } }

        /// <summary>
        /// マイリストポイント
        /// </summary>
        [JsonProperty("PointMyList")]
        public long PointMyList;

        /// <summary>
        /// 再生ポイント
        /// </summary>
        [JsonProperty("PointPlay")]
        public long PointPlay;

        /// <summary>
        /// コメントポイント
        /// </summary>
        [JsonProperty("PointComment")]
        public long PointComment;

        /// <summary>
        /// いいねポイント
        /// </summary>
        [JsonProperty("PointLike")]
        public long PointLike;

        /// <summary>
        /// 総合順位
        /// </summary>
        [JsonProperty("RankTotal")]
        public long RankTotal;

        /// <summary>
        /// ユーザー／チャンネルID
        /// </summary>
        [JsonProperty("UserID")]
        public string UserID = string.Empty;

        /// <summary>
        /// ユーザー／チャンネル名
        /// </summary>
        [JsonProperty("UserName")]
        public string UserName = string.Empty;

        /// <summary>
        /// ユーザー／チャンネルアイコン
        /// </summary>
        [JsonProperty("UserImageURL")]
        public string UserImageURL = string.Empty;

        [JsonProperty("isDelete")]
        public bool isDelete = false;

        public void clear()
        {
            ID = "";
            PlayTime = "";
            Date = DateTime.Now;
            ID = "";

            ThumbnailURL = "";

            LastRank = 0;
            LastPoint = 0;

            CountMyList = 0;
            CountComment = 0;
            CountPlay = 0;
            CountLike = 0;

            CountMyListTotal = 0;
            CountCommentTotal = 0;
            CountPlayTotal = 0;
            CountLikeTotal = 0;   

            workPointTotal = null;

            RankMyList = 0;
            RankComment = 0;
            RankPlay = 0;

            HoseiComment = 1;

            Category = ""; //カテゴリ
            RankCategory = 0; //カテゴリランク

            RankTotal = 0;

            HoseiPlay = 1;

            UserID = "";
            UserName = "";
            UserImageURL = "";

            HoseiMylist = 1;
            PointMyList = 0;
            PointComment = 0;
            PointPlay = 0;
            PointLike = 0;
            isDelete = false;

            HoseiAllPoint = 1;

            FavoriteTags = new HashSet<string>();
        }

        public void PointCalcReset()
        {
            this.workPointTotal = null;
        }

        public void SetPlayTime(string Time)
        {
            PlayTime = $"{RegLib.RegExpRep(Time, "[:：]", "分")}秒";
        }

        /// <summary>
        /// ポイントを計算する
        /// </summary>
        /// <returns></returns>
        protected long CalcPoint()
        {

            if (isDelete)
            {
                return 0;
            }
            if (workPointTotal == null)
            {
                var config = Config.GetInstance();

                //マイリスト
                double myListPoint = (double)CountMyList * config.CalcMyList;

                long iJyogenMylist;
                long iJyogenMylistPoint;
                double divide;

                switch (config.CalcMyListKind)
                {
                    case HOSEI_MYLIST_YOWAI:
                        //上限マイリスト数＝再生数÷マイリスト倍数
                        iJyogenMylist = (int)((double)CountPlay / (double)config.CalcMyList);

                        if (iJyogenMylist >= CountMyList)
                        {
                            iJyogenMylist = CountMyList;
                        }

                        //補正値＝(再生数+コメント数+上限マイリスト数)÷(再生数+コメント数+マイリスト数)
                        divide = CountPlay + CountMyList + CountComment;
                        HoseiMylist = divide == 0 ?
                            1 :
                            (double)(CountPlay + CountComment + iJyogenMylist) / divide;
                        myListPoint = myListPoint * HoseiMylist;
                        break;
                    case HOSEI_MYLIST_TUYOI:
                        //上限Mylistポイント＝再生数
                        iJyogenMylistPoint = CountPlay;
                        if (iJyogenMylistPoint >= myListPoint)
                        {
                            iJyogenMylistPoint = (int)myListPoint;
                        }

                        //補正値＝(再生数+コメント数+上限マイリストポイント)÷(再生数+コメント数+マイリスト数×マイリスト倍数)
                        divide = CountPlay + CountComment + (CountMyList * config.CalcMyList);
                        HoseiMylist = divide == 0 ?
                            1 :
                            (double)(CountPlay + CountComment + iJyogenMylistPoint) / divide;
                        myListPoint = myListPoint * HoseiMylist;
                        break;
                    default:
                        //補正無し
                        break;
                }

                PointMyList = (long)myListPoint;

                workPointTotal = PointMyList;
                //再生
                //ポイント
                double myListDiv = 1;//マイリスト率
                switch (config.CalcPlayKind)
                {
                    case HOSEI_PLAY_ARI:

                        if (CountPlay == 0)
                        {
                            HoseiPlay = 1;
                        }
                        else
                        {
                            //マイリスト率の計算
                            myListDiv = CountMyList / (double)CountPlay;
                            //マイリスト率が0.001(0.1%)以上
                            if (myListDiv >= 0.001)
                            {
                                HoseiPlay = 1;
                            }
                            else
                            {
                                HoseiPlay = myListDiv * 1000;
                                if (HoseiPlay < 0.01)
                                {
                                    HoseiPlay = 0.01;
                                }
                            }
                        }
                        break;
                    case HOSEI_PLAY_ARI_LIKE:
                        if (CountPlay == 0)
                        {
                            //0割りはさせない
                            HoseiPlay = 1;
                        }
                        else
                        {
                            //補正率の計算
                            HoseiPlay = ((double)(CountComment + CountMyList + CountLike)) / (double)CountPlay * 1000;

                            //100%以上には補正しない
                            if (HoseiPlay >= 1.00)
                            {
                                HoseiPlay = 1;
                            }
                            else if (HoseiPlay <= 0.01)
                            {//下限は 0.01倍
                                HoseiPlay = 0.01;
                            }
                        }
                        break;
                    default:
                        HoseiPlay = 1;
                        break;
                }
                PointPlay = (long)(CountPlay * config.CalcPlay * HoseiPlay);
                workPointTotal += PointPlay;

                //ポイント
                switch (config.CalcCommentKind)
                {
                    case HOSEI_ARI:
                        {
                            long divideComment = CountPlay + CountMyList + CountComment;
                            if (divideComment == 0)
                            {
                                HoseiComment = 1;
                                PointComment = CountComment;
                            }
                            else
                            {
                                //double hosei = 0.01;
                                if (CountPlay == 0 && CountMyList == 0)
                                {
                                    HoseiComment = 0.01;
                                }
                                else
                                {
                                    HoseiComment = (CountComment * config.CalcComment + CountPlay + CountMyList) / (double)divideComment;
                                }
                                PointComment = (long)(CountComment * HoseiComment * config.CalcComment);
                            }
                            workPointTotal += PointComment;
                        }
                        break;
                    case HOSEI_ARI_TOUKA:
                        {
                            //pts	＝（再生+コメ×補正+マイリス×倍率）

                            long divideComment = CountPlay + (CountMyList) + CountComment;

                            long iTouka;
                            if (CountPlay < CountComment)
                            {
                                iTouka = CountPlay;
                            }
                            else
                            {
                                iTouka = CountComment;
                            }

                            if (divideComment == 0)
                            {
                                HoseiComment = 1;
                                PointComment = 0;
                            }
                            else
                            {
                                //補正	＝（再生+等価コメ+マイリス×倍率）÷（再生+コメ+マイリス×倍率）
                                HoseiComment = (CountPlay + iTouka + (CountMyList/* * config->m_CalcMyList*/)) / (double)divideComment;

                                double work2 = HoseiComment * 100;
                                long work1 = (long)work2;
                                long commentUnderLimit = (long)(config.CalcCommentUnderLimit * 100);
                                work2 -= work1;

                                if (work2 > 0.0)
                                {
                                    work1++;
                                }
                                if (CountComment > 0 && work1 < commentUnderLimit)
                                {
                                    work1 = commentUnderLimit;
                                }
                                HoseiComment = work1;

                                PointComment = (long)(CountComment * HoseiComment / 100.0 * config.CalcComment);
                                workPointTotal += PointComment;

                                HoseiComment /= 100;

                            }
                        }
                        break;

                    case HOSEI_ARI_SQRT:
                        {
                            PointComment = (long)(Math.Sqrt((double)CountComment) * 100 * config.CalcComment);
                            workPointTotal += PointComment;
                        }
                        break;

                    default:
                        {//補正無し
                            PointComment = (long)(CountComment * config.CalcComment);
                            workPointTotal += PointComment;
                        }
                        break;
                }
                //いいねポイント
                PointLike = (long)((double)CountLike * config.CalcLike);
                workPointTotal += PointLike;

                //全体ポイント補正
                switch ((HOSEI_POINT_ALL)config.CalcPointAllKind)
                {
                    case HOSEI_POINT_ALL.VCOLE2023:
                        {
                            //現在の集計ポイント[全体]に補正値Dを掛けたものを新ポイントとする
                            //ただし、0.25≦D≦1.0とする
                            //D = 2.5 *（コメント数 / 再生数）*100`
                            double hoseiD = 1;
                            if (CountPlay <= 0)
                            {
                                //再生数0の場合、Dが無限大＝1.0になる
                                hoseiD = 1;
                            }
                            else
                            {
                                hoseiD = 2.5 * ((double)CountComment / (double)CountPlay) * 100;
                                if (hoseiD <= 0.25)
                                {
                                    hoseiD = 0.25;
                                }
                                else if (hoseiD >= 1.0)
                                {
                                    hoseiD = 1;
                                }
                                else
                                {// 有効桁は下二桁(切り捨て）
                                    hoseiD = Math.Floor(hoseiD * 100) / 100;
                                }
                            }
                            HoseiAllPoint = hoseiD;
                            workPointTotal = (long)(workPointTotal * hoseiD);
                        }
                        break;
                }
            }
            return (long)workPointTotal;
        }

        public static string GetCommaValue(long value)
        {
            return $"{ value:#,0}";
        }

        /// <summary>
        /// ランキングリストをマージする
        /// </summary>
        /// <param name="list1"></param>
        /// <param name="list2"></param>
        /// <returns></returns>
        public static List<Ranking> MergeRankingList(List<List<Ranking>> taskResultList)
        {
            //重複IDの削除
            var movieDic = new Dictionary<string, Ranking>();
            foreach (var rankList in taskResultList)
            {
                foreach (var rankInfo in rankList)
                {
                    if (!movieDic.ContainsKey(rankInfo.ID))
                    {// まだ登録されていないID
                        var a = rankInfo.PointTotal;
                        movieDic[rankInfo.ID] = rankInfo;
                    }
                    else
                    {// 既に登録があるID
                        var editInfo = movieDic[rankInfo.ID];

                        // カテゴリ判定
                        if (string.IsNullOrEmpty(rankInfo.Category))
                        {
                            //ジャンルではないので上書きしない(できない）
                        }
                        else if (string.IsNullOrEmpty(editInfo.Category))
                        {// 現時点でカテゴリがなければ上書きする
                            editInfo.Category = rankInfo.Category;
                        }
                        else if (editInfo.Category == "全ジャンル" || editInfo.Category == "話題" )
                        {// 全ジャンルや話題カテゴリは優先度が低いので、情報を上書きする
                            editInfo.Category = rankInfo.Category;
                        }
                        //else if (editInfo.FavoriteTags.Count == 0 && rankInfo.FavoriteTags.Count > 0)
                        //{// トレンドタグ登録がない場合、トレンドタグがあるカテゴリを優先する
                        //    editInfo.Category = rankInfo.Category;
                        
                        //}

                        if (rankInfo.FavoriteTags.Count > 0)
                        {
                            foreach (var tag in rankInfo.FavoriteTags)
                            {
                                var workTag = tag.Trim();
                                if (!string.IsNullOrEmpty(workTag))
                                {
                                    editInfo.FavoriteTags.Add(workTag);
                                }
                            }

                        }
                    }
                }
            }
            return movieDic.Values.ToList();
        }

        public static Ranking FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Ranking>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }


    }
}
