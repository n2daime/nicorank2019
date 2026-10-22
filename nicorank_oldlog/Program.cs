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

    // 再試行の予算は実行全体で1つを共有する（Issue #51）。
    // ジャンル一覧取得とランキング取得がそれぞれ1時間待つと最悪2時間になり、定期タスク1枠に収まらないため。
    var retryPoller = new RetryPoller();

    //①取得するジャンル一覧を決定する
    var api2JsonContoller = new RankApi2JsonContoller();

    // 取得対象termはジャンル一覧に依存しないため先に確定する（Issue #53）。
    // ジャンル一覧の取得に失敗したとき、対象termの日付フォルダへ「データなし」マーカーを
    // 置けるようにするためである（失敗が確定してからではtermを組み立てられない）。
    var apigetRankingList = api2JsonContoller.GetRankingInfo( args );

    List<GenreInfo> genreList = new List<GenreInfo>();
    var isOK_1 = retryPoller.RunUntilSuccess(
        () =>
        {
            bool ok = api2JsonContoller.GetGenreInfoList(out var workGenreList);
            if (ok)
            {
                genreList = workGenreList;
            }
            return ok;
        },
        message => StatusLog.WriteLine($"ジャンル一覧: {message}"));

    if (!isOK_1)
    {
        // ジャンル一覧が取れないと全termが空になるため、ここで打ち切って異常終了する（Issue #51）
        const string genreError = "ジャンル一覧を取得できませんでした（約1時間リトライ後）。nicorankerr.log を確認してください。";
        StatusLog.WriteLine(genreError);
        ErrLog.GetInstance().Write(genreError);

        // 1時間リトライしても復旧しなかった＝その日のデータは取得できない。
        // nicorank2019 が404で止まらないよう、明示的な「データなし」マーカーを残す（Issue #53）
        api2JsonContoller.WriteNoDataMarkers(apigetRankingList);
        return 2;
    }

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

    if (apigetRankingList.Count == 0)
    {
        // 引数不正などで対象0件のまま「集計終了」にすると、何も取得していないのに成功扱いになる（Issue #51）
        const string noTarget = "取得対象のランキングがありません（引数を確認してください）";
        StatusLog.WriteLine(noTarget);
        ErrLog.GetInstance().Write(noTarget);
        return 2;
    }

    //③ ②毎に各ランキングを取得する（失敗したtermだけを約1時間リトライする）
    var runResult = api2JsonContoller.AsyncExecuteAnalyzeRank(apigetRankingList, genreList, retryPoller).GetAwaiter().GetResult();

    //④ 各フォルダに出力する
    //
    // See https://aka.ms/new-console-template for more information
    //RankApi2Json.SaveOldRankingData(api2jsonList);

    // 終了コードに結果を反映する（Issue #51）。
    // 部分失敗はユーザー判断で成功扱いにし、警告だけ残す（1カテゴリの一時的な失敗でNASのエラーメールを
    // 出し続けないため）。全termが失敗したときだけ異常終了させ、NASのエラーメールで気づけるようにする。
    if (runResult.Succeeded.Count == 0)
    {
        string allFailed = $"集計がエラーになりました。{runResult.TotalCount}件すべてのランキングを取得できませんでした（約1時間リトライ後）。nicorankerr.log を確認してください。";
        StatusLog.WriteLine(allFailed);
        ErrLog.GetInstance().Write(allFailed);

        // 全term失敗＝その日のデータは取得できない。nicorank2019 が404で止まらないよう、
        // 明示的な「データなし」マーカーを残す（Issue #53）。
        // 部分失敗（exit 0）では置かない: 一部termのデータは残っており、日全体を
        // メンテナンス扱いにすると取得済みデータまで捨ててしまうためである。
        api2JsonContoller.WriteNoDataMarkers(apigetRankingList);
        return 2;
    }
    if (runResult.Failed.Count > 0)
    {
        StatusLog.WriteLine($"注意: {string.Join(", ", runResult.Failed)} の取得に失敗しました（他のランキングは保存済み）");
    }

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

