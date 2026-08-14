# リリース手順・ブランチ運用（release.md）

## ブランチ運用

- **main が本流**（2026-06 に整理済み。旧 develop / release/v1.0.0 / snapshotBugFix / openspectest / unitest_refer2026 は削除）
- 作業は **main から feature ブランチを切り、完了後に main へマージ**（--no-ff）
- リリースは main にタグを打つ（下記）
- origin（GitHub: n2daime/nicorank2019）と同期する

```powershell
git checkout main
git pull origin main
git checkout -b feature/<作業名>
# ... 作業 ...
git push origin feature/<作業名>
# PR 作成 → マージ（または直接 main にマージ）
```

## タグ・リリースの慣習

- タグ形式: `vYYYYMMDD_<名前>`（例: `v20260603_nicorank` / `v20260603_snapshot_preview`）
- リリース時は main にタグを打って push

```powershell
git tag -a v20260603_nicorank -m "v20260603_nicorank: <変更内容の概要>"
git push origin v20260603_nicorank
```

## リリース前チェックリスト

リリース前に以下をすべて確認する（AGENTS.md の「リリース前チェックリスト」はこの一覧を参照）。

- [ ] `docs/knowledge/` 配下の全ファイルを最新化（新しいクラス・依存関係・変更点）
- [ ] `docs/specs.md` の仕様を最新化（挙動の定義が変わった場合）
- [ ] `docs/tasks.md` の該当タスクに完了マーク
- [ ] 関連する GitHub Issue をクローズ
- [ ] `dotnet restore` + `dotnet test UnitTest/UnitTest.csproj` が全件 PASS
- [ ] 実機で集計（週刊/中間/SP）が通ることの確認

## リリース手順

1. main で必要な変更をマージ
2. 上記チェックリストを確認
3. タグを打って push
4. 必要に応じて GitHub Release を作成（`gh release create`）

## 週刊ランキングの運用フロー（毎週）

```
土日: 中間集計 → 事前準備（紹介位置の決定など）
月曜: 週刊集計 → 動画作成 → ニコニコに投稿
     → ニコランWeb管理画面で情報を手動アップロード → ニコランWebサイト更新
```

- ニコランWeb（https://nicoranweb.com/）へのデータ提供は手動アップロード（自動連携なし）
- スナップショット取得（nicorank_SnapShot）は任意のタイミングで実行（引数ありでコンソールモード）

## 実行環境

| ツール | 用途 |
|---|---|
| Visual Studio 2026 Community | ビルド・デバッグ |
| dotnet SDK ≥ 8.0 | ビルド・テスト（nicorank_oldlog は net8.0、UnitTest は .NET 10 SDK でビルド） |
| .NET Framework 4.8 | ランタイム（nicorankLib / nicorank2019 / nicorank_SnapShot） |
