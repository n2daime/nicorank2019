# 実装ノウハウ・再発防止（pitfalls.md）

過去に発生した問題とその対策の記録。**同じ失敗を繰り返さないために**、実装・修正時に必ず確認する。

---

## SQLite 関連

### 1. 巨大トランザクションで btreeInitPage() returns error code 11（SQLITE_CORRUPT）

- **症状**: スナップショット API から数十万件を登録する際、`btreeInitPage() returns error code 11`（SQLITE_CORRUPT）が断続発生し、DB が破損して以後の読み書きが不可能になる
- **原因**: 単一の巨大トランザクションにより WAL ファイルが肥大化し、ディスク I/O やページ管理の限界を超える
- **対策（実装済み）**:
  - 5000件ごとのバッチコミット（`SnapShotDB.RegistDB`）
  - `INSERT OR IGNORE` 化（重複 ID スキップ）
  - SQLiteCtrl で WAL モード + PRAGMA（synchronous=NORMAL / temp_store=MEMORY / cache_size=-8000）
  - パラメータの事前生成・再利用
- **詳細**: `db.md` を参照

### 2. WAL モードの副作用

- `.db-wal` / `.db-shm` ファイルが別途作成される（通常は自動管理）
- 不要になった場合は `PRAGMA wal_checkpoint(TRUNCATE)` で手動整理可能
- バッチコミットの途中でクラッシュした場合、未コミット分（最大5000件未満）は失われるが、`LogSnapshot*.db` は日次作成されるため再実行で復元可能

### 3. PRAGMA 設定のグレースフルフォールバック

- PRAGMA 設定（WAL 等）に失敗しても接続自体は開いているため、**例外を握りつぶして継続する**（`SQLiteCtrl.Open()`）。呼び出し側でログを出す
- PRAGMA 設定の失敗で接続を閉じてしまうと、エラー処理が複雑化するため

### 4. 接続文字列の Pooling

- プーリングは**環境によって問題を起こすことがある**ため無効化（`Pooling=False`）

### 4b. Microsoft.Data.Sqlite 移行時の非互換

- `new SQLiteCommand(connection)` → `connection.CreateCommand()`（単引数コンストラクタが存在しない）
- `SQLiteConnection.CreateFile(path)` → `System.IO.File.Create(path).Dispose()`（同等の静的メソッドが存在しない）
- `SQLiteParameter` の型指定は `System.Data.DbType` ではなく `SqliteType`（`String`→`Text`、`Int64`→`Integer`）
- `Connection.BeginTransaction()` の戻り値は `DbTransaction` のため `SqliteCommand.Transaction` への代入時に `(SqliteTransaction)` キャストが必要
- 接続文字列は `SQLiteConnectionStringBuilder` ではなく `Data Source="<path>";Pooling=False;Default Timeout=30` の文字列連結で構築
- ライブラリ本体は `Microsoft.Data.Sqlite.Core` の `lib/netstandard2.0/Microsoft.Data.Sqlite.dll` に存在（`Microsoft.Data.Sqlite` パッケージは空のメタパッケージ）
- `packages.config` 形式では `packages.config` への列挙だけでは `CopyLocal` されず、`csproj` への `Reference` 追加が必須（`SQLitePCLRaw.core` / `batteries_v2` / `provider.dynamic_cdecl` の 3件を `HintPath` + `Private=True` で追加。`Version`/`PublicKeyToken` は `AssemblyName.GetAssemblyName` の実値と一致させる。例: `batteries_v2` は `8226ea5df37bcae9`、`provider.dynamic_cdecl` は `b68184102cba0b3b`）
- `e_sqlite3.dll` ネイティブは `runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` を `Content` + `CopyToOutputDirectory` と `buildTransitive/net461/SQLitePCLRaw.lib.e_sqlite3.targets` の明示 Import で `bin/runtimes` にコピー（`SQLitePCLRaw.lib.e_sqlite3` は `build/` がなく `buildTransitive` のみのため `packages.config` では自動 Import されない。両方併用で確実にコピー）

### 4c. `lib` サブフォルダ集約と `Costura` 埋め込み除外（2026-08 大規模トラブル）

