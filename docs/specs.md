# 仕様（Specs）

このドキュメントは、プロジェクトの仕様（挙動の定義）を記録する。
**ドキュメント駆動開発のベース**であり、変更時は必ずここを最新化する。

- コード構造・依存関係の速読用情報は `docs/knowledge/` を参照（このファイルとは役割が異なる）。
- 変更提案は `docs/proposal.md`、設計判断は `docs/design.md`、タスクは `docs/tasks.md`。

---

## 1. 集計モード

| モード | 入力 | 特徴 |
|---|---|---|
| 週刊（Weekly） | 公式週間ランキング JSON | 週次集計の本体。先週差分・長期判定あり |
| 中間（Tyukan） | 日次ランキング JSON + Dailylog | 土日に行う仮集計。メンテ日を除外して日別集計→期間合計 |
| SP（SP） | 動画IDリスト + スナップショットDB差分 | 半期/年間 SP 動画用 |

### 週刊集計の入力分岐

- 集計日がメンテナンス日の場合、中間集計（`TyukanAnalyze`）で代替する（`RankingHistory.CheckMaintananceDay`）。
- 通常時: `JsonReaderWeekly` → 差分（`SabunReader`、基準日は設定で変更可・月曜日のみ）→ 先週順位（`LastRankReader`）→ ジャンル補完（`GenreInfoReader`）。
- 順位計算後: お気に入りタグ（`FavoriteTagReader`）→ ユーザー情報（`UserInfoReader`）→ 長期判定（`TyokiHantei`）。

### 順位計算の種類

- 総合順位（PointTotal 降順）/ 再生順位 / コメント順位 / マイリスト順位 / いいね順位 / カテゴリ順位
- 6 種類の計算は `Task.Run` で並列実行（`RankingAnalyze.calcRanking`）。

### 紹介枠（GetRank）

- 紹介順位 = `Config.Rank`（既定 20 位）+ 長期動画（門番）の数だけ拡張
- ED枠: 紹介枠の次から `RankED` までをサムネイル形式で紹介（情報提供は `rankED.txt`）

### モードごとの出力（frmMainSyukei.AnalyzeAsync が 12 種を順次 Execute）

| 出力 | 週刊（Weekly） | 中間（Tyukan） | SP（SP） |
|---|---|---|---|
| `CreateHistory()`（NicoranHistory.db 登録） | ✅ ResultHistory | —（null） | —（null） |
| `TyokiHantei`（長期動画判定・リスト出力） | ✅ | —（null） | —（null） |
| `CreateNRMRank()`（`rank.txt`） | ✅ 0〜GetRank() | ✅ | ✅ |
| `CreateNRMRank1000()`（`rank{UserNum}.txt` / `rank1000.txt`） | ✅ rank{UserNum}.txt | ✅ **rank1000.txt（固定0〜1000）** | ✅ rank{UserNum}.txt |
| `CreateNRMRankED()`（`rankED.txt`） | ✅ | ✅ | ✅ |
| `CreateOutputCSV()`（result CSV 2種） | ✅ | ✅ | ✅ |
| `CreateOutputCSV_rankDB()`（DB登録用 CSV 2種） | ✅ | —（null） | ✅ |
| `CreateOutputHTML()` | **未実装（null）** | 未実装（null） | 未実装（null） |
| `CreateOutputMovieIconGet()`（`queue.irv`） | ✅ | —（null） | ✅ |
| `CreateOutputUserIconGet()`（`queue_UserIcon.irv`） | ✅ | —（null） | ✅ |
| `CreateOutputWORK()` | **未実装（null）** | 未実装（null） | 未実装（null） |
| `CreateOutputJson_rankDB()`（DB登録用 JSON） | ✅ | —（null） | ✅ |

- 実効出力は **週刊 10 種 / 中間 4 種（NRM×3 + CSV）/ SP 9 種**（HTML・WORK は未実装のまま）
- SP は `ModeFactoryWeekly` 継承。履歴登録・長期判定なし（`CreateHistory` は override で null）
- 中間の `CreateNRMRank1000` は `rank1000.txt` に固定 1000 位（週刊/SP は `rank{UserNum}.txt`）

---

## 2. ポイント計算仕様（Ranking.CalcPoint）

