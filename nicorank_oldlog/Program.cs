


using nicorank_oldlog;
using nicorankLib.Util;
using System.Collections.Generic;
using System.Reflection;

try
{
    var convConfig = ConvertConfig.GetInstance();
    if (convConfig == null)
    {
        Console.WriteLine("config.jsonが見つかりません");
        return;
    }

    Rss2JsonContoller rss2JsonContoller　= new Rss2JsonContoller();


    //①取得するRSSの一覧を決定する
    var genreList = new List<GenreInfo>();
    //T.B.D 現在は固定値のみ
    genreList.AddRange(convConfig.genreList);


    //② 取得するランキングの種類を決定する
    //  daily       毎日更新
    //  weekly      毎週月曜日更新
    //  monthly     毎月１日更新
    //  total       毎日更新

    var rssgetRankingList = rss2JsonContoller.GetRankingInfo( args );

    //③ ②毎に各ジャンル（旧カテゴリ）のRSSを取得する
    var rss2jsonList = rss2JsonContoller.AsyncExecuteAnalyzeRank(rssgetRankingList, genreList).Result;

    //④ 各フォルダに出力する
    //
    // See https://aka.ms/new-console-template for more information
    //Rss2Json.SaveOldRankingData(rss2jsonList);

    Console.WriteLine("集計終了");

}
catch (Exception e)
{
    //throw e;
    var errlog = ErrLog.GetInstance();
    errlog.Write(e);
}

