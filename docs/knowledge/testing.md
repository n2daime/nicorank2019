# テスト構成（testing.md）

## 実行コマンド

```powershell
dotnet restore
dotnet test UnitTest/UnitTest.csproj
```

- 環境: **.NET Framework 4.8 ターゲットだが .NET 10 SDK でビルド・実行**（Windows）
- テストフレームワーク: MSTest 3.5.2 / モック: Moq 4.20.72
- 全 **270 件**のテストが PASS（内訳はテスト実行で確認する。#31で15件・#38で17件・#40で20件・#39で16件・#44で10件を追加）

## 構成

```
UnitTest/
├── UnitTest.csproj          # SDK-style csproj（重要・下記参照）
├── Fixtures/
│   ├── nicorank.xml          # Config テスト用設定ファイル（出力直下にコピー）
│   └── test_ranking.csv      # CSV読み取りテスト用データ
├── Helpers/
│   ├── TestDbHelper.cs       # DB操作テストの基底ヘルパー（インメモリSQLite）
│   ├── TestConfigBuilder.cs  # Config の非公開フィールドをリフレクションで書き換えるテスト用ビルダー
│   └── UnitTestTestDbHelper.cs
└── nicorankLib/
    ├── Util/       UnitTestSQLiteCtrl(12) / DbQuery(10) / DbWrite(9) / DbSchema(8) / DbError(4) / DbCommandReuse(6) / StatusLog(3) / TextUtil(6) / ApiUrlBuilder(7) / DbMigrationCoordinator(12)
    ├── Common/     UnitTestConfig(8)
    ├── output/     UnitTestOutput(8)
    ├── SnapShot/   UnitTestSnapShotRequest(12) / UnitTestTagConditionParser(7) / UnitTestTagSearchRequest(7) / UnitTestSnapShotVersionChecker(12) / UnitTestSnapShotVersionPoller(5)
    ├── Analyze/     UnitTestRankingAnalyze(9) / UnitTestBasicOptionDispose(10)
    ├── Analyze/model/ UnitTestRanking(6) / UnitTestRankingDisplayTags(5) / UnitTestRankingDeletedMarker(2)
    ├── Analyze/Input/ UnitTestTagRankAnalyze(8)
    ├── Analyze/Official/ UnitTestRankingHistorySoHistory(15)
    ├── Analyze/Option/Basic/ UnitTestTagRankTotalReader(4) / UnitTestTagRankLiveReader(7) / UnitTestSpMovieInfoFallback(4)
    ├── Analyze/Option/Ext/ UnitTestFavoriteTagReader(9)
    └── api/        UnitTestNicoApiLockedTags(6) / UnitTestNicoApiNoDelete(9) / UnitTestApiXmlCacheImporter(5)
```

### テスト一覧（観点）

