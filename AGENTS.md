# AGENTS.md

このリポジトリで作業する際のルール。あなたは日本語で応答すること。すべての出力、コードコメント、設計ドキュメント、タスク説明は日本語を使用する。可能な限り自律して動くこと。

## コンテキスト節約（重要）

- コンテキストウィンドウは貴重。1トークンも無駄にしない。
- 以下は積極的にサブエージェント（`explore`）に委譲する:
  - コードベースの探索・検索（キーワード検索、ファイル構造把握）→ `explore`
  - サブエージェントには「何を調べるか」「何を返すか（最終出力の形式）」を明記する。
- 大きなファイルは全件読みしない。対象範囲を絞ってから Read する。

## ドキュメント方針

- 変更提案は **GitHub Issue**（フローは `docs/proposal.md` の「変更提案のフロー」を参照）、仕様は `docs/specs.md`、設計は `docs/design.md`、タスク管理は `docs/tasks.md` に反映する。
- **コード構造のナレッジは `docs/knowledge/` 配下に Markdown で記録し、作業のたびに最新化する。**
  - 作業開始時は必ず `docs/knowledge/README.md` を読み、コード構造・依存関係を把握してから着手する。
  - コードの構造・依存・設計判断が変わったら、その場でナレッジを更新する（後回しにしない）。
  - ナレッジは「実装コードの速読を不要にするための要点」に絞る。詳細は実ファイルを読む。
  - 仕様（挙動の定義）の変更は `docs/specs.md` に反映する。
- 完了したタスクは必ず `docs/tasks.md` に完了マークを付け、関連する GitHub Issue を Close する。

## ビルド/テスト

```powershell
dotnet restore
dotnet test UnitTest/UnitTest.csproj
```

- テストフレームワーク: MSTest (v3.5.2)。モック: Moq (v4.20.72)。
- DB操作テスト: TestDbHelper を使用しインメモリSQLite（`OpenInMemory()`）で検証。
- ISQLiteCtrl インターフェース経由でSQLite操作を抽象化し、コンストラクタ注入で差し替え可能。
- テストの詳細・既知の制約は `docs/knowledge/testing.md` を参照。

## 実装ノウハウ（再発防止）

過去の失敗と対策は `docs/knowledge/pitfalls.md` に詳細がある。主要な注意点:

- **SQLite 大量登録は巨大トランザクション禁止**。5000件単位のバッチコミット + INSERT OR IGNORE + パラメータ再利用（btreeInitPage error code 11 対策）。
- **ニコニコ API は連続アクセスで 403 になる**。NicoApi は全スレッド停止 + 指数バックオフで再試行する設計。`InternetUtil` の 403 相当は即断念。
- **ニコ動の仕様変更に追従する**。過去の対応はコミット履歴と `docs/proposal.md`（開発背景）に記録されている。仕様変更を疑ったら確認する。
- **UnitTest.csproj は SDK-style を維持**（old-style に戻すと .NET 10 SDK で StackOverflow）。

## ブランチ運用

- `main` が本流。作業は main から feature ブランチを切り、完了後 main にマージ（--no-ff）。
- リリースは main にタグを打つ（`vYYYYMMDD_<名前>` 形式）。詳細は `docs/knowledge/release.md`。

## リリース前チェックリスト

リリース前に確認する項目は `docs/knowledge/release.md` に一元化している。リリース時は必ず参照すること。
