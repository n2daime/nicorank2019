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
