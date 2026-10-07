# タスク Archive（archive.md）

完了したタスクの**経緯・検証履歴・実装ノウハウ**の記録場所。GitHub Issue に無い固有情報の置き場所とする。

## 記録ルール

- タスク完了時（AGENTS.md §2 のマージ後ゲート）に、検証履歴・実装ノウハウ・設計判断の経緯を追記する。
- 形式は自由だが、日付・Issue 番号・背景・実施内容・検証結果を含める。
- コードの現状態の速読用まとめは `docs/knowledge/` に反映する。ここでは経緯・検証・判断の理由を省略せず書く。重複を避けるため詳しい構造説明は知識へ譲ってよいが、判断が追える最小限の前提はここにも書く。仕様・設計の定義は `../specs.md` / `../design.md` を更新する。

---

## 2026-06-03 btreeInitPage() returns error code 11 対策

- **Issue／ブランチ**: なし（Issue 運用開始前の対応のため番号なし）
- **背景**: スナップショット API から数十万件を登録する際、`btreeInitPage() returns error code 11`（SQLITE_CORRUPT）が断続発生し、DB が破損して以後の読み書きが不可能になる状態が起きていた。原因は巨大トランザクションである
- **実施内容**: `SQLiteCtrl` の接続を強化（WAL・PRAGMA・グレースフルフォールバック）。`SnapShotDB` の大量登録を 5000件単位のバッチコミット＋`INSERT OR IGNORE`＋パラメータ再利用へ変更した
- **検証**: 設定値やクラッシュ時の挙動（未コミット分は失われるが `LogSnapshot*.db` は日次作成のため再実行で復元可能）は `../knowledge/pitfalls.md` 項目1 に集約済み。新規実装前のゲートとしても AGENTS.md §1 に置いている

---

## 2026-06-23 SQLite 操作の単体テスト設計

- **Issue／ブランチ**: なし
- **背景**: DB 操作の実行時問題（上記の破損やコマンド使い回し系）を、実 DB を触らずに検出できる形にしたかったため
- **実施内容**: `ISQLiteCtrl` インターフェースを抽出し、実装 `SQLiteCtrl` とテスト用実装を差し替え可能にした。テスト側は `OpenInMemory()` でインメモリ DB を作る `TestDbHelper` を用意し、SELECT / INSERT / DDL / エラー系のテストを書いた
- **検証**: 構成・実行方法・新規テスト追加の作法は `../knowledge/testing.md` に反映済み。後の #22（コマンド再利用の問題検出）はこの土台の上に載っている

---

## 2026-06-23 単体テストの活性化（基盤）

- **Issue／ブランチ**: なし
- **背景**: 長期にわたり知識がコードにしか残っておらず（`../proposal.md` 参照）、変更時の回帰を確認する手段が無かった。当時の `UnitTest.csproj` は old-style（packages.config + Reference）で、.NET 10 SDK 上の MSTest.TestAdapter 3.5.2 と組み合わせると testhost 起動時に StackOverflow が発生して実行できない状態だった
- **実施内容**: csproj を SDK-style へ変換して StackOverflow を解消。Moq を導入し、`Fixtures/` に `nicorank.xml` と `test_ranking.csv` を配置した（`Config.GetInstance().Initilize()` がカレントの `nicorank.xml` を読むため、出力直下にもコピーする）。Ranking / Config / TextUtil / StatusLog / Output のテストを書き、計69件とした
- **検証**: 69件全件 PASS。csproj 形式の制約と出力先パス（`bin\Debug\net48\`）の注意は `../knowledge/testing.md` の「重要: csproj は SDK-style を使用すること」に残している。集計ロジック側の拡充は未完了のまま `../tasks.md` の「テスト拡充（集計ロジック）」に残っている

---

## 2026-08-30 SQLite ライブラリ移行 (#20)

- **Issue**: #20 `System.Data.SQLite 1.0.118 → Microsoft.Data.Sqlite 10.0.11 / SQLitePCLRaw 2.1.12`
- **ブランチ**: `feature/t020-sqlite-microsoft-data-sqlite` → `main` (`70c7110` Merge)
- **背景**: `System.Data.SQLite` の `lib` 散乱と `Costura` 埋め込みによる `batteries_v2` の `Location=""` 問題、`.NET 4.8` の `AnyCPU` 制約、`System.ValueTuple` の `CopyLocal` 問題を抱えたまま `NuGet` を最新安定版へ更新。
- **実施内容**:
  - `22` ファイルの `using System.Data.SQLite` を `Microsoft.Data.Sqlite` に置換、`CreateCommand`/`File.Create`/`SqliteType`/`SqliteTransaction` キャスト等を対応。
  - `Microsoft.Data.Sqlite 10.0.11` / `SQLitePCLRaw 2.1.12` に統一（`3.0.5` は `AnyCPU` 禁止のため `2.1.12` を採用）。`AngleSharp 1.7.2` / `Costura.Fody 6.2.0` / `Fody 6.9.3` / `EF6 6.5.2` 等も更新。
  - `lib` サブフォルダ集約: `5 DLL + runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` を `bin/lib` に配置。`FodyWeavers.xml ExcludeAssemblies` + `AfterResolveReferences` 二重除外 + `probing privatePath="lib"` + `AssemblyResolve` で解決。`CheckForAnyCPU` 空ターゲットで `AnyCPU` 禁止を無効化、`Prefer32Bit=false` で `64bit` 起動を保証（`win-x86` は `32bit` フォールバック用に `3` 種同梱）。
  - `System.ValueTuple 4.6.2` を `HintPath` 付き `CopyLocal` 化し `AutoGenerateBindingRedirects=false` で二重 `assemblyBinding` を抑止。
- **検証**:
  - `MSBuild Release/AnyCPU` ソリューション `EXIT=0`（`MSB3245` 無し）、`dotnet test` `69` 件 `PASS`（`Debug/Release` とも）、`GetManifestResourceNames` で `SQLitePCLRaw/Microsoft.Data.Sqlite` の埋め込み無しを確認、ユーザー実行で `Debug` の `Batteries_V2.Init()` が `win-x64` で成功。
  - `reviewer` 再レビューで `7` 点指摘 → `5` 点指摘とも解消し `問題なし` 判定。
- **残課題**: Issue #22（単体テストでコマンド再利用パターン検出不可）を別途対応予定。

---

## 2026-08-31 Nicochart TSV 取得の完全廃止と ID 番号による新着偽造判定への代替 (#23)

- **Issue**: #23
- **ブランチ**: `feature/Removal_Logic_NicoChart` → `develop` (`0aab8f0` Merge)
- **背景**: nicochart.jp のポイント TSV（`http://www.nicochart.jp/point/{id}.tsv`）が利用できなくなった。従来は so 動画（公式チャンネル）の「新着偽造」（非公開→再公開で投稿日時だけ更新され、過去の数字が新着のように集計される問題）判定を TSV 補完で対応していた。
- **実施内容**:
  - ユーザー実装（`7ccd4b1`）: `CheckSoMovieNeedSabun` から TSV 取得フォールバックを削除。`SabunReader` で差分が取れない so 動画を ID 番号（so40000000 未満）で新着偽造判定し `isDelete`。
  - 追加実装（`a427129`）: `LogNicoChart.db` の attach/detach・`NicoChart.Ranking` 参照・`SYSTEM.NicoChart` 設定・`LOG_NICOCHART` 定数を完全削除。`依存ファイル/nicorank.xml` と `UnitTest/Fixtures/nicorank.xml` から `<NicoChart>` を削除。
  - reviewer 指摘対応（`77425d9`）: コメント整合性（「Nicochart節約」削除・DB エラー時除外のコメント修正）・`CheckSoMovieNeedSabun` のエラーメッセージ旧メソッド名修正・`!isNew` / `out var` へのスタイル修正。
- **設計判断**:
  - DB エラー時も `isDelete`（全除外）とする: 意図的な仕様。全除外が発生した時点でユーザーが問題に気づきやすい。
  - `40000000` の定数化は見送り: 当該箇所でしか使わないため。
  - `LogNicoChart.db` は参照ごと完全削除（ユーザー判断）。既存ユーザーの nicorank.xml に `<NicoChart>` が残っていても XmlSerializer が未知要素を無視するため互換性は維持される。
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 69 件 PASS（feature / develop とも EXIT CODE 0）。
  - ユーザーが実データでの集計実行確認済み（新着偽造動画の除外挙動）。
  - reviewer レビュー: 高・中深刻度の指摘なし。低深刻度 6 点はすべて対応済み。
- **残課題**: `NocoChartReader` は `now.nicochart.jp` の today フィードに依存しており、nicochart.jp 全体の利用停止状況次第で動作しない可能性（reviewer 推奨。→ #24 でデッドコードとして削除し解消）

---

## 2026-08-31 デッドロジック削除: NocoChartReader / NicoChartModel / AngleSharp (#24)

- **Issue**: #24
- **ブランチ**: `feature/t024-remove-dead-nocochart` → `develop` (`186ad1c` Merge)
- **背景**: `NocoChartReader`（now.nicochart.jp の today フィードから当日ランキングを取得）が `ModeFactory` 等どこからも呼ばれていないデッドロジックだった。同ドメインの別機能は #23 で廃止済みで、フィードの存続保証もない。
- **実施内容**:
  - `NocoChartReader.cs` / `NicoChartModel.cs`（Atom feed モデル6クラス）を削除
  - `nicorankLib.csproj` から Compile Include と AngleSharp 1.7.2 の Reference を削除、`packages.config` から AngleSharp を削除
  - `nicorank2019.csproj` の PostBuildEvent から不要になった `del AngleSharp*` を削除
- **設計判断**:
  - `RankGenreJson` / `RankLogJson` は `JsonReaderBase` / `RankApi2Json` で現役使用のため残す
  - `nicorank_oldlog` の AngleSharp 1.1.2 PackageReference は別プロジェクト依存のため触らない（.cs での使用なしは確認済み、スコープ外）
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 69 件 PASS（EXIT CODE 0）＋ `dotnet build nicorank2019/nicorank2019.csproj` 成功（PostBuildEvent 変更の検証を含む）
  - reviewer レビュー: 必須指摘なし（低2点は対応不要と判断、見送り）
- **備考**: ユーザーに見える挙動の変更なし（呼び出されていない機能の削除のみ）

---

## 2026-09-01 配布 zip 展開時の MOTW で SQLiteCtrl のタイプ初期化が失敗する対処 (#26)

- **Issue**: #26
- **ブランチ**: `feature/t026-motw-loadfromremotesources` → `develop` (`ef43f81` Merge)
- **背景**: GitHub Release(v20260831_nicorank)から配布した zip をエクスプローラで展開した環境で `'nicorankLib.Util.SQLiteCtrl' のタイプ初期化子が例外をスローしました` が発生するという報告。配布 lib の DLL は正常環境とバイト一致(SHA256)しており、開発 PC でも「テストしたい lib を lib にリネームする」手順のみで再現/解消する現象だった
- **原因**: エクスプローラで zip を展開すると中の全ファイルに Zone.Identifier(MOTW, ZoneId=3)が付く。.NET Framework 4.8 は `loadFromRemoteSources` 未設定のアプリでリモートゾーンのマネージ DLL のロードを `FileLoadException` で拒否し、`Batteries_V2.Init()` が失敗する。ネイティブ `e_sqlite3.dll` は `LoadLibrary` のため影響を受けない
- **切り分けの経緯**: DLL のバイト一致・exe/config 共通・実行時フォルダ名は常に lib に統一、という条件で残った差分はメタデータ(ADS/ACL/隠し属性)に絞られた。`Get-Item -Stream Zone.Identifier` で確認したところ、配布 lib の DLL は全件 MOTW あり(正常 lib はなし)。exe.config に MOTW が残っていたことも配布物展開の痕跡として手掛かりになった。なお切り分け中にテストフォルダ名のアンダースコアが半角/全角で不一致となり出力が空になる一幕があった(パス確認の重要性)
- **実施内容**:
  - `nicorank2019` / `nicorank_SnapShot` の App.config に `<loadFromRemoteSources enabled="true" />` を追加
  - `frmMain.cs` の起動時/集計時のエラー表示を `ex.Message` → `GetExceptionMessages`(例外チェーン連結)に変更。従来は SQLiteCtrl が付与する診断情報(InnerException)が一行目しか見えなかった
  - pitfalls.md 項目16 追記、release.md にリリース前チェックリスト・Release ノート指針を追記
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 69 件 PASS(EXIT CODE 0)、`dotnet build nicorank2019/nicorank2019.csproj -c Release` 成功(EXIT CODE 0)
  - reviewer レビュー: 高・中深刻度の指摘なし。低深刻度 2 件(AggregateException 展開・循環参照防御)は見送り、理由と将来対応を Issue #26 コメントに記録
  - ユーザー実行確認: lib DLL に MOTW を再付与した状態で新 exe + exe.config による正常起動を確認(loadFromRemoteSources の効果確認済み)
