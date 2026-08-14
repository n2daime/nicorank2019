# ニコランク (NicoRank)

ニコニコ動画のランキングを取得・解析し、CSV/HTML・画像などに出力するツール群です。

## プロジェクト構成

| プロジェクト | 説明 |
|---|---|
| `nicorank2019` / `nicorankLib` / `nicorank_oldlog` | 週刊・中間・SPのランキングデータ集計を直接行うツール群 |
| `nicorank_SnapShot` | SP集計のためのデータを定期的に取得するツール |
| `UnitTest` | 単体テストプロジェクト（MSTest、全69件） |

.NET Framework 4.8（テストプロジェクト）と .NET 8（ビルドパッケージ）に対応しています。

---

## 環境・インストール

| 必須 | バージョン | 備考 |
|------|------------|------|
| `dotnet` SDK | ≥ 8.0 | `dotnet --list-sdks` で確認 |
| .NET Framework | 4.8 (Windows 10/11) | .NET Framework 4.8 のランタイムがインストールされていること |
| `git` | 2.x | ソース取得用 |
| `NuGet` | 5.x | 依存パッケージを復元時に使用 |

```bash
# ソース取得
git clone https://github.com/n2daime/nicorank2019.git
cd nicorank2019

# 依存パッケージ復元（NuGet & dotnet）
dotnet restore       # .NET 8 プロジェクト
nuget restore nicorankLib\nicorankLib.csproj  # 4.8 用プロジェクト
```

### 開発環境（実績あり）

| ツール | 用途 |
|--------|------|
| Visual Studio 2026 Community | ビルド・デバッグ |
| OpenCode | docs/ による変更管理・タスク管理・設計書管理 |

---

## 設定ファイル

| ファイル | 使い所 | 内容例 |
|----------|--------|--------|
| `config.json` | 取得・解析の設定（ログレベル、出力ディレクトリなど） | `{ "outputBaseDir": "output", "logLevel": "INFO" }` |
| `cookie.txt` | ニコニコ動画のログイン cookie  | `cookie=xxxxxxxxxxxxxxxxxxxx; userId=123456;` |

> **注意**  
> `config.json` と `cookie.txt` は **リポジトリに含めない** で、プロジェクトルートに手動で配置してください。  
> 例: `T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank2019\cookie.txt`

---

## 実行

### nicorank_SnapShot（SP集計用データ取得）

引数なしで起動すると **WinForms UI** が開きます。**任意の引数**（値は問いません）を1つ以上指定すると **コンソールモード** でスナップショットデータを自動取得します。

```
:: WinForms UI 起動
nicorank_SnapShot

:: UIをせず、自動取得。コンソールモード（引数は何でもよい）。自動でスリープしないので注意。
nicorank_SnapShot.csproj /get
nicorank_SnapShot.csproj console
nicorank_SnapShot.csproj 1
```

### nicorank_oldlog（公式ランキングデータを回収する）

1. Windows
```
nicorank_oldlog <options>
```

2. Linux
```
dotnet run nicorank_oldlog.dll <options>
```

#### コマンドラインオプション

| 形式 | 意味 | 例 |
|------|------|----|
| `/checklogin` | ログイン状態チェック | `... /checklogin` |
| `daily`, `weekly`, `monthly`, `total` | 取得するランキング種別 | `... daily` |

---

## 出力構造

`nicorank2019` の `frmMain.AnalyzeAsync()` が集計を実行し、`OutputBase` 派生クラス群が `Output/` ディレクトリ（カレントディレクトリ直下、固定）に以下のファイルを生成します。

```
Output/
├─ rank*.txt                 # ニコニコランキングメーカーで画像を作成するためのデータ
├─ result*.csv               # Excel などでデータ解析するためのデータ
├─ result_DB登録用*.csv       # ニコランWeb登録用データ（JSON形式が現在の実績）
├─ result_DB登録用*.json
├─ queue.irv                 # 動画アイコンダウンロード用キューファイル（Irvine 等を想定）
├─ queue_UserIcon.irv        # ユーザーアイコンダウンロード用キューファイル
├─ 長期動画リスト.txt          # 長期動画判定結果
└─ DB/
   ├─ NicoranHistory.db      # 集計履歴データベース
   ├─ ApiXML.db              # API取得XMLキャッシュ
   ├─ Dailylog.db            # 中間集計時のキャッシュデータ（デイリー単位での集計結果）
   ├─ LogNicoChart.db        # ニコニコチャートログ
   └─ LogOfficial.db         # 公式ランキングログ
```

