# nicorankLib 構造（nicorankLib.md）

`nicorankLib/` — .NET Framework 4.8 の集計コアライブラリ。全プロジェクトの共通基盤。
詳細仕様（計算式・出力形式）は `../specs.md` を参照。

## ディレクトリ構成

```
nicorankLib/
├── Factory/       モード分岐（ModeFactoryBase / Weekly / Tyukan / SP）
├── Analyze/       集計パイプライン・入力・過去データ管理
│   ├── RankingAnalyze.cs   パイプライン制御
│   ├── Input/              入力（JsonReader系 / SPAnalyze / TyukanAnalyze / GenreAnalyze）
│   ├── Official/           公式ランキング DB 管理（RankingHistory）
│   ├── Option/             オプション処理（Basic: 順位計算前 / Ext: 順位計算後）
│   └── model/              ドメインモデル（Ranking / DB / EAnalyzeMode / 各種 JSON モデル）
├── output/        出力（OutputBase 派生 8 クラス）
├── api/           NicoApi（動画情報取得・キャッシュ）
├── SnapShot/      スナップショット取得・DB 登録
├── Common/        Config（設定シングルトン）/ NicoRankXml
├── Util/          ユーティリティ（SQLiteCtrl / StatusLog / ErrLog / TextUtil 等）
└── Settings.cs    空（実質未使用。設定は Config が nicorank.xml から読む）
```

## Factory（モード分岐）

| クラス | 責務 |
|---|---|
| `ModeFactoryBase` | 抽象基底。`AnalyzeRank()` は `RankingAnalyze.AnalyzeRank` を呼び結果を `RankingList` に格納。出力系は抽象メソッド（`CreateAnalyzer` / `CreateHistory` / `CreateOutputCSV` / `CreateNRMRank` 等） |
| `ModeFactoryWeekly` | 週間集計。メンテ日は `RankingHistory.CheckMaintananceDay` で中間集計に代替。BasicOption: HiddenMovieDelete → SabunReader → LastRankReader → GenreInfoReader。ExtOption: FavoriteTagReader → UserInfoReader → TyokiHantei |
| `ModeFactoryTyukan` | 中間集計。TyokiHantei = null、履歴DB登録なし。`CreateNRMRank1000` は固定 1000 位 |
| `ModeFactroySP` | **ファイル名タイポは元コードのまま**。`ModeFactoryWeekly` 継承。4種の入力ファイルを `SetInputFile` で設定（analyzeDB / baseDB / movieList / 前回結果CSV） |

## Analyze / RankingAnalyze

`RankingAnalyze.cs` — 集計パイプライン制御。

```
AnalyzeRank():
  Input.AnalyzeRank(out rankingList)   … データ取得
  BasicOptionBase.AnalyzeRank(ref list) … 順位計算前の付加処理
  calcRanking(list)                    … 順位計算（6種を Task.Run で並列）
  RankTotal 順にソート
  IExtOptionBase.AnalyzeRank(list)     … 順位計算後の付加処理
```

`calcRanking` の6種の順位（すべて並列）: 総合（PointTotal 降順）/ 再生 / コメント / マイリスト / いいね / カテゴリ（`!isDelete` のみ、Category グループごとに PointTotal 降順）。

## Analyze/Input

| クラス | 責務 |
|---|---|
| `InputBase` | 抽象基底。`AnalyzeDay` / `AnalyzeRank(out List<Ranking>)` を規定 |
| `JsonReaderBase` | 公式過去ランキング JSON 取得の基底。`file_name_list.json` → ジャンル別 JSON を `Config.ThreadMax` 並列でダウンロード → `Ranking` に変換 → `MergeRankingList` で重複 ID マージ。`CheckAnalyzeTime`（当日 1:00 前は集計不可判定） |
| `JsonReaderDaily` / `Weekly` / `Monthly` / `Total` | 種別ごと。Weekly は直近の月曜まで遡る、Monthly は直近の1日まで |
| `GenreAnalyze` | 「演奏してみた」ジャンル特化入力。LogOfficial.db の Movie と Ranking を JOIN |
| `SPAnalyze` | SP 用。動画 ID リスト（改行区切り）を読み込み Ranking リスト化 |
| `TyukanAnalyze` | 中間集計。`Dailylog.db` を使用。対象日リスト（メンテ日除外）を日別に `JsonReaderDaily` + `SabunReader` で集計し Dailylog に INSERT → 期間合計で中間ランキング生成 |

## Analyze/Official

`RankingHistory.cs` — LogOfficial.db（公式過去ランキング DB）の更新・参照。`IDisposable`。