集計順位（総合順位）は以下の合計で計算される。削除動画（isDelete）は 0。

| 項目 | 計算 | 補正モード |
|---|---|---|
| マイリスト | マイリスト数 × CalcMyList | `CalcMyListKind`: 1=弱 / 2=強（上限 = 再生数÷倍率、比で補正） |
| 再生 | 再生数 × CalcPlay × HoseiPlay | `CalcPlayKind`: 1=マイリスト率0.1%未満は率×1000（下限0.01倍）/ 2=いいね対応（(コメ+マイリス+いいね)÷再生×1000、1.0超は1.0、下限0.01） |
| コメント | コメント数 × CalcComment | `CalcCommentKind`: 0=補正なし / 1=補正あり / 2=等価コメント（再生とコメントの小さい方を等価とし、CalcCommentUnderLimit 下限・四捨五入）/ 3=√コメント×100×倍率 |
| いいね | いいね数 × CalcLike | — |

### 全体補正（HOSEI_POINT_ALL）

- `POINTALL_OFFSET` でモード切替。VCOLE2023:
  - `D = 2.5 × (コメント数 / 再生数) × 100` を **0.25 ≦ D ≦ 1.0 にクランプ**（有効桁下2桁切り捨て）し、全体ポイントに乗算
  - 再生数 0 なら D = 1.0
  - **位置づけ: ボカコレ2023 対策の一時的な補正として追加された。現在はオフ（Mode=0）で、必要な時期のみ設定で有効化する**
- 補正結果はキャッシュされる（`PointCalcReset()` で破棄。差分計算後に呼ばれる前提）。

### 現在の設定値（nicorank.xml デフォルト）

| 設定 | 週間 | SP | 意味 |
|---|---|---|---|
| `COMMENT_OFFSET Mode` | 2（等価） | — | コメント補正モード（UnderLimit=0.01） |
| `MYLIST_OFFSET Mode` | 1（弱） | — | マイリスト補正モード |
| `PLAY_OFFSET Mode` | 2（いいね対応） | — | 再生補正モード（判定閾値は 0.1% = 0.001） |
| `POINTALL_OFFSET Mode` | 0（オフ） | — | 全体補正モード（1=VCOLE2023） |
| `CALC_MYLIST` | 40 | 20 | マイリスト倍率 |
| `CALC_PLAY` | 1 | 1 | 再生倍率 |
| `CALC_COMMENT` | 1 | 1 | コメント倍率 |
| `CALC_LIKE` | 10 | 20 | いいね倍率 |
| `RANK Num` | 20 | 100 | 紹介枠数（Tyouki=1 で門番拡張あり） |
| `RANKED Num` | 200 | 400 | ED枠数 |

### nicorank.xml の全設定項目

`nicorank.xml` は `Config`（シングルトン）が読み込む唯一の設定ファイル。カレントディレクトリに配置（ビルド時は「依存ファイル/」からコピー）。SP モード時は `SP` 節の値に切り替わる。

| 要素 | 属性 | 現在値 | 意味 |
|---|---|---|---|
| `RANK` | `Num` / `Tyouki` | 週間 20/1、SP 100/0 | 紹介枠数。Tyouki=1 なら門番拡張あり |
| `RANKED` | `Num` | 週間 200、SP 400 | ED枠の紹介数 |
| `UserInfo` | `Num` | 1000 | ユーザー情報/アイコン取得数 |
| `ICONDL_PATH` | — | ローカル設定 | ED用アイコン DL 先 |
| `POINT` | `CALC_MYLIST` / `CALC_PLAY` / `CALC_COMMENT` / `CALC_LIKE` | 40/1/1/10（SP 20/1/1/20） | 各ポイント倍率 |
| `SP.CheckDateOver` | — | 20170701 | lastresultSP.csv チェック用（前回 SP の集計日） |
| `COMMENT_OFFSET` | `Mode` / `UnderLimit` | 2 / 0.01 | コメント補正モード・下限 |
| `MYLIST_OFFSET` | `Mode` | 1 | マイリスト補正モード |
| `PLAY_OFFSET` | `Mode` | 2 | 再生補正モード |
| `POINTALL_OFFSET` | `Mode` | 0 | 全体補正モード（1=VCOLE2023） |
| `SYSTEM.ResultCsv` | `Code` | 0 | result.csv の文字コード（0=shift-jis / 1=Unicode） |
| `SYSTEM.Thread` | `Max` | 16 | マルチスレッドの最大スレッド数 |
| `SYSTEM.NicoChart` | `Mode` | 1 | ニコチャート取得（0=しない / 1=する） |
| `SYSTEM.Download.NicoAPI` | `Retry` | 20 | NicoApi 取得のリトライ回数（SP に影響） |
| `SYSTEM.Download.UserIcon` | `Retry` | 1 | ユーザーアイコン取得のリトライ回数 |
| `SYSTEM.URL_JSON_TARGET` | `Url` | `https://2daime.myds.me/old-ranking/{0}/{1}/` | 過去ランキング JSON の取得元（`{0}`=種別、`{1}`=日付） |