- **背景**: `System.Data.SQLite 1.0.118` → `Microsoft.Data.Sqlite 10.0.11` / `SQLitePCLRaw 2.1.12` 移行時に `bin` 直下の `Dll` 散乱を避けるため `lib` サブフォルダに集約。`SQLitePCLRaw.batteries_v2` は自 `Assembly.Location` を基準に `runtimes/{rid}/native/e_sqlite3.dll` を `NativeLibrary.TryLoad` するため、埋め込むと `Location=""` になり必ず `e_sqlite3 not found` となる。
- **対策**:
  - 5 DLL (`Microsoft.Data.Sqlite.dll` / `SQLitePCLRaw.core.dll` / `batteries_v2.dll` / `provider.dynamic_cdecl.dll` / `provider.e_sqlite3.dll`(3.xのみ)) と `runtimes/win-{x64,x86,arm,arm64}/native/e_sqlite3.dll` を **同一親 `lib` 配下** に配置。`lib` と `lib\runtimes` が別親だと `batteries_v2` の `Location` 基準探索が失敗する。
  - `csproj` の `Reference` は `Private=False` ではなく `FodyWeavers.xml` の `<Costura ExcludeAssemblies="SQLitePCLRaw.core|SQLitePCLRaw.batteries_v2|SQLitePCLRaw.provider.dynamic_cdecl|SQLitePCLRaw.provider.e_sqlite3|Microsoft.Data.Sqlite" />` と `AfterResolveReferences` ターゲットの `EmbeddedResource` 除外で二重に埋め込み抑止。`Private=False` だけでは `ProjectReference` 経由で `ReferenceCopyLocalPaths` に残り `Costura` が再埋め込みするため。
  - 実行 `EXE` 側にも同じ `None/Content` を冗長配置（`ProjectReference` の `None` は `transitive` で流れない場合がある）。`probing privatePath="lib"` を `App.config` に加え、`Program.cs` の `AssemblyResolve` で `lib\*.dll` へのフォールバックを実装。
  - `FodyWeavers.xml` は `DisableCleanup="true"` を併用し `Costura` の `cleanup` が `lib` の物理 `Dll` を削除しないようにする。

### 4d. `SQLitePCLRaw.lib.e_sqlite3` の `AnyCPU` 禁止と `Prefer32Bit`（2026-08）

- `3.53.3` は `build/net471/SQLitePCLRaw.lib.e_sqlite3.props` で `Platform==AnyCPU && RuntimeIdentifier==""` の場合に `error : This package does not support Any CPU builds` を出す。`net48` の `AnyCPU` ビルドが必ず失敗する。
- **対策**: `csproj` 末尾に空ターゲット `<Target Name="CheckForAnyCPU" />` を後勝ち定義して上書き無効化。`x64` 必須/`ARM` 任意のため `AnyCPU` でも手動で `lib\runtimes` を配置して動的ロードで対応する。
- `AnyCPU Prefer32Bit=true`（既定）の `exe` は `32bit` で起動し `win-x86` を探す。`x64` 必須化のため `nicorank2019.csproj:19` の `Debug|AnyCPU` に `<Prefer32Bit>false</Prefer32Bit>` を追加し `64bit` 起動で `win-x64` を使用。`win-x86` は削除せず `win-x64/x86/arm` の3種を同梱して互換性を維持（`x86` 不要だが `32bit` フォールバック用）。

### 4e. `System.ValueTuple` / `bindingRedirect` の落とし穴（2026-08）

- `System.Memory 4.6.3` 等は `System.ValueTuple 4.0.2.0` に依存するが、`.NET 4.8` の `GAC` は `4.0.0.0`、`System.ValueTuple 4.6.2` のパッケージは `4.0.5.0`。`nuget` 更新で `System.ValueTuple` の参照が `GAC` の `<Reference Include="System.ValueTuple" />` のまま `CopyLocal=false` になると `bin` に `System.ValueTuple.dll` が出力されず `FileNotFoundException: System.ValueTuple 4.0.2.0` となる。
- **対策**:
  - `nicorankLib.csproj:233` を `HintPath` 付き `System.ValueTuple 4.0.5.0` (`..\packages\System.ValueTuple.4.6.2\lib\net47\System.ValueTuple.dll`) に変更し `CopyLocal` 化。
  - `None` で `System.ValueTuple.dll` を `CopyToOutputDirectory` で `bin` 直下に手動配置。`PostBuildEvent` の `del System.*` は `System.ValueTuple` を除外（現在は `del System.*` 自体を廃止し `System.Buffers/Memory` 等のみ個別削除）。
  - `App.config` の `bindingRedirect` は `SQLitePCLRaw.core 2.1.12.3116` と `System.ValueTuple 4.0.5.0` を単一 `assemblyBinding` 内に集約し `probing` と分離。`AutoGenerateBindingRedirects=true` のままでは二重 `assemblyBinding` が生成され後者が無視されるため `csproj:14` で `false` にし手動管理。