---

## テスト

```bash
dotnet restore
dotnet test UnitTest/UnitTest.csproj
```

テストは **MSTest** を採用しています（v3.5.2）。全 **69 件** のテストが以下のカテゴリに分類されています。

| カテゴリ | ファイル | 件数 | テスト内容 |
|----------|----------|------|------------|
| ISQLiteCtrl | `UnitTestSQLiteCtrl.cs` | 16 | Open/Close/Dispose、インメモリ接続、OpenInMemory+SQL実行、二重呼び出しの安全性 |
| DbSchema | `UnitTestDbSchema.cs` | 8 | CREATE TABLE / PRAGMA / ALTER TABLE / sqlite_master / ATTACH-DETACH |
| DbQuery | `UnitTestDbQuery.cs` | 8 | PRIMARY KEY検索 / BETWEEN / MAX / COUNT / IFNULL / JOIN / ORDER BY DESC |
| DbWrite | `UnitTestDbWrite.cs` | 6 | INSERT / DELETE / トランザクション Commit/Rollback / AddWithValue |
| DbError | `UnitTestDbError.cs` | 4 | ファイル不在 / 未接続 / 二重Close/Disposeの安全性 |
| TestDbHelper | `TestDbHelper.cs` + `UnitTestTestDbHelper.cs` | 8 | 全7種類のテーブル作成＋インメモリDB生成 |
| その他 | `UnitTestTextUtil.cs`, `UnitTestStatusLog.cs`, `UnitTestConfig.cs`, `UnitTestRanking.cs`, `UnitTestOutput.cs` | 19 | 設定ファイル / CSV入出力 / ポイント計算 / ステータスログ / テキストユーティリティ |

> **モック** には Moq (v4.20.72) を利用。DB操作は `ISQLiteCtrl` インターフェース経由で抽象化し、コンストラクタ注入によりテスト用の実装（インメモリSQLite）と差し替え可能です。

---

## 既知の問題・注意点

| 項目 | 内容 |
|------|------|
| `cookie.txt` の更新 | ブラウザクッキーを毎月変更する場合、手動更新が必要 |
| `config.json` の書式 | JSON の構文エラーにより実行失敗するので必ず `JSON Lint` 等で確認 |
| `RankApi2Json.SaveOldRankingData` の呼び出し | コメンタードアウトされているため、保存機能は手動で `Program.cs` 内のコメントを外す必要があります |
| `nuget restore` の失敗 | `packages` ディレクトリが古い場合、`nuget restore -verbosity detailed` で原因を特定してください |

---

## ドキュメント

開発・保守用のドキュメントは `docs/` にまとまっています。

| ドキュメント | 内容 |
|---|---|
| `docs/proposal.md` | 開発背景（プロジェクトの成り立ち・運用フロー・変更提案のフロー） |
| `docs/specs.md` | 仕様（ポイント計算・出力形式・DB テーブル・API 仕様） |
| `docs/design.md` | 設計判断の記録 |
| `docs/tasks.md` | タスク管理（未完了・完了履歴） |
| `docs/knowledge/` | コード構造ナレッジ（作業開始時は `knowledge/README.md` から読むこと） |

---

## 貢献方法

1. まず Fork → クローン  
2. ブランチ作成 → コミット  
3. PR を送信してください。  
4. コミットメッセージは次の形式を推奨します  
   ```
   <type>: <短い説明>
   （example）feat: add support for monthly ranking
   ```
5. テストを追加 / 修正したら必ず `dotnet test UnitTest/UnitTest.csproj` で通ることを確認してください。

---

## ライセンス

MIT License © 2026 n2daime

詳しくは `LICENSE` ファイルを参照ください。

---

> **サポート**  
> バグ報告・機能要望は Issues へお願いします。  
> 既存の実装だけでなく、`config.json` のサンプルも用意しています (`config.sample.json`)。

```