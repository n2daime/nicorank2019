# DB 構成（db.md）

## DB ファイル一覧

| DB ファイル | 定数（DB.cs） | 用途 | 作成・更新元 |
|---|---|---|---|
| `DB/LogOfficial.db` | `LOG_OFFICEIAL` | 公式過去ランキング（Ranking / Movie / RankingDate） | RankingHistory（nicorank2019 起動時） |
| `DB/NicoranHistory.db` | `NiCORAN_HISTORY` | 集計履歴（History / LastResult / LastResultInfo） | ResultHistory / LastRankReader / TyokiHantei |
| `DB/ApiXML.db` | — | NicoApi 動画情報キャッシュ（NicovideoThumb） | NicoApi |
| `DB/Dailylog.db` | — | 中間集計の日別キャッシュ（Dailylog） | TyukanAnalyze |
| `LogSnapshot{yyyyMMdd}.db` | `LOG_SNAPSHOT` | スナップショット取得結果（Ranking / DBVersion） | nicorank_SnapShot → SnapShotDB |

## テーブル概要

### LogOfficial.db

- **Ranking**: `ID` / `集計日`（INTEGER・yyyyMMdd）/ `再生数` / `コメント数` / `マイリスト数` / `いいね数` / `人気のタグ`（JSON文字列）
  - 集計日は主キーの一部（同一動画の日別履歴）。いいね数は ALTER TABLE で自動追加（無い場合のみ）
- **Movie**: 動画の基本情報（Ranking と JOIN して使用）
- **RankingDate**: 集計日とメンテナンスフラグ。`CheckMaintananceDay` でメンテ日判定。初期値 20190610
- **DBVersion**: `Ver` INTEGER（Issue #28。旧DBはテーブルなし→Ver0扱い。集計開始時の自動移行でVer=0を1行追加）
- 更新フロー: 集計開始時に `DbMigrationCoordinator` が LogOfficial→NicoranHistory の順に更新確認（失敗時は中断）→ `UpdateOfficialRankingDB()` が RankingDate の `Max(集計日)+1` から今日までを日別取得。データ 0 件の日はメンテナンス日として登録（UI で確認）

### NicoranHistory.db

- **History**: 動画ごとの過去ランクイン履歴 → 長期動画判定（TyokiHantei）の材料
- **LastResult**: 前回の集計結果（種別=モード名、集計日）。JSON列は空文字で登録（Issue #28。LastRankReaderは総合ランク・ポイントのみ参照）。LastRankReader が前回順位を参照
- **LastResultInfo**: 前回集計時の設定 XML（`Config.GetXMLString()`）
- **DBVersion**: `Ver` INTEGER（旧DBはテーブルなし→Ver0扱い。集計開始時の自動移行でVer=0を1行追加。以後の構成変更時は+1）

### ApiXML.db

- **NicovideoThumb**: 動画 ID / 取得日 / Status（ok=1 / その他 0）/ XML（getthumbinfo の生XML）
- 取得日（`MAX(取得日)`）が指定日より古いものだけ更新対象
- キャッシュ扱いのためDBVersion管理の対象外（Issue #28。最悪作り直しで対応）

### Dailylog.db

- **Dailylog**: 日別集計結果（ID / 集計日 / 再生数 / コメント数 / マイリスト数 / いいね数 等）。中間集計が日別に INSERT → 期間合計（SUM GROUP BY）で中間ランキング生成
- いいね関連フィールドが無ければ ALTER TABLE で自動追加
- キャッシュ扱いのためDBVersion管理の対象外（Issue #28。最悪作り直しで対応）

### LogSnapshot{yyyyMMdd}.db

- **Ranking**: `ID`（主キー）/ `再生数` / `コメント数` / `マイリスト数` / `いいね数`。`INSERT OR IGNORE` で追記
- **DBVersion**: `集計日` / `Ver`（1.0.1.0）
- `InitilizeDB()` が既存ファイルを**削除して再作成**

## SQLiteCtrl 接続設計

`nicorankLib/Util/SQLiteCtrl.cs`（`Microsoft.Data.Sqlite 10.0.11` + `SQLitePCLRaw 2.1.12`、2026-08 更新。`lib` サブフォルダ集約）

