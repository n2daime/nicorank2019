# 設計判断（Design）

このドキュメントは、プロジェクトの重要な設計判断とその根拠を記録する。

- 設計判断を下したら（または既存の判断を発見したら）このファイルに追記する。
- 実装済みの設計の詳細（コード構造・依存関係）は `docs/knowledge/` を参照。ここでは「なぜそう設計したか」に焦点を置く。

---

## SQLite 移行設計（実施済み 2026-08-29）

> タスクは `docs/tasks.md` の「SQLite ライブラリ移行」を参照。Issue #20 対応で `Microsoft.Data.Sqlite 8.0.7` + `SQLitePCLRaw 2.1.6` に移行済み。

### Context

- nicorankLib は .NET Framework 4.8 環境で System.Data.SQLite 1.0.118.0 を使用。22 ファイル（nicorankLib 15 + UnitTest 7）にわたって `using System.Data.SQLite` が存在し、SQLiteCtrl.cs で `SQLiteConnectionStringBuilder` による接続文字列構築を行っていた。
- EntityFramework6 パッケージは存在するがコード上は未使用。
- packages.config を保持し続ける方針。

### Goals / Non-Goals

**Goals:**
- System.Data.SQLite から Microsoft.Data.Sqlite への完全移行
- 既存の全機能・振る舞いの維持（WALモード、同期設定、キャッシュ設定、バッチコミット、INSERT OR IGNORE 等）
- packages.config の継続使用
- app.config の DbProviderFactories セクション削除

**Non-Goals:**
- NuGet.config 移行（今回は保留）
- EntityFramework6 関連セクションの削除（今回は保留）
- .NET Core / .NET 5+ への移行
- DB ファイルパスの変更
- 新機能の追加

### Decisions

#### Decision: 接続文字列を単純な文字列で構築
- **選択**: `SQLiteConnectionStringBuilder` から `Data Source=...;Pooling=False;Default Timeout=30` 形式へ
- **理由**: Microsoft.Data.Sqlite に `SqliteConnectionStringBuilder` は存在するが、現状の固定パラメータ（Data Source, Pooling, Default Timeout のみ）であれば文字列連結で十分。複雑な組み立てが不要なため可読性が高まる。
- **代替案**: `SqliteConnectionStringBuilder` を使用する方法も検討したが、パラメータ数が少なく拡張性も不要なため採用せず。

#### Decision: 既存 PRAGMA 設定は維持
- **選択**: `PRAGMA journal_mode = WAL`、`PRAGMA synchronous = NORMAL`、`PRAGMA temp_store = MEMORY`、`PRAGMA cache_size = -8000` は従来通り `SqliteCommand` で実行
- **理由**: これはデータベース動作の安定性に直結する設定であり、移行後も同一の PRAGMA が発行されるべき。

#### Decision: API 名の機械的置き換え
- **選択**: `SQLiteConnection` → `SqliteConnection`、`SQLiteCommand` → `SqliteCommand`、`SQLiteTransaction` → `SqliteTransaction`、`SQLiteParameter` → `SqliteParameter`、`SQLiteDataReader` → `SqliteDataReader`、`SQLiteException` → `SqliteException`
- **理由**: 名前空間とクラス名の変更のみで、API シグネチャはほぼ互換。機械的な置き換えで対応可能。

#### Decision: packages.config のバージョン指定
- **選択**: Microsoft.Data.Sqlite 8.0.7 + SQLitePCLRaw 2.1.6（.NET Framework 4.8 対応の最新安定版）
- **理由**: `packages` フォルダに既存キャッシュが存在し、net48 で動作確認済み。9.x も netstandard2.0 で動作するが、本環境では 8.0.7 が最短でビルドが通る。将来 9.x への上げ替えは容易。
- **補足**: `Microsoft.Data.Sqlite` 本体はメタパッケージで実体は `Microsoft.Data.Sqlite.Core` の `lib/netstandard2.0/Microsoft.Data.Sqlite.dll`。`packages.config` には `Microsoft.Data.Sqlite` / `Core` / `SQLitePCLRaw.bundle_e_sqlite3` / `core` / `lib.e_sqlite3` / `provider.dynamic_cdecl` の 6 パッケージを列挙。

#### Decision: 非互換 API の対応
- **SqliteCommand 単引数コンストラクタ**: `new SqliteCommand(conn)` は `conn.CreateCommand()` に置換（Microsoft.Data.Sqlite には単引数コンストラクタが存在しない）
- **SqliteConnection.CreateFile**: `System.IO.File.Create(path).Dispose()` に置換（Microsoft.Data.Sqlite には同等の静的メソッドが存在しない）
- **SQLiteParameter 型指定**: `System.Data.DbType` → `SqliteType`（`String`→`Text`、`Int64`→`Integer`）に変更（Microsoft.Data.Sqlite のコンストラクタは `SqliteType` を要求）
- **BeginTransaction 戻り値**: `SqliteConnection.BeginTransaction()` は `DbTransaction` を返すため `SqliteCommand.Transaction`（`SqliteTransaction` 型）への代入時に `(SqliteTransaction)` キャストが必要

