# タスク Archive�E�Erchive.md�E�E

完亁E��たタスクの**経緯・検証履歴・実裁E��ウハウ**の記録場所、EitHub Issue に無ぁE��有情報の置き場所とする、E

## 記録ルール

- タスク完亁E���E�EGENTS.md §2 のマ�Eジ後ゲート）に、検証履歴・実裁E��ウハウ・設計判断の経緯を追記する、E
- 形式�E自由だが、日付�EIssue 番号・背景・実施冁E��・検証結果を含める、E
- コード�E現状態�E速読用まとめ�E `docs/knowledge/` に反映する。ここでは経緯・検証・判断の琁E��を省略せず書く。重褁E��避けるため詳しい構造説明�E知識へ譲ってよいが、判断が追える最小限の前提はここにも書く。仕様�E設計�E定義は `../specs.md` / `../design.md` を更新する、E

---

## 2026-08-30 SQLite ライブラリ移衁E(#20)

- **Issue**: #20 `System.Data.SQLite 1.0.118 ↁEMicrosoft.Data.Sqlite 10.0.11 / SQLitePCLRaw 2.1.12`
- **ブランチE*: `feature/t020-sqlite-microsoft-data-sqlite` ↁE`main` (`70c7110` Merge)
- **背景**: `System.Data.SQLite` の `lib` 散乱と `Costura` 埋め込みによる `batteries_v2` の `Location=""` 問題、`.NET 4.8` の `AnyCPU` 制紁E��`System.ValueTuple` の `CopyLocal` 問題を抱えたまま `NuGet` を最新安定版へ更新、E
- **実施冁E��**:
  - `22` ファイルの `using System.Data.SQLite` めE`Microsoft.Data.Sqlite` に置換、`CreateCommand`/`File.Create`/`SqliteType`/`SqliteTransaction` キャスト等を対応、E
  - `Microsoft.Data.Sqlite 10.0.11` / `SQLitePCLRaw 2.1.12` に統一�E�E3.0.5` は `AnyCPU` 禁止のため `2.1.12` を採用�E�。`AngleSharp 1.7.2` / `Costura.Fody 6.2.0` / `Fody 6.9.3` / `EF6 6.5.2` 等も更新、E
  - `lib` サブフォルダ雁E��E `5 DLL + runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` めE`bin/lib` に配置。`FodyWeavers.xml ExcludeAssemblies` + `AfterResolveReferences` 二重除夁E+ `probing privatePath="lib"` + `AssemblyResolve` で解決。`CheckForAnyCPU` 空ターゲチE��で `AnyCPU` 禁止を無効化、`Prefer32Bit=false` で `64bit` 起動を保証�E�Ewin-x86` は `32bit` フォールバック用に `3` 種同梱�E�、E
  - `System.ValueTuple 4.6.2` めE`HintPath` 付き `CopyLocal` 化し `AutoGenerateBindingRedirects=false` で二重 `assemblyBinding` を抑止、E
- **検証**:
  - `MSBuild Release/AnyCPU` ソリューション `EXIT=0`�E�EMSB3245` 無し）、`dotnet test` `69` 件 `PASS`�E�EDebug/Release` とも）、`GetManifestResourceNames` で `SQLitePCLRaw/Microsoft.Data.Sqlite` の埋め込み無しを確認、ユーザー実行で `Debug` の `Batteries_V2.Init()` ぁE`win-x64` で成功、E
  - `reviewer` 再レビューで `7` 点持E�� ↁE`5` 点持E��とも解消し `問題なし` 判定、E
- **残課顁E*: Issue #22�E�単体テストでコマンド�E利用パターン検�E不可�E�を別途対応予定、E

---

## 2026-08-31 Nicochart TSV 取得�E完�E廁E��と ID 番号による新着偽造判定への代替 (#23)

