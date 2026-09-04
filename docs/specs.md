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

### 人気タグの収集と出力（FavoriteTagReader・Issue #27）

- 収集: LogOfficial.db の `人気のタグ`（最新集計日）に加え、ApiXML.db（NicovideoThumb・最新取得日）の `lock="1"` タグを定義順に全件補完する（件数上限なし、重複除外）。行なし・Status非ok・パース失敗は素通り。対象は UserEnd 以内 or カテゴリ1位。中間集計は `isLocalOnly` のため外部取得を行わずキャッシュ参照のみ
- `Ranking.FavoriteTags` は `List<string>` で挿入順を保持する（人気タグ→タグロック定義順）
- 出力: `Ranking.GetDisplayTags()` が挿入順のままカテゴリ名と同名のタグを除外する（`Trim` 後完全一致。空カテゴリは除外なし）。ファイル別の件数制限は `NrmOutput` の上限パラメータで行う（`rank.txt`・`rankED.txt` のみ3件）

### 差分集計と so 新着偽造判定（SabunReader）

- 差分は LogOfficial.db の過去ランキングから取得する（`CheckSoMovieNeedSabun` / `GetRankingSabunDataLogOfficial`）。過去ログにデータがなければ差分なし
- 過去ログに差分が取れない so 動画（公式チャンネル）は ID 番号で新着判定する:
  - so + 数値が **40000000 未満 → 新着偽造**（非公開→再公開で過去にランクイン済みとみなし、`isDelete` で集計対象外）
  - **40000000 以上 → 新着**として通常集計
  - 数値に変換できない ID も新着偽造扱いで対象外（ID 採番ルール変更時は閾値とともに見直し）
- DB エラー等で差分判定できなかった場合も対象外にする（全除外が発生した時点で問題に気づけるようにする意図）

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
| `CreateNRMRank()`（`rank.txt`） | ✅ 0〜GetRank()・タグ最大3件 | ✅ | ✅ |
| `CreateNRMRank1000()`（`rank{UserNum}.txt` / `rank1000.txt`） | ✅ rank{UserNum}.txt・タグ全件 | ✅ **rank1000.txt（固定0〜1000）・タグ全件** | ✅ rank{UserNum}.txt・タグ全件 |
| `CreateNRMRankED()`（`rankED.txt`） | ✅ タグ最大3件 | ✅ | ✅ |
| `CreateOutputCSV()`（result CSV） | ✅ result(UTF8).csv のみ | ✅ | ✅ |
| `CreateOutputCSV_rankDB()`（DB登録用 CSV） | —（null・生成停止） | —（null） | —（null・継承） |
| `CreateOutputHTML()` | **未実装（null）** | 未実装（null） | 未実装（null） |
| `CreateOutputMovieIconGet()`（`queue.irv`） | ✅ | —（null） | ✅ |
| `CreateOutputUserIconGet()`（`queue_UserIcon.irv`） | ✅ | —（null） | ✅ |
| `CreateOutputWORK()` | **未実装（null）** | 未実装（null） | 未実装（null） |
| `CreateOutputJson_rankDB()`（DB登録用 JSON） | ✅ | —（null） | ✅ |

- 実効出力は **週刊 9 種 / 中間 4 種（NRM×3 + CSV）/ SP 7 種**（HTML・WORK は未実装のまま。null を除く実数）
- SP は `ModeFactoryWeekly` 継承。履歴登録・長期判定なし（`CreateHistory` は override で null）
- 中間の `CreateNRMRank1000` は `rank1000.txt` に固定 1000 位（週刊/SP は `rank{UserNum}.txt`）
- `result(SJIS).csv` / `result_DB登録用(UTF8).csv` / `result_DB登録用(SJIS).csv` は生成停止（他システム連携は `result_DB登録用(UTF8).json` に一本化したため。Issue #27）

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
| `SYSTEM.Download.NicoAPI` | `Retry` | 20 | NicoApi 取得のリトライ回数（SP に影響） |
| `SYSTEM.Download.UserIcon` | `Retry` | 1 | ユーザーアイコン取得のリトライ回数 |
| `SYSTEM.URL_JSON_TARGET` | `Url` | （自前運用の互換サイト） | 過去ランキング JSON の取得元（`{0}`=種別、`{1}`=日付）。**URL は公開しない** |

- 旧設定 `<NicoChart Mode="..." />`（ニコチャート取得）は廃止済み（#23）。nicorank.xml に残っていてもデシリアライズ時に無視される

---

## 3. 出力ファイル仕様

出力先: `Output/`（カレントディレクトリ相対・固定）

| ファイル | クラス | 内容 |
|---|---|---|
| `rank.txt` / `rankED.txt` | NrmOutput（タグ最大3件） | 紹介枠 TSV（ID/投稿日/タイトル/再生時間/総合ランク/ポイント/カテゴリ/各ランク・数/前回ランク/補正値/ユーザー情報/人気タグ）。TSV・クォートなし |
| `rank{UserNum}.txt` / `rank1000.txt` | NrmOutput（タグ無制限） | 同上。人気タグは全件 |
| `result(UTF8).csv` | ResultCsv | 集計結果全件 CSV。ヘッダー31列（30列＋最終列「人気のタグ」・全件カンマ結合）。文字列は `"` 囲み、`"`→`”` 置換 |
| `result_DB登録用(UTF8).json` | ResultJsonRankDB | DB 登録用 JSON（Newtonsoft.Json シリアライズ、Formatting.None。変更なし） |
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

