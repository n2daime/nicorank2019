# nicorank2019 — メンテナ向け概要

このドキュメントはソリューションを初めてメンテナする人向けの短いガイドです。

---

## プロジェクト構成（概略）

- `nicorankLib` - 集計ロジック、ファクトリ、出力、ユーティリティ等を含む再利用ライブラリ。
- `nicorank2019` - WinForms UI（`frmMain` など）。実行フローの入り口。
- `nicorank_SnapShot` - スナップショット取得・差分処理に関連するプロジェクト。
- `nicorank_oldlog` - 廃止されてしまった公式Dailylogの代わりを作成するツール。
- `UnitTest` - MSTest ベースの単体テストプロジェクト（全69件）。`nicorankLib` の全レイヤをカバー。

---

## 主要クラスと責務

以下は集計フローに沿った主要クラスとその責務です。クラス名はバッククォートで表記しています。

1. 実行エントリ / UI
   - `frmMain` / `frmMainSyukei`（WinForms）
     - ユーザ操作でモード選択（`SelectMode()`）→ ファクトリ取得（`GetModeFactory()`）→ 非同期解析開始（`AnalyzeAsync()`）。

2. ファクトリ（モード分岐）
   - `ModeFactoryBase`（基底）
     - 共通のプロパティ（`TargetDay`, `BaseDay`, `RankingAnalyze`, `RankingList` 等）とインターフェースを定義。
   - `ModeFactoryWeekly`, `ModeFactoryTyukan`, `ModeFactroySP`
     - 各モードに合わせた入力セットアップを行い、`CreateAnalyzer()` で `RankingAnalyze` を構築する。
     - `ModeFactroySP` は外部ファイル（スナップショットDB、ベースDB、movie list、前回結果CSV）を受け取り専用の初期化を行う。

3. 入出力（Input / Analyze / Option）
   - `RankingAnalyze`
     - 実際の集計ロジックを担う。データソース（`JsonReaderWeekly` や `SPAnalyze` 等）とオプションを受けて解析を実行する。
   - `IExtOptionBase` / `BasicOptionBase`
     - 集計中／集計後に必要な追加処理（例: `LastRankCsvReader`, `SnapShotSabunReader`, `FavoriteTagReader`）。

4. 過去の集計結果管理
   - `RankingHistory`
     - 履歴DBのオープン、公式ランキングDBの更新（`UpdateOfficialRankingDB()`）などを行う。

5. 出力
   - `OutputBase`（基底）
     - 各種出力の共通処理を定義。派生クラスは `Execute(IReadOnlyList<Ranking>)` を実装。
   - 代表的な派生: `ResultCsv`, `ResultJsonRankDB`（`ResultJsonRankDB` 名は参考）、`ResultImagegetMovieIcon`, `ResultImagegetUserIcon`, `NrmOutput`, `ResultCsvRankDB`, `ResultHtml` など

6. ユーティリティ / 共通設定
   - `Config`（シングルトン）: 各種設定（重み、補正、出力フォルダ、ユーザ数など）。
   - `StatusLog`, `ErrLog`：ログ出力の抽象化。
   - `ISQLiteCtrl`（インターフェース） / `SQLiteCtrl`（実装）: SQLite 操作を抽象化。コンストラクタ注入によりテスト時にインメモリDBと差し替え可能。
   - `TextUtil`, `DateConvert` 等のヘルパークラス。

7. ドメインモデル
   - `Ranking`（集計結果の1件を表すモデル）
   - その他 JSON や内部モデル（ジャンル情報、タグ情報など）

---

## 典型的な実行フロー（クラス/メソッド中心）

1. `frmMain.SelectMode()` が UI から設定を読み取り `Config` を更新。
2. `frmMain.GetModeFactory()` が選択モードに適した `ModeFactory*` を生成。
3. `frmMain.AnalyzeAsync()` 内で `RankingHistory.Open()` を呼び、`UpdateOfficialRankingDB()` を実行。
4. `MainFactory.CreateAnalyzer()` → `RankingAnalyze` のインスタンスが生成される。
5. `MainFactory.AnalyzeRank()` を呼び、`RankingAnalyze` が実データを集計して `RankingList` を作成。
6. 成功後、`MainFactory.Create*()` 系で `OutputBase` 派生を列挙し、それぞれ `Execute(RankingList)` を呼ぶ。
7. 必要に応じて `RankingHistory.Close()` で DB を閉じる。

---

## 初めて見る人への注意点 / よくある変更箇所

- モード追加や入力仕様変更は `ModeFactory*` に影響する。
- 出力形式追加は `OutputBase` 派生クラスを追加して `ModeFactory*` の出力列挙部に登録。
- 設定の変更は `Config` を通して一元管理する。
- SQLite 操作のテスト・差し替えは `ISQLiteCtrl` インターフェース経由。実装追加時は `SQLiteCtrl` に変更を加え、単体テストでは `TestDbHelper` を継承してインメモリDBで検証する。
- `nicorank_SnapShot/SnapShotDB.cs` は `btreeInitPage() returns error code 11` 対策済み（`snapshotBugFix` ブランチよりマージ）。

---

## 参照ファイル（スタートポイント）

- UI / 実行: `nicorank2019/frm/frmMain.cs`, `frmMainSyukei.cs`
- ファクトリ: `nicorankLib/Factory/ModeFactoryBase.cs`, `ModeFactoryWeekly.cs`, `ModeFactoryTyukan.cs`, `ModeFactroySP.cs`
- 集計: `nicorankLib/Analyze/RankingAnalyze.cs`
- 過去の集計結果管理: `nicorankLib/Analyze/RankingHistory.cs`
- 出力基底: `nicorankLib/output/OutputBase.cs`
- オプション例: `nicorankLib/Analyze/Option/TyokiHantei.cs`
- DB抽象化: `nicorankLib/Util/ISQLiteCtrl.cs`, `nicorankLib/Util/SQLiteCtrl.cs`
- スナップショットDB: `nicorankLib/SnapShot/SnapShotDB.cs`
- ユーティリティ: `nicorankLib/Util`, `nicorankLib/Common`
- 単体テスト: `UnitTest/Helpers/TestDbHelper.cs`, `UnitTest/Helpers/UnitTestTestDbHelper.cs`, `UnitTest/nicorankLib/Util/UnitTestSQLiteCtrl.cs`, `UnitTest/nicorankLib/Util/UnitTestDbSchema.cs`, `UnitTest/nicorankLib/Util/UnitTestDbQuery.cs`, `UnitTest/nicorankLib/Util/UnitTestDbWrite.cs`, `UnitTest/nicorankLib/Util/UnitTestDbError.cs`, `UnitTest/nicorankLib/Util/UnitTestTextUtil.cs`, `UnitTest/nicorankLib/Util/UnitTestStatusLog.cs`, `UnitTest/nicorankLib/Common/UnitTestConfig.cs`, `UnitTest/nicorankLib/Analyze/model/UnitTestRanking.cs`, `UnitTest/nicorankLib/output/UnitTestOutput.cs`
- 変更管理: `openspec/changes/archive/`（完了済み変更のアーカイブ）, `openspec/specs/`（仕様書）
