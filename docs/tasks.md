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

### ランキングJSON肥大化対策（#28）

> #27 の全件補完で肥大化が進行した件の対策。方針確定済み。
> DBVersionは LogOfficial.db／NicoranHistory.db の2DBに限定（ニコ動仕様変更で構成が変わり得るDB）。
> Dailylog.db／ApiXML.db はキャッシュ扱い（最悪作り直し）のためバージョン管理なし。

- [ ] 28.1 FavoriteTag見直しなし確定（コード不変・文書確定のみ）
- [ ] 28.2 LastResult.JSON列のDROP（新規INSERT除外＋既存DROP）＋旧SP種別行の削除（LastResult/LastResultInfo）
- [ ] 28.3 DBVersion導入（2DB・Ver=0開始）＋集計開始時の司令塔（DbMigrationCoordinator）による更新指示（失敗時中断・VACUUM含む。bat配布は自動移行で代替し見送り）

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

## 完了済みタスク（履歴）

| タスク | 完了日 | 主な成果物 |
|---|---|---|
| result(UTF8).csv不要列削除(#29)✅ | 2026-09-05 | TextUtil動的検出化（新旧両対応・ColLmt廃止・いいね対応・タグOption・マイリストポイント含む8列は再計算のため読取対象外）・ResultCsvRankDB削除（Factory枠まで）・ResultCsv30列新順化（人気タグ4列目・運営2列削除）・UnitTest5件追加（計124件）・fixture余分列修正・specs/design/knowledge更新 |
| 人気タグのタグロック補完(#27)✅ | 2026-09-04 | GetLockedTags新設・全件補完・FavoriteTags List化・GetDisplayTags・TSV系上限3・UTF8/JSON全件・SJIS/DB登録用CSV停止・isLocalOnly・所有権全10箇所対応、UnitTest25件追加（計119件）、specs/design/knowledge更新 |
| ニコ動APIのリクエスト組み立てを型付きリクエストへ変更(#19) | 2026-09-04 | SnapShotRequest・ApiUrlBuilder新設、nvapi辞書化、UnitTest19件追加（計94件）、specs/design更新 |
| 単体テストでDB操作のビジネスロジック問題を検出できるようにする(#22) | 2026-09-03 | NicoApi残存Clear漏れ2件修正、UnitTestDbCommandReuse新設6件（計75件）、pitfalls項目17・testing/structure更新 |
| ビルド警告の対処と未使用 AngleSharp の削除 | 2026-09-03 | CS0168×3・CS0414・MSB3276(System.Memory 4.0.5.0整合)・CS0162・Fody警告を解消しソリューション警告0、nicorank_oldlog の AngleSharp 削除 |
| 配布 zip 展開時の MOTW で SQLiteCtrl のタイプ初期化が失敗する対処 | 2026-09-01 | App.config に `loadFromRemoteSources` 追加(両アプリ)、起動時エラー表示の例外チェーン化、pitfalls.md 項目16・release.md 更新 |
| デッドロジック削除（NocoChartReader / NicoChartModel / AngleSharp） | 2026-08-31 | 呼び出し元ゼロの NocoChartReader・専用モデル削除、AngleSharp 依存の除去 |
| Nicochartの仕様変更対応（別ロジックで代替） | 2026-08-31 | RankingHistory/SabunReader 改修（so40000000 未満を新着偽造として除外）、LogNicoChart.db 依存・SYSTEM.NicoChart 設定の完全削除 |
| btreeInitPage() returns error code 11 対策 | 2026-06-03 | SQLiteCtrl 接続強化（WAL・PRAGMA・グレースフルフォールバック）、SnapShotDB 5000件バッチコミット・INSERT OR IGNORE・パラメータ再利用 |
| SQLite 操作の単体テスト設計 | 2026-06-23 | ISQLiteCtrl 抽出、OpenInMemory、TestDbHelper、DB操作テスト（SELECT/INSERT/DDL/エラー） |
| 単体テストの活性化（基盤） | 2026-06-23 | SDK-style csproj 化、Moq 導入、Fixtures 配置、Ranking/Config/TextUtil/StatusLog/Output のテスト（計69件） |
