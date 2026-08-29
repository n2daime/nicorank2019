# アプリケーション詳細（apps.md）

## nicorank2019（集計メイン UI）

**役割**: 週刊/中間/SP の3モードで集計し、各種ファイル・DB を出力する WinForms アプリ。

- 起動: 引数なし。UI モードのみ（コンソール切替なし）
- `Program.cs`: 埋め込み DLL（Costura.Fody）を `AppDomain.AssemblyResolve` で解決 → `StatusLog.SetLogWriter(new ConsolWriter())` → `Application.Run(new frmMain())`
- `frm/frmMain.cs`: メインフォーム。Load 時に `SelectMode()`。`btnAnalyze_Click` で `Config` に補正値を設定し `AnalyzeAsync()` を実行
- `frm/frmMainSyukei.cs`: `frmMain` の partial。モード選択（Weekly/Tyukan/SP）→ `GetModeFactory()` → 集計フロー実行
- `frm/frmMesseageDialog.cs`: `RunFunction` デリゲートを `BackgroundWorker` で実行するモーダルダイアログ。`StatusLog` の出力先を TextBox に差し替え

**ビルド**: .NET Framework 4.8。Costura.Fody 6.2.0（単一 EXE 化）。packages.config 方式。PostBuild で「依存ファイル」を xcopy。`AnyCPU Prefer32Bit=false` で `64bit` 起動。

## nicorank_SnapShot（スナップショット取得ツール）

**役割**: 集計に使うスナップショット DB（再生/コメント/マイリスト/いいね数）をスナップショット API から取得する。

- 起動: **引数があればコンソールモード、引数なしなら UI モード**（`Program.cs`）
  - `nicorank_SnapShot.csproj /get` のように任意の引数1つ以上でコンソールモード（引数の中身は解釈されない）
- コンソールモード: `new SnapController().GetSnapShotAsync().Result` を実行
- UI モード（`Form1.cs`）: 「OK」ボタンで `SnapController.GetSnapShotAsync()` を await。完了後、チェックボックス ON なら TaskDialog で 30 秒カウントダウン後に `Application.SetSuspendState`（PC サスペンド）、OFF なら即終了
- **ビルド**: .NET Framework 4.8。WindowsAPICodePack-Core 1.1.2。Costura.Fody 6.2.0

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
- `RankApi2JsonDaily.cs`: 派生クラス。トレンドタグ展開 + タグ別定番ランキング追加取得
- **ビルド**: net8.0、SDK-style。AngleSharp / Newtonsoft.Json / Costura.Fody。**nicorankLib（net48）を参照するハイブリッド構成**

## 設定ファイル（アプリ起動に必要）

| ファイル | 場所 | 内容 |
|---|---|---|
| `config.json` | プロジェクトルート | nicorank_oldlog 用（Ranking_Info / GenreInfo）。**リポジトリに含めない** |
| `cookie.txt` | プロジェクトルート | ニコニコのログイン cookie（user_session）。**リポジトリに含めない** |
| `nicorank.xml` | カレントディレクトリ（依存ファイル/） | nicorankLib の `Config` が読む設定（補正値・出力先等） |
