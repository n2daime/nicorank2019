# アプリケーション詳細（apps.md）

## nicorank2019（集計メイン UI）

**役割**: 週刊/中間/SP/タグ検索の4モードで集計し、各種ファイル・DB を出力する WinForms アプリ。

- 起動: 引数なし。UI モードのみ（コンソール切替なし）
- `Program.cs`: 埋め込み DLL（Costura.Fody）を `AppDomain.AssemblyResolve` で解決 → `StatusLog.SetLogWriter(new ConsolWriter())` → `Application.Run(new frmMain())`
- `frm/frmMain.cs`: メインフォーム。Load 時に `SelectMode()`。`btnAnalyze_Click` で `Config` に補正値を設定し `AnalyzeAsync()` を実行
- `frm/frmMainSyukei.cs`: `frmMain` の partial。モード選択（Weekly/Tyukan/SP）＋タグ検索タブ（TagRank）→ `GetModeFactory()` → 集計フロー実行。ポイント計算パネルの表示・書戻し（`LoadPointCalcPanel`／`SavePointCalcPanel`）は `Config` 経由のため、OFFSET節別化（Issue #39）後もコード不変でモード別表示・保存になる
- タグ検索タブの件数確認（`btnTagSearch_Click`／`btnAnalyzeTag_Click` 内の `CheckTagCountAsync`）では、v2最新値モード（`chkUseLiveCounter` ON）の件数取得成功後に `SnapShotVersionChecker` で version を取得し、`lblTagSnapshotTime` に `MM/DD 05:00 時点のスナップショットで集計` と出す（Issue #39）。日付は `last_modified` のJST日・時刻は05:00固定（反映完了時刻との混同防止）。OFF時・未確認時・条件変更時は非表示に戻す。確認不能時は件数確認自体を失敗扱いにして集計に進めない。取得は `await Task.Run` でUIブロックしない
- タブ構成: 「集計」「タグ検索集計」「メンテナンス」の3タブ。ポイント計算パネル（`panel3`）は実体1つを集計・タグ検索の2タブ切替で付け替えて共有する（相対配置でAutoScaleずれ対策。Issue #30）。メンテナンスタブ（Issue #32・`tabPageMaint`）は集計モードと無関係のため `panel3` に触らず、モード切替も行わない。ログ欄も持たず、集計タブと同様にコンソール側へ出す運用
- メンテナンスタブの内訳: `grpVacuum`（DB最適化。対象4DBのチェック既定ON・実行前後2列サイズ欄・実行ボタン・状態＋進捗・注意文。中身実装は32.2）＋ `grpFutureApiXml`（長期キャッシュ再構築の場所予約。Issue #41着手時に埋める。操作部は無効化表示）
- `frm/frmMesseageDialog.cs`: `RunFunction` デリゲートを `BackgroundWorker` で実行するモーダルダイアログ。`StatusLog` の出力先を TextBox に差し替え

**ビルド**: .NET Framework 4.8。Costura.Fody 6.2.0（単一 EXE 化）。packages.config 方式。PostBuild で「依存ファイル」を xcopy。`AnyCPU Prefer32Bit=false` で `64bit` 起動。

## nicorank_SnapShot（スナップショット取得ツール）

**役割**: 集計に使うスナップショット DB（再生/コメント/マイリスト/いいね数）をスナップショット API から取得する。

- 起動: **引数があればコンソールモード、引数なしなら UI モード**（`Program.cs`）
  - `nicorank_SnapShot.csproj /get` のように任意の引数1つ以上でコンソールモード（引数の中身は解釈されない）
- コンソールモード: `new SnapController().GetSnapShotAsync().Result` を実行
- UI モード（`Form1.cs`）: 「OK」ボタンでまず `SnapShotVersionChecker` で更新確認（Issue #38）。未更新なら日時入りのOK/キャンセル確認ダイアログ（キャンセルは取得せず終了）、確認不能ならエラーダイアログで中断。更新済み（またはダイアログでOK）の場合のみ `SnapController.GetSnapShotAsync()` を await。完了後、チェックボックス ON なら TaskDialog で 30 秒カウントダウン後に `Application.SetSuspendState`（PC サスペンド）、OFF なら即終了
- **ビルド**: .NET Framework 4.8。WindowsAPICodePack-Core 1.1.2。Costura.Fody 6.2.0

## nicorank_SnapShot.Cli（スナップショット取得の Linux 版。Issue #37）

**役割**: WinForms を持たない net8.0 コンソール。NAS（Linux・x64・.NET 8 ランタイムあり）での定期取得用。取得中核は `nicorankLib/SnapShot` の `SnapController` をそのまま使う。

