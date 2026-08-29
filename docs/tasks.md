# タスク管理（Tasks）

タスクは**別エージェント / サブエージェントが単独で実行できる**ことを目的とする。
各タスクは「依存」「対象」「受け入れ条件」を満たすこと。

## タスクの実行ルール

- 実装前に `docs/proposal.md`（開発背景）`docs/design.md` `docs/specs.md` `docs/knowledge/` を必ず読むこと。
- 各タスクは対応する **GitHub Issue 番号と関連付ける**（提案は Issue で管理。フローは `docs/proposal.md` 参照）。
- 完了したタスクには `✅` を付け、`docs/knowledge/` を最新化し、関連する GitHub Issue を Close する。経緯・検証履歴は `docs/tasks/archive.md` に追記する。
- ビルド: `dotnet restore` → `dotnet test UnitTest/UnitTest.csproj` が通ること。
- ブランチ: main から作業用ブランチを切り、完了後 main にマージ。

---

## 未完了タスク

### SQLite ライブラリ移行（System.Data.SQLite → Microsoft.Data.Sqlite）

> GitHub issue #20 対応。背景・設計判断は `docs/design.md` の「SQLite 移行設計」、移行仕様は `docs/specs.md`「6. 移行仕様」を参照。✅ 完了（2026-08-29 実装）

#### 1. プロジェクト設定ファイルの変更

- [x] ✅ 1.1 nicorankLib.csproj の Reference を System.Data.SQLite 関連から Microsoft.Data.Sqlite に変更（`Microsoft.Data.Sqlite.Core.8.0.7` の `lib/netstandard2.0/Microsoft.Data.Sqlite.dll` を参照。Stub targets 削除）
- [x] ✅ 1.2 packages.config の System.Data.SQLite 関連パッケージを削除し Microsoft.Data.Sqlite を追加（`Microsoft.Data.Sqlite 8.0.7` / `Microsoft.Data.Sqlite.Core 8.0.7` / `SQLitePCLRaw.bundle_e_sqlite3 2.1.6` / `core 2.1.6` / `lib.e_sqlite3 2.1.6` / `provider.dynamic_cdecl 2.1.6`）
- [x] ✅ 1.3 app.config の DbProviderFactories セクションを削除 + entityFramework/providers の `System.Data.SQLite.EF6` 行を削除
- [x] ✅ 1.4 UnitTest.csproj の `System.Data.SQLite.Core 1.0.118` → `Microsoft.Data.Sqlite 8.0.7`（PackageReference）

#### 2. SQLiteCtrl.cs の接続コード再実装

- [x] ✅ 2.1 接続文字列を `SQLiteConnectionStringBuilder` から `Data Source="<path>";Pooling=False;Default Timeout=30` 形式に変更（`JournalMode=Wal` は廃止し `PRAGMA journal_mode=WAL` で代替。パスは二重引用符で囲む）
- [x] ✅ 2.2 `SQLiteConnection` → `SqliteConnection`、`SQLiteCommand` → `SqliteCommand` に置き換え（`SQLiteTransaction` は明示的使用なしだが `BeginTransaction()` 戻り値のキャストで対応）
- [x] ✅ 2.3 `using System.Data.SQLite` → `using Microsoft.Data.Sqlite` に変更（`ISQLiteCtrl`/`SQLiteCtrl`）
- [x] ✅ 2.4 OpenInMemory を `Data Source=:memory:` 形式に変更。PRAGMA は `Connection.CreateCommand()` で実行
- [x] ✅ 2.5 ビルドが通ることを確認

#### 3. 全ファイルの using 文一括置換

- [x] ✅ 3.1 22 ファイルの `using System.Data.SQLite` を `using Microsoft.Data.Sqlite` に置換（nicorankLib 15 + UnitTest 7。Issue 記載の 31 は実態と不一致のため修正）
- [x] ✅ 3.2 ビルドエラーがないことを確認

#### 4. API 呼び出しの機械的置換（残存箇所の対応）

- [x] ✅ 4.1 `SQLiteConnection` → `SqliteConnection` の置換（`ISQLiteCtrl.Connection` 公開型含む）
- [x] ✅ 4.2 `SQLiteCommand` → `SqliteCommand` の置換（`new SqliteCommand(conn)` は `conn.CreateCommand()` に置換。2引数コンストラクタはそのまま）
- [x] ✅ 4.3 `SQLiteTransaction` → `SqliteTransaction` の置換 + `BeginTransaction()` 戻り値（`DbTransaction`）を `SqliteTransaction` にキャスト（TyukanAnalyze / RankingHistory / NicoApi / ResultHistory / SnapShotDB / UnitTestDbWrite）
- [x] ✅ 4.4 `SQLiteParameter` → `SqliteParameter` の置換 + `System.Data.DbType` → `SqliteType`（`String`→`Text`、`Int64`→`Integer`）に変更（SnapShotDB 5件）
- [x] ✅ 4.5 `SQLiteConnection.CreateFile(path)` → `System.IO.File.Create(path).Dispose()` に置換（SnapShotDB / UnitTest 3ファイル）
- [x] ✅ 4.6 `SQLiteDataReader` / `SQLiteException` は明示的使用なし（`var reader = cmd.ExecuteReader()` のため置換不要。確認済み）
- [x] ✅ 4.7 `CommandType` や `IsDBNull` など上記以外の非互換 API がないか確認し対応（`CommandType` 使用なし、非互換なし）