- `Open()` / `Close()`: LogOfficial を開閉し `LogNicoChart.db` を `attach 'NicoChart'` で併用
- `UpdateOfficialRankingDB()`: RankingDate の `Max(集計日)+1`（初期値 20190610）から今日までを日別に取得・登録。0 件の日はメンテナンス日として登録（UI で確認）
- `CheckMaintananceDay(DateTime)`: RankingDate でメンテ日判定
- `CheckSoMovieNeedSabun(id, baseTime)`: 公式チャンネル動画（so）の新着判定。過去ランキング既出なら差分、なければ NicoChart 確認
- `GetRankingDataLogNicoChart(id, baseTime)`: `http://www.nicochart.jp/point/{id}.tsv` を取得し `NicoChart.Ranking` に登録（2019年未満は1件に間引き、空データは直前値引き継ぎ）
- `ISQLiteCtrl` コンストラクタ注入可

## Analyze/Option

### Basic（順位計算前に実行、`AnalyzeRank(ref List<Ranking>)`）

| クラス | 責務 |
|---|---|
| `HiddenMovieDelete` | サムネイル `/video_deleted` の動画を `isDelete=true` に |
| `SabunReader` | 基準日との差分計算。so 動画は新着判定。差分取れず投稿が過去なら isDelete |
| `LastRankReader` | NicoranHistory.db の Lastresult から前回総合ランク/ポイントをセット |
| `LastRankCsvReader` | SP 用。前回 result CSV から前回順位を付与 |
| `GenreInfoReader` | カテゴリ不明の動画を NicoApi で補完 |
| `MovieInfoReader` | NicoApi で動画情報（タイトル・投稿日）を取得 |
| `SnapShotSabunReader` | SP 集計の中核。スナップショット DB 2本（AnalyzeDB/BaseDB）の累積値差分を計算。`IDisposable` |
| `NocoChartReader` | now.nicochart.jp の today フィード 3 URL（mylist/res/view）から当日ランキング取得 |

### Ext（順位計算後に実行、`bool AnalyzeRank(List<Ranking>)`）

| クラス | 責務 |
|---|---|
| `TyokiHantei` | **長期動画判定**。NicoranHistory.db の History で過去ランクイン回数を数え、今回含め3回目以降を長期リストに。`Output/長期動画リスト.txt` 出力。`ModeFactoryBase.GetRank()` が件数を紹介枠に加算（門番拡張） |
| `FavoriteTagReader` | LogOfficial.db の Ranking.人気のタグ から最新データを取得し FavoriteTags に追加（UserEnd 以内 or カテゴリ1位） |
| `UserInfoReader` | NicoApi でユーザー名/アイコン情報を取得（未取得のみ） |

## Analyze/model

- `Ranking` — 集計結果1件。`CalcPoint()`（ポイント計算、キャッシュ付き）・`PointCalcReset()`・`MergeRankingList`・`IsChannel`（`so` 始まり）。計算式の詳細は `../specs.md` セクション2
- `EAnalyzeMode` — Weekly / SP / Tyukan / Daily / Mothly（タイポ）/ Unknown
- `DB` — DB ファイルパス定数（`LOG_OFFICEIAL` / `LOG_NICOCHART` / `NiCORAN_HISTORY` / `LOG_SNAPSHOT`）
- `NicoChartModel` / `RankGenreJson` / `RankLogJson` — デシリアライズ用モデル

## output

`OutputBase`（抽象基底、`Execute(IReadOnlyList<Ranking>)`）の派生:

| クラス | 生成物 |
|---|---|
| `NrmOutput` | `rank.txt` / `rank{UserNum}.txt` / `rankED.txt`（TSV） |
| `ResultCsv` | `result(UTF8).csv` / `result(SJIS).csv` |
| `ResultCsvRankDB` | `result_DB登録用(UTF8).csv` / `result_DB登録用(SJIS).csv`（27列・旧フォーマット互換） |
| `ResultJsonRankDB` | `result_DB登録用(UTF8).json` |
| `ResultImagegetBase`（abstract） | 画像 DL キュー出力基底（.irv 形式） |
| `ResultImagegetMovieIcon` | `queue.irv`（動画サムネイル、ED枠まで） |
| `ResultImagegetUserIcon` | `queue_UserIcon.irv`（ユーザーアイコン、全件） |
| `ResultHistory` | NicoranHistory.db へ登録（History / LastResult / LastResultInfo） |

出力先は `Output/`（`ModeFactoryBase.OUTPUTDIR`、カレントディレクトリ相対・固定）。実行順は `ModeFactoryBase.AnalyzeRank()` 内で生成した各 OutputBase の `Execute()` を順次呼ぶ。

## api

