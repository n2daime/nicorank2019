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
| BasicOption破棄経路の整備（案B・#44。提案元#42）✅ | 2026-09-26 | BasicOptionBaseのIDisposable化（空の仮想Dispose・資源なし7件は無変更）・資源持ち3件のoverride寄せ替え＋_ownsDbCtrl所有権（注入接続は閉じない）・RankingAnalyze／ModeFactoryBaseのIDisposable化と破棄委譲（冪等・null安全・1件失敗でも継続）・SP／TagRank工場の失敗経路破棄・Tyukan内側using化・frmMainSyukeiの付け替え前＋出力後try-finally破棄・UnitTest10件追加（計270件）・design更新（Ext見送り理由含む）・reviewer再レビューでマージ可（中3件＋低3件すべて対応）・ユーザーSP実機検証OK（集計後にworkファイル消去を確認）・developマージ |
| タグ検索v2最新値のデータ時点表示＋OFFSET節別化(#39)✅ | 2026-09-26 | lblTagSnapshotTime新設（ON時のみ表示・MM/DD 05:00固定・確認不能時は中断）・TagSnapshotTimestamp新設（JST日・定数05:00・TryFormat）・SP/TAGRANK節にOFFSET4種（項目単位フォールバック・書込は節内生成）・集計中タブ固定・Initilize既定生成・配布テンプレートTAGRANK-OFFSET全0化・UnitTest16件追加（計260件）・specs/design/knowledge更新・reviewer再レビュー2回でマージ可（低4件対応）・ユーザー実機検証OK・developマージ |
| メンテナンスタブにDBの最適化(#32)✅ | 2026-09-26 | tabPageMaint新設（4DBチェック既定ON・実行前後2列・#41予告枠・ログ欄なし）・DbOptimizer新設（DBごとにDROP→DELETE→VACUUM・削除行数＋前後サイズ・実行日起点1年前・種別パラメータ化・境界固定）・convertMovieID除去・非同期実行＋同時実行ガード（両方向・実行中フラグ）・UnitTest10件追加（計244件）・specs/design/knowledge更新・AGENTSにIssueコメント全件読み追加・reviewer再レビュー4回でマージ可（低見送り3件）・ユーザー実機検証OK・developマージ |
| ApiXML削除判定の順位影響排除(#40)✅ | 2026-09-26 | NicoApiのisDelete除去・最新行読み・Reader中断廃止・SP予備補完SpMovieInfoFallback（案B。LastResultタイトル＋LogOfficial初見日）・週刊事前取得（oldlogが全ID約26000件をApiXML.dbへ・2019が取込）・削除目印【集計後削除】・並列数のconfig.json管理（ThreadMaxOverride）・UnitTest20件追加（計234件）・specs/design/knowledge更新・reviewer再レビュー問題なし（低2件見送り）・週刊実機検証（26770件取込・取得2372件に削減・有無両経路）・SP実機検証（エラーなし・マーカー0件正常）・developマージ |
| Snapshot API v2更新チェック(#38)✅ | 2026-09-23 | SnapShotVersionChecker/Poller新設（version取得・JST日付比較・更新済み/未更新/確認不能の3値判定・5分×最大1時間待機）・取得開始時にlast_modifiedをStatusLog出力・WinForm未更新時に日時入りOK/キャンセル確認ダイアログ・CLIタイムアウト時は取得せず終了コード2＋リトライタイムアウト記録・UnitTest17件追加（計214件）・specs/design/knowledge更新（切替時刻実測・競合注意）・reviewer再レビュー問題なし・NAS実機検証（9/21に8回未更新→更新検知→全期間取得・終了コード0）・調査スクリプトはマージ前除外・developマージ |
|---|---|---|
| LogOfficial.db肥大化対策(#31)✅ | 2026-09-21 | SoHistory新設＋Ver1移行（初期退避・混入行清掃・prune・Movie廃止・初回VACUUM）・日次prune駆動＋SoHistoryフォールバック（Ranking優先2クエリ逐次・基準日ガード）・GenreAnalyze削除・UnitTest15件追加（計197件）・specs/design/knowledge更新・reviewer総合判定マージ可（低8件：4件対応・4件見送り）・実DB破損救出再建＋通し再実行（2.01GB・integrity ok）・対策前後比較で旧exe混入と確定し検証終了・developマージ |
| SnapShot Linux対応CLI(#37)✅ | 2026-09-14 | nicorank_SnapShot.Cli新設（net8・ハイブリッド・終了コード0/2・CodePages登録）・SnapController失敗検知3件修正（InitilizeDB/RegistDB/例外）・sln登録・UnitTest182件維持・NAS実機で取得＋DB正常性確認（899万行・低再生99万行・integrity ok）・develop＋t031両マージ（t031で197件PASS）・knowledge更新 |
| 順位計算の同点時タイブレーク(#34)✅ | 2026-09-13 | RankingIdComparer新設（種別→数字・群分離・ASCII限定）・calcRanking6種にThenBy＋並列前ポイント確定（既存競合解消）・UnitTest9件追加（計182件）・specs/design/knowledge更新・develop→t031取込（t031で197件PASS）・実集計rank1000比較で値列差分0・順位のみ同点安定化を確認 |
| タグ検索v2最新値オプション(#35)✅ | 2026-09-12 | 集計日にv2最新値モード追加（chkUseLiveCounter・既定ON・DBなし実行可）・TagRankAnalyze.LiveCounters保持・TagRankLiveTotalReader/LiveSabunReader新設・ModeFactoryTagRank4分岐（集計日=実行日）・UnitTest8件追加（計173件）・specs/design/knowledge更新・develop→t031取込 |
| タグ検索ランキング(#30)✅ | 2026-09-06 | タグ検索集計タブ（共有係数パネル・件数確認・Enter確定・上限超過時実行不可）・TagConditionParser（A&B\|C*→jsonFilter）・CreateTagSearch・TagRankAnalyze（5万判定・100件×4並列）・TagRankTotalReader（基準なし時）・ModeFactoryTagRank（SP相当・前回CSV任意・基準DB任意）・TAGRANK節（節単位フォールバック）・UnitTest29件追加（計165件）・specs/design/knowledge更新 |
|---|---|---|
| ランキングJSON肥大化対策(#28)✅ | 2026-09-05 | LastResult.JSON列DROP（INSERT除外＋Ver0移行でDROP）・旧SP種別行削除（両テーブル約11万行）・DBVersion導入（2DB・Ver0・逐次適用）・DbMigrationCoordinator新設（集計開始時指示・失敗時中断）・UnitTest12件追加（計136件）・specs/design/knowledge更新 |
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
