# Knowledge: コード構造ナレッジ

作業開始時のコード理解を円滑にするための要点。**詳細は実ファイルを読むこと。** この README は必ず最初に読む。

- `structure.md` — ソリューション構成・プロジェクト間の依存関係・全体フロー
- `nicorankLib.md` — コアライブラリの構造（Factory / Analyze / Input / Option / model / output / api / SnapShot / Util / Common）
- `apps.md` — 各アプリの詳細（nicorank2019 / nicorank_SnapShot / nicorank_oldlog）
- `db.md` — SQLite DB 構成・接続設計・SnapShotDB の大量登録設計
- `testing.md` — テスト構成・実行方法・既知の制約
- `pitfalls.md` — 実装ノウハウ・再発防止（過去の失敗と対策）
- `release.md` — リリース手順・ブランチ運用

**更新ルール**: コードの構造・依存・設計判断が変わったら、その場で更新する。仕様（挙動の定義）は `../specs.md`、設計判断は `../design.md`、タスクは `../tasks.md` を参照。