- **残課題**: 将来 `Task.WhenAll` / 並列集計導入時に `GetExceptionMessages` へ `AggregateException` 展開を追加(Issue #26 コメント参照)

---

## 2026-09-03 ビルド警告の対処と未使用 AngleSharp の削除 (#25)

- **Issue**: #25（Dependabot alert #10 の AngleSharp 脆弱性も本件で解消）
- **ブランチ**: `feature/t025-build-warnings-anglesharp` → `develop`
- **背景**: Release ビルドの警告 5 件（MSB3276 / CS0414 / CS0168×3）と、`nicorank_oldlog` の未使用 AngleSharp 1.1.2（脆弱性 medium）
- **実施内容**:
  - CS0168×3: `InternetUtil.cs` の未使用 catch 変数 `ex` を除去（`WebException ex` は使用中のため対象外）。reviewer 指摘で `ex` 参照の死にコメント 2 行も削除
  - CS0414: `frmMainSyukei.cs` の `eAnalyzeMode`（宣言＋代入 3 件＋関連 using）を削除。`GetModeFactory` は従来通りラジオボタン直接参照。`frmMain.cs` の未使用 using も併せて削除
  - MSB3276（A 案）: 詳細ログで競合は `System.Memory` のみと特定。両 EXE の `App.config` の redirect を `4.0.1.2` → `4.0.5.0` に修正し `nicorankLib/app.config` と整合。`AutoGenerateBindingRedirects=false` は維持（#20 の二重 `assemblyBinding` 再発防止）
  - AngleSharp 削除: `nicorank_oldlog.csproj` から PackageReference を削除（`.cs` からの使用ゼロ確認済み）
  - ソリューション全体ビルドで追加発覚した警告も対処（Issue 記載通り追記相当）: `RankApi2Json.cs` の `if(false)` デバッグ分岐を削除（CS0162。`Parallel.ForEach` 側のみ残し挙動不変）、Fody/Costura.Fody の `IncludeAssets` 除去（Fody 警告の推奨通り、`PrivateAssets=all` 維持）
  - `docs/knowledge/apps.md` の依存記述から AngleSharp を除去
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 成功（EXIT CODE 0）
  - `dotnet build nicorank2019.sln -c Release --no-incremental` で警告 0・EXIT 0
  - reviewer レビュー: 高・中深刻度なし（マージ可判定）。低 2 件は対応済み
  - ユーザーが週刊/中間/SP の実集計で実行確認済み（問題なし）
- **設計判断**:
  - `eAnalyzeMode` は削除（使う形への修正ではなく）。`GetModeFactory` との二重状態解消より最小差分を優先
  - MSB3276 は `AutoGenerateBindingRedirects=true` 化ではなく手動 redirect 追加。`true` 化は #20 の二重化問題に逆戻りするため
  - `nicorank_SnapShot/App.config` も同一の陳腐化 redirect だったため同時修正（ソリューション警告 0 のため）

---

## 2026-09-03 単体テストでDB操作のビジネスロジック問題を検出できるようにする (#22)

- **Issue**: #22
- **ブランチ**: `feature/t022-db-command-reuse-tests` → `develop`
- **背景**: #20移行の検証過程で実行時テストでのみ同一コマンド再利用問題が発覚した（単体テスト69件PASSでは検出不可）。本番コードを網羅調査し、残存Clear漏れ2件のみ検出（他は暫定修正済み）。
- **実施内容**:
  - `NicoApi.UpdateTumbInfo` のDELETE/INSERTループをループ内Clear化（2件以上更新時の `InvalidOperationException: Must add values...` を解消。`@取得日` は `todayStr` に退避して毎回再設定）
  - `UnitTest/nicorankLib/Util/UnitTestDbCommandReuse.cs` 新設6件（ネガティブ・NicoApi型DELETE・DELETE→INSERT切替・GetRankingSabun型SELECT切替・calcDailyRank型ALTER同一Tx成功系・GetMovieData型）。計75件
  - `pitfalls.md` 項目17に移行時ランタイム差分チェックリスト追記、`testing.md`/`structure.md` の件数を75に更新、`tasks.md` に#22タスク追記→完了化
  - reviewer指摘対応: 中1件（ALTER同一Txの成功系限定化）・低4件（例外文言一般化・PRAGMA両記・ヘルパー順序・拡張コメント）を修正、低2件（NicoApi外側Clear冗長・testing.md内訳欠落）を見送り。再レビューでマージ可判定
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全75件PASS（EXIT CODE 0）、`dotnet build nicorank2019.sln -c Release --no-incremental` 成功（EXIT CODE 0）
  - 実集計での実行確認はユーザー判断で省略（自動テストでガード、出力形式の変更なし）
- **残課題**（Issue #22 のクローズ時コメントに転記）:
  - ALTER同一Txのロールバック→再実行検証（本件は成功系のみ）
  - DB操作の共通ヘルパー集約改修の要否検討（Issue #22 方針3、スコープ大・要相談のため見送り）
  - `NicoApi` ループ外Clear冗長の微修正、`testing.md` 内訳の `UnitTestTestDbHelper` 欠落とREADME件数表ドリフトの整理

---

## 2026-08-31 リリース v20260831_nicorank

- **タグ**: `v20260831_nicorank`（main `d8af711`。annotated tag でコミット位置を確認済み）
- **GitHub Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260831_nicorank
- **成果物**: `nicorank2019_20260831.zip`（パターンA ホワイトリストのみ: exe / exe.config / nicorank.xml.org / lib 4件 + runtimes 3種）
- **含まれる変更**: #20（SQLite移行）/ #23（nicochart TSV廃止・新着偽造判定代替）/ #24（デッドコード削除）。リリース対象は nicorank2019 のみ
- **検証**: main 上で Release ビルド EXIT=0、`dotnet test` 69件 PASS、lib 配置確認（4件 + runtimes 3種）、zip ホワイトリスト照合（ユーザー確認済み）、実機確認済み（ユーザー）
- **release.md 初実施で判明した問題**（後続の release.md 改善で反映）:
  - `bin/Release/nicorank.xml` が PostBuildEvent xcopy のタイミングで更新されず、廃止済み設定（`<NicoChart>`）が残った古い版が zip に入った（ユーザー指摘で発覚）。zip 作成前に bin/Release と依存ファイルの一致確認が必要
  - PowerShell から `gh --notes` に日本語 + バッククォート入り本文を直接渡すと `` `n `` が改行に置換され文字欠けが発生 → `--notes-file` を使うべき
  - develop の未プッシュコミット push が手順の明示ステップに無かった

---

## 2026-09-01 リリース v20260901_nicorank

- **タグ**: `v20260901_nicorank`（main `e54963f`。annotated tag でコミット位置を確認済み）
- **GitHub Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260901_nicorank
- **成果物**: `nicorank2019_20260901.zip`（パターンA）/ `nicorank_SnapShot_20260901.zip`（パターンB）— **初めて SnapShot を配布**（#26 の exe.config 修正が両アプリに影響するためユーザー判断で追加）
- **含まれる変更**: #26（MOTW 対応: loadFromRemoteSources 追加・起動時エラー表示の例外チェーン化）+ 配布テンプレート `nicorank.xml` の Thread Max 既定値 16→6（ユーザー指示）
- **検証**:
  - main 上で `dotnet test` 69件 PASS、nicorank2019 / nicorank_SnapShot の Release ビルド EXIT=0
  - lib 配置確認（両アプリとも 4件 + runtimes 3種）、両 exe.config に `loadFromRemoteSources enabled="true"` 含まれることを確認（今回から追加したチェック項目）
  - `依存ファイル/nicorank.xml` と `bin\Release\nicorank.xml` のハッシュ照合で **Thread Max の不一致（16 vs 6）を検出** → 依存ファイル側を 6 に修正して統一してから zip 化（チェックリストが再び有効に機能した。bin\Release 側は実行中に書き換わるため今後も必ず確認）
  - zip ホワイトリスト照合（不要ファイルの混入なし）、MOTW 付き lib での起動確認済み（ユーザー）
- **実機集計確認（週刊/中間/SP）**: ユーザー判断で省略（変更が config・エラー表示のみで集計ロジックに影響しないため）
- **備考**: Release ノートは `--notes-file` 方式（前回の知見通り）、exe と exe.config のセット上書き案内を明記。既知の Dependabot 警告（moderate × 1、Dependabot #10・#25 対応対象）は継続中

---

## 2026-09-04 ニコ動APIのリクエスト組み立てを型付きリクエストへ変更 (#19)

- **Issue**: #19（作成時に未記載だった「なぜ変更するか」を追記済み）
- **ブランチ**: `feature/t019-snapshot-typed-request` → `develop`（`44795c7` Merge）
- **背景**: スナップショット検索 API v2 には未使用パラメータが多数あり、将来的な CLI 操作等での外部検索条件指定の下地として Get パラメータ直書きの技術負債を解消する。スナップショット API v2 を優先度高、nvapi は拡張予定なしのため横展開程度（優先度低）で実施。
- **実施内容**:
  - `nicorankLib/SnapShot/SnapShotRequest.cs` 新設（q / targets / fields / filters / jsonFilter / _sort / _limit / _offset / _context）。キーはブラケット記法のまま、値のみ `EscapeDataString`。`_context=WeeklyNicoranProgram` 追加、`_limit/_offset` クランプ。`jsonFilter` は string 経路のみで型階層は先送り
  - `SnapShotAnalyze.cs` の `REQUEST_URL` 直書き廃止。`SetRequestResult` の `flgLimit1000` 無視（常に1000制限URL）を解消し件数取得と統一。未使用 `dateTime` 引数を `flgLimit1000` に置換
  - `nicorankLib/Util/ApiUrlBuilder.cs` 新設（reviewer指摘対応。汎用クエリ組み立て＋`?`/`&` 切替）。`NicoRankiApi.requestAPI` を辞書受けに変更し文字列連結廃止、`_frontendId`・UA 定数化、genre/featuredKey パスをエンコード、`tag` は term=24h/hour 以外省略＋ログ
  - `UnitTestSnapShotRequest.cs` 9件＋`UnitTestApiUrlBuilder.cs` 7件＋境界値3件追加。計94件（75→94）
  - `specs.md`（API仕様の現実装）・`design.md`（型付き化の設計判断）に反映
- **設計判断**（詳細は `design.md`）:
  - `Replace(":null", ":0")` は温存。null→`long` 直結の `FromJson` は `JsonSerializationException` を実証済み。`long?` 化は `RegistDB` まで波及するためリスク＞効果
  - `NicoApi.cs` のID連結・`JsonReader`系パス連結・`InternetUtil` デッドコードは対象外（実害なし・変更リスク＞効果）
  - 日付逆転等のバリデーションは将来のCLI外部指定時に実施
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全94件PASS（EXIT CODE 0）、`dotnet build nicorank2019.sln` 成功・警告0
  - 全面エンコード＋`_context` の件数取得1件で実サーバー HTTP 200・`status:200` を確認
  - reviewerレビュー＋再レビュー: 必須（高・中）指摘を全解消（中1件は ApiUrlBuilder 抽出で対応）。低指摘の見送り分は理由をコミットメッセージ・design.md に記録。再レビューでマージ可判定
  - ユーザー実機確認: 修正前後の daily（2026-09-04）200ファイルをID集合比較。ファイル集合一致・AFTER空ファイル0・総件数+0.15%・共通IDタイトル変更4件のみ。上位100位は100%維持（r18除き90%）で変動は501位以降に集中＝取得時刻差のランキング変動。取得欠落なしと判定
- **残課題**: `tag` 省略ガードの実動作確認は weekly（term=week＋tag付き区分）取得時に目視するのが確実（daily は term=24h のため新旧同一条件）

---

## 2026-09-04 人気タグのタグロック補完 (#27)

- **Issue**: #27
- **ブランチ**: `feature/t27-favorite-tag-complement` → `develop`（`2fe6f3c` Merge）
- **背景**: `FavoriteTagReader` は LogOfficial.db の「人気のタグ」のみ取得していた。実利用者から「カテゴリ名とタグの重複」「ロックタグは3つだけ欲しい時と全部欲しい時がある」と要望があった。文字コード違いの出力群（SJIS / DB登録用CSV）は旧連携方式の名残で、現行は `result_DB登録用(UTF8).json` に一本化済みのため存在理由が消滅していた
- **実施内容**:
  - `NicoApi.GetLockedTags` 新設（取得専責。最新取得日行・`lock="1"` 定義順・異常時は空リスト）。`UpdateTumbInfo` と分離し他オプションの処理順に依存しない自己完結型。テスト容易性のため `UpdateTumbInfo` / `OpenDB` / `CloseDB` を virtual 化
  - `FavoriteTagReader` は件数上限を廃止し全件補完（全対象を `UpdateTumbInfo` で確保。中間集計のみ `isLocalOnly: true` で外部取得なし・キャッシュ参照のみ。週刊/SPは先行オプションが確保するため現状維持）
  - `FavoriteTags` を `HashSet`→`List` 化し挿入順を保証（人気タグ→タグロック定義順）。`Ranking.GetDisplayTags()` で出力時にカテゴリ同名タグを除外（Trim後完全一致・空カテゴリは除外なし・非破壊）
  - `NrmOutput` にタグ上限パラメータ追加（TSV系4ファイルはすべて3件。全件は `result(UTF8).csv` 最終列と `result_DB登録用(UTF8).json` のみ）。`result(SJIS).csv`・DB登録用CSV×2の生成停止（`ResultCsvRankDB` クラスは温存）
  - `using`＋`_dbCtrlOverride` 全10箇所を try/finally 化（注入分は破棄しない所有権対応。2026-06-23 の注入対応時に混入した将来の共有破壊リスクを解消）
  - 仕様変更の経緯: 当初3件上限→全件補完＋出力側制限へ転換、rankED→rank1000/rankUserNum も3件に統一（ニコランWEB管理者の意見）
- **設計判断**（詳細は `design.md`）:
  - 除外は出力側ヘルパー（`UserInfoReader` が後段でカテゴリ補完するため収集時点では未確定の動画がある。DB格納値は重複のまま許容）
  - `RankingHistory` / `NicoApi` の Open/Close は現状維持（`using` なし明示ライフサイクルのため別タスク化が適切）
  - `IsOpen` ガード統一・Trim統一は見送り（対象クラスにテストなし・出力側で吸収済み。テスト整備時に実施）
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全119件PASS（既存94＋新規25。内訳: LockedTags 6・FavoriteTagReader 9・DisplayTags 6・Output 4。EXIT CODE 0）、`dotnet build nicorank2019.sln` 成功
  - reviewerレビュー＋2回の再レビュー: 必須（高・中）指摘を全解消（注入NicoApiのOpenDB非対称・nullガード2件）。再レビューでマージ可判定。低指摘の見送り分は理由をコミットメッセージに記録
  - ユーザー実行確認: コードレビューOK・マージ指示あり（出力内容変更のため）
- **残課題**: Issue #28（ランキングJSON肥大化対策: FavoriteTag見直し＋LastResult.JSON空文字化。外部システムと協議中。今回は対応せず）

---

## 2026-09-04 プレリリース v20260904_nicorank_preview（nicorank2019のみ）

- **タグ**: `v20260904_nicorank_preview`（main `b303db3`。annotated tag が main HEAD を指すことを確認済み）
- **GitHub Release（Pre-release）**: https://github.com/n2daime/nicorank2019/releases/tag/v20260904_nicorank_preview
- **成果物**: `nicorank2019_20260904.zip`（パターンA ホワイトリストのみ: exe / exe.config / nicorank.xml.org / lib 4件 + runtimes 3種）。`nicorank_SnapShot` の配布なし
- **含まれる変更**: #25（ビルド警告対処）/ #22（NicoApi残存Clear漏れ修正）/ #19（型付きリクエスト化）/ #27（人気タグ全件補完・出力仕様変更）。#28は対象外
- **検証**:
  - `develop` で `dotnet test` 119件 PASS（EXIT CODE 0）、`dotnet build nicorank2019.sln -c Release --no-incremental` 成功・警告0（EXIT CODE 0）
  - main 上で lib 配置（4件 + runtimes 3種）・`exe.config` の `loadFromRemoteSources`・`依存ファイル/nicorank.xml` と `bin\Release\nicorank.xml` のハッシュ一致を確認
  - zip ホワイトリスト照合（不要ファイル混入なし）
  - タグが main HEAD を指すこと・`git diff main develop --stat` が空であることを確認
- **実機集計確認**: ユーザー指示で簡易化のため省略（#19 daily比較・#25週刊中間SP・#27出力確認で代替）。Releaseノートに検証フィールドは記載なし
- **備考**: ソリューションビルドの初回は `nicorank2019.exe` 常駐＋VS による `bin\Release` ロックで `MSB3021/MSB3027` 失敗。VS終了後に再実行して成功。Releaseノートは `--notes-file` 方式、冒頭にプレリリース（動作確認用）の一文あり

---

## 2026-09-05 result(UTF8).csv不要列削除（#29）

- **Issue**: https://github.com/n2daime/nicorank2019/issues/29
- **ブランチ**: `feature/t29-result-csv-cleanup` → `develop` に `--no-ff` でマージ（5e41fbd）。マージ後にfeatureブランチ削除
- **実装**:
  - `TextUtil.ReadCsv` を列番号固定switch＋`hoseiari`ハック＋`ColLmt`から、ヘッダー名→辞書の動的検出に変更。新旧両対応（旧CSVの運営・補正あり、タグなしも読める）。`ColLmt` 廃止（`LastRankCsvReader` の第3引数削除）。いいねランク/いいね数に新規対応。人気タグはOption（なければ空リスト、あればカンマ区切り）。ユーザーアイコンは旧名エイリアス対応
  - 読み取らない列: 運営2列（古すぎる）、マイリストポイントを含む補正系・ポイント内訳8列（再計算するため）。マイリストポイントは当初読取対象だったがユーザー指示で除外に変更
  - `ResultCsvRankDB.cs` 削除＋csproj参照削除＋`ModeFactoryBase.CreateOutputCSV_rankDB` 抽象とWeekly/Tyukanのoverride＋`frmMainSyukei` 列挙1行を削除（いずれもnull返却のみだったため実効出力数は不変）
  - `ResultCsv` を30列新順化（人気タグを最終列→4列目へ移動、運営2列削除、マイリストポイント23列目単独化。ヘッダー名は省略なし）
- **設計判断**（詳細は `design.md` のIssue #29節）:
  - 欠落列は既定値（数値0・総合ランク空→9999999・文字列空・タグなし→空リスト）で吸収。`PointTotal` キャッシュ（`workPointTotal`）により `LastRankCsvReader` の「当時のPointTotalを使う」動作は維持
  - 出力するが読まない列（マイリストポイント含む8列）は再計算パターンで統一。タグ往復非対称（出力はカテゴリ除外済み表示値）は旧実装から同一のため見送り
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全124件PASS（既存119＋新規5。内訳: FixtureValues・列順入替/欠落既定値・アイコン別名・ヘッダー30列完全一致・出力→読取ラウンドトリップ。EXIT CODE 0）、`dotnet build nicorank2019.sln` 成功
  - 副産物: 旧fixture `test_ranking.csv` のデータ行に空列が1つ余分にある潜在バグ（ヘッダー31列に対しデータ32列。旧テストは値検証なしで素通り）を新テストで検出・修正
  - reviewerレビュー＋再レビュー＋差分レビューの計3回: 必須指摘なしでマージ可判定。低指摘の見送り分（タグ往復非対称）は理由をコミットメッセージに記録
  - ユーザー実行確認: 実利用者の意見聴取後にマージOK（出力内容変更のため）
- **残課題**: なし（`Roundtrip` テストの `read.PointMyList == 0` は仕様の固定化。将来CSV由来の `PointMyList` を使う機能追加時は前提から見直すこと。現状そのような呼出はなし）

---

## 2026-09-05 ランキングJSON肥大化対策（#28）

- **Issue**: https://github.com/n2daime/nicorank2019/issues/28
- **ブランチ**: `feature/t28-dbversion-migration` → `develop` に `--no-ff` でマージ（51218ec）。マージ後にfeatureブランチ削除
- **背景**: #27のタグ全件補完で `LastResult.JSON`（全件シリアライズ保存）が肥大化。読み側（`LastRankReader`）は総合ランク・ポイントのみ参照のため削減可能だった。旧SP集計の残骸約11万行も残存
- **実施内容**:
  - `LastResult.JSON列をDROP`（新規INSERT除外＋Ver0移行で `DROP COLUMN`。当初は空文字化→ユーザー指示でDROPに強化。失敗時はフォールバックなしで中断）
  - 旧SP種別行を削除（`LastResult` / `LastResultInfo` 両テーブル。SPは `CreateHistory()=null`＋CSV経路のためDB不使用）
  - `DBVersion` 導入（LogOfficial / NicoranHistoryの2DBに限定。`Ver` INTEGER・Ver0開始。未記録DBはVer0から順に適用し未定義は失敗。Dailylog / ApiXMLはキャッシュ扱いで対象外）
  - 司令塔 `DbMigrationCoordinator` 新設（`nicorankLib/Util`）＋`IDbMigratable`。集計開始時（`AnalyzeAsync`・Open直後・公式DB更新前）に指示し、失敗時は中断（fail-fast）。実処理は各クラスに委譲
  - Ver0移行のDDL＋DMLはトランザクション化（VACUUMは不可のため確定後に実行。バージョン記録は成功確定後のTxn外書き込み）。FavoriteTag見直しなし（コード不変）。bat配布は自動移行で代替し見送り
  - ユーザー指摘対応：コメントと実装の乖離（Ver=0記録→逐次適用に作り替え）、復旧機構の質問→移行Txn化、前提確認の位置→ループ外＋理由コメント化
- **設計判断**（詳細は `design.md` のIssue #28節）:
  - 前提条件（テーブル存在）と移行手順を分離。ダウングレード（記録Ver＞現在値）は無変更成功。VACUUMはVer0移行時の1回のみ
  - `RankingHistory.Open` は注入済み開接続を再利用（テスト容易性。本番経路不変）
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全136件PASS（既存124＋新規12。内訳: 司令塔4・Ranking移行3・Nicoran移行5。EXIT CODE 0）、`dotnet build nicorank2019.sln` 成功・警告0
  - reviewerレビュー＋再レビュー: 必須（高・中）指摘を全解消（createRankingDateTableのRollback・Ranking前提確認）。再レビューでマージ可判定。低指摘の見送り分（二重Open・CreateDBVersionTable改名）は理由をコミットメッセージに記録
  - ユーザー実行確認: コードチェックOK（reviewer前実施）・実環境確認OK後にマージ
- **残課題**: `RankingHistory.Open` の二重呼び出し所有権・`TestDbHelper.CreateDBVersionTable` のSnapshot用スキーマ名・移行処理のMigrator分離（バージョン増加時の肥大化対策。`design.md` 将来検討に記録）。いずれも本タスクでは見送り

---

## 2026-09-05 リリース実績 v20260905_nicorank

- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260905_nicorank（タグはmain HEADを指すことを確認）
- **範囲**: v20260904_nicorank_preview → develop（#29 result csv不要列削除＋#28 JSON肥大化対策）
- **成果物**: `nicorank2019_20260905.zip` / `nicorank_SnapShot_20260905.zip` / `nicorank_oldlog_20260905.zip`（ホワイトリスト方式で作成・内容検証済み。DB・設定本体・pdb・Outputなし）
- **検証**: `dotnet restore`＋`dotnet test` 136件PASS、MSBuild Releaseビルド成功・警告0、`loadFromRemoteSources` 両config確認、`nicorank.xml` 一致（SHA256）、bin/Release lib 4件＋runtimes 3種確認。実機集計は#28・#29のユーザー実行確認でカバー
- **判明した問題**: なし（zip内容検証スクリプトの正規表現が `runtimeconfig.json` に誤検出するiskeあり。ホワイトリスト品のため問題なし。次回は `config\.json$` の前方不一致に注意）
- **同期**: main → develop を `--no-ff` でバックマージ（32ef15a）。バックマージ直後は `git diff main develop --stat` 空を確認。本エントリ追記によりdevelopが1件先行（前回リリースと同様の運用）

---

## 2026-09-06 プレリリース実績 v20260906_tagrank

- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260906_tagrank（プレリリース。タグはmain HEADを指すことを確認）
- **範囲**: v20260905_nicorank → develop（#30 タグ検索ランキングのみ）
- **成果物**: `nicorank2019_20260906.zip`（nicorank2019.exe / exe.config / nicorank.xml.org(TAGRANK節入り) / lib 4件＋runtimes 3種）に加え、最新DB作成導線として `nicorank_SnapShot_20260906.zip`（exe / exe.config / lib。コード無変更・Releaseビルド成果物をホワイトリスト方式で圧縮）を後から追加添付。いずれもDB・設定本体・pdb・Outputなし。oldlogは無変更のため添付なし
- **検証**: `dotnet test` 165件PASS、ソリューションDebugビルド＋Releaseビルド成功・警告0、`loadFromRemoteSources` 確認、`nicorank.xml` 一致（SHA256。PostBuild xcopy未反映のため手動コピーで解消）。ユーザー実行確認はタグ検索のみ（新DBで件数一致）。週間/中間/SPの回帰実行は本リリース前に実施
- **判明した問題**: なし
- **同期**: main → develop を `--no-ff` でバックマージ。バックマージ直後は `git diff main develop --stat` 空を確認。本エントリ追記によりdevelopが1件先行（前回リリースと同様の運用）

---

## 2026-09-06 タグ検索ランキング (#30)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/30
- **ブランチ**: `feature/t30-tagrank-search` → `develop` に `--no-ff` でマージ。ブランチ削除済み
- **背景**: SPモード相当の集計をテキストの動画IDリストではなく、スナップショットv2のライブ検索結果で行う。新タブ「タグ検索集計」を追加し、ポイント計算パネルは集計タブと共有する
- **実装内容**:
  - UI: 新TabPage「タグ検索集計」（1.タグ条件→2.絞り込み→3.DB・前回結果→共有係数パネル→実行ボタン）。係数パネル（`panel3`）は実体1つのままタブ切替で付け替え＋`grpDb` 基準の相対配置（AutoScaleずれ対策）。下限初期値0（0=指定なし）・種別「指定なし」・投稿日はチェックボックスONで入力可。タグ条件のEnter確定で件数確認を実行。上限超過時は実行ボタンを押せなくし、条件変更で復帰。未入力時は実行不可
  - `TagConditionParser`（`nicorankLib/SnapShot`）: `タグ1&タグ2|タグ3*` → jsonFilter（`&`=AND優先・`|`=OR・`*`なし=`tagsExact`・`*`あり=`tags`、` *`は末尾1文字のみ許可）
  - `SnapShotRequest.CreateTagSearch`: タグはjsonFilter、数値下限4種・日付・種別は `filters[]` 実証済み記法。`q` 空・`targets` 不使用。下限0・日付OFF（中立期間2000-01-01〜2100-01-01）・種別「指定なし」は指定なし
  - `TagRankAnalyze`（`Analyze/Input`）: 件数取得→5万超過時は中断通知→100件×4並列ページング→ID重複除去・ID順。`MaxTotalCount` 定数化
  - `TagRankTotalReader`（`Analyze/Option/Basic`）: 基準日DBなし専用（AnalyzeDBのみ→Total取得→MovieInfo補完→全件 `Count=Total`）。SP共用クラスに手を入れないための新設（空DBダミー案は不採用）
  - `ModeFactoryTagRank`（`Factory`）: Base有無で分岐（なし時は `BaseDay=TargetDay`）。SP相当の7種出力・履歴なし・前回CSV任意。`EAnalyzeMode.TagRank` 追加
  - `NicoRankXml.TAG RANK`＋`Config.IsTagRank`（節単位フォールバック。項目欠落があれば週間設定）＋`依存ファイル/nicorank.xml` にTAGRANK節（SP同値）
  - UI実行配線: タブ切替時の係数値 保存→切替→読込（不正値は切替中断）。集計スレッドからはコントロールに触れないため実行条件を `TagExecuteContext` に退避（クロススレッド例外対策）。係数 Load/Save 抽出＋`CALC_LIKE` 保存漏れ修正（従来は表示のみ）。`GetModeFactory` null・`CreateAnalyzer` false のガード追加
- **設計判断**（詳細は `design.md` のIssue #30節）: 入力のみ差し替え・差分以降はSP流用。数値・日付・種別は `filters[]` 実証済み記法（jsonFilterのrangeは未検証のため回避）。5万規制は件数取得で中断方式。TAGRANK節は節単位切替・OFFSET系は共通
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全165件PASS（既存136＋新規29。EXIT CODE 0）、`dotnet build nicorank2019.sln` 成功・警告0
  - reviewerレビュー×2＋再レビュー: 中4件（`*`位置検証・TAGRANK部分欠落NRE・Query null防御・CreateAnalyzer戻り値無視）を修正、再レビューでマージ可判定。低指摘の見送り分は理由をコミットメッセージに記録
  - ユーザー実行確認: 新DBでタグ検索件数と集計対象件数がほぼ一致することを確認後にマージ
  - 別件切り分け（実装なし）: 当初900→90件不一致は集計実装ではなくDB側の問題と特定。2024-08-30〜2026-09-04の取得コード不具合（`flgLimit1000` 無視の常時1000制限）で作られたDBは全期間1000以上のみ。`再生数 < 1000` が0件なら旧DB。再取得で解消確認済み（低再生行1008313件）
- **残課題**: `AnalyzeAsync` の無条件「集計成功」ログ・`TagRankAnalyze` 0件成功の扱い・辞書式ID順・`_tagMockLoaded` 命名（いずれも別Issue化推奨。低指摘見送り分）
- **リリースノート原稿**（リリース時に転記）:
  - タグ検索集計を使う方へ：スナップショットDBは最新版を推奨（旧DBは総合・SP集計に支障なし。タグ検索には不十分）。目安：`SELECT COUNT(*) FROM Ranking WHERE 再生数 < 1000;` が0件なら旧DB（2026-09-04より前の取得分）
  - トラブルシューティング（件数不一致時）: 1. 1000再生フィルタ（1年超の古動画は1000以上のみ収録が仕様）2. DBの鮮度（作成日より後の投稿は未収録）3. DB作成時のエラー（前回と比べファイルサイズが著しく小さい場合は再取得）

---

## 2026-09-07 LogOfficial.db肥大化の調査(編集部向けレポート作成)

- **Issue**: なし(docs・調査のため不要)。**ブランチ**: `develop` 直(コード変更なし)
- **背景**: 7年運用で `LogOfficial.db` が12GB超。SQLite仕様上の限界ではないが実用上の受け渡し・バックアップが重い。「問題になる前に」改善ポイントを知るため、Rankingテーブル参照処理のうち集計日期間の指定がない(≒古いデータ削除で壊れる)箇所を洗い出す調査
- **実施内容**:
  - `docs/knowledge/` 読了＋explore×2並列でRanking参照SQLを全件洗い出し。過去無制限遡りは `RankingHistory.cs:220-223`(`CheckSoMovieNeedSabun`)のみと特定。他はID点照会＋`LIMIT 1`か約7営業日の `BETWEEN`
  - 実DB(`T:\集計プログラム仮\DB\LogOfficial.db`)を `mode=ro` 読取り専用で実測(書込みなし): 11.31GB・2646日・Ranking全1億4107万行/1年超1億1666万行(82.7%)・so distinct 275,657・so行1020万行・Movie 233万行(実データ約121MB)・`PRIMARY KEY(ID,集計日)` あり・全問合せインデックス経路・freelist=0・WAL
  - so全行走査で休眠ギャップ実測: ギャップ持ちso 55,925件(約20%)・イベント累計70,701件・直近1年終了分21,780件・最大2617日。ユーザーの運用知見(公式動画の再公開は日常的。いわゆる見るタイツ動画問題)を裏付け、当初の「稀」想定を撤回しSoHistory併設必須に切替え
  - 壁打ちで認識整合: 推奨=①1年保持＋SoHistory併設(live約2GB維持)、比較=②何もしない(年2GB増・3年後約17GB・便益ほぼなし)。Movie約200MBは対象外
- **設計判断・運用決定**(編集部相談用レポートはリポジトリ管理外のため要点のみここに残す):
  - 運用方式: 年1回手動ではなく、毎回の更新動作時に古いデータの自動削除・SoHistory更新。初回集計時に初回実行
  - ロールバック保険: 対策版リリース前に2daime管理のNASへ旧DBを温存
  - 人気タグ窓の受容: 年間ランキングの取得窓が1年に切り詰められるが、公式タグ未更新＋固定タグ代替(#27)実装済みのため影響ほぼなし
  - `RankingDate` は削除対象外: 更新再開位置のしおりであり、消すと2019年からの全期間再取得になる。メンテ日判定にも使用。2646行・数十KBでコストは無視できる。来歴は `d7e5070`(2024-06サイバー攻撃対応)で新設、メンテ列は新設時から同梱、旧来はRanking直MAX取得だった
- **検証**: コード変更なしのためビルド・テスト対象外(MUST 5はユーザー承認で省略)。測定クエリはすべて読取り専用で実DB無改変を確認。施策前後の差分検証(対策前後2環境で1ヶ月間週刊比較後にリリース)は2daimeが実施予定
- **残課題**: メンテナンスタブ「DBの最適化」追加予定(断片化対策)。実施フェーズ(Issue化・SoHistory設計・prune手順の開発検証)は別タスク化予定

---

## 2026-09-12 タグ検索v2最新値オプション (#35)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/35
- **ブランチ**: `feature/t035-tagrank-live-counter` → `develop` に `--no-ff` でマージ。続けて `feature/t031-logofficial-prune-sohistory` へも取込（t031のdevelopマージ判断には影響させない）。マージ後にfeatureブランチ削除
- **背景**: リアルタイムに結果だけ知りたい時にSnapshotDB（20070306からの全期間全動画フルスナップショット）の取得は時間・データ量とも過剰なため、集計日にv2最新値の選択肢を足す
- **実装内容**:
  - UI（ユーザー担当）: `chkUseLiveCounter` をgrpDbに配置・既定TRUE。判定・退避・Enable切替配線は後工程で実施。Designer全体が環境差で再生成されたため表示確認はユーザー実行確認に委ねた
  - `TagSearchQuery.UseLiveCounter`（既定false。唯一の切替。`SetInputFile` の引数は不変）
  - `TagRankAnalyze.LiveCounters`（ID→4数値。従来捨てていた `DefaultFields` のカウンタを保持。`CollectContentIds`→`CollectContentData` に変更。重複は先勝ちでコメント明記）
  - `TagRankLiveTotalReader`（基準なし。DB不要）・`TagRankLiveSabunReader`（基準あり。Target=ライブ・Base=基準日DB。新着救済はSabunReaderと同一）新設。純粋処理 `ApplyLiveTotals` はstatic共用
  - `ModeFactoryTagRank` を4分岐化（SnapshotDB×基準あり/なし＋v2最新×基準あり/なし）。v2時の集計日は実行日
  - frmMain配線: `TryBuildTagSearchQuery` 退避・ライブON時のAnalyzeDB存在確認スキップ・`frmMain_Load` でのCheckedChanged配線＋初期Disable（Designer再生成差分を避けコード側で実施）
- **設計判断**（詳細は `design.md` のIssue #35追記）: Reader分離・Input共有参照（Input内完結案は肥大化のため不採用）。`RankingAnalyze` のInput→Option順序を前提とし事前条件をremarks明記。ポイント係数見直しは `TAGRANK` 節の運用調整に分離
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全173件PASS（既存165＋新規8。内訳: LiveCounters保持1・ApplyLiveTotals対応付け1・Open成否4・工場分岐2。EXIT CODE 0）、`dotnet build nicorank2019.csproj` 成功
  - reviewerレビュー＋再レビュー: 中1件（無効欄旧値ブロック→存在確認スキップ）・低4件（GetBaseTime改名・GetMovieData要約・先勝ち明記・事前条件remarks）を解消し再レビュー通過。工場の基準DBあり成功系を外した理由はテスト内コメントに記録
  - ユーザー実行確認: エラーなく実行＋当日SnapshotDB版との比較検証。188行vs187行で共通187件の値列完全一致、差分は `sm43925516`（再生737の低再生古動画。旧DBの1000再生足切りに該当）1件の有無による順位+1シフトのみ。LiveCountersとSnapshotDB値の等価性を実証。specs.mdに1件出入りの旨を追記
- **残課題**: DB以外（登録用JSON・前回CSV）を基準にする案は検討の結果不採用（差分にはDBが必要と結論。新Issueなし）。`TagRankLiveSabunReader` 差分分岐の純粋関数切り出しは将来検討（reviewer任意指摘）

---

## 2026-09-13 順位計算の同点時タイブレーク (#34)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/34
- **ブランチ**: `feature/t034-ranking-tiebreak` → `develop` に `--no-ff` でマージ（c25790d）。続けて `feature/t031-logofficial-prune-sohistory` へも取込（9d09bb0。t031のdevelopマージ判断には影響させない旨を明記。#35と同一方式）。マージ後にfeatureブランチ削除
- **背景**: #31対策の前後比較（初週・2026-09-08）で前回順位が2件だけ1ずつずれた。今週の計算は1000/1000完全一致で、ずれは先週時点で発生していた（同点で順位だけ±1）。原因は `RankingAnalyze.calcRanking` が単一キー降順＋連番のみで、同点時の順序が入力順依存（並列取得のため不定）だったため。本来#34は#31比較終了後に着手する建前だったが、順位が安定しないと比較自体がやりにくいため両ブランチへマージした
- **実装内容**:
  - `RankingIdComparer` 新設（`nicorankLib/Analyze/model`。`IComparer<string>`・ステートレス共有Instance）。種別（先頭の非数字部。sm/so等）→数字部の順に比べ、数値化の有無で群を分けてから群内で辞書式比較する（直接フォールバックすると推移律が崩れ sm10・sm10a・sm9 の循環になるため。reviewer指摘対応）。桁判定はASCIIの0〜9限定（`char.IsDigit` のままだと全角数字の扱いが `TryParse` とずれるため）
  - `RankingAnalyze.calcRanking` の6種（総合・再生・コメント・マイリスト・いいね・カテゴリ）に `.ThenBy(ID, RankingIdComparer.Instance)` を追加。順位値は連番維持（同順位スキップなし）
  - 並列ソート前に全件の `PointTotal` を単一スレッドで確定させるウォームアップを追加。`Ranking.CalcPoint` のキャッシュ（`workPointTotal`）はスレッドセーフでなく計算途中の部分値を書き込みながら進めるため、6タスクの並列初回計算が重なると別タスクが部分値を読んで順序が不定になる既存の競合が、単体テスト全件実行で1回だけ発覚した（カテゴリ順位ずれ）。`CalcPoint` 自体へのロックは範囲拡大のため見送り
  - `nicorankLib.csproj` にCompile1行追加（old-styleのため手動登録が必要）
- **設計判断**（詳細は `design.md` のIssue #34節）: 第二キーはIDのみ（投稿日・再生数も同点があり得るため）・6種すべて・連番維持（差分最小）。辞書式ではなく数値認識（辞書式では sm199 が sm20 より先になる桁違い逆転があるため）。Comparer1個にまとめる（ThenBy二次比較子は同点時にだけ呼ばれるため処理コスト最小）
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全182件PASS（既存173＋新規9。内訳: 数値順2・種別順1・非数値フォールバックと群分離1・境界（空文字・前ゼロ）1・null/同一1・総合数値順1・入力順反転の決定性1・副順位数値順1。3回連続PASSでflaky解消を確認。EXIT CODE 0）、`dotnet build nicorank2019.sln -c Release --no-incremental` 成功・警告0
  - reviewerレビュー＋再レビュー: 高・中なし。低4件（推移律・IsDigit・副順位assert・境界テスト）を全対応し、追加で既存競合のウォームアップを実施。再レビューでコード問題なし・ドキュメント2件のみ指摘→修正済み（再レビュー不要と判定）
  - ユーザー実行確認: 実集計の `rank1000.txt` 修正前後比較。1000行・27列・ID集合は完全一致、値列の差分は0セル（順位以外の全列がIDごとに完全一致）。差分は6種の順位列のみ（総合42・カテゴリ8・再生78・コメント525・マイリス905・いいね552。マイリス等が多いのは同点群が巨大なため。例：マイリス数は176種類しかなく「10」が33件）。修正後ファイルは6種すべてで主キー降順＋ID数値認識順に完全一致（違反0件）
  - t031取込時は `docs/design.md`・`docs/knowledge/testing.md` がコンフリクト（隣接セクション追加同士）。両方残す形で解消し、`testing.md` はt031側のSoHistory15件と#35系8件と#34系9件を合算して197件に更新。`docs/tasks.md` の自動マージは#35完了状態（履歴行あり）を優先し、t031側の旧#35未完了節は解消済みとして扱った。取込後にt031上で全197件PASSを確認
- **残課題**: なし。`CalcPoint` のロック化・ `MergeRankingList` の辞書順序の確定化は、現状ウォームアップとタイブレークで決定的になるため見送り

---

## 2026-09-14 SnapShot Linux対応CLI (#37)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/37
- **ブランチ**: `feature/t037-snapshot-linux-cli` → `develop` に `--no-ff` でマージ。続けて `feature/t031-logofficial-prune-sohistory` へも取込（t031のdevelopマージ時競合の事前回避。#34・#35と同一方式）。マージ後にfeatureブランチ削除。プッシュはユーザー指示待ち
- **背景**: nicorank_SnapShot（スナップショット取得ツール）を Linux（NAS・x64・.NET 8 ランタイムあり）で動かす。取得中核は nicorankLib/SnapShot に分離済みで、調査の結果 nicorank.xml・DB/ フォルダは取得単体では不要と確認したため、WinForms を持たない net8 CLI の新設で足りると判断した。WinForms 部分（開始ボタン・サスペンド・TaskDialog）は Linux 版に持ち込まない
- **実装内容**:
  - `nicorank_SnapShot.Cli` 新設（net8.0・SDK-style・top-level statements）。`SnapController.GetSnapShotAsync()` を呼び、終了コードは 0=成功/2=エラー（`nicorank_oldlog` と同一規約）。`--help` のみ取得せず終了する
  - `SnapController` の失敗検知3件修正（#37レビュー指摘対応）。`InitilizeDB()` 失敗時は早期確定、`RegistDB()` 2か所（途中・最終残件）は失敗を記録しつつ続行（被害最小化）、catch の例外時は `false` を返す（従来は成功扱いだった）。いずれも Windows 側のエラー表示が正しくなる方向の変更
  - パッケージは `Microsoft.Data.Sqlite 10.0.11` / `Newtonsoft.Json 13.0.4`（UnitTest と同版。pitfalls 4f の混在回避）・`System.Text.Encoding.CodePages 8.0.0`（oldlog と同版）。Costura は使わない。起動直後に `CodePagesEncodingProvider` を登録する（登録呼び出しは oldlog になく CLI で追加）
  - `nicorank2019.sln` に登録（oldlog と同じ SDK-style 種別・AnyCPU マッピング）。既存 net48 WinForms は Windows 用として残す
  - nicorankLib 全体の net8 化は範囲が広すぎるため見送り。`InternetUtil` の HttpClient 化も段階移行として別タスク化。oldlog の Newtonsoft 13.0.3 は稼働実績構成を変えないため見送り
- **設計判断**: 案Aハイブリッド（net8 から net48 ライブラリを参照。Linux 稼働実績のある nicorank_oldlog と同じ形）。`RankingHistory` の `MessageBox` は SnapShot 経路から到達しないため初回は不問とし、実際にハイブリッド参照でビルドが通ることを確認して確定した
- **検証**:
  - `dotnet build` 成功、`dotnet test UnitTest/UnitTest.csproj` 全182件PASS（EXIT CODE 0）。develop マージ後も182件PASS、t031取込後は197件PASSを確認
  - reviewerレビュー＋再レビュー: 中3件（InitilizeDB/RegistDB 戻り値無視・CodePages版差）・低4件（例外時の画面出力・csprojコメント・apps.md表現・Newtonsoft微差）を全対応または理由付き見送りし再レビュー通過（問題なし・マージ可）
  - NAS実機（DS224）で portable publish（`-r` なし）を配布し `--help` 終了コード 0 を確認。続けて実取得を実行し `LogSnapshot_20260914.db`（415MB・8,997,750行・低再生993,261行・integrity_check ok・DBVersion=20260914/1.0.1.0）の正常性を確認。日付表示の `M/d/yyyy` は NAS ロケールによる `ToShortDateString` の差であり仕様通り
  - t031取込時はコンフリクトなし（ort 自動マージ）。`docs/tasks.md` は develop 側の#34完了状態（履歴行あり）を優先し、t031側の旧#34未完了節は解消済みとして扱われた。`docs/tasks/archive.md` の#34追記（46d40e8）も t031 が取り込んでいなかった分として一緒に取り込まれた（ドキュメントのみで t031 の develop マージ判断に影響しない）
- **実装ノウハウ（Linux 配布・運用）**:
  - 配布は `-r` なし portable publish＋`dotnet xxx.dll` 実行に決定。`-r linux-x64` 付き publish では NuGet 由来の `runtimes/linux-x64/native/libe_sqlite3.so` が出力から落ち、win 用だけが残って Linux で動かないことを実確認した（`-r` なしなら全 RID 同梱で linux-x64 を含む）。出力直下の `lib/`（win 用 DLL 群）は net48 参照元から流れ込む残骸であり Linux では無視される
  - `CodePages.dll` は共有フレームワーク提供のため publish 出力に含まれず、8.0.0 と 10.0.11 の競合は起きない（project.assets.json で確認）
  - コピー対象は `.exe`・`.pdb`・`lib/` 以外の全部。`runtimes/` は `linux-x64` のみ残して他は削除可（約35MB→約2MB）。`nicorank_SnapShot.Cli.runtimeconfig.json` は必須（コピー漏れに注意）。プログラム群は読取のみ、出力先フォルダに実行ユーザーの書込権限が必要
  - 同一日は出力ファイル名が同一で `InitilizeDB` が削除→再作成するため、同時実行は DB 破壊につながる。月1日・毎週月曜の2タスク運用では `flock -n` で重複時スキップ（同一処理のため実害なし）、スクリプトは `set -euo pipefail`＋終了コードゲート＋`wal_checkpoint(TRUNCATE)` 後の移動（`mv`。作業側に旧DBを残さないため glob が曖昧にならない）とする
- **残課題**: Linux 実機での定期タスク化はユーザー運用側で継続（ロック付きスクリプト・月1＋週1の2タスク構成）。取得物の月次アーカイブ先は `/volume1/nicoran/Snapshot/2026/`

---

## 2026-09-21 LogOfficial.db肥大化対策 (#31)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/31（検証結果を追記してクローズ）
- **ブランチ**: `feature/t031-logofficial-prune-sohistory` → `develop` に `--no-ff` でマージ（23f2abe）。#37は別途先行マージ済み（ce81623）のため本差分は#31のみ。tasks.mdコンフリクトはdevelop側の#37完了状態を優先し#31節を残す形で解消。マージ後にfeatureブランチ削除。プッシュはユーザー実施
- **背景**: 7年運用で `LogOfficial.db` が12GB超（Ranking全1億4107万行の82.7%が1年超）。2026-09-07調査で過去無制限遡りは `CheckSoMovieNeedSabun` のみと特定し、1年保持＋SoHistory併設の方針に決定した
- **実装内容**:
  - Ver1移行（`DbCurrentVersion` 0→1）: SoHistory作成＋保持境界より古い行の最新を初期退避＋境界以降の混入行清掃＋古いRanking削除＋Movie廃止＋初回のみVACUUM。退避は日次と同じ条件付きUPSERT（`WHERE excluded.集計日 > SoHistory.集計日`）に統一し、中断再開時のcutoffずれでも最新1件に収束する（reviewer低指摘対応）
  - 日次 `RefreshSoHistoryAndPrune`: 消える行を拾ってから削除し、同日次トランザクションに同梱する（VACUUMなし）。当日分の上書きはしない（当日分はRankingに残るため不要で、置き換えると基準日より新しい値になり再公開チェックが効かなくなる）
  - `CheckSoMovieNeedSabun` はRanking優先・なければSoHistoryを見る2クエリ逐次。両方になければ新着扱いし、表なし旧DBでも正常終了する。問合せは `集計日 <= @Date` ガード付き。JOIN・VIEWの1本化は見送り
  - `GenreAnalyze.cs` 削除＋csproj参照削除＋Movie書込みブロック削除。SPAnalyze側の同名Genre SQLは残置（#31範囲外のため別タスク化）
  - 保持境界はDB最大日基準（`MAX(集計日)-365日` 未満削除）。prune用索引 `idx_Ranking_集計日` を恒久化
- **設計判断**（詳細は `design.md` のIssue #31節）: SoHistoryはID＋集計日＋4数値のみ（タグは差分に使わず別経路のため含めない）。最新1件のみ保持し全履歴は持たない（差分元の用途には十分）。SoHistoryの日付は常に保持境界より古い不変条件により、無制限履歴の「基準日以前の最新行」と同じ結果になる
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj -c Release` 全197件PASS（既存182＋新規15。EXIT CODE 0）、`dotnet build nicorank2019.sln -c Release` 成功。ビルド1回目は起動中のnicorank2019.exeがbinをロックしMSB3021/3027で失敗したため、exe終了後に同条件で再実行し成功を確認
  - reviewerレビュー: 総合判定マージ可。低8件のうち4件対応（Backfill条件付きUPSERT統一・db.md現行Ver1・nicorankLib.mdフォールバック追記・specs Ver1明記）、4件見送り（SPAnalyze.GenreSQL削除・SoHistoryExistsキャッシュ・追加テスト3件は別タスク化。理由はコミットメッセージに記録）
  - 実DB破損対応: 2026-09-08試行で `SQLite Error 11: database disk image is malformed`（Ranking本体ページ2468645〜2508875帯・約100件）を確定。rowidチャンク＋row-by-row救出で再建し、バックアップから最新仕様で通し再実行（約3時間）。結果は `LogOfficial.db` 2.01GB・integrity_check ok・Ranking 24400035行・SoHistory 244852行・週刊出力一式正常
  - 対策前後2環境比較: 9-08週は総合1000/1000一致・前回2件±1（同点帯の先週持ち越し）。9-21週は件数・ポイント一致で順位のみ差（マイリスト等）があり、原因は修正前exeがタイブレーク導入前（#34は9-13導入）の古いビルドだったと確定。#31起因の揺らぎなしとして検証終了し、1ヶ月比較を待たずユーザー指示でマージした
- **残課題**: 見送り分の別タスク化（SPAnalyze.GenreSQL整理・SoHistoryExistsキャッシュ検討・追加テスト3件）。#32メンテナンスタブ「DBの最適化」（VACUUM）は#31完了後に着手

## 2026-09-23 Snapshot API v2更新チェック(#38)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/38
- **ブランチ**: `feature/t038-snapshot-version-check` → `develop` に `--no-ff` でマージ（ca5f3a0）。developの#31取込を事前にfeature側へマージし競合1件（testing.md件数）を214件に統合
- **背景**: Snapshot API v2のデータ更新時刻が後ろ倒し傾向にあり、前日データでLogSnapshot DBを作ると後段集計の差分がずれる。公式のversionエンドポイント（`.../snapshot/version` → `{"last_modified": "..."}`）で取得前に更新有無を判定する
- **実装・検証**:
  - `SnapShotVersionChecker`（3値判定・JST日付比較・`DateParseHandling.None`での日時パース）・`SnapShotVersionPoller`（5分×最大1時間待機・時計/待機は注入可）を新設。取得実行とは分離し、事後動作（ダイアログ/リトライ）は呼び出し側に委ねる
  - WinForm（Form1）: 未更新時に日時入りOK/キャンセル確認ダイアログ、確認不能時にエラーダイアログ。チェックは`await Task.Run`化（UI凍結回避・reviewer中指摘）
  - CLI（nicorank_SnapShot.Cliのみ）: 未更新/確認不能の間リトライ、更新検知で通常取得（終了コード0）、1時間経過も未更新なら取得せず終了コード2（`InitilizeDB`に触れない。DBエラーではなくリトライタイムアウトであることをnicorankerr.logに記録）
  - `LastModifiedRaw`は値そのものを保持（本文全体ではない・reviewer中指摘）。CLIの二重出力解消・InvariantCulture指定（reviewer低指摘）。Pollerのsleep上限化は見送り（精度不要のため）
  - 実装中に単体テスト8件失敗で発覚：`JObject.Parse`既定ではISO日時がDateトークンに化けてオフセットが落ちる。`DateParseHandling.None`で文字列のまま`DateTimeOffset.TryParse`に回す（pitfalls項目22）
  - `dotnet test UnitTest/UnitTest.csproj` 214件PASS（#38で17件追加）・両アプリビルド成功（EXIT CODE=0）
  - reviewerレビュー→必須2件修正→再レビュー問題なし（残った低2件も整理済み）
  - NAS実機検証（9/21）: 06:37開始→8回未更新→07:14更新検知→全期間取得→終了コード0。WinFormはユーザー実行確認済み・問題なし。タイムアウト経路の実機テストは省略し単体テストで代替
- **調査（仮説検証）**: 9/13の「8時過ぎに件数増加」記憶とlast_modified（7時台）のズレを調査。versionと実データ件数の同時測定を4朝実施し、切替時刻は07:08/07:14/07:09/07:07と日々バラつくこと（単調長期化ではない）、いずれも実データ段差と一致すること（last_modifiedは信用できる）を確認。specsに実測注記・pitfalls項目23（本取得タスクとの競合注意）を追記。調査スクリプト自体はマージ前に履歴から除去（`.gitattributes`のLF固定ともども除外。動作版はNASに配置済み）
- **残課題**: Issue #39（タグ検索v2最新値モードのデータ時点表示）は別タスクとして継続

---

## 2026-09-26 ApiXML由来の削除判定を順位に影響させない（#40）

- **Issue**: #40（本体）、#41（当初は案Aの将来検討。後に週刊ApiXML蓄積マージへ方向転換）、#42（集計後のBasicOption破棄経路。レビュー残課題から分離）、#43（ライブラリ層の直接コンソール出力の整理。別セッション対応）
- **ブランチ**: `feature/t040-apixml-no-rank-effect` → `develop` に `--no-ff` でマージ。ブランチ削除済み。プッシュはユーザー指示待ち
- **背景**: ApiXML.dbは表示用キャッシュのはずが、取得失敗時にisDeleteを立てるため取得タイミング次第で順位全体がずれていた。同点タイブレーク（#34）と同様、集計タイミングで結果が変わるのは本来の意図ではないため是正した
- **実装内容**:
  - `NicoApi.GetUserInfo` / `GetMovieInfo` からisDelete代入4箇所を除去し、失敗時は空欄・既定値のまま残す。`SELECT XML` を最新取得日に統一した
  - `MovieInfoReader` / `GenreInfoReader` / `UserInfoReader` / `FavoriteTagReader` は確保・読取の失敗でも集計中断しないようにした
  - `SpMovieInfoFallback` を新設し、SPの欠落を案B（LastResult最新タイトル・LogOfficial期間内初見日）で補う。タイトル検索は種別=Weekly優先の2段引きにした。初見日は除外に使わない方針をコメント・designに明記した
  - 週刊の事前取得としてoldlog週刊保存時に全ID約26000件を一括取得して日付フォルダへApiXML.dbを置き、2019側の週刊JSON取得後に取り込む（新しい取得日だけ置き換え）。並列数はconfig.jsonのnicoapi_thread_maxで管理し（ThreadMaxOverride）、nicorank.xml依存をなくした
  - 情報欠落が残る場合はタイトル欄先頭に【集計後削除】を付ける（列追加なし）
  - oldlogのSQLite不足（batteries_v2未配置）は参照側への直接パッケージ参照＋Costura除外で解消した。進捗表示は\r上書き＋リダイレクト間引きに改めた
  - `UnitTest`20件追加（計234件）。既存の「確保失敗時はfalse」テストは新仕様に合わせて更新した
- **検証**:
  - `dotnet test` 234件PASS・ソリューション全体とoldlog Releaseのビルド成功（EXIT CODE=0）
  - reviewerレビューで中4件・低6件の指摘を受け、全件対応（低2件見送り）後に再レビューで総合判定マージ可になった
  - 週刊の実機検証で26770件の取込と取得2372件への削減を確認し、運搬ファイルあり・なしの両経路が動作した。SPの実機検証はエラーなしで集計でき、マーカー0件は正常（欠落が稀なため）
- **実行確認時の不具合と対策**: oldlog初回実行でbatteries_v2不足・nicorank.xml不在・配置ミス（新旧DLL混在のMissingMethodException）が出た。いずれも修正し、再発防止をpitfalls項目4g・4hに記録した。週刊JSONのリアルタイム変動による未取得分は来週月曜の同期後に再確認し、問題があれば別Issue化する
- **残課題**: #41は週刊ApiXMLの蓄積マージによる長期キャッシュ再構築へ方向転換し、#32完了後に2019側メンテナンス機能として追加する。#42・#43は別タスクとして残置する

---

## 2026-09-26 メンテナンスタブのUIモック (#32・32.1)

- **Issue**: #32（OPEN維持。32.2の中身実装が残るため閉じない）
- **ブランチ**: `feature/t032-maintenance-tab` → `develop` に `--no-ff` でマージ。ブランチ削除済み
- **背景**: #31の日次pruneではVACUUMしないため、手動最適化の置き場所が必要になった。いきなり実装せず、UIモック（見た目だけ・完全Dead）でレイアウトを固めてから中身を作る進め方にした
- **実施内容**:
  - `tabPageOut` に第3タブ `tabPageMaint`（表示名「メンテナンス」）を追加。`grpVacuum`（対象4DBのチェック既定ON・実行前後2列サイズ欄・実行ボタン・状態＋進捗・注意文）と `grpFutureApiXml`（#41予告・場所確保だけ・操作部は無効化表示）を縦積み。ログ欄はなし（集計タブと同様にコンソール側へ出す運用）
  - 実行ボタン `btnVacuumExec_Click` は未実装MessageBoxのみでDBに触れない。`panel3` 付け替え・集計モードの logic に触れない
  - ユーザーが配置微調整と#41予告文面の3行化を行い、第2コミットとして確定した
- **検証**:
  - 調整後の最終状態で `dotnet test` 234件PASS・nicorank2019ビルド成功（EXIT CODE=0）
  - ユーザーが実機で見た目を確認してOK。UIのみ変更のためreviewerレビューは省略（ユーザー指示。コミットメッセージに記録）
- **残課題と判断**:
  - 32.2で中身（VACUUM実行・サイズ取得・非同期化・specs/design反映）を通常ワークフローで実装する
  - #41の定義はラベル文面が最新意図（SP集計で基準日以降の取得済み動画はAPIを叩かず、手元のDB欠落時はoldlog取得分マージで復元）。#41着手時にIssue本文をこの定義で更新する
  - 文書内では「本地」を使わず「手元のDB／ローカルのDB」と書く（ユーザー指摘）

---

## 2026-09-26 メンテナンスタブのDB最適化実装 (#32・32.2)

- **Issue**: #32（クローズ。本文＋コメント2件を要求として確定）
- **ブランチ**: `feature/t032-maintenance-vacuum` → `develop` に `--no-ff` でマージ。ブランチ削除済み
- **背景**: #31の日次pruneではVACUUMしないため、手動最適化の置き場所が必要になった。要求は2026-09-08の壁打ちコメント（DBごとのprune手順）にあり、DBごとに処理が異なる
- **見落としの経緯と作り直し**: 初版はIssue本文の「VACUUM」だけを見て壁打ちコメントを見落とし、素のVACUUM実装＋レビュー3回でマージ可まで進めた。ユーザー指摘のgrep（DROP TABLE IF EXISTS IDConvertが全コードに存在しない）で発覚し、DbOptimizer全面書き換え＋テスト境界追加＋文書修正として作り直した。初版のUI配線・同時実行ガードは流用し、公開I/Fの後方互換を保った。再発防止としてAGENTS.mdに「Issueのコメント全件読み・仕様メモのtasks.md転記」を追加した
- **実施内容**:
  - `DbOptimizer`（Util・static）。DBごとにDROP→DELETE→VACUUM。ApiXMLはDROP IDConvert＋1年以上未更新行削除、Dailylogは全行削除（DROP禁止）、NicoranHistoryはWeekly・1001位以下・1年以上前だけ削除（SP削除なし・LastResultInfo不変・種別パラメータ化）、LogOfficialはVACUUMのみ
  - 1年前境界は実行日起点のローリング計算（yyyyMMdd整数）。1000位ちょうど残す・1年前当日はNicoranHistoryのみ含む（元のSQLの書き分け通り。ApiXMLは当日残す）
  - 取得日はINTEGER列のため文字列バインドも整数比較になる。列型変更時は比較を見直すこと（コメントに明記）
  - コメントアウトconvertMovieIDを除去（CONVERTID_API_URLはlive使用のため残す）
  - UIは32.1確定のまま。非同期実行＋両方向の同時実行ガード（実行中フラグで条件編集時のボタン復活も抑止）。不在はスキップ表示、失敗はそのDBだけ失敗表示で残りを続ける
  - `UnitTest`10件追加（計244件）。境界（1000位・当日・他種別・設定XML不変）・冪等・表保持・CWD sandboxで決定性を担保
- **検証**:
  - `dotnet test` 244件PASS・nicorank2019ビルド成功（EXIT CODE=0）
  - reviewerレビュー計5回。初版3回（同時実行ガード等）＋作り直し2回（境界・根拠正確性等）。最終総合判定マージ可
  - 見送り3件：タグ集計同士の相互ガード（既存挙動で範囲外）・パス定数一本化（波及大）・GetDefaultTargets辞書化（過剰）。理由はコミットメッセージに記録
  - ユーザー実機検証OK（バックアップ推奨の注意付き）
- **残課題**: #41はラベル文面が最新意図（SP集計で基準日以降の取得済み動画はAPIを叩かず、手元のDB欠落時はoldlog取得分マージで復元）。#41着手時にIssue本文をこの定義で更新する。文書内では「本地」を使わず「手元のDB／ローカルのDB」と書く

---

## 2026-09-26 タグ検索v2最新値のデータ時点表示＋OFFSET節別化 (#39)

- **Issue**: #39（クローズ。本文＋2026-09-26コメントを要求として確定。ユーザー回答で文言・OFF時扱い・取得方針・中断扱い・全OFFSET節別化を確定）
- **ブランチ**: `feature/t039-tagrank-timestamp-offset` → `develop` に `--no-ff` でマージ（a99a4ce・cd67677・da3d209の3コミット）。ブランチ削除済み
- **背景**: v2最新値モードは集計日DBを使わないため画面上にデータ時点が残らず切り分けができなかった。参照インデックスは日次更新のスナップショットであり表示すべき値は `last_modified` が正しい。別件でタグ検索の `MYLIST_OFFSET` 変更が週刊・SPに波及する問題があり、同Issueのコメントで節別化が要求された
- **実施内容**:
  - `lblTagSnapshotTime`（grpTag内・件数近傍）。ON時の件数確認成功後に `MM/DD 05:00 時点のスナップショットで集計` と出す。日付は `last_modified` のJST日・時刻は05:00固定（反映完了時刻との混同防止）。OFF時・未確認時・条件変更時は非表示。確認不能時は件数確認自体を失敗扱い（null返却）にして集計に進めない。取得は `CheckTagCountAsync` 内で件数成功後に直列・`await Task.Run` でUIブロックなし。整形は `TagSnapshotTimestamp` に分離（Format・TryFormat・DataHour/DataMinute定数）
  - `SP`／`TAGRANK` 節にOFFSET4種（COMMENT/MYLIST/PLAY/POINTALL）を任意要素として追加。読み取りは項目単位フォールバック（節内→共通）、書き込みはモード別の節へ（なければ共通値を引き継いで生成）。`Load`／`Save` 呼び出し側は不変。集計中は `tabPageOut` 無効化でモード変化を防ぐ。`Initilize` で4種の既定生成（既定値は定数集約）
  - 配布テンプレート（依存ファイル/nicorank.xml）のTAGRANK節内OFFSET4種をすべてMode 0（補正なし）に変更（ユーザー指示）。既存環境の手元XMLは節内OFFSETなしのため共通フォールバックで動作不変。新規配布のみタグ検索が補正なしになる
  - GUIのパネル変更は `nicorank.xml` ファイルに書き戻されない（従来通りメモリ内のみ。永続化は手編集）。`GetXMLString` はDBの設定スナップショット用途のみ
  - `UnitTest`16件追加（計260件）。時点整形・Config節別化の境界・フォールバック・setter書込先を含む
- **検証**:
  - `dotnet test` 260件PASS・sln Releaseビルド成功（EXIT CODE=0）。一度だけReleaseビルドが実行中exeのDLLロック（MSB3027/3021）で失敗したが、アプリ終了後の再実行で成功。コード要因ではない
  - reviewerレビュー計3回。初回は中1件（setter非対称）＋低4件で要修正、対応後に再レビューでマージ可。残低4件（定数集約・文書整合・テスト1件）も対応して計260件にした。3度目のレビューは高・中指摘なしのため省略
  - ユーザー実機検証OK（アプリ起動中のまま確認し、指摘は配布XML見本とテンプレート全0化のみ。両方対応済み）
- **残課題**: なし（#39完結）。OFFSET既定値の将来変更時は `Config` 内定数とspecs・テンプレートを同時更新すること

---

## 2026-09-26 BasicOption破棄経路の整備（Issue #44・提案元 #42）

- **Issue**: #44（AI提案の #42 を受けて新規作成。#42 は提案Issueとして残し、実装は #44 で管理）
- **ブランチ**: `feature/t044-dispose-basic-option` を `develop` へ `--no-ff` でマージ。ブランチ削除済み
- **背景**: #40 で SnapShotSabunReader.Dispose 等の中身は直したが、呼び出し側が Dispose を呼ばないため実運用では接続が閉じられなかった。SP の1回集計で最大4本（集計日DB・基準日DB・予備補完2本）の接続が開き、同一プロセスでの再集計時に積み上がる。根本原因は BasicOptionBase が IDisposable を継承しておらず、破棄の契約が型に表れていなかったこと
- **実施内容**:
  - `BasicOptionBase : IDisposable` 化＋空の仮想 `Dispose`。資源を持たない7件は無変更。資源持ち3件（SnapShotSabunReader / TagRankLiveSabunReader / TagRankTotalReader）の明示的実装を `override` に寄せ替え（基底参照から届かせるため。明示的実装のままだと基底の空実装が呼ばれる）。冗長な `, IDisposable` は除去
  - 3 Reader に `_ownsDbCtrl` フラグを追加し自前生成分のみ閉じる（SpMovieInfoFallback と同一流儀。受け入れ条件「注入接続は閉じないこと」の根拠）。本番経路は常に null 渡しのため挙動不変
  - `RankingAnalyze` / `ModeFactoryBase` を `IDisposable` 化して破棄を委譲（冪等・null 安全・1件失敗でも継続して ErrLog に記録。両者ともマネージドのみのためファイナライザなし）。`RankingList` は破棄しない
  - `ModeFactroySP` / `ModeFactoryTagRank` の Open 失敗経路でも生成物を破棄してから false を返す。成功時は RankingAnalyze に渡す一方通行で二重所有にしない
  - `TyukanAnalyze` 内側の使い捨て `RankingAnalyze` を `using` 化（日別ループでの積み上がり防止）
  - `frmMainSyukei.AnalyzeAsync` で新 Factory 代入前の旧 Factory 破棄＋出力処理の `try-finally` 化による終了後破棄
  - `UnitTest`10件追加（計270件）。空Dispose・全Option破棄と冪等・1件失敗でも継続・注入非破棄・自前解放（ファイル削除で検証）・工場委譲
  - `design.md` に Issue #44 の節を追記（Ext 見送り理由を含む）
- **設計判断**:
  - 案B（基底に破棄契約）を採用。案A（Factory 側の `is IDisposable` 分岐）は差分最小だが、追加のたびに注意が必要な構造欠陥が残るため。空Dispose7件は実コード確認済みで空が正しく、基底の空仮想により無変更で済む
  - Ext（`IExtOptionBase`）は対象外。インターフェースであり .NET Framework 4.8 の C# では既定実装を付けにくく、現状持ち越しもないため。将来の net8 移行時に再検討する（knowledge に記録）
- **検証**:
  - `dotnet test` 270件PASS・sln Releaseビルド成功（EXIT CODE=0）
  - reviewerレビュー＋再レビューで総合判定マージ可（中3件：注入所有権・design反映・自前接続テスト、低3件：finally・継続テスト・テスト後始末をすべて対応。低の見送りなし）
  - ユーザーSP実機検証OK（集計後にworkファイル的なものが全部消えた＝DBが閉じたことを確認）
- **残課題**: なし（#44完結）。単一 `dbCtrl` を Analyze/Base 両方へ注入すると後開き側に張り替わる既存挙動は残るが、所有権とは独立のため別扱い。Ext の契約化は net8 移行時に検討
---

## 2026-09-27 ライブラリ層の直接コンソール出力を表示抽象へ寄せる（Issue #43）

- **Issue**: #43（OPENの既存Issueをそのまま使用し、新規作成なし。本文が仕様、コメント0件）
- **ブランチ**: `feature/t043-console-to-statuslog` を `develop` へ `--no-ff` でマージ。ブランチ削除済み
- **背景**: #40でNicoApiの進捗をStatusLog化した際に同種の層分離違反が残っていることが分かり束ねたもの。ライブラリ直書きではWinForm呼び出し・Linux CLI運用・ログリダイレクト時の振る舞いが不定になる。Plan modeで調査（全Console棚卸し約60行・呼び出し元3経路・受け手5箇所・TextBoxWriter矛盾を特定）し、oldlog統一範囲と進捗ヘルパー共通化は調査後に判断する方針で着手した
- **実施内容**:
  - `SnapShotAnalyze.cs:91` の件数表示を `StatusLog` 化（3経路の受け手不整合を解消。文面不変）
  - `UIConfig.GetWch` から `Console.ReadLine` を除去して既定値返却のみに（SilentMode／LocalXmlは互換のため温存。どちらも書き換えなしのデッドフラグであり削除は別タスク）
  - `InternetUtil.cs:76` のコメント残骸を除去（編集中に `delayMax` 行の巻き込みミスを起こし即時修復。以後は境界の小さいeditと直後readを徹底する）
  - `NicoRankiApi` 約25行を `StatusLog`／`ErrLog` 化（進捗・状態はStatusLog、例外詳細はErrLog。呼び出し側が黙ってfalseを返す経路では両書きで可視性を維持。文面は一字一句不変）
  - `RankApi2Json` 系・`Program` 系・Cli結果を `StatusLog`／`ErrLog` 化（`--help` の使い方表示と終了コード0/1/2は維持。起動失敗2件はファイル側にも残す）
  - `frmMesseageDialog.TextBoxWriter` を `TextBox` 参照＋ `BeginInvoke` 対応に修正（保持TextBox無視の矛盾が将来の罠になるため。ダイアログ自体は現在 `new` なし。ダイアログの削除・配線復活は別スコープ）
  - 見送り: 進捗ヘルパー共通化（`\r`＋間引きの第二利用者なしの先取り抽象のため）・フラグ削除・`--help` のStatusLog化
  - `UnitTestUIConfig` 2件追加（計272件。当初3件だったがreviewer指摘の重複で統合）。`specs.md` §7・`design.md`・`knowledge`（nicorankLib／apps／testing）・`pitfalls` 項目24（ErrLog排他は別タスク）を更新
- **設計判断**:
  - oldlog全体の統一を行った。libだけ直すと同一実行内で2系統が残り#43の目的が半減するため。文面・終了コード不変のためNAS運用への影響は表示先の統一のみに閉じる
  - 例外詳細のファイル化で `nicorankerr.log` が増える（起動失敗・API例外時）。従来はコンソールにしか残らなかった記録が残る改善であり、ユーザー実行確認で受け入れ済み
- **検証**:
  - `dotnet test` 272件PASS・slnビルド成功（EXIT CODE=0。修正前は273件だったが重複整理で272件）
  - reviewerレビューで総合判定マージ可（高・中なし。低5件：重複テスト統合・インデント・タスク文修正に対応、ErrLog排他はpitfalls記録＋別タスク化、運用影響は実行確認で受入。再レビュー不要）
  - ユーザー実機検証OK（`\\ds224\Temp\nicorankOld2025` へ新モジュール11ファイルを配置。旧版は `bak20260927` に退避。config.json／cookie.txt・old-ranking・runtimesは不変。`/checklogin` 等の見た目維持を確認）
- **残課題**: `ErrLog.Write` 全体の `lock` 硬化（pitfalls項目24。並列経路での排他不足。別タスク）。`SilentMode`／`LocalXml` 削除（別タスク）。`frmMesseageDialog` の削除・配線復活判断（別スコープ）

---

## 2026-09-28 old-ranking整理とベースラインDB配布（Issue #36）

- **Issue**: #36（OPEN→検証結果を追記してクローズ）
- **ブランチ**: `feature/t036-baseline-distribution` → `develop` に `--no-ff` でマージ。マージ後にfeatureブランチ削除
- **背景**: `\\ds224\web\old-ranking` の日別JSONは2019年からの蓄積で数百GB規模になり、空DBからの追いつき再構築が古い日別JSONに依存していた。前提のT031（#31・Ver1・実測2.01GB）と#32（DbOptimizer）は完了済みのため着手した
- **実施内容**:
  - 配布場所のルール化：PG配布（GitHub Releaseのホワイトリスト、DB含まず）と連動させず、NASのWeb公開（`\\ds224\web\nicorank\baseline\` → `https://2daime.myds.me/nicorank/baseline/`）＋`baseline.json` を最新ポインタにした。当初のGitHub Release登録案から変更した理由は、大容量DBの同梱がMOTWや個人データ上書きの危険を戻すこと、nicoplayerの家庭内配布実績があることのため
  - 配布スクリプト `tools/make-baseline.ps1` を新設（DBごとzip分離＋`baseline.json`自動作成）。運用指摘で作り直し：zip名は固定（`LogOfficial.zip`／`NicoranHistory.zip`）・改名作業なし・単一最新で世代なし（残したい場合は人間が事前退避）・出力はスクリプト自身の場所・DBフォルダは`-DbDir`指定（省略時はカレント）・2GB級の進捗バー抑止と開始完了表示・BOM付きUTF-8
  - 取得側 `nicorankLib/Util/BaselineDownloader.cs` を新設。本地ファイル不在時に限り自動取得し、サイズ・sha256照合後に展開する。既存DBは置き換えない。`ApiXML`／`Dailylog` は不在時に自動生成する（best-effortで中断しない）。表示は`StatusLog`・記録は`ErrLog`（Issue #43作法）。`SYSTEM/URL_BASELINE` は任意要素とし、なければ既定URLを使う
  - 集計開始時（`RankingHistory.Open` の前）に確保＋取得するフックを `frmMainSyukei.AnalyzeAsync` に追加（集計スレッドからコントロールに触れない）
  - `UnitTest`13件追加（計285件。manifest正常・破損・欠落・不正ハッシュ・パス区切り拒否・不足なし・不足時取得配置・ハッシュ不一致・キャッシュ確保と冪等・空ファイルからの回復・Config既定と上書き）
  - specs（新規構築手順）・design（保持窓と配布設計）・knowledge（db/apps/testing/structure）更新
- **設計判断**: 発火条件は本地ファイル不在時に絞った（既存DBの勝手な置換による上書き事故を防ぐため）。取得失敗時は`EnsureMigrated`前に中断する（fail-fast維持）。週刊ApiXMLの蓄積は#41に委譲した
- **検証**:
  - `dotnet test` 285件PASS・`dotnet build nicorank2019.sln -c Release` 成功
  - reviewerレビュー→再レビューで問題なし（中1件：空DB恒久残留を毎回CREATE＋新規作成失敗時削除で解消。低8件：パス区切り拒否・戻り値明示中断・空if除去・BOM化・-wal/-shm同時削除・再インデント・ファイル毎try/catchに対応。低3件見送り：WAL組込・孤児zip自動整理・既定URL一本化は36.4実績を見て判断）
  - 配布物配置：`LogOfficial.zip`（713,240,656 bytes）・`NicoranHistory.zip`（111,792,568 bytes）・`baseline.json` をNASに配置し、HTTP 200到達を確認
  - ユーザー実機検証OK（DBフォルダ不在→キャッシュ2種新規作成→2種自動取得→更新確認「過去ランキングデータは最新です」。単発DB削除→不足分のみ取得）
- **残課題・見送り**: 36.5のWeb側コールド削除は仕様変更により省略（受け入れ条件から除外。ユーザー指示）。WAL組込・孤児zip自動整理・既定URL一本化は運用実績を見て判断（別タスク化検討）

---

## 2026-09-28 リリース実績 v20260928_nicorank

- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260928_nicorank（タグはmain HEADを指すことを確認）
- **範囲**: v20260905_nicorank → develop（#31・#37・#38・#40・#32・#39・#44・#43・#36。間のv20260906_tagrankプレリリースは本リリースに統合のためRelease＋タグを削除）
- **成果物**: `nicorank2019_20260928.zip`／`nicorank_SnapShot_20260928.zip`／`nicorank_oldlog_20260928.zip`（ホワイトリスト方式で作成・内容照合済み。DB・設定本体・pdb・Outputなし）
- **検証**: `dotnet test` 285件PASS、`dotnet build nicorank2019.sln -c Release` 成功・警告0、lib 4件＋runtimes 3種（両アプリ）、`loadFromRemoteSources` 両config確認、`nicorank.xml` 一致（依存ファイルとbinの差異は手動コピーで解消）、ユーザー実機検証（#36の自動取得・#40・#43・#44は各Issue記録参照）
- **リリースノート方針**: ユーザー影響中心＋Issue番号リンク。専門用語（ベースライン→言い換え）と硬い構造を見直し、自然な文章で記載。`nicorank.xml` の追加3要素は「なくても動く任意の要素」として説明し、「節」表記は使わない（リポジトリ内の既存表記は別タスク化見送り）
- **同期**: main → develop へバックマージし、`git diff main develop --stat` 空を確認。本エントリ追記によりdevelopが1件先行する運用は前回リリースと同様

---

## 2026-10-03 過去集計の抜けチェック＋ベースライン復旧 (#45)

- **Issue**: #45（新規作成。再集計バックアップは #46 を別Issue化し、今回はIssueのみ）
- **ブランチ**: `feature/t045-weekly-gap-check-restore` → `develop`（--no-ff マージ、feature削除済み）
- **背景**: 担当制で自分の担当週だけ集計する運用のため、長期の空きで長期動画判定（Historyの連続記録）が切れても気づけない問題があった。短期の空きは前回順位で気づけるが、長期の空きは集計しなおしでは直せない。壁打ちで仕様確定（チェック対象=LastResult Weekly・3か月既定＋1年オプション・メンテ除外・NicoranHistory単独復旧）
- **実施内容**:
  - `nicorankLib/Util/WeeklyGapChecker.cs` 新設（期待月曜列挙・FindMissing・メンテ除外・対象週除外・確認不能時は開始・TryReadパターンで構造失敗と行崩れを分離）
  - `BaselineDownloader.RestoreBaseline` 新設（配布日＋内容日の二重検証・DB/backup退避・wal/shm同伴・陳腐化中断・LogOfficial対象外・StaleReason区分）
  - メンテタブ `grpGapCheck`（手動チェック＋1年オプション＋ベースライン復旧）・週刊集計開始時の自動警告（続行／中止・復旧誘導文面・チェック中は実行系停止）
  - 全期間モードは1年モードに置換（1年より古い抜けは対処不能のため持たない。52週の境界1週は対象外になり得る旨をspecsに注記）
  - `UnitTest`36件追加（計321件）。specs／design／knowledge（apps・db・nicorankLib・testing）更新
- **検証**:
  - `dotnet test` 321件PASS・`dotnet build nicorank2019.csproj` Debug／Release成功
  - reviewer再レビュー4回で必須指摘なし（高1件＋中4件対応。低は対応と見送り記録。見送り：完全アトミック化・IsMaintenance横展開・空内容のWeekly限定）
  - 実機で起動事故1件（Designerのnew生成漏れ→NullReferenceで起動不能）を検出・修正・Release起動確認済み。教訓：Designer手書き時はnew／配置／SuspendLayout／宣言の4点照合
  - ユーザー実機検証OK（メンテタブ表示・週刊集計の警告ダイアログ文面・グループ名のIssue番号除去）
- **残課題・申送り**: #46（再集計による復旧）はIssueのみ。配布が古い場合は人間運用（回収集計者への相談）でカバーする。testing.mdの件数は--list-tests実測とソース件数で裏付けること（推定で書いて2回直した）
---

## 2026-10-03 リリース実績 (v20261003_nicorank)

- **タグ**: `v20261003_nicorank`（main HEAD と一致することを `rev-parse <tag>^{commit}` で確認）
- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20261003_nicorank
- **成果物**: `nicorank2019_20261003.zip` / `nicorank_SnapShot_20261003.zip` / `nicorank_oldlog_20261003.zip`（ホワイトリスト通り。DB・pdb・System直下・設定本体なしを確認）
- **内容**: 前回 `v20260928_nicorank` 以降は #45 のみ（集計抜けチェック＋ベースライン復旧）。`nicorank.xml` に新規設定なしのため既存設定のまま上書き更新できる
- **検証**: `dotnet test` PASS・sln Release ビルド成功（develop と main の両方で確認）。lib 4件＋runtimes 3arch・両exe.configのloadFromRemoteSources・nicorank.xml一致を確認。実機はユーザーがRelease版で起動・抜け表示・警告文面を確認済み（#45の実行確認を流用。集計ロジック自体は#45で触っていないため通し集計の再実行は省略）
- **同期**: `main` へ--no-ffマージ→タグ→push→Release作成→`develop` へバックマージ→push。`git diff main develop --stat` は空
---

## 2026-10-04 nicorank2019の自動更新・updater実装 (#47・47.1)

- **Issue**: #47（OPEN維持。47.2の release.md 更新が残るためクローズしない）
- **ブランチ**: `feature/t47-app-autoupdate` → `develop` に `--no-ff` でマージ。ブランチ削除済み
- **背景**: 手作業上書きによる `lib/` 欠け事故（2026/10/03 報告の `batteries_v2` 不在エラー）を受け、文面改善ではなく構造で防ぐ。第一段は通知＋ワンクリックの任意適用とする。壁打ち（別セッション）で設計合意と `version.json` フォーマットを確定し、実装セッションで仕様詳細を詰めた
- **実施内容**:
  - `nicorankLib/Util/AppUpdateChecker.cs` 新設（`version.json` 取得・`schema<=対応上限` 判定・自前版数正規化＋`System.Version` 比較・24時間間引き・同一版抑制。fetch／時計／TEMPパスは注入可。取得は専用の短いリトライ（3回・5秒）で `InternetUtil` の20回再試行を使わない）
  - `NicoRankXml` に `SYSTEM/URL_APPUPDATE` 任意要素、`Config.AppUpdateManifestUrl`＋既定定数（`URL_BASELINE` と同型）
  - `frmMain` の起動時非同期確認・システム設定タブ（`tabPageSystem` 新設。メンテナンスタブの渋滞解消のためユーザー判断で移設）・更新ダイアログ（版数・サイズ・詳細リンク・現状版数）・集計実行中の適用ガード・二重表示防止
  - `nicorankUpdater` 新設（net48・SDK-style・nicorankLib非参照。タスクファイル→DL→size/sha256照合→本体終了待機→退避→置換→再起動。失敗時は自動復元。再起動失敗は成功と誤認しない）。`url` は http／https／file を許す（ローカル検証用）
  - `version.json` の `url` 組み立て拒否（完全URL必須）は本体・updater 両側で検証する
  - UnitTest25件追加（計346件）。specs.md／design.md／release.md（updater行を47.1へ前倒し）／tasks.md／knowledge更新
- **設計判断**（詳細は `design.md`）: 必須化は将来Issueへ分離・updater自身の置換は別Issue・状態ファイルは削除耐性があるため `%TEMP%`・手動確認は間引き無視・確認不能時は旧版のまま・Assembly版数印は47.2の必須手順化（書き換え忘れは毎日誤通知の運用事故になるため）
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全346件PASS、`dotnet build nicorank2019.sln -c Release` 成功
  - reviewer再レビュー3回でマージ可（高1件＋中8件対応・低は対応と見送り記録。見送りは終了コード3分離・空文字url待機）
  - ユーザー実機検証OK（file://通しテスト：起動時通知→置換→再起動→backup退避→最新判定。刻印ビルドで版数遷移まで確認。システム設定タブ・現状版数表示を含む）
  - 検証中の付帯対応：更新確認の長時間リトライを短縮（確認中固着の解消）、メンテナンスタブからシステム設定タブへの移設（ユーザー実施。Click再配線と改名はこちらで検証）
- **残課題**: 47.2（release.md更新・版数刻印手順・`version.json` 生成・NAS配置）・updater自身の置換（別Issue）・必須化（将来Issue）。`docs/tasks/archive.md` の既存部分に約1万文字の文字化け（不正UTF-8バイト。コミット済みの過去破損。本件作業とは無関係）を検出。追記は既存バイトに触れない方式で行った。修復はd92900aで実施済み（既存24部は16d217cを正本とし新規11部は符号化別に復元、不正UTF-8バイト0・置換文字0・目視正常を確認）

---

## 2026-10-04 リリース実績 (v20261004_nicorank)

- **タグ**: `v20261004_nicorank`（main HEAD と一致することを確認）
- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20261004_nicorank
- **廃盤**: 前回の `v20261003_nicorank` は先行の一部配布のみのため廃盤とする（Release削除は人間が実施、タグは残置）
- **成果物**: `nicorank2019.zip` / `nicorank_SnapShot.zip` / `nicorank_oldlog.zip`（固定名に移行。既存の日付名 asset には触らない。ホワイトリスト通りでDB・pdb・設定本体なしを確認。updater一式を含む）
- **含まれる変更**: #45（集計抜けチェック・ベースライン復旧。v20261003取得者以外への説明のため本Releaseノートに再掲）・#47（自動更新。47.1＋47.2）。SnapShot・oldlogは内容無変更のため再添付のみ
- **版数刻印**: Assembly `2026.10.4.0`（`version.json` の `2026.10.04` と等価。exeプロパティで確認）
- **`version.json`**: 添付実物から `make-version.ps1` で生成（size 15803368・sha256実測）。NAS配置は人間が実施（`https://2daime.myds.me/nicorank/update/version.json`）
- **検証**: `dotnet test` 346件PASS・sln Releaseビルド成功・lib 4件＋runtimes 3種・loadFromRemoteSources・nicorank.xml一致を確認。実機集計は省略（#47は集計ロジック不変・#45は前回実機済み・起動E2Eと全テストで代替。ユーザー合意）
- **同期**: main→developへバックマージ。`git diff main develop --stat` 空を確認
- **残課題**: updater自身の置換（別Issue）・必須化（将来Issue）・NASの `version.json` 配置（人間作業。配置後に更新通知が有効になる）

---

## 2026-10-04 OneDrive配下のbatteries_v2不在エラーの切り分け（docs調査・コード変更なし）

- **Issue／ブランチ**: なし（原因確定済みの配置問題のためIssue化しない。docsのみで `develop` 直修正）
- **背景**: 2026/10/03報告の `SQLitePCLRaw.batteries_v2` の `FileNotFoundException`（`EnsureApiXmlFile()`／`EnsureDailylogFile()` 経由）が、OneDrive上に一式を配置した環境でのみ発生した。手元の新規展開では再現せず、#47の更新手順側は対応済みだったため、残る環境要因を切り分けた
- **実施内容**: パス長260超え仮説をユーザーに提示し、浅い階層（`Documents` 直下の `nicorank` フォルダ）への移動で解消したため確定とした。`pitfalls.md` に項目26（症状・MAX_PATH・浅い配置・OneDrive常時保持・Defender除外2行・SmartScreenとの区別・FileNotFound／FileLoad切り分け）を追記し、`release.md` のReleaseノート指針に配置場所の一文を足した。Defender除外は `nicorank2019.exe` に加え `nicorankUpdater.exe` の2行とし、誤爆削除の回避にはなるが初回SmartScreenの回避ではない旨を明記した
- **検証**: docsのみのため集計ロジックの再実行は不要。次工程でテスト＋ビルド成功を確認する。`tasks.md` の未完了タスクへの追加は不要（新規タスクではなく確定済み事象の記録のため）

---

## 2026-10-05 スナップショット件数取得失敗の無言エラー改善（#48）✅ developマージ済み

- **Issue／ブランチ**: #48（OPEN→本件でクローズ）／`feature/t48-snapshot-silent-error-log`（develop起点。`--no-ff` でマージし削除済み）
- **背景**: nicorank_SnapShot.Cli の定期取得で 09/04/2018 期間の件数取得に失敗した際、画面に `nicorankerr.log` 確認と出るのにログがなく原因不明になった。実行場所（`\\ds224\Temp\nicorank_SnapShot`）のカレントにも無いことをユーザー確認済み。理由は `SnapController` と `SnapShotAnalyze.AnalyzeRank` の `false` 経路が `ErrLog` に何も残さず `StatusLog` だけで終わる構造だったため
- **実施内容**: 取得の挙動は変えずログ記録に絞った。`SnapShotAnalyze.AnalyzeRank` の3経路（TxtDownLoad 失敗・meta なし・Status!=200 継続）で期間・flgLimit1000・最終 Status を `ErrLog` へ。meta なしは元の例外終了の意味を保ち即失敗＋記録（単純な `?.` 統一だと無駄な20回再取得に変わるため）。`SnapController` の取得エラー時に対象期間を記録。`SnapShotDB` の InitilizeDB／RegistDB の接続開始失敗も同種の無言経路として記録（reviewer 横展開指摘の対応）。成功時の `StatusLog` 文面は不変
- **見送り**: `InternetUtil` 内部の毎回ログ化は見送り、既知の残課題として tasks.md に記録（影響範囲が広いため別タスクで検討）。`SetRequestResult` の無限再試行は触らない（今回の落ち場所は件数取得の入口であり影響が大きいため）。新規 UnitTest なし（静的 `InternetUtil` 依存で継ぎ目がなく、ログ追加のみで戻り値不変のため）。specs／design 変更なし（#43 の両書き方針に沿う追認のため）
- **検証**: `dotnet test` 全件PASS・sln Release ビルド成功（いずれも EXIT_CODE=0。指摘対応前後で2回実施）・`dotnet publish` 成功。reviewerレビューで高・中指摘なし（低4件：3件修正・1件記録。総合判定マージ可。再レビュー不要）。修正版を NAS の `\\ds224\Temp\nicorank_SnapShot` に配置（事前に `\\ds224\Temp\nicorank_SnapShot_bak_20261005` へ全量退避。同名ファイルのみ上書きし .lnk／snapshot.lock／runtimes には触らず）。ユーザー実機で再実行し正常完了（成功時ログの見た目不変を確認。新しい失敗時ログは発火条件が失敗時のみのため未発火だが、ログ追加での完了でユーザー合意）
- **残課題**: ダウンロード失敗時は `TxtDownLoad` が例外詳細を握りつぶすため失敗の事実のみ残る。切り分けに足りなければ最終失敗時の1回記録を別タスクで検討する

---

## 2026-10-05 exe アイコンと Form.Icon の設定 (#49) ✅ developマージ済み

- **Issue・ブランチ**: #49（OPEN→本件でクローズ）／`feature/t049-app-icon` を develop 起点に作成。`--no-ff` でマージし削除済み
- **背景**: `nicorank2019.exe` にアイコンが未設定（csproj に `ApplicationIcon` なし）で、エクスプローラーやタスクバーでの識別性が低かった。ユーザーが用意したキャラクター画像（1024x1024 PNG）を exe アイコンにし、フォーム左上・タスクバーも同じ画像にする要望があった
- **実施内容**: マスター PNG から Pillow で 16/32/48/256 のマルチサイズ `nicorank2019/icon.ico`（約122KB）を生成し、csproj の `ApplicationIcon` に指定（Win32 リソース用）。`Form.Icon` 用には同じファイルを `EmbeddedResource`＋`LogicalName` で埋め込み、`frmMain` 構築時にマニフェストストリームから読む。`resx` 経由にしなかったのは旧形式プロジェクトの `dotnet build` で MSB3822/MSB3823 になることを実証したため（`GenerateResourceUsePreserializedResources` 化は実行時依存を増やすため不採用）。`Designer` でなくコード側に書いたのは全体再生成の差分 churn を避けるため（#35 で前例あり）。読込失敗時は既定アイコンで起動を続ける（アイコンは起動の必須要素ではないため）
- **見送り**: `frmMesseageDialog` は生成箇所なしのため対象外。`nicorank_SnapShot`・updater は要望範囲外のため対象外。`ExtractAssociatedIcon` 代替案と 16x16 精細化は reviewer 推奨の現状維持。二重埋め込み（Win32＋マネージド計約244KB）は exe 全体約19.8MB に対し約0.1%で実害なし
- **検証**: `dotnet test` 全件PASS・csproj 直接と sln の Release ビルド成功（いずれも EXIT_CODE=0）。exe の Win32 アイコンとマネージドリソース `nicorank2019.icon.ico` の埋め込みを実測確認。reviewer レビューで高・中指摘なし（低6件は4件修正・2件見送り。再レビュー不要）。ユーザー実機検証OK（エクスプローラー・フォーム左上・タスクバーの表示を確認）
- **残課題**: なし。マスターの再生成手順は csproj コメントに記載。specs・design・knowledge の変更なし（見た目のみで仕様・設計・構造不変のため）

## 2026-10-07 tasks.md 完了履歴テーブルの統合とレビュー指摘対応（docsのみ・コード変更なし）

- **Issue／ブランチ**: なし（docs のみの整理。AGENTS.md MUST 12 により `develop` 直修正）
- **背景**: `docs/tasks.md` はタスク開始時に毎回読むファイルなのに、完了済みタスクの履歴テーブルが載って負担になっていた。実測で統合前の tasks.md は 12,050 文字（推定 7,034 token）、うち削除した履歴テーブル部は 6,367 文字。行内容を本ファイルと突き合わせると25行のうち22行は本ファイル側の詳細版と重複する二重記録だったため、§4 の責務境界（tasks.md は現行・未完了のみ／archive.md は経緯・検証履歴）に沿って統合した
- **実施内容**: 重複する22行は削除し、本ファイルの既存セクションを正本とした。本ファイルにセクションの無かった 2026-06 の3件（上記の「btreeInitPage() returns error code 11 対策」「SQLite 操作の単体テスト設計」「単体テストの活性化（基盤）」）をここに書き下ろした。tasks.md には archive.md への参照文のみを残し、完了済みなのに「未完了タスク」に残っていた #48・#49・#47 の詳細ブロックも削除した
- **レビュー指摘への対応**（別エージェントのレビューで [中]1件・[低]4件。すべて対応）:
  - [中] #47 の削除漏れ — 47.1/47.2 は完了済みで Issue #47 も 2026-10-04 のリリース時にクローズ済み（`gh issue view` で CLOSED を実測済み）。同じ削除基準を #47 にも適用し、tasks.md から削除した。将来事項は本ファイルの 2026-10-04 セクションに残課題として記載済みで、情報損失はない
  - [低] 本セクションの日付 — 初回は 2026-10-05 と書いたが、これは #48/#49 のマージ日であり本作業の実施日ではない。コミット 161c2ec の実日付である 2026-10-07 に修正した
  - [低] 本セクションの配置 — 本ファイルは 2026-08-30 から日付昇順で下に伸びているため、2026-10-07 の記録は末尾が正しい。先頭からここへ移した（2026-06 の3セクションは昇順どおり先頭のまま）
  - [低] 効果量の記述 — 「約1.6万文字」と書いたのは、PowerShell 5.1 が BOM なし UTF-8 を ANSI(Shift-JIS) として読む測定で水増しした値で誤りだった。実測値に置き換えた
  - [低] 対応セクションの列挙 — 削除したテーブルにリリース行は無いため、「リリース実績の各セクション」の記述を落とした
- **派生した Issue 化**: #47 の残課題のうち **updater 自身の置換**は #50 として Issue 化し、tasks.md の未完了タスクに載せた。一方 **更新の必須化**は 2026-10-07 時点で予定なし（#47 当時にエージェントが提案し、ユーザーが不要と判断した）。再提案を防ぐためここに記録する
- **検証**: 削除した各行が本ファイルの対応セクションで追えることを突き合わせて確認した（#19・#22・#23・#24・#25・#26・#27・#28・#29・#30・#31・#32・#34・#35・#36・#37・#38・#39・#40・#43・#44・#45 の各セクション）。統合後の tasks.md は 1,757 文字。docs のみの変更だが MUST 5 に従い `dotnet test` と Release ビルドの成功（EXIT_CODE=0）を再確認済み