- **Issue**: #23
- **ブランチE*: `feature/Removal_Logic_NicoChart` ↁE`develop` (`0aab8f0` Merge)
- **背景**: nicochart.jp のポインチETSV�E�Ehttp://www.nicochart.jp/point/{id}.tsv`�E�が利用できなくなった。従来は so 動画�E��E式チャンネル�E��E「新着偽造」（非公開�E再�E開で投稿日時だけ更新され、E��去の数字が新着のように雁E��される問題）判定を TSV 補完で対応してぁE��、E
- **実施冁E��**:
  - ユーザー実裁E��E7ccd4b1`�E�E `CheckSoMovieNeedSabun` から TSV 取得フォールバックを削除。`SabunReader` で差刁E��取れなぁEso 動画めEID 番号�E�Eo40000000 未満�E�で新着偽造判定し `isDelete`、E
  - 追加実裁E��Ea427129`�E�E `LogNicoChart.db` の attach/detach・`NicoChart.Ranking` 参�E・`SYSTEM.NicoChart` 設定�E`LOG_NICOCHART` 定数を完�E削除。`依存ファイル/nicorank.xml` と `UnitTest/Fixtures/nicorank.xml` から `<NicoChart>` を削除、E
  - reviewer 持E��対応！E77425d9`�E�E コメント整合性�E�「Nicochart節紁E��削除・DB エラー時除外�Eコメント修正�E��E`CheckSoMovieNeedSabun` のエラーメチE��ージ旧メソチE��名修正・`!isNew` / `out var` へのスタイル修正、E
- **設計判断**:
  - DB エラー時も `isDelete`�E��E除外）とする: 意図皁E��仕様。�E除外が発生した時点でユーザーが問題に気づきやすい、E
  - `40000000` の定数化�E見送り: 当該箁E��でしか使わなぁE��め、E
  - `LogNicoChart.db` は参�Eごと完�E削除�E�ユーザー判断�E�。既存ユーザーの nicorank.xml に `<NicoChart>` が残ってぁE��めEXmlSerializer が未知要素を無視するため互換性は維持される、E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 69 件 PASS�E�Eeature / develop とめEEXIT CODE 0�E�、E
  - ユーザーが実データでの雁E��実行確認済み�E�新着偽造動画の除外挙動）、E
  - reviewer レビュー: 高�E中深刻度の持E��なし。低深刻度 6 点はすべて対応済み、E
- **残課顁E*: `NocoChartReader` は `now.nicochart.jp` の today フィードに依存しており、nicochart.jp 全体�E利用停止状況次第で動作しなぁE��能性�E�Eeviewer 推奨。�E #24 でチE��ドコードとして削除し解消！E

---

## 2026-08-31 チE��ドロジチE��削除: NocoChartReader / NicoChartModel / AngleSharp (#24)

- **Issue**: #24
- **ブランチE*: `feature/t024-remove-dead-nocochart` ↁE`develop` (`186ad1c` Merge)
- **背景**: `NocoChartReader`�E�Eow.nicochart.jp の today フィードから当日ランキングを取得）が `ModeFactory` 等どこからも呼ばれてぁE��ぁE��チE��ロジチE��だった。同ドメインの別機�Eは #23 で廁E��済みで、フィード�E存続保証もなぁE��E
- **実施冁E��**:
  - `NocoChartReader.cs` / `NicoChartModel.cs`�E�Etom feed モチE��6クラス�E�を削除
  - `nicorankLib.csproj` から Compile Include と AngleSharp 1.7.2 の Reference を削除、`packages.config` から AngleSharp を削除
  - `nicorank2019.csproj` の PostBuildEvent から不要になっぁE`del AngleSharp*` を削除
- **設計判断**:
  - `RankGenreJson` / `RankLogJson` は `JsonReaderBase` / `RankApi2Json` で現役使用のため残す
  - `nicorank_oldlog` の AngleSharp 1.1.2 PackageReference は別プロジェクト依存�Eため触らなぁE��Ecs での使用なし�E確認済み、スコープ外！E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 69 件 PASS�E�EXIT CODE 0�E�！E`dotnet build nicorank2019/nicorank2019.csproj` 成功�E�EostBuildEvent 変更の検証を含む�E�E
  - reviewer レビュー: 忁E��指摘なし（佁E点は対応不要と判断、見送り�E�E
- **備老E*: ユーザーに見える挙動�E変更なし（呼び出されてぁE��ぁE���Eの削除のみ�E�E

---

## 2026-09-01 配币Ezip 展開時�E MOTW で SQLiteCtrl のタイプ�E期化が失敗する対処 (#26)

- **Issue**: #26
- **ブランチE*: `feature/t026-motw-loadfromremotesources` ↁE`develop` (`ef43f81` Merge)
- **背景**: GitHub Release(v20260831_nicorank)から配币E��ぁEzip をエクスプローラで展開した環墁E�� `'nicorankLib.Util.SQLiteCtrl' のタイプ�E期化子が例外をスローしました` が発生するとぁE��報告。�E币Elib の DLL は正常環墁E��バイト一致(SHA256)しており、E��発 PC でも「テストしたい lib めElib にリネ�Eムする」手頁E�Eみで再現/解消する現象だっぁE
- **原因**: エクスプローラで zip を展開すると中の全ファイルに Zone.Identifier(MOTW, ZoneId=3)が付く、ENET Framework 4.8 は `loadFromRemoteSources` 未設定�Eアプリでリモートゾーンのマネージ DLL のロードを `FileLoadException` で拒否し、`Batteries_V2.Init()` が失敗する。ネイチE��チE`e_sqlite3.dll` は `LoadLibrary` のため影響を受けなぁE
- **刁E��刁E��の経緯**: DLL のバイト一致・exe/config 共通�E実行時フォルダ名�E常に lib に統一、とぁE��条件で残った差刁E�EメタチE�Eタ(ADS/ACL/隠し属性)に絞られた。`Get-Item -Stream Zone.Identifier` で確認したところ、E�E币Elib の DLL は全件 MOTW あり(正常 lib はなぁE。exe.config に MOTW が残ってぁE��ことも�E币E��展開の痕跡として手掛かりになった。なお�Eり�Eけ中にチE��トフォルダ名�Eアンダースコアが半见E全角で不一致となり�E力が空になる一幕があっぁEパス確認�E重要性)
- **実施冁E��**:
  - `nicorank2019` / `nicorank_SnapShot` の App.config に `<loadFromRemoteSources enabled="true" />` を追加
  - `frmMain.cs` の起動時/雁E��時のエラー表示めE`ex.Message` ↁE`GetExceptionMessages`(例外チェーン連絁Eに変更。従来は SQLiteCtrl が付与する診断惁E��(InnerException)が一行目しか見えなかっぁE
  - pitfalls.md 頁E��16 追記、release.md にリリース前チェチE��リスト�ERelease ノ�Eト指針を追訁E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 69 件 PASS(EXIT CODE 0)、`dotnet build nicorank2019/nicorank2019.csproj -c Release` 成功(EXIT CODE 0)
  - reviewer レビュー: 高�E中深刻度の持E��なし。低深刻度 2 件(AggregateException 展開・循環参�E防御)は見送り、理由と封E��対応を Issue #26 コメントに記録
  - ユーザー実行確誁E lib DLL に MOTW を�E付与した状態で新 exe + exe.config による正常起動を確誁EloadFromRemoteSources の効果確認済み)
- **残課顁E*: 封E�� `Task.WhenAll` / 並列集計導�E時に `GetExceptionMessages` へ `AggregateException` 展開を追加(Issue #26 コメント参照)

---

## 2026-09-03 ビルド警告�E対処と未使用 AngleSharp の削除 (#25)

- **Issue**: #25�E�Eependabot alert #10 の AngleSharp 脁E��性も本件で解消！E
- **ブランチE*: `feature/t025-build-warnings-anglesharp` ↁE`develop`
- **背景**: Release ビルド�E警呁E5 件�E�ESB3276 / CS0414 / CS0168ÁE�E�と、`nicorank_oldlog` の未使用 AngleSharp 1.1.2�E�脆弱性 medium�E�E
- **実施冁E��**:
  - CS0168ÁE: `InternetUtil.cs` の未使用 catch 変数 `ex` を除去�E�EWebException ex` は使用中のため対象外）。reviewer 持E��で `ex` 参�Eの死にコメンチE2 行も削除
  - CS0414: `frmMainSyukei.cs` の `eAnalyzeMode`�E�宣言�E�代入 3 件�E�関連 using�E�を削除。`GetModeFactory` は従来通りラジオボタン直接参�E。`frmMain.cs` の未使用 using も併せて削除
  - MSB3276�E�E 案！E 詳細ログで競合�E `System.Memory` のみと特定。両 EXE の `App.config` の redirect めE`4.0.1.2` ↁE`4.0.5.0` に修正ぁE`nicorankLib/app.config` と整合。`AutoGenerateBindingRedirects=false` は維持E��E20 の二重 `assemblyBinding` 再発防止�E�E
  - AngleSharp 削除: `nicorank_oldlog.csproj` から PackageReference を削除�E�E.cs` からの使用ゼロ確認済み�E�E
  - ソリューション全体ビルドで追加発覚した警告も対処�E�Essue 記載通り追記相当！E `RankApi2Json.cs` の `if(false)` チE��チE��刁E��を削除�E�ES0162。`Parallel.ForEach` 側のみ残し挙動不変）、Fody/Costura.Fody の `IncludeAssets` 除去�E�Eody 警告�E推奨通り、`PrivateAssets=all` 維持E��E
  - `docs/knowledge/apps.md` の依存記述から AngleSharp を除去
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 成功�E�EXIT CODE 0�E�E
  - `dotnet build nicorank2019.sln -c Release --no-incremental` で警呁E0・EXIT 0
  - reviewer レビュー: 高�E中深刻度なし（�Eージ可判定）。佁E2 件は対応済み
  - ユーザーが週刁E中閁ESP の実集計で実行確認済み�E�問題なし！E
- **設計判断**:
  - `eAnalyzeMode` は削除�E�使ぁE��への修正ではなく）。`GetModeFactory` との二重状態解消より最小差刁E��優允E
  - MSB3276 は `AutoGenerateBindingRedirects=true` 化ではなく手勁Eredirect 追加。`true` 化�E #20 の二重化問題に送E��りするためE
  - `nicorank_SnapShot/App.config` も同一の陳腐化 redirect だったため同時修正�E�ソリューション警呁E0 のため�E�E

---

## 2026-09-03 単体テストでDB操作�EビジネスロジチE��問題を検�Eできるようにする (#22)

- **Issue**: #22
- **ブランチE*: `feature/t022-db-command-reuse-tests` ↁE`develop`
- **背景**: #20移行�E検証過程で実行時チE��トでのみ同一コマンド�E利用問題が発覚した（単体テスチE9件PASSでは検�E不可�E�。本番コードを網羁E��査し、残存Clear漏れ2件のみ検�E�E�他�E暫定修正済み�E�、E
- **実施冁E��**:
  - `NicoApi.UpdateTumbInfo` のDELETE/INSERTループをループ�EClear化！E件以上更新時�E `InvalidOperationException: Must add values...` を解消。`@取得日` は `todayStr` に退避して毎回再設定！E
  - `UnitTest/nicorankLib/Util/UnitTestDbCommandReuse.cs` 新設6件�E�ネガチE��ブ�ENicoApi型DELETE・DELETE→INSERT刁E��・GetRankingSabun型SELECT刁E��・calcDailyRank型ALTER同一Tx成功系・GetMovieData型）。訁E5件
  - `pitfalls.md` 頁E��17に移行時ランタイム差刁E��ェチE��リスト追記、`testing.md`/`structure.md` の件数めE5に更新、`tasks.md` に#22タスク追記�E完亁E��
  - reviewer持E��対忁E 中1件�E�ELTER同一Txの成功系限定化�E��E佁E件�E�例外文言一般化�EPRAGMA両記�Eヘルパ�E頁E���E拡張コメント）を修正、佁E件�E�EicoApi外�EClear冗長・testing.md冁E��欠落�E�を見送り。�Eレビューでマ�Eジ可判宁E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全75件PASS�E�EXIT CODE 0�E�、`dotnet build nicorank2019.sln -c Release --no-incremental` 成功�E�EXIT CODE 0�E�E
  - 実集計での実行確認�Eユーザー判断で省略�E��E動テストでガード、�E力形式�E変更なし！E
- **残課顁E*�E�Essue #22 のクローズ時コメントに転記！E
  - ALTER同一Txのロールバック→�E実行検証�E�本件は成功系のみ�E�E
  - DB操作�E共通�Eルパ�E雁E��E��修の要否検討！Essue #22 方釁E、スコープ大・要相諁E�Eため見送り�E�E
  - `NicoApi` ループ外Clear冗長の微修正、`testing.md` 冁E��の `UnitTestTestDbHelper` 欠落とREADME件数表ドリフトの整琁E

---

## 2026-08-31 リリース v20260831_nicorank

- **タグ**: `v20260831_nicorank`�E�Eain `d8af711`。annotated tag でコミット位置を確認済み�E�E
- **GitHub Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260831_nicorank
- **成果物**: `nicorank2019_20260831.zip`�E�パターンA ホワイトリスト�Eみ: exe / exe.config / nicorank.xml.org / lib 4件 + runtimes 3種�E�E
- **含まれる変更**: #20�E�EQLite移行！E #23�E�Eicochart TSV廁E��・新着偽造判定代替�E�E #24�E�デチE��コード削除�E�。リリース対象は nicorank2019 のみ
- **検証**: main 上で Release ビルチEEXIT=0、`dotnet test` 69件 PASS、lib 配置確認！E件 + runtimes 3種�E�、zip ホワイトリスト�E合（ユーザー確認済み�E�、実機確認済み�E�ユーザー�E�E
- **release.md 初実施で判明した問顁E*�E�後続�E release.md 改喁E��反映�E�E
  - `bin/Release/nicorank.xml` ぁEPostBuildEvent xcopy のタイミングで更新されず、廁E��済み設定！E<NicoChart>`�E�が残った古ぁE��ぁEzip に入った（ユーザー持E��で発覚）、Eip 作�E前に bin/Release と依存ファイルの一致確認が忁E��E
  - PowerShell から `gh --notes` に日本誁E+ バッククォート�Eり本斁E��直接渡すと `` `n `` が改行に置換され文字欠けが発甁EↁE`--notes-file` を使ぁE��ぁE
  - develop の未プッシュコミッチEpush が手頁E�E明示スチE��プに無かっぁE

---

## 2026-09-01 リリース v20260901_nicorank

- **タグ**: `v20260901_nicorank`�E�Eain `e54963f`。annotated tag でコミット位置を確認済み�E�E
- **GitHub Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260901_nicorank
- **成果物**: `nicorank2019_20260901.zip`�E�パターンA�E�E `nicorank_SnapShot_20260901.zip`�E�パターンB�E� E**初めて SnapShot を�E币E*�E�E26 の exe.config 修正が両アプリに影響するためユーザー判断で追加�E�E
- **含まれる変更**: #26�E�EOTW 対忁E loadFromRemoteSources 追加・起動時エラー表示の例外チェーン化！E 配币E��ンプレーチE`nicorank.xml` の Thread Max 既定値 16ↁE�E�ユーザー持E���E�E
- **検証**:
  - main 上で `dotnet test` 69件 PASS、nicorank2019 / nicorank_SnapShot の Release ビルチEEXIT=0
  - lib 配置確認（両アプリとめE4件 + runtimes 3種�E�、両 exe.config に `loadFromRemoteSources enabled="true"` 含まれることを確認（今回から追加したチェチE��頁E���E�E
  - `依存ファイル/nicorank.xml` と `bin\Release\nicorank.xml` のハッシュ照合で **Thread Max の不一致�E�E6 vs 6�E�を検�E** ↁE依存ファイル側めE6 に修正して統一してから zip 化（チェチE��リストが再�E有効に機�Eした。bin\Release 側は実行中に書き換わるため今後も忁E��確認！E
  - zip ホワイトリスト�E合（不要ファイルの混入なし）、MOTW 付き lib での起動確認済み�E�ユーザー�E�E
- **実機集計確認（週刁E中閁ESP�E�E*: ユーザー判断で省略�E�変更ぁEconfig・エラー表示のみで雁E��ロジチE��に影響しなぁE��めE��E
- **備老E*: Release ノ�Eト�E `--notes-file` 方式（前回�E知見通り�E�、exe と exe.config のセチE��上書き案�Eを�E記。既知の Dependabot 警告！Eoderate ÁE1、Dependabot #10・#25 対応対象�E��E継続中

---

## 2026-09-04 ニコ動APIのリクエスト絁E��立てを型付きリクエストへ変更 (#19)

- **Issue**: #19�E�作�E時に未記載だった「なぜ変更するか」を追記済み�E�E
- **ブランチE*: `feature/t019-snapshot-typed-request` ↁE`develop`�E�E44795c7` Merge�E�E
- **背景**: スナップショチE��検索 API v2 には未使用パラメータが多数あり、封E��皁E�� CLI 操作等での外部検索条件持E���E下地として Get パラメータ直書き�E技術負債を解消する。スナップショチE�� API v2 を優先度高、nvapi は拡張予定なし�Eため横展開程度�E�優先度低）で実施、E
- **実施冁E��**:
  - `nicorankLib/SnapShot/SnapShotRequest.cs` 新設�E�E / targets / fields / filters / jsonFilter / _sort / _limit / _offset / _context�E�。キーはブラケチE��記法�Eまま、値のみ `EscapeDataString`。`_context=WeeklyNicoranProgram` 追加、`_limit/_offset` クランプ。`jsonFilter` は string 経路のみで型階層は先送り
  - `SnapShotAnalyze.cs` の `REQUEST_URL` 直書き廁E��。`SetRequestResult` の `flgLimit1000` 無視（常に1000制限URL�E�を解消し件数取得と統一。未使用 `dateTime` 引数めE`flgLimit1000` に置揁E
  - `nicorankLib/Util/ApiUrlBuilder.cs` 新設�E�Eeviewer持E��対応。汎用クエリ絁E��立て�E�`?`/`&` 刁E���E�。`NicoRankiApi.requestAPI` を辞書受けに変更し文字�E連結廁E��、`_frontendId`・UA 定数化、genre/featuredKey パスをエンコード、`tag` は term=24h/hour 以外省略�E�ログ
  - `UnitTestSnapShotRequest.cs` 9件�E�`UnitTestApiUrlBuilder.cs` 7件�E�墁E��値3件追加。訁E4件�E�E5ↁE4�E�E
  - `specs.md`�E�EPI仕様�E現実裁E���E`design.md`�E�型付き化�E設計判断�E�に反映
- **設計判断**�E�詳細は `design.md`�E�E
  - `Replace(":null", ":0")` は温存。null→`long` 直結�E `FromJson` は `JsonSerializationException` を実証済み。`long?` 化�E `RegistDB` まで波及するためリスク�E�効极E
  - `NicoApi.cs` のID連結�E`JsonReader`系パス連結�E`InternetUtil` チE��ドコード�E対象外（実害なし�E変更リスク�E�効果！E
  - 日付送E��等�EバリチE�Eションは封E��のCLI外部持E��時に実施
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全94件PASS�E�EXIT CODE 0�E�、`dotnet build nicorank2019.sln` 成功・警呁E
  - 全面エンコード＋`_context` の件数取征E件で実サーバ�E HTTP 200・`status:200` を確誁E
  - reviewerレビュー�E��Eレビュー: 忁E��（高�E中�E�指摘を全解消（中1件は ApiUrlBuilder 抽出で対応）。低指摘�E見送り刁E�E琁E��をコミットメチE��ージ・design.md に記録。�Eレビューでマ�Eジ可判宁E
  - ユーザー実機確誁E 修正前後�E daily�E�E026-09-04�E�E00ファイルをID雁E��比輁E��ファイル雁E��一致・AFTER空ファイル0・総件数+0.15%・共通IDタイトル変更4件のみ。上佁E00位�E100%維持E��E18除ぁE0%�E�で変動は501位以降に雁E���E�取得時刻差のランキング変動。取得欠落なしと判宁E
