# タスク管理（Tasks）

タスクは**別エージェント / サブエージェントが単独で実行できる**ことを目的とする。
各タスクは「依存」「対象」「受け入れ条件」を満たすこと。

## タスクの実行ルール

- 実装前に `docs/proposal.md`（開発背景）`docs/design.md` `docs/specs.md` `docs/knowledge/` を必ず読むこと。
- 各タスクは対応する **GitHub Issue 番号と関連付ける**（提案は Issue で管理。フローは `docs/proposal.md` 参照）。
- 完了したタスクには `✅` を付け、`docs/knowledge/` を最新化し、関連する GitHub Issue を Close する。経緯・検証履歴は `docs/tasks/archive.md` に追記する。
- ビルド: `dotnet restore` → `dotnet test UnitTest/UnitTest.csproj` が通ること。
- ブランチ: `develop` から `feature/tXXX-*` を切り、完了後 `develop` にマージ（`--no-ff`）。`develop → main` はユーザーの明示指示時のみ（`docs/knowledge/release.md` 参照）。`main` は保護ブランチ。

---

## 未完了タスク

### 配布 zip 展開時の MOTW で SQLiteCtrl のタイプ初期化が失敗する対処

> GitHub issue #26 対応。GitHub Release 配布 zip をエクスプローラで展開すると DLL に MOTW(Zone.Identifier)が付き、SQLiteCtrl のタイプ初期化が失敗する。App.config へ `loadFromRemoteSources` 追加、起動時エラー表示の改善、knowledge 更新。詳細は issue を参照。

### ビルド警告の対処と未使用 AngleSharp の削除

> GitHub issue #25 対応。Release ビルドの警告 5 件（MSB3276 / CS0414 / CS0168×3）の対処と、`nicorank_oldlog` から未使用の AngleSharp PackageReference を削除（Dependabot #10）。詳細は issue を参照。

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
| デッドロジック削除（NocoChartReader / NicoChartModel / AngleSharp） | 2026-08-31 | 呼び出し元ゼロの NocoChartReader・専用モデル削除、AngleSharp 依存の除去 |
| Nicochartの仕様変更対応（別ロジックで代替） | 2026-08-31 | RankingHistory/SabunReader 改修（so40000000 未満を新着偽造として除外）、LogNicoChart.db 依存・SYSTEM.NicoChart 設定の完全削除 |
| btreeInitPage() returns error code 11 対策 | 2026-06-03 | SQLiteCtrl 接続強化（WAL・PRAGMA・グレースフルフォールバック）、SnapShotDB 5000件バッチコミット・INSERT OR IGNORE・パラメータ再利用 |
| SQLite 操作の単体テスト設計 | 2026-06-23 | ISQLiteCtrl 抽出、OpenInMemory、TestDbHelper、DB操作テスト（SELECT/INSERT/DDL/エラー） |
| 単体テストの活性化（基盤） | 2026-06-23 | SDK-style csproj 化、Moq 導入、Fixtures 配置、Ranking/Config/TextUtil/StatusLog/Output のテスト（計69件） |
