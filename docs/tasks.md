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

### nicorank2019の自動更新（#47・壁打ち済み）

> 手作業上書きによる `lib/` 欠け事故（2026/10/03 報告の `batteries_v2` 不在エラー）を受け、文面改善ではなく構造で防ぐ。第一段は通知＋ワンクリック更新（月曜直前の強制更新は事故になるため）。バイナリ正本は GitHub Release（固定名 asset）・検出は NAS の `version.json`（エージェント生成・人間は配置のみ）・分離 updater が一式置換。詳細な設計合意と `version.json` フォーマットは Issue 参照。壁打ち（別セッション）で以下を確定済み：updaterは専用exe（zip同梱・SQLite/Costuraなし）・版数刻印はAssembly書換え（`version.json` の `2026.10.03` に対し Assembly `2026.10.3.0`。`System.Version` 比較）・確認は起動時＋24時間間引き＋同一版1日1回まで・状態ファイルは `%TEMP%/nicorank2019_update_check.json`（削除耐性があるためTEMPで十分。汎用名衝突回避の接頭辞付き）・タスクファイルは `%TEMP%/nicorank2019_update_task.json`・手動確認はメンテナンスタブ・必須化は将来Issueへ分離（47.1は任意のみ）・updater自身の置換は別Issue。

- [x] 47.1 updater の実装（Issueの壁打ち合意に基づく）✅ 2026-10-04 developマージ済み
  - 依存：#36（BaselineDownloaderの取得・照合パターン）・#38（JST日付比較・3値判定）・#45（メンテタブ手動＋自動の2段UI・退避付き復旧）
  - 対象：`nicorankLib/Util/AppUpdateChecker.cs` 新設（`version.json` 取得・`schema<=対応上限` 判定・`System.Version` 比較・24時間間引き・同一版抑制。fetch／時計／TEMPパスは注入可）・`NicoRankXml` に `SYSTEM/URL_APPUPDATE` 任意要素（既定 `https://2daime.myds.me/nicorank/update/version.json`。`URL_BASELINE` と同型）・`Config.AppUpdateManifestUrl`＋既定定数・`frmMain_Load` での起動時非同期確認（集計実行中は適用しない）・メンテタブの手動「更新を確認」ボタン＋状態／結果ラベル（`SetVacuumControlsEnabled` 対象に追加）・更新ダイアログ（今すぐ更新／後で＋`notes` 詳細リンク＋`size` 表示）・updater exe雛形（`nicorankUpdater`。net48コンソール最小依存。タスクファイル→DL→sha256照合→本体終了待機→退避→exe・config・`lib/` 一式置換→再起動。updater自身の置換は対象外）・UnitTest（manifest正常・破損・schema未知・版数比較・間引き・抑制・状態ファイル）・specs.md／design.md反映
  - 検証結果：`dotnet test` 全件PASS（346件）・sln Releaseビルド成功・reviewer再レビュー3回でマージ可（高1件＋中8件対応・低は対応と見送り記録）・ユーザー実機検証OK（file://通しテスト：通知→置換→再起動→backup退避→刻印ビルドで版数遷移まで確認）。#47は47.2が残るためオープン維持。手動確認UIはメンテナンスタブからシステム設定タブへ移設（ユーザー判断。メンテナンスタブの渋滞解消）
- [x] 47.2 `release.md` の更新（zip 名の固定化・版数刻印・`version.json` 生成手順・NAS 配置手順）✅ 2026-10-04 リリース v20261004_nicorank で実施済み
  - reviewer指摘 `[高]1` の対応として次を必須手順に含める：リリース時に `nicorank2019/Properties/AssemblyInfo.cs` の `AssemblyVersion`／`AssemblyFileVersion` をタグ由来の日付版数（`version.json` の `2026.10.03` に対し `2026.10.3.0`）へ書き換えてからビルドする。書き換え忘れは毎日誤通知（または永久に通知なし）の運用事故になる。可能ならビルド時自動生成を検討する。`nicorankUpdater.exe` と `nicorankUpdater.exe.config` はセットで配布する旨をチェックリストへ足す（#26 と同型の旧 config 混入防止）

### 抜け週の再集計による復旧（#46・Issueのみ・実装は別セッション）

> #45のバックアップ手段。配布者の更新忘れなど万が一に備え、再集計での復旧も残す。再集計の遡及可能条件の仕様化と手順案内が中心で、自動再実行の要否は改めて壁打ちする。

- [ ] 46.1 再集計可否の条件仕様化（Issueの壁打ちから着手）

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

## 完了済みタスク

完了したタスクの経緯・検証履歴・実装ノウハウは `docs/tasks/archive.md` に記録する（AGENTS.md §2 のマージ後ゲートで追記）。本ファイルに現行・未完了のタスクだけを置くのは §4 の責務境界による。過去の変更を追う場合は archive.md の見出し（日付＋Issue 番号）から辿る。
