using System.Text;
using nicorankLib.SnapShot;
using nicorankLib.Util;

// TextUtil が shift_jis 等のコードページを使うため、Linux ではプロバイダ登録が必須である（net48 では OS 標準で利用できる）。
// 登録漏れだと Encoding.GetEncoding が例外になり、エラー系の経路で落ちる。
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// 取得の進捗は StatusLog 経由で出力されるため、コンソールへの書き込み先を注入する（既存 WinForms 版 Program.cs と同じ役割）。
StatusLog.SetLogWriter(new ConsoleWriter());

// ヘルプ表示だけは取得を行わず終了する。それ以外の引数は無視して取得する（既存コンソールモードは引数の中身を解釈しない運用のため合わせる）。
if (args.Any(a => a == "--help" || a == "-h" || a == "-?" || a == "/?"))
{
    Console.WriteLine("ニコラン用スナップショット取得ツール（Linux/CLI 版）");
    Console.WriteLine("使い方: nicorank_SnapShot.Cli [--help]");
    Console.WriteLine("成果物はカレントディレクトリの LogSnapshot_yyyyMMdd.db に保存される。");
    Console.WriteLine("nicorank.xml も DB/ フォルダも使わない。定期実行では出力先の WorkingDirectory を固定すること。");
    return 0;
}

try
{
    // 成果物はカレントディレクトリの LogSnapshot_yyyyMMdd.db に保存される（SnapShotDB の仕様）。
    bool ok = await new SnapController().GetSnapShotAsync();
    if (ok)
    {
        Console.WriteLine("集計終了");
        return 0;
    }
    Console.WriteLine("集計がエラーになりました。nicorankerr.log を確認してください。");
    return 2;
}
catch (Exception e)
{
    // SnapController 内で握りつぶされなかった例外の受け皿。nicorank_oldlog と同じ終了コード規約（0=成功/2=エラー）にする。
    ErrLog.GetInstance().Write(e);
    return 2;
}

// StatusLog の出力先。既存 WinForms 版の ConsolWriter と同じく Console にそのまま流す。
sealed class ConsoleWriter : IStatusLogWriter
{
    void IStatusLogWriter.Write(string log)
    {
        Console.Write(log);
    }
}