#### 5. ビルド検証と動作確認

- [x] ✅ 5.1 nicorankLib プロジェクトのビルドが成功することを確認（`dotnet build nicorankLib/nicorankLib.csproj -c Release` および `dotnet build UnitTest` 経由で確認。`GetManifestResourceNames` で `costura.sqlitepclraw.*` が埋め込まれ、`bin/runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` が出力されることを検証）
- [x] ✅ 5.2 nicorank2019 全体のビルドが成功することを確認（`dotnet build` で nicorankLib の Costura 埋め込みが `costura.sqlitepclraw.*` を含むことを確認。packages.config 形式では `Reference` 追加が必須であることを検証）
- [x] ✅ 5.3 基本的な動作（DB接続・読み取り）が従来通り動作することを確認（`ISQLiteCtrl` 経由のインメモリ DB + `PRAGMA journal_mode=WAL` で検証。`SQLitePCLRaw.batteries_v2` の `runtimes` 配置によりネイティブ解決が成功）
- [x] ✅ 5.4 `dotnet test UnitTest/UnitTest.csproj -c Release` が全件 PASS（69件 PASS。移行前後の挙動一致を検証。Release 構成で実行）

---

### テスト拡充（集計ロジック）

> 2026-06-23 のテスト活性化で基盤は整備済み（69件）。残りは集計ロジックの中核部分。

#### 1. 集計ロジックの単体テスト

- [ ] 1.1 JsonReader/JsonReaderBase の IF 抽出（テスト用のフェイク JsonReader 作成）を nicorankLib 側に行う（必要な場合のみ）
- [ ] 1.2 フェイク JsonReader を使って `RankingAnalyze.AnalyzeRank` のテストを実装する
- [ ] 1.3 週間集計のロジックに対するテストを追加する

#### 2. 最終確認

- [ ] 2.1 全テストがビルド・実行可能であることを確認する
- [ ] 2.2 ビルド警告がないことを確認する

---

### ニコ動APIのリクエスト組み立てを型付きリクエストへ変更

> GitHub issue #19 対応。詳細は `docs/specs.md`「API 仕様」と issue を参照。

#### 1. スナップショット検索 API（優先度高・nicorankLib/SnapShot/SnapShotAnalyze.cs）

- [ ] 1.1 型付きリクエストクラス（q / targets / fields / filters / _sort / _limit / _offset / _context）を新設
- [ ] 1.2 `REQUEST_URL` / `REQUEST_URL_LAST_1YEAR` の `string.Format` を廃止し UriBuilder + 正しいエンコードで URL 生成
- [ ] 1.3 必須パラメータ `_context` を追加
- [ ] 1.4 レスポンスの `Replace(":null", ":0")` ハックをシリアライズ設定側で解消（検討）
- [ ] 1.5 URL 生成の単体テストを追加（パラメータ・エンコード・`_context` 有無）

#### 2. nvapi ランキング API（優先度低・nicorank_oldlog/RankAPI/NicoRankiApi.cs）

- [ ] 2.1 `requestAPI` をクエリパラメータ（辞書型）受け取りに変更し文字列連結を廃止
- [ ] 2.2 `_frontendId=6` を定数化
- [ ] 2.3 `GetGenreRanking` / `GetTeibanRanking` を型付きで呼び出し

---

## 完了済みタスク（履歴）

| タスク | 完了日 | 主な成果物 |
|---|---|---|
| btreeInitPage() returns error code 11 対策 | 2026-06-03 | SQLiteCtrl 接続強化（WAL・PRAGMA・グレースフルフォールバック）、SnapShotDB 5000件バッチコミット・INSERT OR IGNORE・パラメータ再利用 |
| SQLite 操作の単体テスト設計 | 2026-06-23 | ISQLiteCtrl 抽出、OpenInMemory、TestDbHelper、DB操作テスト（SELECT/INSERT/DDL/エラー） |
| 単体テストの活性化（基盤） | 2026-06-23 | SDK-style csproj 化、Moq 導入、Fixtures 配置、Ranking/Config/TextUtil/StatusLog/Output のテスト（計69件） |
