# ニコランク (NicoRank)

ニコニコ動画のランキングを取得・解析し、CSV/HTML・画像などに出力する CLI ツールです。  
.NET Framework 4.8（テストプロジェクト）と .NET 8（ビルドパッケージ）に対応しています。

---

## 📦 環境・インストール

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

---

## 📋 設定ファイル

| ファイル | 使い所 | 内容例 |
|----------|--------|--------|
| `config.json` | 取得・解析の設定（ログレベル、出力ディレクトリなど） | ```json
{<br>  "outputBaseDir" : "output",<br>  "logLevel" : "INFO"<br>}
``` |
| `cookie.txt` | ニコニコ動画のログイン cookie  | `cookie=xxxxxxxxxxxxxxxxxxxx; userId=123456;` |

> **注意**  
> `config.json` と `cookie.txt` は **リポジトリに含めない** で、プロジェクトルートに手動で配置してください。  
> 例: `T:\ニコニコ動画集計\ニコニコ動画集計プログラム\nicorank2019\cookie.txt`

---

## 🚀 実行

```
# 1. ディレクトリ構造を確認
cd nicorank_oldlog

# 2. データ取得 & 解析
dotnet run --project nicorank_oldlog.csproj <arg1> <arg2> ...

# 3. テスト実行 (可視化が必要な場合は `nunit` も使用可)
dotnet test ../UnitTest/UnitTest.csproj
```

### コマンドラインオプション

| 形式 | 意味 | 例 |
|------|------|----|
| `/checklogin` | ログイン状態チェック | `dotnet run --project nicorank_oldlog.csproj /checklogin` |
| `daily`, `weekly`, `monthly`, `total` | 取得するランキング種別 | `... daily` |
| `--output <dir>` | 出力先を指定 | `... --output ./output` |

> **`--output` はサポートされていません。**  
> 既存のコードでは `RankApi2Json.SaveOldRankingData` を呼び出すことで `outputBaseDir`（`config.json`）に保存します。

---

## 📭 出力構造

```
output/
├─ daily/
│  ├─ 2025-04-10/
│  │   ├─ 2025-04-10_05.html
│  │   ├─ 2025-04-10_05.csv
│  │   ├─ 画像/…  (アイコン等)
│  ├─ ...
└─ total/
   ├─ 2025-04-10/
   └─ ...
```

- `*_5.html` : HTML レポート  
- `*_5.csv` : CSV データ  
- `画像/` : 動画・ユーザーアイコン  

> **ファイル名の規則**  
> - `<YYYYMMDD>_<HH>`（例: `20250410_05`）  
> - `<ジャンル>` / `<順位>` などの情報は内部で解析され、`RankGenreJson` へマッピングされます。

---

## 🧪 テスト

```bash
# 1. テストプロジェクト用の NuGet パッケージを復元
nuget restore UnitTest\UnitTest.csproj

# 2. テスト実行
dotnet test UnitTest\UnitTest.csproj
```

> **テストケース**  
> - `TestMethodAAAA` は実際に API へ接続し、解析・出力フローを自動で実行します。  
> - `TestFixFileJson` は旧フォーマットの `file_name_list.json` を新フォーマットへ変換するユーティリティを検証します。

---

## 🚧 既知の問題・注意点

| 項目 | 内容 |
|------|------|
| `cookie.txt` の更新 | ブラウザクッキーを毎月変更する場合、手動更新が必要 |
| `config.json` の書式 | JSON の構文エラーにより実行失敗するので必ず `JSON Lint` 等で確認 |
| `RankApi2Json.SaveOldRankingData` の呼び出し | コメンタードアウトされているため、保存機能は手動で `Program.cs` 内のコメントを外す必要があります |
| `nuget restore` の失敗 | `packages` ディレクトリが古い場合、`nuget restore -verbosity detailed` で原因を特定してください |

---

## 🤝 貢献方法

1. まず Fork → クローン  
2. ブランチ作成 → コミット  
3. PR を送信してください。  
4. コミットメッセージは次の形式を推奨します  
   ```
   <type>: <短い説明>
   （example）feat: add support for monthly ranking
   ```
5. `nunit` テストを追加 / 修正したら必ず CI で通ることを確認してください。

---

## 📄 ライセンス

MIT License © 2026 n2daime

詳しくは `LICENSE` ファイルを参照ください。

---

> **サポート**  
> バグ報告・機能要望は Issues へお願いします。  
> 既存の実装だけでなく、`config.json` のサンプルも用意しています (`config.sample.json`)。

```