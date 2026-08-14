# コード構造（structure.md）

## ソリューション構成

```
nicorank2019.sln
├── nicorankLib/          .NET Framework 4.8 クラスライブラリ（全プロジェクトの共通コア）
├── nicorank2019/         .NET Framework 4.8 WinForms アプリ（集計メイン UI）
├── nicorank_SnapShot/    .NET Framework 4.8 WinForms アプリ（スナップショット取得ツール）
├── nicorank_oldlog/      .NET 8 コンソールアプリ（公式過去ランキング回収ツール、SDK-style）
├── UnitTest/             .NET Framework 4.8 MSTest テストプロジェクト（SDK-style、69件）
├── 依存ファイル/           nicorank.xml・SQLite.Interop.dll・DB/*.db（ソリューションフォルダ）
├── docs/                 ドキュメント（proposal / specs / design / tasks / knowledge）
└── packages/             NuGet パッケージ（packages.config 用）
```

## プロジェクト間の依存

```
nicorank2019 ──→ nicorankLib
nicorank_SnapShot ──→ nicorankLib
nicorank_oldlog ──→ nicorankLib（net48 ライブラリを net8.0 から参照するハイブリッド）
UnitTest ──→ nicorankLib
```

## 全体フロー

```
nicorank_SnapShot ──(引数あり)→ SnapController（コンソールモード）┐
                 └──(引数なし)→ Form1（UI）                     ├→ nicorankLib コア
nicorank2019 ──→ frmMain → ModeFactory(Weekly/Tyukan/SP) ──────┘
nicorank_oldlog ──(net8.0 別系統)──→ NicoRankiApi → old-ranking/ へ JSON 保存
UnitTest ──→ nicorankLib を net48 で直接テスト（インメモリ SQLite）
```

## 集計フロー（週刊集計の例）

1. `frmMainSyukei.SelectMode()` がモードを選択し `GetModeFactory()` で `ModeFactory*` を生成
2. `RankingHistory.Open()` → `UpdateOfficialRankingDB()`（公式ランキング DB 更新）
3. `CreateAnalyzer()` で `RankingAnalyze` を構築（入力・オプションはモードごとに設定）
4. `AnalyzeRank()` → `RankingAnalyze.AnalyzeRank()` が集計
   - Input（データ取得）→ BasicOption（順位計算前処理）→ calcRanking（6種の順位を並列計算）→ ExtOption（順位計算後処理）
5. 各 `Create*()` で生成した `OutputBase` 派生の `Execute(rankingList)` を順次実行
6. `RankingHistory.Close()` で DB を閉じる

## モードとファクトリの対応

| モード | ファクトリ | 入力 | 主要オプション | 備考 |
|---|---|---|---|---|
| Weekly | `ModeFactoryWeekly` | `JsonReaderWeekly` | SabunReader / LastRankReader / GenreInfoReader / FavoriteTagReader / UserInfoReader / TyokiHantei | メンテ日は中間集計で代替 |
| Tyukan | `ModeFactoryTyukan` | `TyukanAnalyze` | LastRankReader / FavoriteTagReader | TyokiHantei なし。履歴DB登録なし |
| SP | `ModeFactroySP`（Weekly 継承） | `SPAnalyze`（IDリスト） | SnapShotSabunReader / LastRankCsvReader / FavoriteTagReader | スナップショットDB差分方式 |

## 主要クラスと責務（集計フロー順）

1. **UI / 実行**: `frmMain` / `frmMainSyukei`（WinForms）— モード選択 → ファクトリ取得 → 非同期解析
2. **ファクトリ**: `ModeFactoryBase`（基底）/ `ModeFactoryWeekly` / `ModeFactoryTyukan` / `ModeFactroySP` — モードに合わせた入力セットアップと出力生成
3. **集計**: `RankingAnalyze` — パイプライン制御（Input → BasicOption → calcRanking → ExtOption）
4. **入力**: `JsonReader*`（公式ランキング JSON）/ `SPAnalyze`（IDリスト）/ `TyukanAnalyze`（中間）/ `GenreAnalyze`（ジャンル特化）
5. **過去データ管理**: `RankingHistory` — LogOfficial.db の更新・参照、メンテ日判定、NicoChart 連携
6. **出力**: `OutputBase` 派生（`NrmOutput` / `ResultCsv` / `ResultCsvRankDB` / `ResultJsonRankDB` / `ResultImageget*` / `ResultHistory`）
7. **ユーティリティ**: `Config`（設定シングルトン）/ `StatusLog` / `ErrLog` / `SQLiteCtrl` / `ISQLiteCtrl` / `TextUtil` 等
8. **ドメインモデル**: `Ranking`（集計結果1件）/ `RankGenreJson` / `RankLogJson` 等

## 注意点 / よくある変更箇所

- モード追加や入力仕様変更は `ModeFactory*` に影響する
- 出力形式追加は `OutputBase` 派生クラスを追加し `ModeFactory*` の出力列挙部に登録
- 設定の変更は `Config` を通して一元管理
- SQLite 操作のテスト・差し替えは `ISQLiteCtrl` 経由。実装追加時は `SQLiteCtrl` に変更を加え、単体テストでは `TestDbHelper` でインメモリ DB を使用

## 参照ファイル（スタートポイント）

- UI / 実行: `nicorank2019/frm/frmMain.cs`, `frmMainSyukei.cs`
- ファクトリ: `nicorankLib/Factory/`
- 集計: `nicorankLib/Analyze/RankingAnalyze.cs`
- 過去の集計結果管理: `nicorankLib/Analyze/Official/RankingHistory.cs`
- 出力基底: `nicorankLib/output/OutputBase.cs`
- DB 抽象化: `nicorankLib/Util/ISQLiteCtrl.cs`, `SQLiteCtrl.cs`
- スナップショット DB: `nicorankLib/SnapShot/SnapShotDB.cs`
- ユーティリティ: `nicorankLib/Util`, `nicorankLib/Common`
- 単体テスト: `UnitTest/`（詳細は `testing.md`）
