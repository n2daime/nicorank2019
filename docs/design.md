# 設計判断（Design）

このドキュメントは、プロジェクトの重要な設計判断とその根拠を記録する。

- 設計判断を下したら（または既存の判断を発見したら）このファイルに追記する。
- 実装済みの設計の詳細（コード構造・依存関係）は `docs/knowledge/` を参照。ここでは「なぜそう設計したか」に焦点を置く。

---

## SQLite 移行設計（実施済み 2026-08-29）

> タスクは `docs/tasks.md` の「SQLite ライブラリ移行」を参照。Issue #20 対応で `Microsoft.Data.Sqlite 10.0.11` + `SQLitePCLRaw 2.1.12` に移行し、`bin` 直下の散乱を避けるため `lib` サブフォルダに集約済み（`FodyWeavers.xml` で `ExcludeAssemblies`）。

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
- **選択**: Microsoft.Data.Sqlite 10.0.11 + SQLitePCLRaw 2.1.12（.NET Framework 4.8 対応の安定版。`10.0.11` の `net48` 依存は `2.1.12`）
- **理由**: `10.0.11` は `net48` でも動作確認済み（`dotnet test` 69件 PASS）。`3.0.5/3.53` は `AnyCPU` 禁止のため `2.1.12` を採用。`lib` 集約は `win-x64/x86/arm` の3種を同梱し `x64` 必須/`ARM` 任意に対応。
- **補足**: `Microsoft.Data.Sqlite` 本体はメタパッケージで実体は `Microsoft.Data.Sqlite.Core` の `lib/netstandard2.0/Microsoft.Data.Sqlite.dll`。`packages.config` には `Microsoft.Data.Sqlite` / `Core` / `SQLitePCLRaw.bundle_e_sqlite3` / `core` / `lib.e_sqlite3` / `provider.dynamic_cdecl` の 6 パッケージを列挙。`System.ValueTuple 4.6.2` / `System.Buffers 4.6.1` 等も同時更新。

#### Decision: 非互換 API の対応
- **SqliteCommand 単引数コンストラクタ**: `new SqliteCommand(conn)` は `conn.CreateCommand()` に置換（Microsoft.Data.Sqlite には単引数コンストラクタが存在しない）
- **SqliteConnection.CreateFile**: `System.IO.File.Create(path).Dispose()` に置換（Microsoft.Data.Sqlite には同等の静的メソッドが存在しない）
- **SQLiteParameter 型指定**: `System.Data.DbType` → `SqliteType`（`String`→`Text`、`Int64`→`Integer`）に変更（Microsoft.Data.Sqlite のコンストラクタは `SqliteType` を要求）
- **BeginTransaction 戻り値**: `SqliteConnection.BeginTransaction()` は `DbTransaction` を返すため `SqliteCommand.Transaction`（`SqliteTransaction` 型）への代入時に `(SqliteTransaction)` キャストが必要

### Risks / Trade-offs

- **[Risk] Microsoft.Data.Sqlite の .NET Framework 4.8 サポートバージョンに制限がある可能性** → `10.0.11` + `2.1.12` でビルド・テストが `PASS` したため解消（`3.0.5` は `AnyCPU` 禁止のため `2.1.12` を採用）
- **[Risk] 22 ファイルにおよぶ書き換えで typo や置き忘れが発生する可能性** → using 文の一括置換後、ビルドエラーで残存箇所（`CreateFile` / 単引数コンストラクタ / `DbType`）を検出し対応
- **[Risk] e_sqlite3.dll ネイティブ DLL の実行時解決** → `SQLitePCLRaw.lib.e_sqlite3` の `runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` を `None/Content` + `CopyToOutputDirectory` で `bin/lib/runtimes` に集約し、`FodyWeavers.xml` の `ExcludeAssemblies` + `AfterResolveReferences` 除外 + `probing privatePath="lib"` + `AssemblyResolve` で解決。`GetManifestResourceNames` で `SQLitePCLRaw/Microsoft.Data.Sqlite` の埋め込みが無いことを確認（`lib` 物理配置）。詳細は `pitfalls 4c` を参照
- **[Trade-off] 機械的置き換えで対応できるが、コードレビュー時の差分が大きくなる** → 1回のコミットで行い、差分の把握を容易にする