| ファイル | 件数 | 対象・観点 |
|---|---|---|
| `UnitTestSQLiteCtrl` | 12 | SQLiteCtrl の Open/OpenInMemory/Close/Dispose ライフサイクル、二重呼び出し安全性 |
| `UnitTestDbQuery` | 10 | SELECT パターン（PK取得、存在しないID、過去/未来方向、BETWEEN、MAX、COUNT、IFNULL、JOIN、ORDER BY DESC LIMIT 1） |
| `UnitTestDbWrite` | 9 | INSERT（単行/重複防止/ループ）、DELETE、トランザクション（Commit/Rollback/例外時）、パラメータ再利用 |
| `UnitTestDbSchema` | 8 | CREATE TABLE、PRAGMA table_info、ALTER TABLE、sqlite_master、ATTACH/DETACH |
| `UnitTestDbError` | 4 | ファイル不在、未接続/多重 Dispose、複数インスタンス同時接続 |
| `UnitTestDbCommandReuse` | 6 | 同一コマンド再利用（Clearなし重複の例外・DELETEループ・DELETE→INSERT切替・SELECT切替・ALTER同一トランザクション・外部コマンド使い回し。Issue #22） |
| `UnitTestStatusLog` | 3 | StatusLog の Write/WriteLine/null writer（モック IStatusLogWriter） |
| `UnitTestTextUtil` | 6 | TextUtil.ReadCsv（List版/Dictionary版/ファイル不在） |
| `UnitTestConfig` | 14 | Config シングルトン、デフォルト値、SP モード、XML 文字列出力、TAGRANK節あり・なし・フラグOFF・項目欠落フォールバック（Issue #30）・OFFSET節別化の節あり・なし・部分あり・setter書込先・部分節／節なしsetterの共通不変（Issue #39） |
| `UnitTestOutput` | 8 | ResultCsv と NrmOutput の一時ディレクトリへの実出力検証（タグ列・上限3・全件・カテゴリ除外。Issue #27） |
| `UnitTestRankingDisplayTags` | 6 | `GetDisplayTags` のカテゴリ除外・順序・Trim・重複・非破壊・null（Issue #27） |
| `UnitTestFavoriteTagReader` | 9 | 人気タグ＋ロックタグ全件補完・重複除外・対象外・確保失敗でも中断せず続ける・null行・`isLocalOnly`×2（Issue #27。確保失敗の扱いはIssue #40で変更） |
| `UnitTestNicoApiLockedTags` | 6 | `GetLockedTags` のlock抽出・行なし・最新取得日・非ok・破損XML（Issue #27） |
| `UnitTestNicoApiNoDelete` | 9 | `GetUserInfo`/`GetMovieInfo` の非ok・行なしでも除外せず中断しない・最新取得日読みの両方向・ThreadMax解決の優先順（Issue #40） |
| `UnitTestSpMovieInfoFallback` | 4 | LastResultタイトル補完・期間内初見日・予備なし残留・取得済み非上書き（Issue #40） |
| `UnitTestRankingDeletedMarker` | 2 | 空欄タイトルへの【集計後削除】付与・取得済み非変更（Issue #40） |
| `UnitTestApiXmlCacheImporter` | 5 | 新規取込・本地が新しい場合の保持・運搬が新しい場合の置換・同取得日の保持・Status NULL行の取込（Issue #40） |
| `UnitTestRanking` | 6 | PointTotal/HoseiAllPoint の補正計算（VOCACOLE2023実測、補正なし、sqrt、削除動画、ゼロ、境界値 0.25〜1.0） |
| `UnitTestRankingAnalyze` | 9 | 同点時タイブレーク（Issue #34。ID数値認識のsm20/sm199・桁境界・sm/so種別・非数値フォールバックと群分離順序・null/同一・空文字・前ゼロ・総合数値順・入力順反転の決定性・副順位数値順） |
| `UnitTestBasicOptionDispose` | 10 | 破棄経路の再発防止（Issue #44。空Dispose7件・全Option破棄と冪等・1件失敗でも継続・注入接続の非破棄・自前接続の解放2件・工場委譲と未生成時） |
| `UnitTestTagConditionParser` | 7 | タグ条件式→jsonFilter（完全一致・部分一致・AND/OR優先・Trim・空条件・空トークン・末尾外`*`却下。Issue #30） |
| `UnitTestTagSearchRequest` | 7 | CreateTagSearch の URL 生成（空q・targets省略・下限0省略・下限あり・種別long/short・不正種別・jsonFilter・中立期間・日付指定。Issue #30） |
| `UnitTestTagRankAnalyze` | 8 | 件数取得・ページ重複除去・ID順・上限超過・件数失敗・不正条件・ページ失敗・null条件（Issue #30）・LiveCounters保持（Issue #35） |
| `UnitTestTagRankTotalReader` | 4 | TotalReader の Open成否・AnalyzeTime・工場分岐（基準なし・あり）の失敗経路（Issue #30） |
| `UnitTestTagRankLiveReader` | 7 | ApplyLiveTotalsの対応付け・欠落除外・LiveTotal/LiveSabunのOpen成否・BaseTime・工場のDB不要成功系（Issue #35） |
| `UnitTestSnapShotRequest` | 12 | SnapShotRequest の URL 生成（1000フィルタ有無・`_context`・`%2B`・旧URL等価・日本語Q・クランプ・ゼロlimit・`_offset`上限・targets省略・jsonFilter・null回帰。Issue #19） |
| `UnitTestSnapShotVersionChecker` | 12 | version判定の同日・前日・月またぎ・非JST環境・非JSTオフセット・取得失敗・例外・JSON破損・欠落・パース失敗・ログ文面（Issue #38） |
| `UnitTestSnapShotVersionPoller` | 5 | 待機仕様値の固定・即更新・リトライ後更新・タイムアウト打ち切り・確認不能のまま打ち切り（フェイク時計。Issue #38） |
| `UnitTestTagSnapshotTimestamp` | 10 | 時点ラベルのJST日＋05:00固定・Z表記・日付境界・仕様値固定・TryFormat成功／確認不能／null・NotUpdated時表示・境界両側・05:00ちょうど（Issue #39） |
| `UnitTestApiUrlBuilder` | 7 | ApiUrlBuilder のクエリ組み立て（日本語tag・tag省略形状・null/空・null値・`?`付きベース・nullベース例外。Issue #19） |
| `UnitTestDbMigrationCoordinator` | 12 | 司令塔の全成功・失敗時中断・null例外、RankingHistory/ResultHistoryのDBVersion確保・JSON列DROP・SP行削除・冪等・記録Verが新しい場合の無変更・最古スキーマ対応（Issue #28。RankingHistoryのVer期待値はVer1に更新） |
| `UnitTestRankingHistorySoHistory` | 15 | SoHistory作成・境界より古い行の退避・混入行清掃・基準日ガード・保持境界prune・Movie廃止・冪等・索引作成・CheckSoMovieNeedSabunのフォールバック/優先/null/表なし・日次の消去行拾い上げと条件付き置換・まとめ消し（Issue #31） |