---

## 3. 出力ファイル仕様

出力先: `Output/`（カレントディレクトリ相対・固定）

| ファイル | クラス | 内容 |
|---|---|---|
| `rank.txt` / `rank{UserNum}.txt` / `rankED.txt` | NrmOutput | 紹介枠 TSV（ID/投稿日/タイトル/再生時間/総合ランク/ポイント/カテゴリ/各ランク・数/前回ランク/補正値/ユーザー情報/人気タグ）。TSV・クォートなし |
| `result(UTF8).csv` / `result(SJIS).csv` | ResultCsv | 集計結果全件 CSV。ヘッダー30列（いいねランク/数含む）。文字列は `"` 囲み、`"`→`”` 置換 |
| `result_DB登録用(UTF8).csv` / `result_DB登録用(SJIS).csv` | ResultCsvRankDB | DB 登録用 CSV。いいね関連列なしの27列（旧フォーマット互換）。エスケープは `"`→`""`、`\`→`\\` |
| `result_DB登録用(UTF8).json` | ResultJsonRankDB | DB 登録用 JSON（Newtonsoft.Json シリアライズ、Formatting.None） |
| `queue.irv` | ResultImagegetMovieIcon | 動画アイコン DL キュー（ED枠まで） |
| `queue_UserIcon.irv` | ResultImagegetUserIcon | ユーザーアイコン DL キュー（全件） |
| `長期動画リスト.txt` | TyokiHantei | 長期動画（ランキングN回目）一覧 |
| `DB/NicoranHistory.db` | ResultHistory | 集計履歴（History / LastResult / LastResultInfo） |

---

## 4. DB テーブル仕様

### 主要 DB 一覧

| DB ファイル | 定数 | 用途 |
|---|---|---|
| `DB/LogOfficial.db` | `LOG_OFFICEIAL` | 公式過去ランキング（Ranking / Movie / RankingDate） |
| `DB/LogNicoChart.db` | `LOG_NICOCHART` | ニコチャート取得データ（Ranking）。`attach 'NicoChart'` で併用 |
| `DB/NicoranHistory.db` | `NiCORAN_HISTORY` | 集計履歴（History / LastResult / LastResultInfo） |
| `DB/ApiXML.db` | — | NicoApi キャッシュ（NicovideoThumb） |
| `DB/Dailylog.db` | — | 中間集計の日別キャッシュ（Dailylog） |
| `LogSnapshot{yyyyMMdd}.db` | `LOG_SNAPSHOT` | スナップショット DB（Ranking / DBVersion）。nicorank_SnapShot が日次作成 |

### Ranking テーブル（LogOfficial.db）

列: `ID`（動画ID）/ `集計日`（INTEGER・yyyyMMdd）/ `再生数` / `コメント数` / `マイリスト数` / `いいね数` / `人気のタグ`（JSON文字列）
- 集計日は主キーの一部（同一動画の日別履歴を持つ）
- いいね数は ALTER TABLE で自動追加される（ない場合のみ）

### Movie テーブル（LogOfficial.db）

動画の基本情報（ID / タイトル / 投稿日時等）。Ranking と JOIN して使用。

### RankingDate テーブル（LogOfficial.db）

`集計日` / `Ver` 等。メンテナンス日判定（`CheckMaintananceDay`）に使用。初期値 20190610。

### Dailylog テーブル（Dailylog.db）

中間集計用の日別集計結果。`集計日` / `種別` 等。いいね関連フィールドが無ければ ALTER TABLE で自動追加。

### History / LastResult / LastResultInfo（NicoranHistory.db）

- `History`: 動画ごとの過去ランクイン履歴（長期動画判定の材料）
- `LastResult`: 前回の集計結果（種別=モード名、集計日。JSON 列にランキング全体を保存）
- `LastResultInfo`: 前回集計時の設定 XML（`Config.GetXMLString()`）

### SnapShot DB（LogSnapshot{yyyyMMdd}.db）

- `Ranking`（ID 主キー / 再生数 / コメント数 / マイリスト数 / いいね数）— `INSERT OR IGNORE` で追記
- `DBVersion`（集計日 / Ver 1.0.1.0）

### SQLite 接続設定（SQLiteCtrl.Open）

- 接続文字列: `Pooling=False` / `JournalMode=Wal` / `DefaultTimeout=30`
- PRAGMA: `journal_mode=WAL` / `synchronous=NORMAL` / `temp_store=MEMORY` / `cache_size=-8000`
- PRAGMA 設定失敗時も接続は継続（グレースフルフォールバック）

---

## 5. API 仕様

### NicoApi（nicorankLib/api/NicoApi.cs）

- `https://ext.nicovideo.jp/api/getthumbinfo/` — 動画情報 XML 取得。キャッシュは `DB/ApiXML.db` の `NicovideoThumb`（取得日ごとに管理）
- `https://api.ce.nicovideo.jp/nicoapi/v1/video.info?v=` — video.info（現在 `convertMovieID` は未使用）
- 取得は `Parallel.ForEach`（`Config.ThreadMax` 並列）。失敗時は**全スレッド一時停止 + 指数バックオフ**（403 対策。後述の pitfalls 参照）