- **残課顁E*: `tag` 省略ガード�E実動作確認�E weekly�E�Eerm=week�E�tag付き区刁E��取得時に目視する�Eが確実！Eaily は term=24h のため新旧同一条件�E�E

---

## 2026-09-04 人気タグのタグロチE��補宁E(#27)

- **Issue**: #27
- **ブランチE*: `feature/t27-favorite-tag-complement` ↁE`develop`�E�E2fe6f3c` Merge�E�E
- **背景**: `FavoriteTagReader` は LogOfficial.db の「人気�Eタグ」�Eみ取得してぁE��。実利用老E��ら「カチE��リ名とタグの重褁E��「ロチE��タグは3つだけ欲しい時と全部欲しい時がある」と要望があった。文字コード違ぁE�E出力群�E�EJIS / DB登録用CSV�E��E旧連携方式�E名残で、現行�E `result_DB登録用(UTF8).json` に一本化済みのため存在琁E��が消滁E��てぁE��
- **実施冁E��**:
  - `NicoApi.GetLockedTags` 新設�E�取得専責。最新取得日行�E`lock="1"` 定義頁E�E異常時�E空リスト）。`UpdateTumbInfo` と刁E��し他オプションの処琁E��E��依存しなぁE�E己完結型。テスト容易性のため `UpdateTumbInfo` / `OpenDB` / `CloseDB` めEvirtual 匁E
  - `FavoriteTagReader` は件数上限を廁E��し�E件補完（�E対象めE`UpdateTumbInfo` で確保。中間集計�Eみ `isLocalOnly: true` で外部取得なし�EキャチE��ュ参�Eのみ。週刁ESPは先行オプションが確保するため現状維持E��E
  - `FavoriteTags` めE`HashSet`→`List` 化し挿入頁E��保証�E�人気タグ→タグロチE��定義頁E��。`Ranking.GetDisplayTags()` で出力時にカチE��リ同名タグを除外！Erim後完�E一致・空カチE��リは除外なし�E非破壊！E
  - `NrmOutput` にタグ上限パラメータ追加�E�ESV系4ファイルはすべて3件。�E件は `result(UTF8).csv` 最終�Eと `result_DB登録用(UTF8).json` のみ�E�。`result(SJIS).csv`・DB登録用CSVÁEの生�E停止�E�EResultCsvRankDB` クラスは温存！E
  - `using`�E�`_dbCtrlOverride` 全10箁E��めEtry/finally 化（注入刁E�E破棁E��なぁE��有権対応、E026-06-23 の注入対応時に混入した封E��の共有破壊リスクを解消！E
  - 仕様変更の経緯: 当�E3件上限→�E件補完＋�E力�E制限へ転換、rankED→rank1000/rankUserNum めE件に統一�E�ニコランWEB管琁E��E�E意見！E
- **設計判断**�E�詳細は `design.md`�E�E
  - 除外�E出力�Eヘルパ�E�E�EUserInfoReader` が後段でカチE��リ補完するため収雁E��点では未確定�E動画がある、EB格納値は重褁E�Eまま許容�E�E
  - `RankingHistory` / `NicoApi` の Open/Close は現状維持E��Eusing` なし�E示ライフサイクルのため別タスク化が適刁E��E
  - `IsOpen` ガード統一・Trim統一は見送り�E�対象クラスにチE��トなし�E出力�Eで吸収済み。テスト整備時に実施�E�E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全119件PASS�E�既孁E4�E�新要E5。�E訳: LockedTags 6・FavoriteTagReader 9・DisplayTags 6・Output 4、EXIT CODE 0�E�、`dotnet build nicorank2019.sln` 成功
  - reviewerレビュー�E�E回�E再レビュー: 忁E��（高�E中�E�指摘を全解消（注入NicoApiのOpenDB非対称・nullガーチE件�E�。�Eレビューでマ�Eジ可判定。低指摘�E見送り刁E�E琁E��をコミットメチE��ージに記録
  - ユーザー実行確誁E コードレビューOK・マ�Eジ持E��あり�E��E力�E容変更のため�E�E
- **残課顁E*: Issue #28�E�ランキングJSON肥大化対筁E FavoriteTag見直し＋LastResult.JSON空斁E��化。外部シスチE��と協議中。今回は対応せず！E

---

## 2026-09-04 プレリリース v20260904_nicorank_preview�E�Eicorank2019のみ�E�E

- **タグ**: `v20260904_nicorank_preview`�E�Eain `b303db3`。annotated tag ぁEmain HEAD を指すことを確認済み�E�E
- **GitHub Release�E�Ere-release�E�E*: https://github.com/n2daime/nicorank2019/releases/tag/v20260904_nicorank_preview
- **成果物**: `nicorank2019_20260904.zip`�E�パターンA ホワイトリスト�Eみ: exe / exe.config / nicorank.xml.org / lib 4件 + runtimes 3種�E�。`nicorank_SnapShot` の配币E��ぁE
- **含まれる変更**: #25�E�ビルド警告対処�E�E #22�E�EicoApi残存Clear漏れ修正�E�E #19�E�型付きリクエスト化�E�E #27�E�人気タグ全件補完�E出力仕様変更�E�、E28は対象夁E
- **検証**:
  - `develop` で `dotnet test` 119件 PASS�E�EXIT CODE 0�E�、`dotnet build nicorank2019.sln -c Release --no-incremental` 成功・警呁E�E�EXIT CODE 0�E�E
  - main 上で lib 配置�E�E件 + runtimes 3種�E��E`exe.config` の `loadFromRemoteSources`・`依存ファイル/nicorank.xml` と `bin\Release\nicorank.xml` のハッシュ一致を確誁E
  - zip ホワイトリスト�E合（不要ファイル混入なし！E
  - タグぁEmain HEAD を指すこと・`git diff main develop --stat` が空であることを確誁E
- **実機集計確誁E*: ユーザー持E��で簡易化のため省略�E�E19 daily比輁E�E#25週刊中間SP・#27出力確認で代替�E�。Releaseノ�Eトに検証フィールド�E記載なぁE
- **備老E*: ソリューションビルド�E初回は `nicorank2019.exe` 常駐＋VS による `bin\Release` ロチE��で `MSB3021/MSB3027` 失敗。VS終亁E��に再実行して成功。Releaseノ�Eト�E `--notes-file` 方式、�E頭にプレリリース�E�動作確認用�E��E一斁E��めE

---

## 2026-09-05 result(UTF8).csv不要�E削除�E�E29�E�E

- **Issue**: https://github.com/n2daime/nicorank2019/issues/29
- **ブランチE*: `feature/t29-result-csv-cleanup` ↁE`develop` に `--no-ff` でマ�Eジ�E�Ee41fbd�E�。�Eージ後にfeatureブランチ削除
- **実裁E*:
  - `TextUtil.ReadCsv` を�E番号固定switch�E�`hoseiari`ハック�E�`ColLmt`から、�EチE��ー名�E辞書の動的検�Eに変更。新旧両対応（旧CSVの運営・補正あり、タグなしも読める�E�。`ColLmt` 廁E���E�ELastRankCsvReader` の第3引数削除�E�。いぁE�Eランク/ぁE��ね数に新規対応。人気タグはOption�E�なければ空リスト、あれ�Eカンマ区刁E���E�。ユーザーアイコンは旧名エイリアス対忁E
  - 読み取らなぁE�E: 運営2列（古すぎる）、�Eイリスト�Eイントを含む補正系・ポイント�E訳8列（�E計算するためE��。�Eイリスト�Eイント�E当�E読取対象だったがユーザー持E��で除外に変更
  - `ResultCsvRankDB.cs` 削除�E�csproj参�E削除�E�`ModeFactoryBase.CreateOutputCSV_rankDB` 抽象とWeekly/Tyukanのoverride�E�`frmMainSyukei` 列挙1行を削除�E�いずれもnull返却のみだったため実効出力数は不変！E
  - `ResultCsv` めE0列新頁E���E�人気タグを最終�EↁE列目へ移動、E��営2列削除、�Eイリスト�EインチE3列目単独化。�EチE��ー名�E省略なし！E
- **設計判断**�E�詳細は `design.md` のIssue #29節�E�E
  - 欠落列�E既定値�E�数値0・総合ランク空ↁE999999・斁E���E空・タグなし�E空リスト）で吸収。`PointTotal` キャチE��ュ�E�EworkPointTotal`�E�により `LastRankCsvReader` の「当時のPointTotalを使ぁE��動作�E維持E
  - 出力するが読まなぁE�E�E��Eイリスト�Eイント含む8列）�E再計算パターンで統一。タグ往復非対称�E��E力�EカチE��リ除外済み表示値�E��E旧実裁E��ら同一のため見送り
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全124件PASS�E�既孁E19�E�新要E。�E訳: FixtureValues・列頁E�E替/欠落既定値・アイコン別名�Eヘッダー30列完�E一致・出力�E読取ラウンドトリチE�E、EXIT CODE 0�E�、`dotnet build nicorank2019.sln` 成功
  - 副産物: 旧fixture `test_ranking.csv` のチE�Eタ行に空列が1つ余�Eにある潜在バグ�E��EチE��ー31列に対しデータ32列。旧チE��ト�E値検証なしで素通り�E�を新チE��トで検�E・修正
  - reviewerレビュー�E��Eレビュー�E�差刁E��ビューの訁E囁E 忁E��指摘なしでマ�Eジ可判定。低指摘�E見送り刁E��タグ往復非対称�E��E琁E��をコミットメチE��ージに記録
  - ユーザー実行確誁E 実利用老E�E意見�E取後にマ�EジOK�E��E力�E容変更のため�E�E