## テストパターン

- **DB操作テスト**: `TestDbHelper` を継承し、`OpenInMemory()` でインメモリ SQLite を生成。`ISQLiteCtrl` 経由でコンストラクタ注入して差し替え
- **設定テスト**: `TestConfigBuilder` で `Config` の非公開フィールド（`xml` / `Instance`）を差し替え
- **ログテスト**: `IStatusLogWriter` の Moq を `StatusLog.SetLogWriter()` に注入

## 重要: csproj は SDK-style を使用すること

### 背景

旧 `UnitTest.csproj` は old-style csproj（packages.config + Reference）で、.NET 10 SDK 上の MSTest.TestAdapter 3.5.2 と組み合わせると testhost 起動時に **StackOverflow** が発生する。

### 解決策

SDK-style csproj に変換済み。ポイント:

- `TargetFramework` は `net48`
- `<IsTestProject>true</IsTestProject>` を設定
- パッケージは `PackageReference` 形式（packages.config は使わない）
- `MSTest.TestAdapter.ExternalAssemblies` が必要な場合がある（.NET Framework ターゲット時）

### 注意点

- SDK-style csproj の出力先は `bin\Debug\net48\`（old-style は `bin\Debug\`）。フィクスチャのコピー設定は出力先のパスに合わせる
- `app.config` は残してよい（binding redirect 用）

## フィクスチャファイル

テスト用データは `UnitTest/Fixtures/` に配置し、csproj の `<None Update="Fixtures\*">` + `CopyToOutputDirectory=PreserveNewest` で出力先にコピー。

- `Fixtures\nicorank.xml` → 出力直下の `nicorank.xml` としてもコピー（`Config.GetInstance().Initilize()` がカレントディレクトリから読むため）
- `Fixtures\*.csv` → `Fixtures\` サブフォルダにコピー

## 既知の制約・落とし穴

1. **テスト実行時に StackOverflow** — old-style csproj + .NET 10 SDK の MSTest 互換性問題。対処: SDK-style csproj に書き換え（済み）
2. **Config テストが失敗する（nicorank.xml が見つからない）** — `Config.GetInstance().Initilize()` がカレントディレクトリの `nicorank.xml` を読む。対処: 出力直下に配置
3. **TextUtil.ReadCsv の戻り値** — ファイル不在時、戻り値は `false` で out 引数は**空の List**（null ではない）。テストは `Assert.IsNotNull` + `Count == 0`
4. **nicorankLib の Costura.Fody は削除しない** — StackOverflow とは無関係
5. **絶対パスの使用禁止** — `T:\...` 等のハードコードは NG。Fixtures は相対パスで参照

## 新規テスト追加の作法

1. `UnitTest/nicorankLib/` 以下に対象クラスと同じ名前空間パスでファイルを作成
2. 既存テストを参考にクラス・メソッドを記述
3. SQLite を使用するテストは `OpenInMemory()` でインメモリ DB を作成
4. `dotnet test UnitTest/UnitTest.csproj` で全件 PASS を確認