- 起動: `--help` / `-h` で使い方表示（終了コード 0）。それ以外（引数なし含む）は更新確認後に取得を実行する（既存コンソールモードが引数の中身を解釈しない運用に合わせた）
- 取得前に `SnapShotVersionPoller` で更新待ち（Issue #38）。未更新／確認不能の間は5分ごとに最大1時間リトライし、毎回 `last_modified` をログ出力。更新検知したら通常取得、1時間待っても更新なしなら取得せず終了コード2（`InitilizeDB` に触れない。リトライタイムアウトであることを `nicorankerr.log` に記録しNASメールで通知）
- 終了コード: 0=成功 / 2=エラー（`nicorank_oldlog` と同じ規約）。`SnapController` の catch で例外時に `false` を返すよう修正したため、例外時も 2 になる（従来は成功扱いだった）
- 依存しないもの: `nicorank.xml`・`DB/` フォルダは使わない（SnapShot 経路に参照なし）。成果物はカレント直下の `LogSnapshot_yyyyMMdd.db`、エラー時のみ `nicorankerr.log`。定期実行では出力先の `WorkingDirectory` を固定する運用が必要
- 持たないもの: 開始ボタン・サスペンド・TaskDialog（`Form1` 由来）。電源管理は cron / systemd 側の責務
- **ビルド**: net8.0、SDK-style、`PackageReference`（`Microsoft.Data.Sqlite 10.0.11` / `Newtonsoft.Json 13.0.4` は UnitTest と同版に統一。`System.Text.Encoding.CodePages 8.0.0`）。**nicorankLib（net48）を参照するハイブリッド構成**（`nicorank_oldlog` と同じ）。Costura は使わない
- 起動直後に `CodePagesEncodingProvider` を登録する（`TextUtil` の shift_jis 判別が Linux で例外にならないため。パッケージの版は `nicorank_oldlog` と同じ 8.0.0だが、登録呼び出し自体は oldlog にはなく CLI で追加した）
- SQLite ネイティブは `Microsoft.Data.Sqlite` 経由で `runtimes/linux-x64/native/libe_sqlite3.so` が出力に含まれる。Windows 用の `lib` 集約・`probing` は持ち込まない
- 配布はポータブルな framework-dependent（`dotnet publish -c Release`。`-r` を付けない）を想定し、実行は `dotnet nicorank_SnapShot.Cli.dll` とする。理由は、`-r linux-x64` 付き publish では NuGet 由来の `runtimes/linux-x64/native/libe_sqlite3.so` が出力から落ち、win 用だけが残って Linux で動かないことを確認したためである。`-r` なし publish なら `runtimes/` 全 RID が同梱され linux-x64 が含まれる。出力直下の `lib/`（win 用 DLL 群）は net48 参照元から流れ込む残骸であり、Linux では無視される
- Linux 実機での取得実行は未検証（Windows 上で `--help` 終了コード 0 とビルド・全テスト 182 件 PASS、portable publish への linux-x64 同梱まで確認）

## nicorank_oldlog（公式過去ランキング回収ツール）

**役割**: ニコニコ公式 API（nvapi.nicovideo.jp）から過去のランキング（ジャンル/定番/トレンドタグ）を JSON 化して `old-ranking/<folder>/<yyyy-MM-dd>/` に保存する。**net8.0 コンソールアプリ（top-level statements）**。

- コマンドラインオプション:
  - `/checklogin` — ログイン状態チェック（OK=0 / NG=2）。**NAS（Linux）の定期タスクから1時間ごとに実行され、セッション切れ（NG）を検知したら NAS 側の機能でメール通知する運用**（定期タスク・メール送信は NAS 側の機能）
  - 引数なし — daily + total を毎回、月曜なら weekly、1日なら monthly を自動追加
  - `/term:daily|weekly|monthly|total` — 特定ランキングのみ取得
  - `/folderappend:<文字列>` — 保存フォルダ名にサフィックス追加
  - 終了コード: 0=成功 / 1=config.json か cookie.txt 不在 / 2=エラー
- フロー: `ConvertConfig.GetInstance()`（config.json）→ `NicoRankiApi.GetInstance()`（cookie.txt の user_session）→ `RankApi2JsonContoller` → term 別に `RankApi2Json` / `RankApi2JsonDaily` を並列実行 → 保存
- `RankAPI/NicoRankiApi.cs`: シングルトン。nvapi に user_session クッキー + UA を付与して GET。GenreList / TeibanGenreList / TrendTagList / GenreRanking（hasNext まで最大20ページ）/ TeibanRanking
- `RankApi2Json.cs`: ジャンル/定番取得（失敗時3回リトライ）、ID 重複排除マージ、`lastweekly_all.json` / `lastmonthly_all.json` との更新チェック（更新なしなら5分ポーリング）
- 週刊保存時は全ジャンルのID一覧（約26000件）で動画情報を一括取得し、日付フォルダへ `ApiXML.db` も置く（2019側の週刊集計が取り込む。取得時点固定で欠落を減らす。Issue #40。一時名で作ってから置き換え）
- `RankApi2JsonDaily.cs`: 派生クラス。トレンドタグ展開 + タグ別定番ランキング追加取得
- **ビルド**: net8.0、SDK-style。Newtonsoft.Json / Costura.Fody。**nicorankLib（net48）を参照するハイブリッド構成**

## 設定ファイル（アプリ起動に必要）

| ファイル | 場所 | 内容 |
|---|---|---|
| `config.json` | プロジェクトルート | nicorank_oldlog 用（Ranking_Info / GenreInfo）。**リポジトリに含めない** |
| `cookie.txt` | プロジェクトルート | ニコニコのログイン cookie（user_session）。**リポジトリに含めない** |
| `nicorank.xml` | カレントディレクトリ（依存ファイル/） | nicorankLib の `Config` が読む設定（補正値・出力先等） |