### 4f. `packages.config` と `buildTransitive` の不一致（2026-08）

- `SQLitePCLRaw.lib.e_sqlite3 2.1.12` は `build/` がなく `buildTransitive/net461` のみ。`packages.config` 形式では自動 `Import` されないため `csproj` で `Import Project="..\packages\...\buildTransitive\net461\..."` を明示。`EnsureNuGetPackageBuildImports` の `Exists` チェックも `buildTransitive\net461` に合わせる。存在しない `build\net471\*.props` をチェックすると `MSB3030` で必ず失敗する。
- `nuget` 更新で `Microsoft.Data.Sqlite 10.0.11` ( `net48` 依存は `2.1.12` ) に対して `3.0.5/3.53.3` を手動で入れると `UnitTest` の `PackageReference` と競合し `MSB3277` で `2.1.12` が優先されて `Batteries_V2.Init() → 3.0.5` の `FileLoadException` となる。`net48` では `2.1.12` に統一すること。

---

## ニコニコ API 関連

### 5. 連続アクセスによる 403 エラー

- **症状**: 大量の動画情報取得（getthumbinfo 等）で連続アクセスすると 403（AccessDenied）になり、XML を取得できない
- **対策（実装済み・NicoApi）**: 取得失敗時に `ManualResetEventSlim` で**全スレッドを一時停止**し、指数バックオフ（`1.5^retry × 1000ms`、最大100回）で再試行 → 成功したら再開
  - 各スレッド冒頭に `Sleep(rnd.Next(50,200))` のジッターを付与して同時アクセスを分散
- **TxtDownLoad（InternetUtil）**: 最大20回リトライ。`ContentType == "application/xml"` の ProtocolError（403 相当）は**即断念**（再試行しない）。それ以外は `2^retry×100ms`（上限30秒）の指数バックオフ + ランダム遅延

### 6. ニコ動の仕様変更への追従

- 過去に発生した仕様変更と対応（コミット履歴より）:
  - **RSS 廃止**（2025-04）: RSS 取得を廃止し、ニコ動 API 経由でランキングを取得するよう変更。取得元は設定ファイルで切替可能（`URL_JSON_TARGET`）
  - **FQDN 変更**（2024-03）: snapshot API のエンドポイント URL を変更
  - **サイバー攻撃対策**（2024-06）: 公式過去ログ提供停止 → 互換サイトを自前運用（個人 NAS 上。URL は公開しない）
  - **24/08/14 の更新**（2024-08）: 中間ランキングの集計対象が正しく産出されない不具合を修正
  - ニコ動側の仕様変更はコミット履歴に情報が残っている。仕様変更を疑ったら `git log` を確認する

### 7. スナップショット API の仕様

- **1年以上前のデータは 1000 再生制限**（`flgLimit1000` で URL 切替）
- **5万件を超える期間は 1日ずつ狭めて再取得**（自主規制）
- レスポンスの `":null"` を `":0"` に置換してからデシリアライズ（null 対策）

---

## テスト関連

### 8. old-style csproj + .NET 10 SDK で StackOverflow

- **症状**: MSTest 3.5.2 の testhost 起動時に StackOverflow が発生
- **原因**: old-style csproj（packages.config + Reference）と .NET 10 SDK の組み合わせ問題
- **対策（実装済み）**: SDK-style csproj に変換。**UnitTest.csproj は SDK-style を維持すること**（詳細: `testing.md`）

### 9. Config テストの nicorank.xml 依存

- `Config.GetInstance().Initilize()` がカレントディレクトリの `nicorank.xml` を読むため、テストでは出力直下にフィクスチャをコピーする
- `TestConfigBuilder` で非公開フィールドを差し替える方式もある

### 10. TextUtil.ReadCsv の戻り値仕様

- ファイル不在時、戻り値は `false` で out 引数は**空の List**（null ではない）
- テストアサートは `Assert.IsNotNull(rankingList)` + `Assert.AreEqual(0, rankingList.Count)`

---

## 運用・その他

### 11. 週刊ランキングの差分の平等性