- 公式ガイド: https://site.nicovideo.jp/search-api-docs/snapshot（2026-08 時点の仕様を反映）
- エンドポイント: `https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search`
- **リクエスト形式は GET クエリパラメータのみ**（JSON ボディの POST は公式仕様に存在しない）
- データ更新は毎日 AM5:00（JST）。切り替え日時は `https://snapshot.search.nicovideo.jp/api/v2/snapshot/version` で取得可能

**クエリパラメータ**

| パラメータ | 必須 | 説明 |
|---|---|---|
| `q` | 必須 | 検索キーワード。**空文字でも可**（キーワード無し検索。`q=` 自体の省略は不可） |
| `targets` | 条件付き | キーワード検索対象フィールド（カンマ区切り）。キーワード無し検索では省略可 |
| `fields` | 任意 | レスポンスに含めるフィールド（カンマ区切り）。例: `contentId,title,viewCounter` |
| `filters` | 任意 | 絞り込み条件。ブラケット記法: `filters[field][gte]=val` / `filters[field][lt]=val` / `filters[field][0]=val`（一致） |
| `jsonFilter` | 任意 | OR/AND/NOT の入れ子など複雑な条件に使用。**URL エンコードした JSON 文字列**を渡す（JSON 構造は `{type: equal/range/or/and/not, ...}`） |
| `_sort` | 必須 | ソート順。`-viewCounter` など方向記号 + フィールド名 |
| `_offset` | 任意 | 取得オフセット。**最大 100,000** |
| `_limit` | 任意 | 取得件数。**最大 100** |
| `_context` | 必須 | サービス/アプリケーション名（最大 40 文字） |

**レスポンス**: `meta.status`（200 成功）・`meta.totalCount`（ヒット件数）・`data[]`。`status: 400` はパラメータ不正、`503` は高負荷/メンテ（5 分以上間隔を空けてリトライ）

**利用上の注意**

- **User-Agent にサービス名/アプリケーション名の指定が必須**
- 同時接続数の制限あり。繰り返しリクエスト時は前回レスポンス時間と同等の待機を推奨
- 検索に 1 秒以上かかる場合はフィルタで分割して取得する

**本プログラムの実装**

- `SnapShotRequest`（型付きリクエスト）で URL 生成。キーは公式のブラケット記法のまま、値は `EscapeDataString` でエンコード。必須の `_context`（`WeeklyNicoranProgram`）を送信
- 1000再生以上フィルタ（直近1年以外）。5万件を超える期間は 1日ずつ狭めて再取得
- 100件ページングを 4 並列で取得。`":null"` は `":0"` に置換してからデシリアライズ（カウンタが `long` 直結のため置換が必須。回帰テストで担保。Issue #19 で検証済み）

### ニコ動 nvapi（nicorank_oldlog/RankAPI/NicoRankiApi.cs）

- `nvapi.nicovideo.jp` — ジャンル/定番/トレンドタグのランキング取得。user_session クッキー + UA 付与
- クエリは辞書受け＋エンコードで組み立て（`_frontendId` 自動付与）。`tag` は `term=24h/hour` の場合のみ送信（公式仕様の制約）
- ページングは hasNext まで最大 20 ページ
- **API 仕様は koizuka 氏の公開メモを参照**（ニコニコ動画 ランキングAPI仕様）:
  - https://gist.github.com/koizuka/2c927c36504cde2f70685e361f9a4678
  - 共通仕様の要点: User-Agent（自身のツール名を識別可能な形で指定）・`user_session` クッキー・クエリパラメータ `_frontendId=6` が必須
  - **user_session クッキーの実態（メモの記述は正確ではない）**: 認証は「R18 コンテンツの取得」にのみ必要（正確には、R18 表示を設定で有効にしているアカウントでの認証が必要）。R18 以外のランキングは認証なしでも取得できる。

### 過去ランキング JSON（自前運用の互換サイト）

- `Config.URL_JSON_TARGET` で指定したベース URL の `{0}/{1}/file_name_list.json`（`{0}`=種別、`{1}`=日付）でジャンル一覧を取得し、ジャンル別 JSON を並列ダウンロード
- 取得元は自前運用の互換サイト（個人 NAS 上。URL は公開しない）
- 種別: daily / weekly / monthly / total

---

## 6. 移行仕様（実施済み 2026-08-29）

> タスクは `docs/tasks.md` の「SQLite ライブラリ移行」を参照。Microsoft.Data.Sqlite 10.0.11 + SQLitePCLRaw 2.1.12 への移行で本仕様を満たすことを検証済み（`dotnet test` 69件 PASS、`lib` 集約で `e_sqlite3` は `bin/lib/runtimes` に配置）。

### System.Data.SQLite → Microsoft.Data.Sqlite 移行

- nicorankLib の全 SQLite アクセスは System.Data.SQLite から Microsoft.Data.Sqlite に移行しなければならない。移行後も既存の全機能・振る舞いが維持されること。
- **ビルド成功**: 全ファイルの置き換え完了後、ビルドエラーが発生しないこと。
- **PRAGMA 維持**: `SQLiteCtrl.Open()` で `PRAGMA journal_mode = WAL` / `synchronous = NORMAL` / `temp_store = MEMORY` / `cache_size = -8000` が従来通り実行されること。
- **接続文字列**: `Data Source=<dbFilePath>;Pooling=False;Default Timeout=30` の単純な接続文字列を使用し、`SqliteConnectionStringBuilder` に依存しないこと。
- **パラメータ再利用**: `SqliteParameter` はループ外で一度生成し、ループ内では `.Value` プロパティのみを更新すること（10,000件以上の大量データ挿入時も新たな `AddWithValue` 呼び出しを行わないこと）。
- **プロジェクト設定**: nicorankLib.csproj / packages.config / app.config の各設定ファイルを移行に合わせて更新すること（Reference 置換、パッケージ置換、DbProviderFactories 削除）。
