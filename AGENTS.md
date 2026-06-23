あなたは日本語で応答してください。すべての出力、コードコメント、設計ドキュメント、タスク説明は日本語を使用すること。ユーザーとのコミュニケーションも日本語で行うこと。
可能な限り自律して動いて。

# ビルド/テスト
dotnet restore
dotnet test UnitTest/UnitTest.csproj

# テストパターン
- テストフレームワーク: MSTest (v3.5.2)
- モック: Moq (v4.20.72)
- DB操作テスト: TestDbHelper を継承してインメモリSQLiteを使用
- ISQLiteCtrl インターフェース経由でSQLite操作を抽象化し、コンストラクタ注入で差し替え可能

# openspec ワークフロー
- 変更管理: openspec/changes/ に各変更をディレクトリ単位で管理
- アーカイブ: openspec/changes/archive/YYYY-MM-DD-<name>/ に移動
- 仕様書: openspec/specs/ に配置

# ブランチ運用
- 現在のブランチ: release/v1.0.0（マージ済み安定版）
- snapshotBugFix ブランチから btreeInitPage error code 11 対策をマージ済み