- 公式データは取得件数に制限があり、半年/1年の差分で「片方の情報がない」ことがある
- 差分計算できた動画とできなかった動画を同じ数字としてランキング計算するのは結果の平等性に問題がある
- 過去は人間が差分情報を手入力していたが、**スナップショット DB で自動化**した（SP 集計）

### 12. 公式チャンネル動画（so）の再公開問題

- so 動画は非公開→再公開で**投稿日時が更新される**（更新条件は不明）
- 本ツールの DB は 2019 年以前の情報を持たないため、その補完に nicochart.jp を利用（`CheckSoMovieNeedSabun`）
- 補完がないと「2026年に100万再生した」という実態と合わない扱いになる

### 13. ビルド・環境の注意

- `nicorankLib` の Costura.Fody は削除しない（単一 EXE 化のため）
- nicorank_oldlog は net8.0 から net48 ライブラリを参照するハイブリッド構成
- 設定ファイル（`config.json` / `cookie.txt`）はリポジトリに含めない（手動配置）

### 14. gh コマンドが見つからない（PATH 未反映）

- **症状**: opencode の bash ツールから `gh` を実行すると「認識されない」エラー
- **原因**: `gh.exe` は `C:\Program Files\GitHub CLI\gh.exe` にあり、Machine/User PATH にも登録済みだが、**opencode デスクトップアプリが起動時点の古い環境変数を保持**しており、そのプロセスツリー全体（bash ツール含む）に PATH が反映されていない
- **対処**: opencode アプリを**再起動**すればレジストリの PATH が反映される（gh の再インストール・PATH 再設定は不要）
- **注意**: opencode のシェルは PowerShell プロファイルを読まないため、プロファイルへの PATH 追記は無効
- **代替**: 再起動前はフルパス `& "C:\Program Files\GitHub CLI\gh.exe"` で実行可能

### 15. 将来対応時の再発防止（Issue #20 関連）

- **背景**: `SQLite` 移行後に残る将来タスクは `NuGet.config` 移行 / `EF6` 関連セクション削除（`entityFramework` 自体は保持、`SQLite.EF6 provider` 行のみ） / `.NET Core/.NET 5+` 移行（未決定） / `DB` ファイルパス変更・新機能追加。いずれも本件と同様に `packages.config` / `csproj` / `App.config` / `FodyWeavers.xml` を横断するため同じ落とし穴に再遭遇しやすい。
- **NuGet.config 移行時**: `packages.config` → `PackageReference` に変える際は `csproj` の `Reference` + `HintPath` + `Private` / `EnsureNuGetPackageBuildImports` / `Import` / `FodyWeavers.xml` / `App.config` の `bindingRedirect` / `lib` の `None/Content` を **同時** に更新すること。片方だけ更新すると `MSB3030`（`HintPath` の `packages\*.dll` が見つからない）や `MSB3277`（`System.*` の二重バージョン）で即失敗する。移行後は `AutoGenerateBindingRedirects` の二重 `assemblyBinding` に注意（`4e` 参照）。
- **EF6 削除時**: `App.config` / `nicorankLib/app.config` の `<entityFramework>` / `<DbProviderFactories>` から `SQLite.EF6` のみ削除し、`System.Data.SQLite.EF6` の `Reference` も同時に削除。`System.Data.SQLite` 本体が残っていると `Costura` が再び `e_sqlite3` を `runtimes` 直下にコピーしようとして `lib` 集約と競合する。
- **.NET Core 移行時**: `Costura.Fody` は `net8.0` では不要（単一ファイル発行は `PublishSingleFile`）。`SQLitePCLRaw` の `AnyCPU` 禁止は `3.53` 以降で顕在化するため `CheckForAnyCPU` 空ターゲットや `RuntimeIdentifier` の扱いを再確認。`Microsoft.Data.Sqlite` の `net48` 依存は `2.1.12` だが `net8.0` では `3.0.5/3.53.3` のため `TargetFramework` ごとに `Condition` 分岐が必要。`System.ValueTuple` は `net8.0` では `Inbox` のため `HintPath` 追加は不要。
- **DB パス変更時**: `DB.cs` の定数と `PostBuildEvent` の `xcopy` 元を同時更新。`lib` 集約と同様に `Probing` や `AssemblyResolve` の対象パスが変わるため `SQLiteCtrl` の診断メッセージ（`batteries_v2.dll の場所`）も更新すること。