---

## APIリクエスト組み立ての型付き化（Issue #19・2026-09）

> タスクは `docs/tasks.md` の「ニコ動APIのリクエスト組み立てを型付きリクエストへ変更」を参照。背景（CLI等による外部検索条件指定の下地・スナップショット優先/nvapi横展開）は Issue #19「なぜ変更するか」を参照。

### Context

- スナップショットAPIのURLを `string.Format` リテラル直書き＋`%2B`手動エンコードで組み立てており、値のエスケープ未実施・必須 `_context` 未送信・フィルタ追加困難の技術負債があった。
- nvapi も `appendURL` 文字列連結で同種の問題（特に日本語 `tag` の未エンコード）を抱えていた。
- 公式仕様はいずれも GET クエリパラメータ形式のため、JSONボディPOST化はしない。

### Decisions

#### Decision: SnapShotRequest（型付きリクエストクラス新設・`nicorankLib/SnapShot/SnapShotRequest.cs`）

- **選択**: `q/targets/fields/filters/_sort/_limit/_offset/_context` をプロパティで保持し、`ToUrl()` で組み立て。キーは公式のブラケット記法（`filters[...][...]`）のまま、**値のみ `Uri.EscapeDataString`**。
- **理由**: ブラケットまでエンコードすると公式curl例の記法と乖離する。値のみのエンコードで `+09:00`→`%2B` の手書きが不要になり、日本語・`&`・`%` 混じり値のクエリ破壊を防ぐ。旧URLとのデコード等価性は単体テストで担保。
- **`_context` は `WeeklyNicoranProgram` を流用**（現行UAと同一・40文字制限内・追跡性維持）。
- **`_limit/_offset` はクランプ**（上限100/100000・下限0）。`_limit=0` の件数取得用途は維持。
- **見送り**: 日付逆転・`Context` 長・`Fields` 空等のバリデーションは将来のCLI外部指定時に実施（現行フローは内部生成のみのため）。
- **代替案**: `UriBuilder.Query`＋`ParseQueryString` は .NET Framework 4.8 で `System.Web` 依存を持ち込むため不採用。素の `StringBuilder`＋`EscapeDataString` で完結させる。

#### Decision: jsonFilter は string 経路のみ・型階層は先送り

- **選択**: `JsonFilterJson`（生JSON文字列＋エンコード経路）のみ用意し、`equal/range/or/and/not` の型階層は作らない。
- **理由**: 現行フローで使用箇所ゼロ。CLI要件未確定の段階で型を固めると手戻りになる。string経路があれば将来の型追加はビルダー内に閉じる。

#### Decision: SetRequestResult の flgLimit1000 不整合を解消

- **選択**: 旧 `SetRequestResult` は `flgLimit1000` を無視し常に1000制限URLを使っていた。`CreateRequestUrl(limit, offset, flgLimit1000)` に一本化し、件数取得時と同じフラグでページング取得する。未使用の `dateTime` 引数は `flgLimit1000` に置換。
- **理由**: 直近1年は「件数取得は無制限・実取得は1000制限」の矛盾があった。specs.md の「1000再生以上フィルタ（直近1年以外）」通りの挙動に合わせる。振る舞い変更のためレビュー依頼文・コミットメッセージに明記。

#### Decision: Replace(":null", ":0") は温存

- **選択**: シリアライズ設定側での解消は見送り、文字列ハックを維持。必須性を示す回帰テスト（`FromJson_NullCounters_RequireNullToZeroReplacement`）を追加。
- **理由**: 実証したところ null→`long` 直結の `FromJson` は `JsonSerializationException` で失敗する。POCOを `long?` 化すると `SnapShotDB.RegistDB` まで波及しリスク＞効果。
- **見送り**: カウンタ限定の正規表現への狭め化（文字列値中の `:null` 破壊は現実リスク極小のため）。