- **残課顁E*: なし！ERoundtrip` チE��ト�E `read.PointMyList == 0` は仕様�E固定化。封E��CSV由来の `PointMyList` を使ぁE���E追加時�E前提から見直すこと。現状そ�Eような呼出はなし！E

---

## 2026-09-05 ランキングJSON肥大化対策！E28�E�E

- **Issue**: https://github.com/n2daime/nicorank2019/issues/28
- **ブランチE*: `feature/t28-dbversion-migration` ↁE`develop` に `--no-ff` でマ�Eジ�E�E1218ec�E�。�Eージ後にfeatureブランチ削除
- **背景**: #27のタグ全件補完で `LastResult.JSON`�E��E件シリアライズ保存）が肥大化。読み側�E�ELastRankReader`�E��E総合ランク・ポイント�Eみ参�Eのため削減可能だった。旧SP雁E���E残骸紁E1丁E��も残孁E
- **実施冁E��**:
  - `LastResult.JSON列をDROP`�E�新規INSERT除外＋Ver0移行で `DROP COLUMN`。当�Eは空斁E��化→ユーザー持E��でDROPに強化。失敗時はフォールバックなしで中断�E�E
  - 旧SP種別行を削除�E�ELastResult` / `LastResultInfo` 両チE�Eブル。SPは `CreateHistory()=null`�E�CSV経路のためDB不使用�E�E
  - `DBVersion` 導�E�E�EogOfficial / NicoranHistoryの2DBに限定。`Ver` INTEGER・Ver0開始。未記録DBはVer0から頁E��適用し未定義は失敗、Eailylog / ApiXMLはキャチE��ュ扱ぁE��対象外！E
  - 司令塁E`DbMigrationCoordinator` 新設�E�EnicorankLib/Util`�E�＋`IDbMigratable`。集計開始時�E�EAnalyzeAsync`・Open直後�E公式DB更新前）に持E��し、失敗時は中断�E�Eail-fast�E�。実�E琁E�E吁E��ラスに委譲
  - Ver0移行�EDDL�E�DMLはトランザクション化！EACUUMは不可のため確定後に実行。バージョン記録は成功確定後�ETxn外書き込み�E�、EavoriteTag見直しなし（コード不変）。bat配币E�E自動移行で代替し見送り
  - ユーザー持E��対応：コメントと実裁E�E乖離�E�Eer=0記録→逐次適用に作り替え）、復旧機構�E質問�E移行Txn化、前提確認�E位置→ループ外＋理由コメント化
- **設計判断**�E�詳細は `design.md` のIssue #28節�E�E
  - 前提条件�E�テーブル存在�E�と移行手頁E��刁E��。ダウングレード（記録Ver�E�現在値�E��E無変更成功。VACUUMはVer0移行時の1回�Eみ
  - `RankingHistory.Open` は注入済み開接続を再利用�E�テスト容易性。本番経路不変！E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全136件PASS�E�既孁E24�E�新要E2。�E訳: 司令塁E・Ranking移衁E・Nicoran移衁E、EXIT CODE 0�E�、`dotnet build nicorank2019.sln` 成功・警呁E
  - reviewerレビュー�E��Eレビュー: 忁E��（高�E中�E�指摘を全解消！EreateRankingDateTableのRollback・Ranking前提確認）。�Eレビューでマ�Eジ可判定。低指摘�E見送り刁E��二重Open・CreateDBVersionTable改名）�E琁E��をコミットメチE��ージに記録
  - ユーザー実行確誁E コードチェチE��OK�E�Eeviewer前実施�E��E実環墁E��認OK後にマ�Eジ
- **残課顁E*: `RankingHistory.Open` の二重呼び出し所有権・`TestDbHelper.CreateDBVersionTable` のSnapshot用スキーマ名・移行�E琁E�EMigrator刁E���E�バージョン増加時�E肥大化対策。`design.md` 封E��検討に記録�E�。いずれも本タスクでは見送り

---

## 2026-09-05 リリース実績 v20260905_nicorank

- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260905_nicorank�E�タグはmain HEADを指すことを確認！E
- **篁E��**: v20260904_nicorank_preview ↁEdevelop�E�E29 result csv不要�E削除�E�E28 JSON肥大化対策！E
- **成果物**: `nicorank2019_20260905.zip` / `nicorank_SnapShot_20260905.zip` / `nicorank_oldlog_20260905.zip`�E��Eワイトリスト方式で作�E・冁E��検証済み、EB・設定本体�Epdb・Outputなし！E
- **検証**: `dotnet restore`�E�`dotnet test` 136件PASS、MSBuild Releaseビルド�E功�E警呁E、`loadFromRemoteSources` 両config確認、`nicorank.xml` 一致�E�EHA256�E�、bin/Release lib 4件�E�runtimes 3種確認。実機集計�E#28・#29のユーザー実行確認でカバ�E
- **判明した問顁E*: なし！Eip冁E��検証スクリプトの正規表現ぁE`runtimeconfig.json` に誤検�Eするiskeあり。�Eワイトリスト品のため問題なし。次回�E `config\.json$` の前方不一致に注意！E
- **同期**: main ↁEdevelop めE`--no-ff` でバックマ�Eジ�E�E2ef15a�E�。バチE��マ�Eジ直後�E `git diff main develop --stat` 空を確認。本エントリ追記によりdevelopぁE件先行（前回リリースと同様�E運用�E�E

---

## 2026-09-06 プレリリース実績 v20260906_tagrank

- **Release**: https://github.com/n2daime/nicorank2019/releases/tag/v20260906_tagrank�E��Eレリリース。タグはmain HEADを指すことを確認！E
- **篁E��**: v20260905_nicorank ↁEdevelop�E�E30 タグ検索ランキングのみ�E�E
- **成果物**: `nicorank2019_20260906.zip`�E�Eicorank2019.exe / exe.config / nicorank.xml.org(TAGRANK節入めE / lib 4件�E�runtimes 3種�E�に加え、最新DB作�E導線として `nicorank_SnapShot_20260906.zip`�E�Exe / exe.config / lib。コード無変更・Releaseビルド�E果物を�Eワイトリスト方式で圧縮�E�を後から追加添付。いずれめEB・設定本体�Epdb・Outputなし。oldlogは無変更のため添付なぁE
- **検証**: `dotnet test` 165件PASS、ソリューションDebugビルド＋Releaseビルド�E功�E警呁E、`loadFromRemoteSources` 確認、`nicorank.xml` 一致�E�EHA256。PostBuild xcopy未反映のため手動コピ�Eで解消）。ユーザー実行確認�Eタグ検索のみ�E�新DBで件数一致�E�。週閁E中閁ESPの回帰実行�E本リリース前に実施
- **判明した問顁E*: なぁE
- **同期**: main ↁEdevelop めE`--no-ff` でバックマ�Eジ。バチE��マ�Eジ直後�E `git diff main develop --stat` 空を確認。本エントリ追記によりdevelopぁE件先行（前回リリースと同様�E運用�E�E

---

## 2026-09-06 タグ検索ランキング (#30)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/30
- **ブランチE*: `feature/t30-tagrank-search` ↁE`develop` に `--no-ff` でマ�Eジ。ブランチ削除済み
- **背景**: SPモード相当�E雁E��をチE��スト�E動画IDリストではなく、スナップショチE��v2のライブ検索結果で行う。新タブ「タグ検索雁E��」を追加し、�Eイント計算パネルは雁E��タブと共有すめE
- **実裁E�E容**:
  - UI: 新TabPage「タグ検索雁E��」！E.タグ条件ↁE.絞り込みↁE.DB・前回結果→�E有係数パネル→実行�Eタン�E�。係数パネル�E�Epanel3`�E��E実佁Eつのままタブ�E替で付け替え＋`grpDb` 基準�E相対配置�E�EutoScaleずれ対策）。下限初期値0�E�E=持E��なし）�E種別「指定なし」�E投稿日はチェチE��ボックスONで入力可。タグ条件のEnter確定で件数確認を実行。上限趁E��時�E実行�Eタンを押せなくし、条件変更で復帰。未入力時は実行不可
  - `TagConditionParser`�E�EnicorankLib/SnapShot`�E�E `タグ1&タグ2|タグ3*` ↁEjsonFilter�E�E&`=AND優先�E`|`=OR・`*`なぁE`tagsExact`・`*`あり=`tags`、` *`は末尾1斁E���Eみ許可�E�E
  - `SnapShotRequest.CreateTagSearch`: タグはjsonFilter、数値下限4種・日付�E種別は `filters[]` 実証済み記法。`q` 空・`targets` 不使用。下限0・日付OFF�E�中立期閁E000-01-01、E100-01-01�E��E種別「指定なし」�E持E��なぁE
  - `TagRankAnalyze`�E�EAnalyze/Input`�E�E 件数取得�E5丁E��E��時�E中断通知ↁE00件ÁE並列�Eージング→ID重褁E��去・ID頁E��`MaxTotalCount` 定数匁E
  - `TagRankTotalReader`�E�EAnalyze/Option/Basic`�E�E 基準日DBなし専用�E�EnalyzeDBのみ→Total取得�EMovieInfo補完�E全件 `Count=Total`�E�。SP共用クラスに手を入れなぁE��め�E新設�E�空DBダミ�E案�E不採用�E�E
  - `ModeFactoryTagRank`�E�EFactory`�E�E Base有無で刁E��（なし時は `BaseDay=TargetDay`�E�。SP相当�E7種出力�E履歴なし�E前回CSV任意。`EAnalyzeMode.TagRank` 追加
  - `NicoRankXml.TAG RANK`�E�`Config.IsTagRank`�E�節単位フォールバック。頁E��欠落があれ�E週間設定）＋`依存ファイル/nicorank.xml` にTAGRANK節�E�EP同値�E�E
  - UI実行�E緁E タブ�E替時�E係数値 保存�E刁E��→読込�E�不正値は刁E��中断�E�。集計スレチE��からはコントロールに触れなぁE��め実行条件めE`TagExecuteContext` に退避�E�クロススレチE��例外対策）。係数 Load/Save 抽出�E�`CALC_LIKE` 保存漏れ修正�E�従来は表示のみ�E�。`GetModeFactory` null・`CreateAnalyzer` false のガード追加
- **設計判断**�E�詳細は `design.md` のIssue #30節�E�E 入力�Eみ差し替え�E差刁E��降�ESP流用。数値・日付�E種別は `filters[]` 実証済み記法！EsonFilterのrangeは未検証のため回避�E�、E丁E��制は件数取得で中断方式。TAGRANK節は節単位�E替・OFFSET系は共送E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全165件PASS�E�既孁E36�E�新要E9、EXIT CODE 0�E�、`dotnet build nicorank2019.sln` 成功・警呁E
  - reviewerレビューÁE�E��Eレビュー: 中4件�E�E*`位置検証・TAGRANK部刁E��落NRE・Query null防御・CreateAnalyzer戻り値無視）を修正、�Eレビューでマ�Eジ可判定。低指摘�E見送り刁E�E琁E��をコミットメチE��ージに記録
  - ユーザー実行確誁E 新DBでタグ検索件数と雁E��対象件数がほぼ一致することを確認後にマ�Eジ
  - 別件刁E��刁E���E�実裁E��し！E 当�E900ↁE0件不一致は雁E��実裁E��はなくDB側の問題と特定、E024-08-30、E026-09-04の取得コード不�E合！EflgLimit1000` 無視�E常晁E000制限）で作られたDBは全期間1000以上�Eみ。`再生数 < 1000` ぁE件なら旧DB。�E取得で解消確認済み�E�低�E生衁E008313件�E�E
- **残課顁E*: `AnalyzeAsync` の無条件「集計�E功」ログ・`TagRankAnalyze` 0件成功の扱ぁE�E辞書式ID頁E�E`_tagMockLoaded` 命名（いずれも別Issue化推奨。低指摘見送り刁E��E
- **リリースノ�Eト原稿**�E�リリース時に転記！E
  - タグ検索雁E��を使ぁE��へ�E�スナップショチE��DBは最新版を推奨�E�旧DBは総合・SP雁E��に支障なし。タグ検索には不十刁E��。目安：`SELECT COUNT(*) FROM Ranking WHERE 再生数 < 1000;` ぁE件なら旧DB�E�E026-09-04より前�E取得�E�E�E
  - トラブルシューチE��ング�E�件数不一致時！E 1. 1000再生フィルタ�E�E年趁E�E古動画は1000以上�Eみ収録が仕様！E. DBの鮮度�E�作�E日より後�E投稿は未収録�E�E. DB作�E時�Eエラー�E�前回と比べファイルサイズが著しく小さぁE��合�E再取得！E

---

## 2026-09-07 LogOfficial.db肥大化�E調査(編雁E��向けレポ�Eト作�E)

- **Issue**: なぁEdocs・調査のため不要E、E*ブランチE*: `develop` 直(コード変更なぁE
- **背景**: 7年運用で `LogOfficial.db` ぁE2GB趁E��SQLite仕様上�E限界ではなぁE��実用上�E受け渡し�EバックアチE�Eが重ぁE��「問題になる前に」改喁E�Eイントを知るため、RankingチE�Eブル参�E処琁E�EぁE��雁E��日期間の持E��がなぁE≒古ぁE��ータ削除で壊れめE箁E��を洗い出す調査
- **実施冁E��**:
  - `docs/knowledge/` 読亁E��exploreÁE並列でRanking参�ESQLを�E件洗い出し。過去無制限遡り�E `RankingHistory.cs:220-223`(`CheckSoMovieNeedSabun`)のみと特定。他�EID点照会＋`LIMIT 1`か紁E営業日の `BETWEEN`
  - 実DB(`T:\雁E���Eログラム仮\DB\LogOfficial.db`)めE`mode=ro` 読取り専用で実測(書込みなぁE: 11.31GB・2646日・Ranking全1儁E107丁E��E1年趁E儁E666丁E��E82.7%)・so distinct 275,657・so衁E020丁E���EMovie 233丁E��E実データ紁E21MB)・`PRIMARY KEY(ID,雁E��日)` あり・全問合せインチE��クス経路・freelist=0・WAL
  - so全行走査で休眠ギャチE�E実測: ギャチE�E持ちso 55,925件(紁E0%)・イベント累訁E0,701件・直迁E年終亁E�E21,780件・最大2617日。ユーザーの運用知要E公式動画の再�E開�E日常皁E��いわゆる見るタイチE��画問顁Eを裏付け、当�Eの「稀」想定を撤回しSoHistory併設忁E��に刁E��ぁE
  - 壁打ちで認識整吁E 推奨=①1年保持�E�SoHistory併設(live紁EGB維持E、比輁E②何もしなぁE年2GB増�E3年後紁E7GB・便益ほぼなぁE、Eovie紁E00MBは対象夁E
- **設計判断・運用決宁E*(編雁E��相諁E��レポ�Eト�Eリポジトリ管琁E���Eため要点のみここに残す):
  - 運用方弁E 年1回手動ではなく、毎回の更新動作時に古ぁE��ータの自動削除・SoHistory更新。�E回集計時に初回実衁E
  - ロールバック保険: 対策版リリース前に2daime管琁E�ENASへ旧DBを温孁E
  - 人気タグ窓�E受容: 年間ランキングの取得窓が1年に刁E��詰められるが、�E式タグ未更新�E�固定タグ代替(#27)実裁E��みのため影響ほぼなぁE
  - `RankingDate` は削除対象夁E 更新再開位置のしおりであり、消すと2019年からの全期間再取得になる。メンチE��判定にも使用、E646行�E数十KBでコスト�E無視できる。来歴は `d7e5070`(2024-06サイバ�E攻撁E��忁Eで新設、メンチE�Eは新設時から同梱、旧来はRanking直MAX取得だっぁE
- **検証**: コード変更なし�Eためビルド�EチE��ト対象夁EMUST 5はユーザー承認で省略)。測定クエリはすべて読取り専用で実DB無改変を確認。施策前後�E差刁E��証(対策前征E環墁E��1ヶ月間週刊比輁E��にリリース)は2daimeが実施予宁E
- **残課顁E*: メンチE��ンスタブ「DBの最適化」追加予宁E断牁E��対筁E。実施フェーズ(Issue化�ESoHistory設計�Eprune手頁E�E開発検証)は別タスク化予宁E

---

## 2026-09-12 タグ検索v2最新値オプション (#35)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/35
- **ブランチE*: `feature/t035-tagrank-live-counter` ↁE`develop` に `--no-ff` でマ�Eジ。続けて `feature/t031-logofficial-prune-sohistory` へも取込�E�E031のdevelopマ�Eジ判断には影響させなぁE��。�Eージ後にfeatureブランチ削除
- **背景**: リアルタイムに結果だけ知りたぁE��にSnapshotDB�E�E0070306からの全期間全動画フルスナップショチE���E��E取得�E時間・チE�Eタ量とも過剰なため、E��計日にv2最新値の選択肢を足ぁE
- **実裁E�E容**:
  - UI�E�ユーザー拁E��！E `chkUseLiveCounter` をgrpDbに配置・既定TRUE。判定�E退避・Enable刁E��配線�E後工程で実施、Eesigner全体が環墁E��で再生成されたため表示確認�Eユーザー実行確認に委�EぁE
  - `TagSearchQuery.UseLiveCounter`�E�既定false。唯一の刁E��。`SetInputFile` の引数は不変！E
  - `TagRankAnalyze.LiveCounters`�E�EDↁE数値。従来捨ててぁE�� `DefaultFields` のカウンタを保持。`CollectContentIds`→`CollectContentData` に変更。重褁E�E先勝ちでコメント�E記！E
  - `TagRankLiveTotalReader`�E�基準なし、EB不要E���E`TagRankLiveSabunReader`�E�基準あり。Target=ライブ�EBase=基準日DB。新着救済�ESabunReaderと同一�E�新設。純粋�E琁E`ApplyLiveTotals` はstatic共用
  - `ModeFactoryTagRank` めE刁E��化�E�EnapshotDB×基準あめEなし＋v2最新×基準あめEなし）。v2時�E雁E��日は実行日
  - frmMain配緁E `TryBuildTagSearchQuery` 退避・ライブON時�EAnalyzeDB存在確認スキチE�E・`frmMain_Load` でのCheckedChanged配線＋�E期Disable�E�Eesigner再生成差刁E��避けコード�Eで実施�E�E
- **設計判断**�E�詳細は `design.md` のIssue #35追記！E Reader刁E��・Input共有参照�E�Enput冁E��結案�E肥大化�Eため不採用�E�。`RankingAnalyze` のInput→Option頁E��を前提とし事前条件をremarks明記。�Eイント係数見直し�E `TAGRANK` 節の運用調整に刁E��
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全173件PASS�E�既孁E65�E�新要E。�E訳: LiveCounters保持1・ApplyLiveTotals対応付け1・Open成否4・工場刁E��E、EXIT CODE 0�E�、`dotnet build nicorank2019.csproj` 成功
  - reviewerレビュー�E��Eレビュー: 中1件�E�無効欁E��値ブロチE��→存在確認スキチE�E�E��E佁E件�E�EetBaseTime改名�EGetMovieData要紁E�E先勝ち明記�E事前条件remarks�E�を解消し再レビュー通過。工場の基準DBあり成功系を外した理由はチE��ト�Eコメントに記録
  - ユーザー実行確誁E エラーなく実行＋当日SnapshotDB版との比輁E��証、E88行vs187行で共送E87件の値列完�E一致、差刁E�E `sm43925516`�E��E甁E37の低�E生古動画。旧DBの1000再生足刁E��に該当！E件の有無による頁E��E1シフトのみ、EiveCountersとSnapshotDB値の等価性を実証。specs.mdに1件出入り�E旨を追訁E
- **残課顁E*: DB以外（登録用JSON・前回CSV�E�を基準にする案�E検討�E結果不採用�E�差刁E��はDBが忁E��と結論。新Issueなし）。`TagRankLiveSabunReader` 差刁E�E岐�E純粋関数刁E��出し�E封E��検討！Eeviewer任意指摘！E

---

## 2026-09-13 頁E��計算�E同点時タイブレーク (#34)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/34
- **ブランチE*: `feature/t034-ranking-tiebreak` ↁE`develop` に `--no-ff` でマ�Eジ�E�E25790d�E�。続けて `feature/t031-logofficial-prune-sohistory` へも取込�E�Ed09bb0。t031のdevelopマ�Eジ判断には影響させなぁE��を�E記、E35と同一方式）。�Eージ後にfeatureブランチ削除
- **背景**: #31対策�E前後比輁E���E週・2026-09-08�E�で前回頁E��が2件だぁEずつずれた。今週の計算�E1000/1000完�E一致で、ずれ�E先週時点で発生してぁE���E�同点で頁E��だけ±1�E�。原因は `RankingAnalyze.calcRanking` が単一キー降頁E��連番のみで、同点時�E頁E��が入力頁E��存（並列取得�Eため不定）だったため。本来#34は#31比輁E��亁E��に着手する建前だったが、E��E��が安定しなぁE��比輁E�E体がめE��にくいため両ブランチへマ�Eジした
- **実裁E�E容**:
  - `RankingIdComparer` 新設�E�EnicorankLib/Analyze/model`。`IComparer<string>`・スチE�Eトレス共有Instance�E�。種別�E��E頭の非数字部。sm/so等）�E数字部の頁E��比べ、数値化�E有無で群を�Eけてから群冁E��辞書式比輁E��る（直接フォールバックすると推移律が崩めEsm10・sm10a・sm9 の循環になるため。reviewer持E��対応）。桁判定�EASCIIの0、E限定！Echar.IsDigit` のままだと全角数字�E扱ぁE�� `TryParse` とずれるためE��E
  - `RankingAnalyze.calcRanking` の6種�E�総合・再生・コメント�Eマイリスト�EぁE��ね・カチE��リ�E�に `.ThenBy(ID, RankingIdComparer.Instance)` を追加。頁E��値は連番維持E��同頁E��スキチE�Eなし！E
  - 並列ソート前に全件の `PointTotal` を単一スレチE��で確定させるウォームアチE�Eを追加。`Ranking.CalcPoint` のキャチE��ュ�E�EworkPointTotal`�E��EスレチE��セーフでなく計算途中の部刁E��を書き込みながら進めるため、Eタスクの並列�E回計算が重なると別タスクが部刁E��を読んで頁E��が不定になる既存�E競合が、単体テスト�E件実行で1回だけ発覚した（カチE��リ頁E��ずれ）。`CalcPoint` 自体へのロチE��は篁E��拡大のため見送り
  - `nicorankLib.csproj` にCompile1行追加�E�Eld-styleのため手動登録が忁E��E��E