- `Open(path)`: ファイル存在チェック → 接続文字列 `Data Source="<path>";Pooling=False;Default Timeout=30`（`SQLiteConnectionStringBuilder` / `JournalMode` は廃止。WAL は `PRAGMA journal_mode=WAL` で設定）→ `SqliteConnection` を `Open()` → PRAGMA 4種 → IsOpen=true
  - PRAGMA: `journal_mode=WAL` / `synchronous=NORMAL` / `temp_store=MEMORY` / `cache_size=-8000`（`Connection.CreateCommand()` で実行）
  - **PRAGMA 設定失敗時も接続は継続**（catch で無視。呼び出し側でログ）
  - 同一 DataSource なら再オープンしない。別 DataSource なら先に Close
- `OpenInMemory()`: テスト用（`Data Source=:memory:`、File.Exists スキップ。`SqliteConnection` で `Open()`）
- `Close()` / `Dispose()`: 二重呼び出し安全
- テストは `ISQLiteCtrl`（`SqliteConnection` 公開）経由でインメモリ実装に差し替え可能。`ISQLiteCtrl` / `SQLiteCtrl` は `Microsoft.Data.Sqlite` に依存

## SnapShotDB の大量登録設計（btreeInitPage 対策）

`nicorankLib/SnapShot/SnapShotDB.cs` の `RegistDB()`

- **5000件ごとのバッチコミット**: 単一巨大トランザクションによる WAL 肥大化を回避（約200Byte/行 → 約1MB/バッチ）
- **INSERT OR IGNORE**: 重複 ID はスキップ（`INSERT ... WHERE NOT EXISTS` から変更、SQLite の最適化パスを利用）
- **パラメータ事前生成・再利用**: ループ外で `SqliteParameter` を `SqliteType.Text` / `Integer` で `Add()`、ループ内で `.Value` 代入（GC 圧力削減。`System.Data.DbType` は `SqliteType` に置換）
- **トランザクション**: `Connection.BeginTransaction()` は `DbTransaction` を返すため `SqliteCommand.Transaction` への代入時に `(SqliteTransaction)` キャスト。`Connection.CreateCommand()` でコマンド生成
- **DB ファイル作成**: `SqliteConnection.CreateFile` は存在しないため `System.IO.File.Create(path).Dispose()` で代替（`InitilizeDB`）
- **最終コミット**: ループ終了後に残りをコミット
- **Rollback の例外処理**: `try { aCmd.Transaction?.Rollback(); } catch { }` で元の例外を上位に伝播

**背景**: 数十万件の登録で `btreeInitPage() returns error code 11`（SQLITE_CORRUPT）が断続発生し DB が破損していた。WAL モード + バッチコミット + PRAGMA 設定で解消。詳細は `../proposal.md`（完了履歴）と `../design.md` を参照。

## 注意点

- 旧 `DB/LogNicoChart.db` は #23（2026-08）で参照廃止。ファイルが残っていても読み書きされない（消してもよい）
- `LogSnapshot*.db` は日次作成されるため、バッチ途中でクラッシュしても再実行で最初から取得できる
- WAL モードでは `.db-wal` / `.db-shm` ファイルが別途作られる（自動管理。不要時は `PRAGMA wal_checkpoint(TRUNCATE)`）
- 実データベースの「依存ファイル/DB/」はビルド時に PostBuild でコピーされる
- ネイティブ `e_sqlite3.dll`（`win-x64/x86/arm` 3種）と `SQLitePCLRaw` 4 DLL（`Microsoft.Data.Sqlite`/`core`/`batteries_v2`/`provider.dynamic_cdecl`）は `bin/{Debug,Release}/lib` に集約（`probing privatePath="lib"` + `AssemblyResolve` フォールバック）。`batteries_v2.dll` の `Location` 基準で `lib\runtimes/{rid}/native/e_sqlite3.dll` を探索するため `lib` 配下に同一親で配置する必要がある。詳細は `pitfalls.md 4c` を参照（`3.x` では `provider.e_sqlite3` を加え5 DLL）