#### Decision: nvapi は辞書受け＋最小限ガード（横展開）

- **選択**: `requestAPI(apiurl, appendURL)` → `requestAPI(apiurl, query辞書)` に変更し文字列連結を廃止。`_frontendId=6`・UAを定数化。パス埋め込み（`genre/featuredKey`）も `EscapeDataString`。`tag` は `term=24h/hour` 以外では省略＋ログ（公式仕様の制約）。
- **理由**: 日本語タグの未エンコードが実害リスク最大のため。`tag` 省略は振る舞い変更のためレビュー依頼文・コミットメッセージに明記。
- **汎用組み立ては `nicorankLib.Util.ApiUrlBuilder` に抽出**（reviewer指摘対応）。値のエンコード・`?`/`&` 切替・null/空辞書を単体テストで担保し、`NicoRankiApi.BuildUrl` は `_frontendId` 付与＋委譲に縮小。`tag` 分岐自体はspec直結の条件のため抽出せず、両分岐のdict形状に対するテストで間接担保する。
- **見送り**: `term` の大文字小文字許容（`config.json` 由来の固定小文字語彙のため）・`BuildUrl` の順序テスト（oldlogは単体テスト対象外・委譲は目視済み）・フラグメント付きURL（呼び出し元なし）。
- **見送り**: `NicoApi.cs` のID連結（`sm/so`＋数字のみで実害なし）・`JsonReader`系のパス連結（クエリなし）・`InternetUtil` のデッドコードは対象外。理由はレビュー依頼文・コミットメッセージに残す。

#### Decision: エンコード済みURLの実サーバー受け入れは実証済み

- 全面エンコード（`fields` の `,`→`%2C`・日時の `:`→`%3A`・`+09:00`→`%2B`）＋`_context` 送信の件数取得1件で HTTP 200・`status:200` を確認（2026-09-04）。旧URLとのデコード等価性と併せ、移行の安全性を担保する。

---

## タグ出力の再設計（Issue #27・2026-09）

### Context

- 実利用者から「カテゴリ名と FavoriteTag が重複する」「ロックタグは3つだけ欲しい時と全部欲しい時がある」と要望があった
- 文字コード違いの出力群（SJIS / DB登録用CSV）は旧連携方式の名残で、現行は `result_DB登録用(UTF8).json` に一本化済みのため存在理由が消滅していた
- `FavoriteTags` は `HashSet<string>` で出力順が不定だったため、「最大3つ」の切り取りが決定的にならない問題があった

### Decisions

- **収集は無制限・制限は出力側**: `FavoriteTagReader` の件数上限を廃止し全件補完する。ファイル別の出し分け（TSV系は3件、`result(UTF8).csv`・`result_DB登録用(UTF8).json` のみ全件）は `NrmOutput` の上限パラメータで行う
- **カテゴリ重複の除外は出力側ヘルパー**（`Ranking.GetDisplayTags()`）: `UserInfoReader` が後段でカテゴリを補完するため、収集時点ではカテゴリ未確定の動画がある。最終カテゴリで判定する出力側が完全。DB格納値は重複のまま残ることを許容
- **順序保障のため `List<string>` 化**: 影響は宣言・初期化・マージ・3箇所の `Add`（重複判定化）のみ。DB・履歴内の既存 JSON は配列形式のため互換性あり
- **SJIS・DB登録用CSV の生成停止**: `CreateOutputCSV` の設定からSJIS除外、`CreateOutputCSV_rankDB`（週刊。SPは継承、中間は既にnull）を `null` 化。`frmMainSyukei` はnull安全のため無変更。`ResultCsvRankDB` クラスは温存
- **中間集計は外部取得なし**（`isLocalOnly`）: `FavoriteTagReader` にフラグを追加し、中間集計のみ `UpdateTumbInfo` を呼ばずキャッシュ参照で補完する。週刊/SPは先行オプション（`GenreInfoReader`/`UserInfoReader`/`MovieInfoReader`）が確保するため現状維持

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