### スナップショット API（nicorankLib/SnapShot/SnapShotAnalyze.cs）

- `https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search`
- 1000再生以上フィルタ（直近1年以外）。5万件を超える期間は 1日ずつ狭めて再取得
- 100件ページングを 4 並列で取得。`":null"` は `":0"` に置換してからデシリアライズ

### ニコ動 nvapi（nicorank_oldlog/RankAPI/NicoRankiApi.cs）

- `nvapi.nicovideo.jp` — ジャンル/定番/トレンドタグのランキング取得。user_session クッキー + UA 付与
- ページングは hasNext まで最大 20 ページ

### 過去ランキング JSON（2daime.myds.me/old-ranking/）

- `{0}/{1}/file_name_list.json` でジャンル一覧を取得し、ジャンル別 JSON を並列ダウンロード（`Config.URL_JSON_TARGET` で切替可能）
- 種別: daily / weekly / monthly / total

---

## 6. 移行仕様（未実施・実装対象）

> タスクは `docs/tasks.md` の「SQLite ライブラリ移行」を参照。

### System.Data.SQLite → Microsoft.Data.Sqlite 移行

- nicorankLib の全 SQLite アクセスは System.Data.SQLite から Microsoft.Data.Sqlite に移行しなければならない。移行後も既存の全機能・振る舞いが維持されること。
- **ビルド成功**: 全ファイルの置き換え完了後、ビルドエラーが発生しないこと。
- **PRAGMA 維持**: `SQLiteCtrl.Open()` で `PRAGMA journal_mode = WAL` / `synchronous = NORMAL` / `temp_store = MEMORY` / `cache_size = -8000` が従来通り実行されること。
- **接続文字列**: `Data Source=<dbFilePath>;Pooling=False;Default Timeout=30` の単純な接続文字列を使用し、`SqliteConnectionStringBuilder` に依存しないこと。
- **パラメータ再利用**: `SqliteParameter` はループ外で一度生成し、ループ内では `.Value` プロパティのみを更新すること（10,000件以上の大量データ挿入時も新たな `AddWithValue` 呼び出しを行わないこと）。
- **プロジェクト設定**: nicorankLib.csproj / packages.config / app.config の各設定ファイルを移行に合わせて更新すること（Reference 置換、パッケージ置換、DbProviderFactories 削除）。