- **設計判断**�E�詳細は `design.md` のIssue #34節�E�E 第二キーはIDのみ�E�投稿日・再生数も同点があり得るため�E��E6種すべて・連番維持E��差刁E��小）。辞書式ではなく数値認識（辞書式では sm199 ぁEsm20 より先になる桁E��ぁE��E��があるためE��、Eomparer1個にまとめる�E�EhenBy二次比輁E���E同点時にだけ呼ばれるため処琁E��スト最小！E
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj` 全182件PASS�E�既孁E73�E�新要E。�E訳: 数値頁E・種別頁E・非数値フォールバックと群刁E��1・墁E���E�空斁E���E前ゼロ�E�E・null/同一1・総合数値頁E・入力頁E��転の決定性1・副頁E��数値頁E、E回連続PASSでflaky解消を確認、EXIT CODE 0�E�、`dotnet build nicorank2019.sln -c Release --no-incremental` 成功・警呁E
  - reviewerレビュー�E��Eレビュー: 高�E中なし。佁E件�E�推移律�EIsDigit・副頁E��assert・墁E��チE��ト）を全対応し、追加で既存競合�EウォームアチE�Eを実施。�Eレビューでコード問題なし�EドキュメンチE件のみ持E��→修正済み�E��Eレビュー不要と判定！E
  - ユーザー実行確誁E 実集計�E `rank1000.txt` 修正前後比輁E��E000行�E27列�EID雁E��は完�E一致、値列�E差刁E�E0セル�E�頁E��以外�E全列がIDごとに完�E一致�E�。差刁E�E6種の頁E���Eのみ�E�総合42・カチE��リ8・再生78・コメンチE25・マイリス905・ぁE��ね552。�Eイリス等が多いのは同点群が巨大なため。例：�Eイリス数は176種類しかなく、E0」が33件�E�。修正後ファイルは6種すべてで主キー降頁E��ID数値認識頁E��完�E一致�E�違叁E件�E�E
  - t031取込時�E `docs/design.md`・`docs/knowledge/testing.md` がコンフリクト（隣接セクション追加同士�E�。両方残す形で解消し、`testing.md` はt031側のSoHistory15件と#35系8件と#34系9件を合算して197件に更新。`docs/tasks.md` の自動�Eージは#35完亁E��態（履歴行あり）を優先し、t031側の旧#35未完亁E��は解消済みとして扱った。取込後にt031上で全197件PASSを確誁E
- **残課顁E*: なし。`CalcPoint` のロチE��化�E `MergeRankingList` の辞書頁E���E確定化は、現状ウォームアチE�Eとタイブレークで決定的になるため見送り

---

## 2026-09-14 SnapShot Linux対応CLI (#37)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/37
- **ブランチE*: `feature/t037-snapshot-linux-cli` ↁE`develop` に `--no-ff` でマ�Eジ。続けて `feature/t031-logofficial-prune-sohistory` へも取込�E�E031のdevelopマ�Eジ時競合�E事前回避、E34・#35と同一方式）。�Eージ後にfeatureブランチ削除。�EチE��ュはユーザー持E��征E��
- **背景**: nicorank_SnapShot�E�スナップショチE��取得ツール�E�を Linux�E�EAS・x64・.NET 8 ランタイムあり�E�で動かす。取得中核は nicorankLib/SnapShot に刁E��済みで、調査の結果 nicorank.xml・DB/ フォルダは取得単体では不要と確認したため、WinForms を持たなぁEnet8 CLI の新設で足りると判断した。WinForms 部刁E��開始�Eタン・サスペンド�ETaskDialog�E��E Linux 版に持ち込まなぁE
- **実裁E�E容**:
  - `nicorank_SnapShot.Cli` 新設�E�Eet8.0・SDK-style・top-level statements�E�。`SnapController.GetSnapShotAsync()` を呼び、終亁E��ード�E 0=成功/2=エラー�E�Enicorank_oldlog` と同一規紁E��。`--help` のみ取得せず終亁E��めE
  - `SnapController` の失敗検知3件修正�E�E37レビュー持E��対応）。`InitilizeDB()` 失敗時は早期確定、`RegistDB()` 2か所�E�途中・最終残件�E��E失敗を記録しつつ続行（被害最小化�E�、catch の例外時は `false` を返す�E�従来は成功扱ぁE��った）。いずれめEWindows 側のエラー表示が正しくなる方向�E変更
  - パッケージは `Microsoft.Data.Sqlite 10.0.11` / `Newtonsoft.Json 13.0.4`�E�EnitTest と同版。pitfalls 4f の混在回避�E��E`System.Text.Encoding.CodePages 8.0.0`�E�Eldlog と同版�E�、Eostura は使わなぁE��起動直後に `CodePagesEncodingProvider` を登録する�E�登録呼び出し�E oldlog になぁECLI で追加�E�E
  - `nicorank2019.sln` に登録�E�Eldlog と同じ SDK-style 種別・AnyCPU マッピング�E�。既孁Enet48 WinForms は Windows 用として残す
  - nicorankLib 全体�E net8 化�E篁E��が庁E��ぎるため見送り。`InternetUtil` の HttpClient 化も段階移行として別タスク化。oldlog の Newtonsoft 13.0.3 は稼働実績構�Eを変えなぁE��め見送り
