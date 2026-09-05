# タスク Archive（archive.md）

完了したタスクの**経緯・検証履歴・実装ノウハウ**の記録場所。GitHub Issue に無い固有情報の置き場所とする。

## 記録ルール

- タスク完了時（AGENTS.md §2 のマージ後ゲート）に、検証履歴・実装ノウハウ・設計判断の経緯を追記する。
- 形式は自由だが、日付・Issue 番号・背景・実施内容・検証結果を含める。
- コードの現状態（構造・依存・設計判断）はここには書かない。`docs/knowledge/` に反映する。仕様・設計の定義は `../specs.md` / `../design.md` を更新する。

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
