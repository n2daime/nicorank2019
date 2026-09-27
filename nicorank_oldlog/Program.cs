using nicorank_oldlog;
using nicorank_oldlog.RankAPI;
using nicorankLib.Util;

try
{
    //StatusLogの出力先をコンソールにする。受け手がないと動画情報取得などの進捗表示が捨てられる（Issue #40）
    StatusLog.SetLogWriter(new ConsoleLogWriter());

    var convConfig = ConvertConfig.GetInstance();
    if (convConfig == null)
    {
        // 起動失敗は即時終了するが、事後切り分けのためファイル側にも残す。コンソール側の文面と終了コード1は変えない（Issue #43）。
        const string missingConfig = "config.jsonが見つかりません";
        StatusLog.WriteLine(missingConfig);
        ErrLog.GetInstance().Write(missingConfig);
        return 1;
    }
    var api = NicoRankiApi.GetInstance();
    if (api == null)
    {
        // cookie.txt不在も同様に両方へ残す（Issue #43）。
        const string missingCookie = "cookie.txtが見つかりません";
        StatusLog.WriteLine(missingCookie);
        ErrLog.GetInstance().Write(missingCookie);
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
                // NASの定期チェック（/checklogin）の成否はNASメールの本文になるため、文面は変えずStatusLog経由にする（Issue #43）。
                StatusLog.WriteLine("ログインチェック：r18カテが取得できません");
                StatusLog.WriteLine("→原因候補1: アカウント設定で「センシティブなコンテンツの表示」がOFFになっている");
                StatusLog.WriteLine("→原因候補2: ユーザーログインセッションが切れている");
                return 2;
            }
            else
            {
                StatusLog.WriteLine("ログインチェック：OK");
                return 0;
            }
        }
        else
        {
            StatusLog.WriteLine("ログインチェック：APIに接続できません");
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
        // 警告のみで続行する運用のため、文面は変えずStatusLog経由にする（Issue #43）。
        StatusLog.WriteLine("ユーザーログインセッション切れの可能性があります");
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

    // 終了報告もStatusLogに一本化する。受け手がコンソールのためcronメールの見た目は変わらない（Issue #43）。
    StatusLog.WriteLine("集計終了");

}
catch (Exception e)
{
    //throw e;
    var errlog = ErrLog.GetInstance();
    errlog.Write(e);
    return 2;
}
return 0;

/// <summary>
/// StatusLogをコンソールに出す受け手。Writeを受け取った順にそのまま出すだけ。
/// </summary>
public class ConsoleLogWriter : IStatusLogWriter
{
    public void Write(string log)
    {
        Console.Write(log);
    }
}

