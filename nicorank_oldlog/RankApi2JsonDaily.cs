using nicorank_oldlog.RankAPI;
using nicorankLib.Util;

namespace nicorank_oldlog
{
    /// <summary>
    /// デイリー派生（トレンドタグ展開付き）。表示は基底と同一方針でStatusLogに寄せる（Issue #43）。
    /// </summary>
    public class RankApi2JsonDaily : RankApi2Json
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="rankInfo"></param>
        public RankApi2JsonDaily(Ranking_Info rankInfo, in string checkstring = "") : base(rankInfo, checkstring)
        {

        }

        /// <summary>
        /// 集計対象の指定
        /// </summary>
        /// <param name="genreList"></param>
        public override bool Initilize(in List<GenreInfo> genreList, in DateTime today,in string optionFolderAppend)
        {
            if (!base.Initilize(genreList, today, optionFolderAppend))
            {
                return false;
            }

            var nicoApi = NicoRankiApi.GetInstance() ?? throw new InvalidOperationException("NicoRankiApi instance is null");

            var workgenreList = new List<GenreInfo>(genreList);

            foreach (var genreinfo in workgenreList)
            {
                if (!genreinfo.isEnabledTrendTag || genreinfo.featuredKey == "")
                {
                    //トレンドタグなし
                    continue;
                }

                StatusLog.WriteLine($@"{this.RankInfo.folder}:{genreinfo.genre} のトレンドタグを取得しています...");
                //トレンドタグの取得
                bool getResult = nicoApi.GetTrendTagList(genreinfo.featuredKey, out var trendTagList);

                uint tagCount = 1;
                foreach (var tagName in trendTagList)
                {
                    if ( tagName == genreinfo.genre)
                    {
                        //ジャンル名と同じタグは２重取得になるので対象外
                        continue;
                    }

                    var workGenre = new GenreInfo();
                    workGenre.genre = genreinfo.genre;
                    workGenre.featuredKey = genreinfo.featuredKey;
                    workGenre.tag = tagName;
                    workGenre.isGenre = genreinfo.isGenre;  //親がジャンルであれば子もジャンルである
                    workGenre.isTeibanRank = true;          //トレンドタグは定番ランキングから取得する
                    workGenre.isGenreRank = false;

                    //ファイル名を取り出す
                    var fileInfo = genreinfo.file.Split(".json");
                    if (fileInfo.Length < 1)
                    {
                        continue;
                    }
                    workGenre.file = $@"{fileInfo[0]}_{tagCount:00}.json";
                    tagCount++;

                    this.GenreResultList.Add(new GenreRankResult(workGenre, this.TargetSaveDir));
                    StatusLog.WriteLine($@"{workGenre.file}:{workGenre.tag}");
                }

            }

            return true;
        }
    }
}