- **設計判断**: 桁EハイブリチE���E�Eet8 から net48 ライブラリを参照、Einux 稼働実績のある nicorank_oldlog と同じ形�E�。`RankingHistory` の `MessageBox` は SnapShot 経路から到達しなぁE��め�E回�E不問とし、実際にハイブリチE��参�Eでビルドが通ることを確認して確定しぁE
- **検証**:
  - `dotnet build` 成功、`dotnet test UnitTest/UnitTest.csproj` 全182件PASS�E�EXIT CODE 0�E�。develop マ�Eジ後も182件PASS、t031取込後�E197件PASSを確誁E
  - reviewerレビュー�E��Eレビュー: 中3件�E�EnitilizeDB/RegistDB 戻り値無視�ECodePages版差�E��E佁E件�E�例外時の画面出力�Ecsprojコメント�Eapps.md表現・Newtonsoft微差�E�を全対応また�E琁E��付き見送りし�Eレビュー通過�E�問題なし�Eマ�Eジ可�E�E
  - NAS実機！ES224�E�で portable publish�E�E-r` なし）を配币E�� `--help` 終亁E��ーチE0 を確認。続けて実取得を実行し `LogSnapshot_20260914.db`�E�E15MB・8,997,750行�E低�E甁E93,261行�Eintegrity_check ok・DBVersion=20260914/1.0.1.0�E��E正常性を確認。日付表示の `M/d/yyyy` は NAS ロケールによる `ToShortDateString` の差であり仕様通り
  - t031取込時�Eコンフリクトなし！Ert 自動�Eージ�E�。`docs/tasks.md` は develop 側の#34完亁E��態（履歴行あり）を優先し、t031側の旧#34未完亁E��は解消済みとして扱われた。`docs/tasks/archive.md` の#34追記！E6d40e8�E�も t031 が取り込んでぁE��かった�Eとして一緒に取り込まれた�E�ドキュメント�Eみで t031 の develop マ�Eジ判断に影響しなぁE��E
- **実裁E��ウハウ�E�Einux 配币E�E運用�E�E*:
  - 配币E�E `-r` なぁEportable publish�E�`dotnet xxx.dll` 実行に決定。`-r linux-x64` 付き publish では NuGet 由来の `runtimes/linux-x64/native/libe_sqlite3.so` が�E力から落ち、win 用だけが残って Linux で動かなぁE��とを実確認した！E-r` なしなら�E RID 同梱で linux-x64 を含む�E�。�E力直下�E `lib/`�E�Ein 用 DLL 群�E��E net48 参�E允E��ら流れ込む残骸であり Linux では無視される
  - `CodePages.dll` は共有フレームワーク提供�Eため publish 出力に含まれず、E.0.0 と 10.0.11 の競合�E起きなぁE��Eroject.assets.json で確認！E
  - コピ�E対象は `.exe`・`.pdb`・`lib/` 以外�E全部。`runtimes/` は `linux-x64` のみ残して他�E削除可�E�紁E5MB→紁EMB�E�。`nicorank_SnapShot.Cli.runtimeconfig.json` は忁E��（コピ�E漏れに注意）。�Eログラム群は読取�Eみ、�E力�Eフォルダに実行ユーザーの書込権限が忁E��E
  - 同一日は出力ファイル名が同一で `InitilizeDB` が削除→�E作�Eするため、同時実行�E DB 破壊につながる。月1日・毎週月曜の2タスク運用では `flock -n` で重褁E��スキチE�E�E�同一処琁E�Eため実害なし）、スクリプトは `set -euo pipefail`�E�終亁E��ードゲート＋`wal_checkpoint(TRUNCATE)` 後�E移動！Emv`。作業側に旧DBを残さなぁE��めEglob が曖昧にならなぁE��とする
- **残課顁E*: Linux 実機での定期タスク化�Eユーザー運用側で継続（ロチE��付きスクリプト・朁E�E�週1の2タスク構�E�E�。取得物の月次アーカイブ�Eは `/volume1/nicoran/Snapshot/2026/`

---

## 2026-09-21 LogOfficial.db肥大化対筁E(#31)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/31�E�検証結果を追記してクローズ�E�E
- **ブランチE*: `feature/t031-logofficial-prune-sohistory` ↁE`develop` に `--no-ff` でマ�Eジ�E�E3f2abe�E�、E37は別途�E行�Eージ済み�E�Ee81623�E��Eため本差刁E�E#31のみ。tasks.mdコンフリクト�Edevelop側の#37完亁E��態を優先し#31節を残す形で解消。�Eージ後にfeatureブランチ削除。�EチE��ュはユーザー実施
- **背景**: 7年運用で `LogOfficial.db` ぁE2GB趁E��Eanking全1儁E107丁E���E82.7%ぁE年趁E��、E026-09-07調査で過去無制限遡り�E `CheckSoMovieNeedSabun` のみと特定し、E年保持�E�SoHistory併設の方針に決定しぁE
- **実裁E�E容**:
  - Ver1移行！EDbCurrentVersion` 0ↁE�E�E SoHistory作�E�E�保持墁E��より古ぁE���E最新を�E期退避�E�墁E��以降�E混入行渁E���E�古いRanking削除�E�Movie廁E���E��E回�EみVACUUM。退避は日次と同じ条件付きUPSERT�E�EWHERE excluded.雁E��日 > SoHistory.雁E��日`�E�に統一し、中断再開時�Ecutoffずれでも最新1件に収束する�E�Eeviewer低指摘対応！E
  - 日次 `RefreshSoHistoryAndPrune`: 消える行を拾ってから削除し、同日次トランザクションに同梱する�E�EACUUMなし）。当日刁E�E上書き�EしなぁE��当日刁E�ERankingに残るため不要で、置き換えると基準日より新しい値になり�E公開チェチE��が効かなくなる！E
  - `CheckSoMovieNeedSabun` はRanking優先�EなければSoHistoryを見る2クエリ逐次。両方になければ新着扱ぁE��、表なし旧DBでも正常終亁E��る。問合せは `雁E��日 <= @Date` ガード付き、EOIN・VIEWの1本化�E見送り
  - `GenreAnalyze.cs` 削除�E�csproj参�E削除�E�Movie書込みブロチE��削除。SPAnalyze側の同名Genre SQLは残置�E�E31篁E��外�Eため別タスク化！E
  - 保持墁E��はDB最大日基準！EMAX(雁E��日)-365日` 未満削除�E�。prune用索弁E`idx_Ranking_雁E��日` を恒乁E��
- **設計判断**�E�詳細は `design.md` のIssue #31節�E�E SoHistoryはID�E�集計日�E�E数値のみ�E�タグは差刁E��使わず別経路のため含めなぁE��。最新1件のみ保持し�E履歴は持たなぁE��差刁E�Eの用途には十�E�E�。SoHistoryの日付�E常に保持墁E��より古ぁE��変条件により、無制限履歴の「基準日以前�E最新行」と同じ結果になめE
- **検証**:
  - `dotnet test UnitTest/UnitTest.csproj -c Release` 全197件PASS�E�既孁E82�E�新要E5、EXIT CODE 0�E�、`dotnet build nicorank2019.sln -c Release` 成功。ビルチE回目は起動中のnicorank2019.exeがbinをロチE��しMSB3021/3027で失敗したため、exe終亁E��に同条件で再実行し成功を確誁E
  - reviewerレビュー: 総合判定�Eージ可。佁E件のぁE��4件対応！Eackfill条件付きUPSERT統一・db.md現行Ver1・nicorankLib.mdフォールバック追記�Especs Ver1明記）、E件見送り�E�EPAnalyze.GenreSQL削除・SoHistoryExistsキャチE��ュ・追加チE��チE件は別タスク化。理由はコミットメチE��ージに記録�E�E
  - 実DB破損対忁E 2026-09-08試行で `SQLite Error 11: database disk image is malformed`�E�Eanking本体�Eージ2468645、E508875帯・紁E00件�E�を確定。rowidチャンク�E�row-by-row救�Eで再建し、バチE��アチE�Eから最新仕様で通し再実行（紁E時間�E�。結果は `LogOfficial.db` 2.01GB・integrity_check ok・Ranking 24400035行�ESoHistory 244852行�E週刊�E力一式正常
  - 対策前征E環墁E��輁E 9-08週は総合1000/1000一致・前回2件±1�E�同点帯の先週持ち越し�E�、E-21週は件数・ポイント一致で頁E���Eみ差�E��Eイリスト等）があり、原因は修正前exeがタイブレーク導�E前！E34は9-13導�E�E��E古ぁE��ルドだったと確定、E31起因の揺らぎなしとして検証終亁E��、Eヶ月比輁E��征E��ずユーザー持E��でマ�Eジした
- **残課顁E*: 見送り刁E�E別タスク化！EPAnalyze.GenreSQL整琁E�ESoHistoryExistsキャチE��ュ検討�E追加チE��チE件�E�、E32メンチE��ンスタブ「DBの最適化」！EACUUM�E��E#31完亁E��に着扁E

## 2026-09-23 Snapshot API v2更新チェチE��(#38)

