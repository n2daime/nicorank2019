using nicorank_oldlog;
using nicorank_oldlog.RankAPI;
using nicorankLib.Util;

try
{

    var convConfig = ConvertConfig.GetInstance();
    if (convConfig == null)
    {
        Console.WriteLine("config.jsonが見つかりません");
        return 1;
    }
    var api = NicoRankiApi.GetInstance();
    if (api == null)
    {
        Console.WriteLine("cookie.txtが見つかりません");
        return 1;
    }

    // ログインチェックかどうか
    if ( args.Any(x => x == "/checklogin"))
    {
        var result = api.GetGenreList(out var workGenreList);

        if(result)
        {
            if (!workGenreList.Any(x => x.key == "r18"))
            {
                Console.WriteLine("ログインチェック：r18カテが取得できません");
                Console.WriteLine("→原因候補1: アカウント設定で「センシティブなコンテンツの表示」がOFFになっている");
                Console.WriteLine("→原因候補2: ユーザーログインセッションが切れている");
                return 2;
            }
            else
            {
                Console.WriteLine("ログインチェック：OK");
                return 0;
            }
        }
        else
        {
            Console.WriteLine("ログインチェック：APIに接続できません");
            return 2;
        }
    }

    //①取得するジャンル一覧を決定する
    var api2JsonContoller = new RankApi2JsonContoller();

    var isOK_1 = api2JsonContoller.GetGenreInfoList(out var genreList);

    // ユーザーセッションが有効かの確認
    // r18が存在するかどうかで確認する
    if (!genreList.Any(x => x.genrekey == "r18"))
    {
        Console.WriteLine("ユーザーログインセッション切れの可能性があります");
 //       return 2;
    }


    //② 取得するランキングの種類を決定する
    //  daily       毎日更新
    //  weekly      毎週月曜日更新
    //  monthly     毎月１日更新
    //  total       毎日更新

    var apigetRankingList = api2JsonContoller.GetRankingInfo( args );

    //③ ②毎に各ランキングを取得する
    var api2jsonList = api2JsonContoller.AsyncExecuteAnalyzeRank(apigetRankingList, genreList).Result;

    //④ 各フォルダに出力する
    //
    // See https://aka.ms/new-console-template for more information
    //RankApi2Json.SaveOldRankingData(api2jsonList);

    Console.WriteLine("集計終了");

}
catch (Exception e)
{
    //throw e;
    var errlog = ErrLog.GetInstance();
    errlog.Write(e);
    return 2;
}
return 0;