### Risks / Trade-offs

- **[Risk] Microsoft.Data.Sqlite の .NET Framework 4.8 サポートバージョンに制限がある可能性** → 8.0.7 でビルド・テストが PASS したため解消
- **[Risk] 22 ファイルにおよぶ書き換えで typo や置き忘れが発生する可能性** → using 文の一括置換後、ビルドエラーで残存箇所（`CreateFile` / 単引数コンストラクタ / `DbType`）を検出し対応
- **[Risk] e_sqlite3.dll ネイティブ DLL の実行時解決** → `SQLitePCLRaw.lib.e_sqlite3` の `runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` を `Content` + `CopyToOutputDirectory` および `buildTransitive` Import で `bin/runtimes` にコピーし、テスト（69件 PASS）および `nicorankLib.dll` の `GetManifestResourceNames`（`costura.sqlitepclraw.*` 3件 + `costura.microsoft.data.sqlite`）で Costura 埋め込みを確認。packages.config 形式では `packages.config` への列挙だけでは CopyLocal されず、`csproj` への `Reference` 追加が必須であることを検証（pitfalls 4b 追記）
- **[Trade-off] 機械的置き換えで対応できるが、コードレビュー時の差分が大きくなる** → 1回のコミットで行い、差分の把握を容易にする

---

## 実装済みの設計判断（要点）

詳細は `docs/knowledge/db.md`・`docs/knowledge/testing.md` を参照。

### SQLiteCtrl の接続設計（btreeInitPage 対策・2026-06-03）

- **接続文字列**: `Pooling=False`（環境によって問題を起こすため）、`JournalMode=Wal`、`DefaultTimeout=30`
- **PRAGMA**: `journal_mode=WAL` / `synchronous=NORMAL` / `temp_store=MEMORY` / `cache_size=-8000` を接続時に一律適用
- **グレースフルフォールバック**: PRAGMA 設定に失敗しても接続自体は維持（例外は握りつぶし、呼び出し側でログ出力）
- **根拠**: 単一の巨大トランザクションで WAL ファイルが肥大化し `btreeInitPage() returns error code 11`（SQLITE_CORRUPT）が発生していた。バッチコミットと併せて破損リスクを低減。

### SnapShotDB の大量データ登録設計（2026-06-03）

- **5000件ごとのバッチコミット**: 単一トランザクションでの WAL 肥大化を回避。1件ずつのコミットによるオーバーヘッドも回避（約200Byte/行 → 約1MB/バッチで I/O とメモリのバランスが良い）
- **INSERT OR IGNORE**: 従来の `INSERT ... WHERE NOT EXISTS` から変更。重複 ID 除外のセマンティクスを保ちつつ SQLite の最適化パスを利用
- **パラメータ事前生成・再利用**: `SQLiteParameter` をループ外で一度 `Add()` し、ループ内では `.Value` のみ代入。GC 圧力の削減
- **Rollback の例外処理**: `try { aCmd.Transaction?.Rollback(); } catch { }` でロールバック失敗を握りつぶし、元の例外を確実に上位へ伝播

### ISQLiteCtrl 抽象化とテスト容易性（2026-06-23）

- `SQLiteCtrl` から `ISQLiteCtrl` インターフェースを抽出し、`OpenInMemory()` を追加
- DB 操作クラスには `ISQLiteCtrl dbCtrl = null` のオプショナル引数でコンストラクタ注入（テスト時のみ注入、本番は従来通り `new SQLiteCtrl()`）
- **根拠**: SQLite 移行時に Microsoft.Data.Sqlite 版が同一インターフェースを実装すればテストコードの変更がゼロになる。DI コンテナは過剰設計。

### テスト観点は「操作パターン」単位（2026-06-23）

- クラス単位ではなく SELECT / INSERT / DDL / エラー の操作パターン単位でテストを整理
- **根拠**: 移行後のクラス構成が変わる可能性があり、操作パターンごとのテストがあればどの実装でも検証可能

### テスト基盤（2026-06-23）

- モック: Moq 4.x（.NET Framework 4.8 対応・MSTest との互換性・広く使われている）
- テストデータ: `UnitTest/Fixtures/` に配置し、ビルド時に出力ディレクトリへコピー（絶対パス依存の排除）
- 既存の実行不能テストは削除し、書き直し（`TestPointCalc_POINTALL_VOCACOLE2023` は維持）
