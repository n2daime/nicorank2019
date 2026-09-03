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