- **Issue**: https://github.com/n2daime/nicorank2019/issues/38
- **ブランチE*: `feature/t038-snapshot-version-check` ↁE`develop` に `--no-ff` でマ�Eジ�E�Ea5f3a0�E�。developの#31取込を事前にfeature側へマ�Eジし競吁E件�E�Eesting.md件数�E�を214件に統吁E
- **背景**: Snapshot API v2のチE�Eタ更新時刻が後ろ倒し傾向にあり、前日チE�EタでLogSnapshot DBを作ると後段雁E���E差刁E��ずれる。�E式�Eversionエンド�Eイント！E.../snapshot/version` ↁE`{"last_modified": "..."}`�E�で取得前に更新有無を判定すめE
- **実裁E�E検証**:
  - `SnapShotVersionChecker`�E�E値判定�EJST日付比輁E�E`DateParseHandling.None`での日時パース�E��E`SnapShotVersionPoller`�E�E刁E�最大1時間征E���E時訁E征E���E注入可�E�を新設。取得実行とは刁E��し、事後動作（ダイアログ/リトライ�E��E呼び出し�Eに委�EめE
  - WinForm�E�Eorm1�E�E 未更新時に日時�EりOK/キャンセル確認ダイアログ、確認不�E時にエラーダイアログ。チェチE��は`await Task.Run`化！EI凍結回避・reviewer中持E���E�E
  - CLI�E�Eicorank_SnapShot.Cliのみ�E�E 未更新/確認不�Eの間リトライ、更新検知で通常取得（終亁E��ーチE�E�、E時間経過も未更新なら取得せず終亁E��ーチE�E�EInitilizeDB`に触れなぁE��EBエラーではなくリトライタイムアウトであることをnicorankerr.logに記録�E�E
  - `LastModifiedRaw`は値そ�Eも�Eを保持�E�本斁E�E体ではなぁE�Ereviewer中持E���E�、ELIの二重出力解消�EInvariantCulture持E��！Eeviewer低指摘）。Pollerのsleep上限化�E見送り�E�精度不要�Eため�E�E
  - 実裁E��に単体テスチE件失敗で発覚：`JObject.Parse`既定ではISO日時がDateト�Eクンに化けてオフセチE��が落ちる。`DateParseHandling.None`で斁E���Eのまま`DateTimeOffset.TryParse`に回す�E�Eitfalls頁E��22�E�E
  - `dotnet test UnitTest/UnitTest.csproj` 214件PASS�E�E38で17件追加�E��E両アプリビルド�E功！EXIT CODE=0�E�E
  - reviewerレビュー→忁E��E件修正→�Eレビュー問題なし（残った佁E件も整琁E��み�E�E
  - NAS実機検証�E�E/21�E�E 06:37開始�E8回未更新ↁE7:14更新検知→�E期間取得�E終亁E��ーチE。WinFormはユーザー実行確認済み・問題なし。タイムアウト経路の実機テスト�E省略し単体テストで代替
