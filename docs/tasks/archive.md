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