- `NicoApi` — getthumbinfo API 取得 + `DB/ApiXML.db`（NicovideoThumb）キャッシュ。`Parallel.ForEach`（`Config.ThreadMax`）。失敗時は全スレッド一時停止 + 指数バックオフ（pitfalls 参照）
- `model/ThumbinfoBase` / `model/VideoResponse` — レスポンスデシリアライズ用 POCO。`GetUserID/GetUserName/GetUserIconUrl` はユーザー動画なら `user_*`、チャンネルなら `ch_*` を返す

## SnapShot

- `SnapController` — スナップショット一括取得エントリ。20070306 から現在まで 15 日間隔でループ。直近1年以内は 1000 再生制限なし URL、それ以前は制限あり URL。10000 件ごとに `SnapShotDB.RegistDB`
- `SnapShotAnalyze` — snapshot API リクエスト構築・並列ページング（4 並列）。総件数 5 万超なら期間を狭めて再試行。`":null"` → `":0"` 置換
- `SnapShotDB` — `LogSnapshot{yyyyMMdd}.db` の作成・登録（5000件バッチコミット・INSERT OR IGNORE・パラメータ再利用）。`ISQLiteCtrl` 注入可。旧 JSON ファイル読込（`GetJsonData`）も保持
- `SnapShotJson` — レスポンス POCO

## Common

- `Config` — **シングルトン**。`nicorank.xml` を `NicoRankXml` にデシリアライズして保持。ほぼ全クラスから参照。`IsSP` フラグで RANK/RANKED/UserInfo/POINT が SP 用 XML 節に切り替わる
- `NicoRankXml` — nicorank.xml の POCO 群

## Util

| クラス | 責務 |
|---|---|
| `SQLiteCtrl : ISQLiteCtrl, IDisposable` | SQLite 接続管理（`Microsoft.Data.Sqlite 10.0.11` + `SQLitePCLRaw 2.1.12`）。`Open()`（`Data Source="<path>";Pooling=False;Default Timeout=30` 文字列構築 + PRAGMA 4種・失敗時継続。`lib` 集約で `probing`）/ `OpenInMemory()`（`Data Source=:memory:`）/ `Close()` / `Dispose()`。`Connection` は `SqliteConnection` 型 |
| `ISQLiteCtrl` | SQLite 操作の抽象化（`SqliteConnection` 公開。テストでインメモリ実装に差し替え） |
| `StatusLog` | 静的。`IStatusLogWriter` を注入するプラグイン方式（UI 側が実装を注入。未設定なら何も出さない） |
| `ErrLog` | シングルトン。`nicorankerr.log` に追記（UTF8）。`Close()` で非 SilentMode ならキー入力待ち |
| `DateConvert` | 日付 ↔ 文字列（yyyyMMdd / yyyyMMddHHmmss）変換 |
| `InternetUtil` | HTTP ダウンロード（UA "WeeklyNicoranProgram"）。最大20回リトライ。403 相当（application/xml の ProtocolError）は即断念。それ以外は指数バックオフ |
| `RegLib` | 正規表現置換ラッパー |
| `UIConfig` | シングルトン。SilentMode（既定 true）/ LocalXml。`GetWch` は SilentMode なら既定値 |
| `Text/CsvUtil` | CSV/TSV 書き込み・読み込み（TextFieldParser） |
| `Text/TextUtil` | テキスト読み書き。文字コード自動判別（JIS/EUC/SJIS/UTF8/Unicode/ASCII）。`ReadCsv` は Ranking リスト/辞書に変換（ファイル不在時は false + 空 List） |
| `Text/XmlSerializerUtil` | XmlSerializer ラッパー |

## 依存関係の要点（new の一覧）

| クラス | new する | new される場所 |
|---|---|---|
| `RankingAnalyze` | InputBase, BasicOption 群, ExtOption 群 | ModeFactory 3種、`TyukanAnalyze.calcDailyRank` |
| `RankingHistory` | SQLiteCtrl, JsonReader 4種 | ModeFactoryWeekly, TyukanAnalyze, SabunReader, frmMainSyukei |
| `SabunReader` | RankingHistory | ModeFactoryWeekly, TyukanAnalyze |
| `TyokiHantei` | SQLiteCtrl | ModeFactoryWeekly |
| `SnapShotSabunReader` | SQLiteCtrl×2, MovieInfoReader | ModeFactroySP |
| `NicoApi` | — | GenreInfoReader / MovieInfoReader / UserInfoReader |
| `ModeFactory*` | — | `frmMainSyukei.cs`（Weekly:86 / Tyukan:93 / SP:99 で切替） |

テスト容易性のための `_dbCtrlOverride` パターン（`ISQLiteCtrl` コンストラクタ注入）は `RankingHistory` / `TyukanAnalyze` / `SnapShotSabunReader` / `LastRankReader` / `TyokiHantei` / `FavoriteTagReader` / `GenreAnalyze` に実装済み。