- **調査�E�仮説検証�E�E*: 9/13の、E時過ぎに件数増加」記�Eとlast_modified�E�E時台�E��Eズレを調査。versionと実データ件数の同時測定を4朝実施し、�E替時刻は07:08/07:14/07:09/07:07と日、E��ラつくこと�E�単調長期化ではなぁE��、いずれも実データ段差と一致すること�E�East_modifiedは信用できる�E�を確認。specsに実測注記�Epitfalls頁E��23�E�本取得タスクとの競合注意）を追記。調査スクリプト自体�Eマ�Eジ前に履歴から除去�E�E.gitattributes`のLF固定ともども除外。動作版はNASに配置済み�E�E
- **残課顁E*: Issue #39�E�タグ検索v2最新値モード�EチE�Eタ時点表示�E��E別タスクとして継綁E

---

## 2026-09-26 ApiXML�R���̍폜��������ʂɉe�������Ȃ��i#40�j

- **Issue**: #40�i�{�́j�A#41�i�����͈�A�̏��������B��ɏT��ApiXML�~�σ}�[�W�֕����]���j�A#42�i�W�v���BasicOption�j���o�H�B���r���[�c�ۑ肩�番���j�A#43�i���C�u�����w�̒��ڃR���\�[���o�͂̐����B�ʃZ�b�V�����Ή��j
- **�u�����`**: `feature/t040-apixml-no-rank-effect` �� `develop` �� `--no-ff` �Ń}�[�W�B�u�����`�폜�ς݁B�v�b�V���̓��[�U�[�w���҂�
- **�w�i**: ApiXML.db�͕\���p�L���b�V���̂͂����A�擾���s����isDelete�𗧂Ă邽�ߎ擾�^�C�~���O����ŏ��ʑS�̂�����Ă����B���_�^�C�u���[�N�i#34�j�Ɠ��l�A�W�v�^�C�~���O�Ō��ʂ��ς��͖̂{���̈Ӑ}�ł͂Ȃ����ߐ�������
- **�������e**:
  - `NicoApi.GetUserInfo` / `GetMovieInfo` ����isDelete���4�ӏ����������A���s���͋󗓁E����l�̂܂܎c���B`SELECT XML` ���ŐV�擾���ɓ��ꂵ��
  - `MovieInfoReader` / `GenreInfoReader` / `UserInfoReader` / `FavoriteTagReader` �͊m�ہE�ǎ�̎��s�ł��W�v���f���Ȃ��悤�ɂ���
  - `SpMovieInfoFallback` ��V�݂��ASP�̌�������B�iLastResult�ŐV�^�C�g���ELogOfficial���ԓ��������j�ŕ₤�B�^�C�g�������͎��=Weekly�D���2�i�����ɂ����B�������͏��O�Ɏg��Ȃ����j���R�����g�Edesign�ɖ��L����
  - �T���̎��O�擾�Ƃ���oldlog�T���ۑ����ɑSID��26000�����ꊇ�擾���ē��t�t�H���_��ApiXML.db��u���A2019���̏T��JSON�擾��Ɏ�荞�ށi�V�����擾�������u�������j�B���񐔂�config.json��nicoapi_thread_max�ŊǗ����iThreadMaxOverride�j�Anicorank.xml�ˑ����Ȃ�����
  - ��񌇗����c��ꍇ�̓^�C�g�����擪�Ɂy�W�v��폜�z��t����i��ǉ��Ȃ��j
  - oldlog��SQLite�s���ibatteries_v2���z�u�j�͎Q�Ƒ��ւ̒��ڃp�b�P�[�W�Q�Ɓ{Costura���O�ŉ��������B�i���\����\r�㏑���{���_�C���N�g�Ԉ����ɉ��߂�
  - `UnitTest`20���ǉ��i�v234���j�B�����́u�m�ێ��s����false�v�e�X�g�͐V�d�l�ɍ��킹�čX�V����
- **����**:
  - `dotnet test` 234��PASS�E�\�����[�V�����S�̂�oldlog Release�̃r���h�����iEXIT CODE=0�j
  - reviewer���r���[�Œ�4���E��6���̎w�E���󂯁A�S���Ή��i��2��������j��ɍă��r���[�ő�������}�[�W�ɂȂ���
  - �T���̎��@���؂�26770���̎捞�Ǝ擾2372���ւ̍팸���m�F���A�^���t�@�C������E�Ȃ��̗��o�H�����삵���BSP�̎��@���؂̓G���[�Ȃ��ŏW�v�ł��A�}�[�J�[0���͐���i�������H�Ȃ��߁j
- **���s�m�F���̕s��Ƒ΍�**: oldlog������s��batteries_v2�s���Enicorank.xml�s�݁E�z�u�~�X�i�V��DLL���݂�MissingMethodException�j���o���B��������C�����A�Ĕ��h�~��pitfalls����4g�E4h�ɋL�^�����B�T��JSON�̃��A���^�C���ϓ��ɂ�関�擾���͗��T���j�̓�����ɍĊm�F���A��肪����Ε�Issue������
- **�c�ۑ�**: #41�͏T��ApiXML�̒~�σ}�[�W�ɂ�钷���L���b�V���č\�z�֕����]�����A#32�������2019�������e�i���X�@�\�Ƃ��Ēǉ�����B#42�E#43�͕ʃ^�X�N�Ƃ��Ďc�u����

---

## 2026-09-26 �����e�i���X�^�u��UI���b�N (#32�E32.1)

- **Issue**: #32�iOPEN�ێ��B32.2�̒��g�������c�邽�ߕ��Ȃ��j
- **�u�����`**: `feature/t032-maintenance-tab` �� `develop` �� `--no-ff` �Ń}�[�W�B�u�����`�폜�ς�
- **�w�i**: #31�̓���prune�ł�VACUUM���Ȃ����߁A�蓮�œK���̒u���ꏊ���K�v�ɂȂ����B�����Ȃ���������AUI���b�N�i�����ڂ����E���SDead�j�Ń��C�A�E�g���ł߂Ă��璆�g�����i�ߕ��ɂ���
- **���{���e**:
  - `tabPageOut` �ɑ�3�^�u `tabPageMaint`�i�\�����u�����e�i���X�v�j��ǉ��B`grpVacuum`�i�Ώ�4DB�̃`�F�b�N����ON�E���s�O��2��T�C�Y���E���s�{�^���E��ԁ{�i���E���ӕ��j�� `grpFutureApiXml`�i#41�\���E�ꏊ�m�ۂ����E���암�͖������\���j���c�ς݁B���O���͂Ȃ��i�W�v�^�u�Ɠ��l�ɃR���\�[�����֏o���^�p�j
  - ���s�{�^�� `btnVacuumExec_Click` �͖�����MessageBox�݂̂�DB�ɐG��Ȃ��B`panel3` �t���ւ��E�W�v���[�h�� logic �ɐG��Ȃ�
  - ���[�U�[���z�u��������#41�\�����ʂ�3�s�����s���A��2�R�~�b�g�Ƃ��Ċm�肵��
- **����**:
  - ������̍ŏI��Ԃ� `dotnet test` 234��PASS�Enicorank2019�r���h�����iEXIT CODE=0�j
  - ���[�U�[�����@�Ō����ڂ��m�F����OK�BUI�̂ݕύX�̂���reviewer���r���[�͏ȗ��i���[�U�[�w���B�R�~�b�g���b�Z�[�W�ɋL�^�j
- **�c�ۑ�Ɣ��f**:
  - 32.2�Œ��g�iVACUUM���s�E�T�C�Y�擾�E�񓯊����Especs/design���f�j��ʏ탏�[�N�t���[�Ŏ�������
  - #41�̒�`�̓��x�����ʂ��ŐV�Ӑ}�iSP�W�v�Ŋ���ȍ~�̎擾�ςݓ����API��@�����A�茳��DB��������oldlog�擾���}�[�W�ŕ����j�B#41���莞��Issue�{�������̒�`�ōX�V����
  - �������ł́u�{�n�v���g�킸�u�茳��DB�^���[�J����DB�v�Ə����i���[�U�[�w�E�j

---

## 2026-09-26 �����e�i���X�^�u��DB�œK������ (#32�E32.2)

- **Issue**: #32�i�N���[�Y�B�{���{�R�����g2����v���Ƃ��Ċm��j
- **�u�����`**: `feature/t032-maintenance-vacuum` �� `develop` �� `--no-ff` �Ń}�[�W�B�u�����`�폜�ς�
- **�w�i**: #31�̓���prune�ł�VACUUM���Ȃ����߁A�蓮�œK���̒u���ꏊ���K�v�ɂȂ����B�v����2026-09-08�̕Ǒł��R�����g�iDB���Ƃ�prune�菇�j�ɂ���ADB���Ƃɏ������قȂ�
- **�����Ƃ��̌o�܂ƍ�蒼��**: ���ł�Issue�{���́uVACUUM�v���������ĕǑł��R�����g�������Ƃ��A�f��VACUUM�����{���r���[3��Ń}�[�W�܂Ői�߂��B���[�U�[�w�E��grep�iDROP TABLE IF EXISTS IDConvert���S�R�[�h�ɑ��݂��Ȃ��j�Ŕ��o���ADbOptimizer�S�ʏ��������{�e�X�g���E�ǉ��{�����C���Ƃ��č�蒼�����B���ł�UI�z���E�������s�K�[�h�͗��p���A���JI/F�̌���݊���ۂ����B�Ĕ��h�~�Ƃ���AGENTS.md�ɁuIssue�̃R�����g�S���ǂ݁E�d�l������tasks.md�]�L�v��ǉ�����
- **���{���e**:
  - `DbOptimizer`�iUtil�Estatic�j�BDB���Ƃ�DROP��DELETE��VACUUM�BApiXML��DROP IDConvert�{1�N�ȏ㖢�X�V�s�폜�ADailylog�͑S�s�폜�iDROP�֎~�j�ANicoranHistory��Weekly�E1001�ʈȉ��E1�N�ȏ�O�����폜�iSP�폜�Ȃ��ELastResultInfo�s�ρE��ʃp�����[�^���j�ALogOfficial��VACUUM�̂�
  - 1�N�O���E�͎��s���N�_�̃��[�����O�v�Z�iyyyyMMdd�����j�B1000�ʂ��傤�ǎc���E1�N�O������NicoranHistory�̂݊܂ށi����SQL�̏��������ʂ�BApiXML�͓����c���j
  - �擾����INTEGER��̂��ߕ�����o�C���h��������r�ɂȂ�B��^�ύX���͔�r�����������Ɓi�R�����g�ɖ��L�j
  - �R�����g�A�E�gconvertMovieID�������iCONVERTID_API_URL��live�g�p�̂��ߎc���j
  - UI��32.1�m��̂܂܁B�񓯊����s�{�������̓������s�K�[�h�i���s���t���O�ŏ����ҏW���̃{�^���������}�~�j�B�s�݂̓X�L�b�v�\���A���s�͂���DB�������s�\���Ŏc��𑱂���
  - `UnitTest`10���ǉ��i�v244���j�B���E�i1000�ʁE�����E����ʁE�ݒ�XML�s�ρj�E�p���E�\�ێ��ECWD sandbox�Ō��萫��S��
- **����**:
  - `dotnet test` 244��PASS�Enicorank2019�r���h�����iEXIT CODE=0�j
  - reviewer���r���[�v5��B����3��i�������s�K�[�h���j�{��蒼��2��i���E�E�������m�����j�B�ŏI��������}�[�W��
  - ������3���F�^�O�W�v���m�̑��݃K�[�h�i���������Ŕ͈͊O�j�E�p�X�萔��{���i�g�y��j�EGetDefaultTargets�������i�ߏ�j�B���R�̓R�~�b�g���b�Z�[�W�ɋL�^
  - ���[�U�[���@����OK�i�o�b�N�A�b�v�����̒��ӕt���j
- **�c�ۑ�**: #41�̓��x�����ʂ��ŐV�Ӑ}�iSP�W�v�Ŋ���ȍ~�̎擾�ςݓ����API��@�����A�茳��DB��������oldlog�擾���}�[�W�ŕ����j�B#41���莞��Issue�{�������̒�`�ōX�V����B�������ł́u�{�n�v���g�킸�u�茳��DB�^���[�J����DB�v�Ə���

---

## 2026-09-26 �^�O����v2�ŐV�l�̃f�[�^���_�\���{OFFSET�ߕʉ� (#39)

- **Issue**: #39�i�N���[�Y�B�{���{2026-09-26�R�����g��v���Ƃ��Ċm��B���[�U�[�񓚂ŕ����EOFF�������E�擾���j�E���f�����E�SOFFSET�ߕʉ����m��j
- **�u�����`**: `feature/t039-tagrank-timestamp-offset` �� `develop` �� `--no-ff` �Ń}�[�W�ia99a4ce�Ecd67677�Eda3d209��3�R�~�b�g�j�B�u�����`�폜�ς�
- **�w�i**: v2�ŐV�l���[�h�͏W�v��DB���g��Ȃ����߉�ʏ�Ƀf�[�^���_���c�炸�؂蕪�����ł��Ȃ������B�Q�ƃC���f�b�N�X�͓����X�V�̃X�i�b�v�V���b�g�ł���\�����ׂ��l�� `last_modified` ���������B�ʌ��Ń^�O������ `MYLIST_OFFSET` �ύX���T���ESP�ɔg�y�����肪����A��Issue�̃R�����g�Őߕʉ����v�����ꂽ
- **���{���e**:
  - `lblTagSnapshotTime`�igrpTag���E�����ߖT�j�BON���̌����m�F������� `MM/DD 05:00 ���_�̃X�i�b�v�V���b�g�ŏW�v` �Əo���B���t�� `last_modified` ��JST���E������05:00�Œ�i���f���������Ƃ̍����h�~�j�BOFF���E���m�F���E�����ύX���͔�\���B�m�F�s�\���͌����m�F���̂����s�����inull�ԋp�j�ɂ��ďW�v�ɐi�߂Ȃ��B�擾�� `CheckTagCountAsync` ���Ō���������ɒ���E`await Task.Run` ��UI�u���b�N�Ȃ��B���`�� `TagSnapshotTimestamp` �ɕ����iFormat�ETryFormat�EDataHour/DataMinute�萔�j
  - `SP`�^`TAGRANK` �߂�OFFSET4��iCOMMENT/MYLIST/PLAY/POINTALL�j��C�ӗv�f�Ƃ��Ēǉ��B�ǂݎ��͍��ڒP�ʃt�H�[���o�b�N�i�ߓ������ʁj�A�������݂̓��[�h�ʂ̐߂ցi�Ȃ���΋��ʒl�������p���Ő����j�B`Load`�^`Save` �Ăяo�����͕s�ρB�W�v���� `tabPageOut` �������Ń��[�h�ω���h���B`Initilize` ��4��̊��萶���i����l�͒萔�W��j
  - �z�z�e���v���[�g�i�ˑ��t�@�C��/nicorank.xml�j��TAGRANK�ߓ�OFFSET4������ׂ�Mode 0�i�␳�Ȃ��j�ɕύX�i���[�U�[�w���j�B�������̎茳XML�͐ߓ�OFFSET�Ȃ��̂��ߋ��ʃt�H�[���o�b�N�œ���s�ρB�V�K�z�z�̂݃^�O�������␳�Ȃ��ɂȂ�
  - GUI�̃p�l���ύX�� `nicorank.xml` �t�@�C���ɏ����߂���Ȃ��i�]���ʂ胁�������̂݁B�i�����͎�ҏW�j�B`GetXMLString` ��DB�̐ݒ�X�i�b�v�V���b�g�p�r�̂�
  - `UnitTest`16���ǉ��i�v260���j�B���_���`�EConfig�ߕʉ��̋��E�E�t�H�[���o�b�N�Esetter��������܂�
- **����**:
  - `dotnet test` 260��PASS�Esln Release�r���h�����iEXIT CODE=0�j�B��x����Release�r���h�����s��exe��DLL���b�N�iMSB3027/3021�j�Ŏ��s�������A�A�v���I����̍Ď��s�Ő����B�R�[�h�v���ł͂Ȃ�
  - reviewer���r���[�v3��B����͒�1���isetter��Ώ́j�{��4���ŗv�C���A�Ή���ɍă��r���[�Ń}�[�W�B�c��4���i�萔�W��E���������E�e�X�g1���j���Ή����Čv260���ɂ����B3�x�ڂ̃��r���[�͍��E���w�E�Ȃ��̂��ߏȗ�
  - ���[�U�[���@����OK�i�A�v���N�����̂܂܊m�F���A�w�E�͔z�zXML���{�ƃe���v���[�g�S0���̂݁B�����Ή��ς݁j
- **�c�ۑ�**: �Ȃ��i#39�����j�BOFFSET����l�̏����ύX���� `Config` ���萔��specs�E�e���v���[�g�𓯎��X�V���邱��

---

## 2026-09-26 BasicOption�j���o�H�̐����iIssue #44�E��Č� #42�j

- **Issue**: #44�iAI��Ă� #42 ���󂯂ĐV�K�쐬�B#42 �͒��Issue�Ƃ��Ďc���A������ #44 �ŊǗ��j
- **�u�����`**: `feature/t044-dispose-basic-option` �� `develop` �� `--no-ff` �Ń}�[�W�B�u�����`�폜�ς�
- **�w�i**: #40 �� SnapShotSabunReader.Dispose ���̒��g�͒��������A�Ăяo������ Dispose ���Ă΂Ȃ����ߎ��^�p�ł͐ڑ��������Ȃ������BSP ��1��W�v�ōő�4�{�i�W�v��DB�E���DB�E�\���⊮2�{�j�̐ڑ����J���A����v���Z�X�ł̍ďW�v���ɐςݏオ��B���{������ BasicOptionBase �� IDisposable ���p�����Ă��炸�A�j���̌_�񂪌^�ɕ\��Ă��Ȃ���������
- **���{���e**:
  - `BasicOptionBase : IDisposable` ���{��̉��z `Dispose`�B�����������Ȃ�7���͖��ύX�B��������3���iSnapShotSabunReader / TagRankLiveSabunReader / TagRankTotalReader�j�̖����I������ `override` �Ɋ񂹑ւ��i���Q�Ƃ���͂����邽�߁B�����I�����̂܂܂��Ɗ��̋�������Ă΂��j�B�璷�� `, IDisposable` �͏���
  - 3 Reader �� `_ownsDbCtrl` �t���O��ǉ������O�������̂ݕ���iSpMovieInfoFallback �Ɠ��ꗬ�V�B�󂯓�������u�����ڑ��͕��Ȃ����Ɓv�̍����j�B�{�Ԍo�H�͏�� null �n���̂��ߋ����s��
  - `RankingAnalyze` / `ModeFactoryBase` �� `IDisposable` �����Ĕj�����Ϗ��i�p���Enull ���S�E1�����s�ł��p������ ErrLog �ɋL�^�B���҂Ƃ��}�l�[�W�h�݂̂̂��߃t�@�C�i���C�U�Ȃ��j�B`RankingList` �͔j�����Ȃ�
  - `ModeFactroySP` / `ModeFactoryTagRank` �� Open ���s�o�H�ł���������j�����Ă��� false ��Ԃ��B�������� RankingAnalyze �ɓn������ʍs�œ�d���L�ɂ��Ȃ�
  - `TyukanAnalyze` �����̎g���̂� `RankingAnalyze` �� `using` ���i���ʃ��[�v�ł̐ςݏオ��h�~�j
  - `frmMainSyukei.AnalyzeAsync` �ŐV Factory ����O�̋� Factory �j���{�o�͏����� `try-finally` ���ɂ��I����j��
  - `UnitTest`10���ǉ��i�v270���j�B��Dispose�E�SOption�j���ƙp���E1�����s�ł��p���E������j���E���O����i�t�@�C���폜�Ō��؁j�E�H��Ϗ�
  - `design.md` �� Issue #44 �̐߂�ǋL�iExt �����藝�R���܂ށj
- **�݌v���f**:
  - ��B�i���ɔj���_��j���̗p�B��A�iFactory ���� `is IDisposable` ����j�͍����ŏ������A�ǉ��̂��тɒ��ӂ��K�v�ȍ\�����ׂ��c�邽�߁B��Dispose7���͎��R�[�h�m�F�ς݂ŋ󂪐������A���̋󉼑z�ɂ�薳�ύX�ōς�
  - Ext�i`IExtOptionBase`�j�͑ΏۊO�B�C���^�[�t�F�[�X�ł��� .NET Framework 4.8 �� C# �ł͊��������t���ɂ����A���󎝂��z�����Ȃ����߁B������ net8 �ڍs���ɍČ�������iknowledge �ɋL�^�j
- **����**:
  - `dotnet test` 270��PASS�Esln Release�r���h�����iEXIT CODE=0�j
  - reviewer���r���[�{�ă��r���[�ő�������}�[�W�i��3���F�������L���Edesign���f�E���O�ڑ��e�X�g�A��3���Ffinally�E�p���e�X�g�E�e�X�g��n�������ׂđΉ��B��̌�����Ȃ��j
  - ���[�U�[SP���@����OK�i�W�v���work�t�@�C���I�Ȃ��̂��S����������DB���������Ƃ��m�F�j
- **�c�ۑ�**: �Ȃ��i#44�����j�B�P�� `dbCtrl` �� Analyze/Base �����֒�������ƌ�J�����ɒ���ւ����������͎c�邪�A���L���Ƃ͓Ɨ��̂��ߕʈ����BExt �̌_�񉻂� net8 �ڍs���Ɍ���