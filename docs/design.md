# 設計判断（Design）

このドキュメントは、プロジェクトの重要な設計判断とその根拠を記録する。

- 設計判断を下したら（または既存の判断を発見したら）このファイルに追記する。
- 実装済みの設計の詳細（コード構造・依存関係）は `docs/knowledge/` を参照。ここでは「なぜそう設計したか」に焦点を置く。

---

## SQLite 移行設計（実施済み 2026-08-29）

> タスクは `docs/tasks.md` の「SQLite ライブラリ移行」を参照。Issue #20 対応で `Microsoft.Data.Sqlite 10.0.11` + `SQLitePCLRaw 2.1.12` に移行し、`bin` 直下の散乱を避けるため `lib` サブフォルダに集約済み（`FodyWeavers.xml` で `ExcludeAssemblies`）。

### Context

- nicorankLib は .NET Framework 4.8 環境で System.Data.SQLite 1.0.118.0 を使用。22 ファイル（nicorankLib 15 + UnitTest 7）にわたって `using System.Data.SQLite` が存在し、SQLiteCtrl.cs で `SQLiteConnectionStringBuilder` による接続文字列構築を行っていた。
- EntityFramework6 パッケージは存在するがコード上は未使用。
- packages.config を保持し続ける方針。

### Goals / Non-Goals

**Goals:**
- System.Data.SQLite から Microsoft.Data.Sqlite への完全移行
- 既存の全機能・振る舞いの維持（WALモード、同期設定、キャッシュ設定、バッチコミット、INSERT OR IGNORE 等）
- packages.config の継続使用
- app.config の DbProviderFactories セクション削除

**Non-Goals:**
- NuGet.config 移行（今回は保留）
- EntityFramework6 関連セクションの削除（今回は保留）
- .NET Core / .NET 5+ への移行
- DB ファイルパスの変更
- 新機能の追加

### Decisions

#### Decision: 接続文字列を単純な文字列で構築
- **選択**: `SQLiteConnectionStringBuilder` から `Data Source=...;Pooling=False;Default Timeout=30` 形式へ
- **理由**: Microsoft.Data.Sqlite に `SqliteConnectionStringBuilder` は存在するが、現状の固定パラメータ（Data Source, Pooling, Default Timeout のみ）であれば文字列連結で十分。複雑な組み立てが不要なため可読性が高まる。
- **代替案**: `SqliteConnectionStringBuilder` を使用する方法も検討したが、パラメータ数が少なく拡張性も不要なため採用せず。

#### Decision: 既存 PRAGMA 設定は維持
- **選択**: `PRAGMA journal_mode = WAL`、`PRAGMA synchronous = NORMAL`、`PRAGMA temp_store = MEMORY`、`PRAGMA cache_size = -8000` は従来通り `SqliteCommand` で実行
- **理由**: これはデータベース動作の安定性に直結する設定であり、移行後も同一の PRAGMA が発行されるべき。

#### Decision: API 名の機械的置き換え
- **選択**: `SQLiteConnection` → `SqliteConnection`、`SQLiteCommand` → `SqliteCommand`、`SQLiteTransaction` → `SqliteTransaction`、`SQLiteParameter` → `SqliteParameter`、`SQLiteDataReader` → `SqliteDataReader`、`SQLiteException` → `SqliteException`
- **理由**: 名前空間とクラス名の変更のみで、API シグネチャはほぼ互換。機械的な置き換えで対応可能。

#### Decision: packages.config のバージョン指定
- **選択**: Microsoft.Data.Sqlite 10.0.11 + SQLitePCLRaw 2.1.12（.NET Framework 4.8 対応の安定版。`10.0.11` の `net48` 依存は `2.1.12`）
- **理由**: `10.0.11` は `net48` でも動作確認済み（`dotnet test` 69件 PASS）。`3.0.5/3.53` は `AnyCPU` 禁止のため `2.1.12` を採用。`lib` 集約は `win-x64/x86/arm` の3種を同梱し `x64` 必須/`ARM` 任意に対応。
- **補足**: `Microsoft.Data.Sqlite` 本体はメタパッケージで実体は `Microsoft.Data.Sqlite.Core` の `lib/netstandard2.0/Microsoft.Data.Sqlite.dll`。`packages.config` には `Microsoft.Data.Sqlite` / `Core` / `SQLitePCLRaw.bundle_e_sqlite3` / `core` / `lib.e_sqlite3` / `provider.dynamic_cdecl` の 6 パッケージを列挙。`System.ValueTuple 4.6.2` / `System.Buffers 4.6.1` 等も同時更新。

#### Decision: 非互換 API の対応
- **SqliteCommand 単引数コンストラクタ**: `new SqliteCommand(conn)` は `conn.CreateCommand()` に置換（Microsoft.Data.Sqlite には単引数コンストラクタが存在しない）
- **SqliteConnection.CreateFile**: `System.IO.File.Create(path).Dispose()` に置換（Microsoft.Data.Sqlite には同等の静的メソッドが存在しない）
- **SQLiteParameter 型指定**: `System.Data.DbType` → `SqliteType`（`String`→`Text`、`Int64`→`Integer`）に変更（Microsoft.Data.Sqlite のコンストラクタは `SqliteType` を要求）
- **BeginTransaction 戻り値**: `SqliteConnection.BeginTransaction()` は `DbTransaction` を返すため `SqliteCommand.Transaction`（`SqliteTransaction` 型）への代入時に `(SqliteTransaction)` キャストが必要

### Risks / Trade-offs

- **[Risk] Microsoft.Data.Sqlite の .NET Framework 4.8 サポートバージョンに制限がある可能性** → `10.0.11` + `2.1.12` でビルド・テストが `PASS` したため解消（`3.0.5` は `AnyCPU` 禁止のため `2.1.12` を採用）
- **[Risk] 22 ファイルにおよぶ書き換えで typo や置き忘れが発生する可能性** → using 文の一括置換後、ビルドエラーで残存箇所（`CreateFile` / 単引数コンストラクタ / `DbType`）を検出し対応
- **[Risk] e_sqlite3.dll ネイティブ DLL の実行時解決** → `SQLitePCLRaw.lib.e_sqlite3` の `runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll` を `None/Content` + `CopyToOutputDirectory` で `bin/lib/runtimes` に集約し、`FodyWeavers.xml` の `ExcludeAssemblies` + `AfterResolveReferences` 除外 + `probing privatePath="lib"` + `AssemblyResolve` で解決。`GetManifestResourceNames` で `SQLitePCLRaw/Microsoft.Data.Sqlite` の埋め込みが無いことを確認（`lib` 物理配置）。詳細は `pitfalls 4c` を参照
- **[Trade-off] 機械的置き換えで対応できるが、コードレビュー時の差分が大きくなる** → 1回のコミットで行い、差分の把握を容易にする

---

## APIリクエスト組み立ての型付き化（Issue #19・2026-09）

> タスクは `docs/tasks.md` の「ニコ動APIのリクエスト組み立てを型付きリクエストへ変更」を参照。背景（CLI等による外部検索条件指定の下地・スナップショット優先/nvapi横展開）は Issue #19「なぜ変更するか」を参照。

### Context

- スナップショットAPIのURLを `string.Format` リテラル直書き＋`%2B`手動エンコードで組み立てており、値のエスケープ未実施・必須 `_context` 未送信・フィルタ追加困難の技術負債があった。
- nvapi も `appendURL` 文字列連結で同種の問題（特に日本語 `tag` の未エンコード）を抱えていた。
- 公式仕様はいずれも GET クエリパラメータ形式のため、JSONボディPOST化はしない。

### Decisions

#### Decision: SnapShotRequest（型付きリクエストクラス新設・`nicorankLib/SnapShot/SnapShotRequest.cs`）

- **選択**: `q/targets/fields/filters/_sort/_limit/_offset/_context` をプロパティで保持し、`ToUrl()` で組み立て。キーは公式のブラケット記法（`filters[...][...]`）のまま、**値のみ `Uri.EscapeDataString`**。
- **理由**: ブラケットまでエンコードすると公式curl例の記法と乖離する。値のみのエンコードで `+09:00`→`%2B` の手書きが不要になり、日本語・`&`・`%` 混じり値のクエリ破壊を防ぐ。旧URLとのデコード等価性は単体テストで担保。
- **`_context` は `WeeklyNicoranProgram` を流用**（現行UAと同一・40文字制限内・追跡性維持）。
- **`_limit/_offset` はクランプ**（上限100/100000・下限0）。`_limit=0` の件数取得用途は維持。
- **見送り**: 日付逆転・`Context` 長・`Fields` 空等のバリデーションは将来のCLI外部指定時に実施（現行フローは内部生成のみのため）。
- **代替案**: `UriBuilder.Query`＋`ParseQueryString` は .NET Framework 4.8 で `System.Web` 依存を持ち込むため不採用。素の `StringBuilder`＋`EscapeDataString` で完結させる。

#### Decision: jsonFilter は string 経路のみ・型階層は先送り

- **選択**: `JsonFilterJson`（生JSON文字列＋エンコード経路）のみ用意し、`equal/range/or/and/not` の型階層は作らない。
- **理由**: 現行フローで使用箇所ゼロ。CLI要件未確定の段階で型を固めると手戻りになる。string経路があれば将来の型追加はビルダー内に閉じる。

#### Decision: SetRequestResult の flgLimit1000 不整合を解消

- **選択**: 旧 `SetRequestResult` は `flgLimit1000` を無視し常に1000制限URLを使っていた。`CreateRequestUrl(limit, offset, flgLimit1000)` に一本化し、件数取得時と同じフラグでページング取得する。未使用の `dateTime` 引数は `flgLimit1000` に置換。
- **理由**: 直近1年は「件数取得は無制限・実取得は1000制限」の矛盾があった。specs.md の「1000再生以上フィルタ（直近1年以外）」通りの挙動に合わせる。振る舞い変更のためレビュー依頼文・コミットメッセージに明記。

#### Decision: Replace(":null", ":0") は温存

- **選択**: シリアライズ設定側での解消は見送り、文字列ハックを維持。必須性を示す回帰テスト（`FromJson_NullCounters_RequireNullToZeroReplacement`）を追加。
- **理由**: 実証したところ null→`long` 直結の `FromJson` は `JsonSerializationException` で失敗する。POCOを `long?` 化すると `SnapShotDB.RegistDB` まで波及しリスク＞効果。
- **見送り**: カウンタ限定の正規表現への狭め化（文字列値中の `:null` 破壊は現実リスク極小のため）。

#### Decision: nvapi は辞書受け＋最小限ガード（横展開）

- **選択**: `requestAPI(apiurl, appendURL)` → `requestAPI(apiurl, query辞書)` に変更し文字列連結を廃止。`_frontendId=6`・UAを定数化。パス埋め込み（`genre/featuredKey`）も `EscapeDataString`。`tag` は `term=24h/hour` 以外では省略＋ログ（公式仕様の制約）。
- **理由**: 日本語タグの未エンコードが実害リスク最大のため。`tag` 省略は振る舞い変更のためレビュー依頼文・コミットメッセージに明記。
- **汎用組み立ては `nicorankLib.Util.ApiUrlBuilder` に抽出**（reviewer指摘対応）。値のエンコード・`?`/`&` 切替・null/空辞書を単体テストで担保し、`NicoRankiApi.BuildUrl` は `_frontendId` 付与＋委譲に縮小。`tag` 分岐自体はspec直結の条件のため抽出せず、両分岐のdict形状に対するテストで間接担保する。
- **見送り**: `term` の大文字小文字許容（`config.json` 由来の固定小文字語彙のため）・`BuildUrl` の順序テスト（oldlogは単体テスト対象外・委譲は目視済み）・フラグメント付きURL（呼び出し元なし）。
- **見送り**: `NicoApi.cs` のID連結（`sm/so`＋数字のみで実害なし）・`JsonReader`系のパス連結（クエリなし）・`InternetUtil` のデッドコードは対象外。理由はレビュー依頼文・コミットメッセージに残す。

#### Decision: エンコード済みURLの実サーバー受け入れは実証済み

- 全面エンコード（`fields` の `,`→`%2C`・日時の `:`→`%3A`・`+09:00`→`%2B`）＋`_context` 送信の件数取得1件で HTTP 200・`status:200` を確認（2026-09-04）。旧URLとのデコード等価性と併せ、移行の安全性を担保する。

---

## タグ出力の再設計（Issue #27・2026-09）

### Context

- 実利用者から「カテゴリ名と FavoriteTag が重複する」「ロックタグは3つだけ欲しい時と全部欲しい時がある」と要望があった
- 文字コード違いの出力群（SJIS / DB登録用CSV）は旧連携方式の名残で、現行は `result_DB登録用(UTF8).json` に一本化済みのため存在理由が消滅していた
- `FavoriteTags` は `HashSet<string>` で出力順が不定だったため、「最大3つ」の切り取りが決定的にならない問題があった

### Decisions

- **収集は無制限・制限は出力側**: `FavoriteTagReader` の件数上限を廃止し全件補完する。TSV系の3件制限は `NrmOutput` の上限パラメータで行う（`result(UTF8).csv`・`result_DB登録用(UTF8).json` はそれぞれ全件仕様）
- **カテゴリ重複の除外は出力側ヘルパー**（`Ranking.GetDisplayTags()`）: `UserInfoReader` が後段でカテゴリを補完するため、収集時点ではカテゴリ未確定の動画がある。最終カテゴリで判定する出力側が完全。DB格納値は重複のまま残ることを許容
- **順序保障のため `List<string>` 化**: 影響は宣言・初期化・マージ・3箇所の `Add`（重複判定化）のみ。DB・履歴内の既存 JSON は配列形式のため互換性あり
- **SJIS・DB登録用CSV の生成停止**: `CreateOutputCSV` の設定からSJIS除外、`CreateOutputCSV_rankDB`（週刊。SPは継承、中間は既にnull）を `null` 化。`frmMainSyukei` はnull安全のため無変更。`ResultCsvRankDB` クラスは温存（Issue #29で削除）
- **中間集計は外部取得なし**（`isLocalOnly`）: `FavoriteTagReader` にフラグを追加し、中間集計のみ `UpdateTumbInfo` を呼ばずキャッシュ参照で補完する。週刊/SPは先行オプション（`GenreInfoReader`/`UserInfoReader`/`MovieInfoReader`）が確保するため現状維持

---

## result(UTF8).csv不要列削除（Issue #29）

### Context

- `result(UTF8).csv` に運営ポイント系（情報が古すぎる）と補正内訳の一部が残り、列数・順序の見直しが必要になった
- `TextUtil.ReadCsv` が列番号固定のため、列削除・順序変更のたびに読取が壊れる技術負債があった
- `ResultCsvRankDB` は生成停止後もクラス・Factoryのnull枠だけが残っていた

### Decisions

- **TextUtilはカラム名から動的に検出**: ヘッダー行を辞書化し、欠落列は既定値（数値0・総合ランク空→9999999・タグなし→空リスト）で吸収する。新旧両対応とし、旧CSV（運営・補正あり、タグなし）も読める。`ColLmt` は動的化で不要になるため廃止し、`LastRankCsvReader` の第3引数を削除する
- **読み取らない列**: 運営2列は古すぎるため、マイリストポイントを含む補正系・ポイント内訳8列は再計算するため読まない。出力列のうち読取対象に残すのは人気タグのみ（カンマ区切りでリスト化）
- **ResultCsvは30列新順**: 人気タグを4列目（タイトルの後）に移動し、運営2列を削除する。マイリストポイントは23列目に単独配置し、マイリスト補正は26列目に残す。ヘッダー名は省略しない
- **ResultCsvRankDBはFactoryまで全削除**: クラス・csproj参照に加え、`ModeFactoryBase.CreateOutputCSV_rankDB` 抽象と派生override、`frmMainSyukei` の列挙1行を削除する（いずれもnull返却のみだったため実効出力数は不変）

---

## DB構成バージョン管理と集計開始時の自動移行（Issue #28）

### Context

- #27のタグ全件補完で `LastResult.JSON`（`rank.ToJson()` 全件保存）がさらに肥大化した。読み側（`LastRankReader`）は総合ランク・ポイントのみ参照しJSON列を使わないため、空文字化できる
- いいね列追加等の場当たり的ALTERが3箇所に分散していた（`ResultHistory` / `TyukanAnalyze` / `RankingHistory`）。DB構成変更のたびに判定方式が増える技術負債があった

### Decisions

- **DBVersionは LogOfficial / NicoranHistory の2DBに限定**: ニコ動仕様変更で構成が変わり得るDBのみを対象とする。`Dailylog` / `ApiXML` はキャッシュ扱い（最悪作り直し）のため対象外とし、既存の場当たりALTERは温存する
- **司令塔 `DbMigrationCoordinator` を新設（`nicorankLib/Util`）**: 集計開始時（`frmMainSyukei.AnalyzeAsync`・オープン直後・公式DB更新前）に更新を指示する。実処理は `IDbMigratable` として各DB担当クラスに委譲し、司令塔は具象クラスに依存しない（Util→上位層の依存を作らない）
- **失敗時は集計中断**: NicoranHistory不在時等は開始時点で中断する（fail-fast。従来は出力時失敗だったため振る舞い変更として記録する）
- **未記録DBはVer0から順に適用・未定義は失敗**: テーブルなし→いきなり最新Verにせず、`MigrateToVersion` をVer0から順に回して各段階を記録する。将来バージョン追加時はcaseを足す。未定義バージョンは取りこぼし防止のため失敗させる。記録Verが現在値より新しい場合（ダウングレード）は何もせず成功とする。VACUUMはVer0移行時の1回のみ実行する
- **新規INSERTからJSON列を除去・既存列はVer0移行でDROP**: 読み側不使用が確定したため空文字化からDROPに強化した。DROPは列存在確認後に実行し、最古スキーマ（JSON列なし）はスキップで成功する。DROP失敗時は空文字化へのフォールバックなしで集計中断する（中途半端な状態を作らない）。初回実行前の実環境DBバックアップを推奨する
- **移行手順はトランザクション化（VACUUM除く）**: Ver0移行のDDL＋UPDATEをBEGIN/COMMITで包み、失敗時はロールバックする。バージョン記録は成功確定後のトランザクション外書き込みとし、業務データとの巻き戻り連鎖を起こさない（pitfalls項目17との整合）。VACUUMはトランザクション不可のため確定後に実行し、失敗時は未記録→再実行でリトライする
- **新規INSERTは列維持・空文字**: `LastResult` のJSON列は残し空文字で登録する（`PRAGMA table_info` 互換維持）。`Execute()` 内の旧判定ブロックはヘルパー抽出して温存する（直接Execute呼びの互換性維持）
- **FavoriteTag見直しなし**: #27の収集無制限・出力側制限の分離が完成しているためコード不変とする
- **Ver0で旧SP種別行を削除（LastResult＋LastResultInfo）**: 旧SP集計が残した約11万行の残骸。SPは `CreateHistory()=null`＋前回順位CSV経路のためDB不使用で、新規書き込みもWeeklyのみ。種別指定は `EAnalyzeMode.SP.ToString()` のパラメータ化（ダブルクォート直書き回避）
- **`RankingHistory.Open` は注入済みの開接続を再利用**: テスト容易性のため。注入なしの本番経路は従来どおりファイルを開くため動作不変

### 将来検討（今回対応外）

- バージョンが進むと `MigrateToVersion` が各DBクラスの本来処理より肥大化する懸念がある。移行処理そのものを別クラス（例：DBごとのMigrator）に委譲する分離を将来検討する

---

## タグ検索ランキングの追加（Issue #30）

### Context

- SPモード相当の集計を、テキストの動画IDリストではなくスナップショットv2の検索結果で行いたい
- 検索条件は暫定でタグ式＋数値下限4種＋投稿日＋種別に絞る。ポイント計算はTAGRANK節（SP同型）で切り替え、節がなければ週間設定を使う
- UIは「タグ検索集計」タブを新設し、ポイント計算パネルは集計タブと共有する

### Decisions

- **入力のみ差し替え・差分以降はSP流用**: `TagRankAnalyze : InputBase` がライブ検索でID列を生成し、`SnapShotSabunReader`・`LastRankCsvReader`（任意）・`FavoriteTagReader`・`RankingAnalyze` パイプライン・7種出力はSP流用とする。新規 `ModeFactoryTagRank : ModeFactoryWeekly` は `CreateAnalyzer` / `CreateHistory(null)` / `CreateOutputJson_rankDB` のみoverrideする（SPの継承構成に倣う）
- **タグ式は `&`/`|`/`*` の単一フィールド**: `&`=AND優先・`|`=OR・括弧なし。`*`なし=`tagsExact`・あり=`tags` とし `*` 自体は除去する。`TagConditionParser` が `equal`/`and`/`or` の jsonFilter に変換する（string経路。Issue #19 の拡張口を使用）
- **数値・日付・種別は `filters[]` の実証済み記法**: jsonFilterの `range`（from/to必須可否が未検証）を避け、`filters[カウンタ][gte]`・`filters[startTime][gte/lt]`・`filters[contentType][0]` を使う。日付フィルタOFF時は中立期間（2000-01-01〜2100-01-01）で検索する。`q` は空・`targets` は不使用
- **5万件の自主規制は件数取得で判定**: `_limit=0` で `totalCount` を取得し、超過時は集計せず通知する（スナップショット取得側の5万で期間短縮する方式ではなく中断方式）。ページングは100件×4並列でIDを重複除去・ID順にする
- **TAGRANK節は節単位切替**: `Config.UseTagRank`（`IsTagRank && TAGRANK節あり`）で分岐し、節がなければ週間設定にフォールバックする。項目単位の補完はしない。OFFSET系は共通のためTAGRANK節に含めない
- **基準なし時は SnapShotSabunReader を使わない**: 差分なし専用の `TagRankTotalReader : BasicOptionBase`（AnalyzeDBのみ→`Total` 取得→`MovieInfoReader` 補完→全件 `Count=Total`。差分ループを持たない）を新設し、`ModeFactoryTagRank` でBase有無で分岐する（なし時は `BaseDay = TargetDay`）。`MovieInfoReader` には `AnalyzeTime`（＝`TargetDay`）を渡す（SPの `BaseTime` 渡しとは意図的に異なる。新規取得のため再取得寄りでよい）。SP共用クラスにTagRank専用分岐を入れない。空DBダミー案は不採用（`BaseTime` の置き方で古動画の误删・タグ範囲肥大が起き、分岐明示より見通しが悪いため）
- **v2最新値モードはReader分離・Input共有参照（Issue #35）**: `TagRankAnalyze.LiveCounters`（ID→4数値。従来は捨てていた `DefaultFields` のカウンタを保持）にし、新規 `TagRankLiveTotalReader`（基準なし）・`TagRankLiveSabunReader`（基準あり。Target=ライブ・Base=基準日DB）が参照する。なぜInput参照か: `RankingAnalyze` はInput→Optionの順に実行されるためOption実行時点では取得結果が確定しており、工場作成時点の未取得を共有参照で橋渡しできる。Input内完結案は入力と集計値の二役で肥大化するため不採用。純粋処理 `ApplyLiveTotals` は `TagRankLiveTotalReader` のstaticに置き両Readerで共用・単体テストで直接検証する（MovieInfoはネットワークのためReader成功系の結合テストは行わない前例を踏襲）。`TagSearchQuery.UseLiveCounter`（既定false）を唯一の切替とし、`SetInputFile` の引数は変えない（既存テスト・SP経路への影響を避けるため）。ポイント係数の見直しはコードと切り離し `TAGRANK` 節の運用調整に委ねる（累積値は桁が大きくSP同値のままでは順位の意味が変わるため）
- **共有パネルは実行時付け替え＋相対配置**: `panel3` の実体は1つのままタブ切替で親を付け替える（複製方式は同期ずれの温床のため不採用）。固定座標はAutoScaleの対象外でずれるため、`grpDb.Bottom` 基準の相対配置にする。タブ切替時はパネル値の保存（旧モード）→モード切替→読込（新モード）を行い、不正値があれば切替を中断して元のタブに戻す
- **いいね倍率の保存漏れを修正**: 従来の書戻しは `CALC_LIKE` を保存していなかった（表示のみ）。TAGRANK対応で全モード共通の `SavePointCalcPanel` に一本化する際に保存対象に加える。振る舞い変更として記録する

---

## LogOfficialの1年保持とSoHistory併設（Issue #31）

### Context

- `LogOfficial.db` が7年運用で10GB超。年2GB弱の増加に対し、1年超データの用途はso再公開チェックの差分元のみだった
- 過去無制限に遡る問合せは `CheckSoMovieNeedSabun` の1箇所のみ。他はID点照会か直近窓のため、差分元さえ別に残せば古い `Ranking` を消せる
- `Movie` は削除動画専用の記録で読み手は呼出元なし。`集計日` 列がなく日付pruneのキーにできない

### Decisions

- **保持境界はDB内最新日起点**: 実行日ではなく `RankingDate` のMAXからさかのぼる。更新停止期間があっても窓がずれず、2環境比較でも安定する
- **SoHistoryはID＋4数値＋集計日**: 差分計算に使う分のみ。タグは差分に使わず `FavoriteTagReader` が別経路で取るため含めない。ID主キーで消えた行のうち最新のみ
- **読み側はRanking優先・SoHistoryは補い**: `CheckSoMovieNeedSabun` は `Ranking` ヒットなしのときだけ `SoHistory` を見る2クエリ逐次。`SoHistory` 側にも基準日以前の絞りを付け、混入行があっても基準日より新しい値は使わない二重化で守る。表なし旧DBでは新着扱いで正常終了し、DB異常扱いにしない（新着動画を誤除外しないため）。結合・VIEWの1本化は見送り（共通ケースで速くもならず読みにくくなるため）
- **SoHistoryはprune駆動で維持する**: 消す直前に消去行を拾い、入っている日付より新しい消去行だけ置き換える。当日分の上書きはしない（当日分は `Ranking` に残るため不要で、置き換えると基準日より新しい値になり再公開チェックが効かなくなる）。この不変条件（SoHistoryの日付は常に保持境界より古い）により、無制限履歴の「基準日以前の最新行」と同じ結果になる
- **Ver1移行は日付区切り確定**: 初回の大量削除を一括トランザクションにせず集計日ごとに確定する（一括はWAL肥大で破損の前例あり）。退避は境界より古い行のみ新しい日から順に無視方式で入れ、削除とDROPは存在確認系でやり直し可能。バージョン記録は全工程の成功後に行う。VACUUMは確定後の1回のみ
- **日次は同日トランザクションに同梱・VACUUMなし**: 当日分更新＋消去行拾い上げ＋古い分削除を同じ区切りにする。毎日の最適化は重いため将来の手動最適化（#32）に委ねる
- **prune用索引を恒久化**: PKが(ID,集計日)のため集計日だけの削除が全走査になる。`Ranking(集計日)` の索引を作り、日次削除を索引経路にする
- **Movieは表ごと廃止**: 孤児判定のpruneより表削除が単純で、容量も同じ最適化で回収できる。`GenreAnalyze` 本体＋csproj参照＋日次書込みを削除する。`SPAnalyze` の同名フィールドは別物（`Ranking` 系）のため残す。テスト側のMovieヘルパーは汎用JOIN確認として温存する

### 将来検討（今回対応外）

- 手動のDB最適化タブ（#32）。日次削除の断片化が気になったら使う

## 順位計算の同点時タイブレーク（Issue #34）

### Context

- #31対策の前後比較で前回順位が2件だけ1ずつずれた。今週の計算は完全一致で、ずれは先週時点で発生していた（同点で順位だけ±1）
- `RankingAnalyze.calcRanking` の6種は単一キー降順＋連番のみで、同点時の順序が入力順依存だった。入力は並列取得のため実行ごとに変わり得る

### Decisions

- **第二キーはIDのみ・6種すべて・連番維持**: 投稿日・再生数も同点があり得るため、重複なしのIDだけが単一キーで完全決定的になる。対象は総合・再生・コメント・マイリスト・いいね・カテゴリの6種すべて（絞ると他種で同じ不定が残るため）。順位値は連番のまま変えず、同順位スキップはしない（差分最小・T31比較への影響最小化のため）
- **辞書式ではなく数値認識（種別→数字）**: 単純Ordinalでは桁違いのID順が数値順と一致しない（sm199 が sm20 より先になる）。IDの大小を割り当て順と直感的に一致させるため、`RankingIdComparer`（`nicorankLib/Analyze/model`）で種別→数字の順に比べる。数字化できないIDが混ざっても例外にせず決定的順序を保つため、数値化の有無で群を分けてから群内で辞書式比較し、数値化できる正規IDを先にする（直接フォールバックすると推移律が崩れ sm10・sm10a・sm9 の循環になるため）。最終段も辞書式のため常に決定的になる
- **Comparer1個にまとめる**: ThenByの二次比較子は一次キーが等しい同点ペアにだけ呼ばれるため、同点時だけ分解すれば処理コストが最小になる。キー抽出をThenBy2段に分けると全件分解が毎回走るため不採用。`SabunReader` の `Substring(2)＋TryParse` と同型の defensive な扱いにする
- **並列ソート前にポイントを単一スレッドで確定させる**: `Ranking.CalcPoint` のキャッシュ（`workPointTotal`）はスレッドセーフでなく計算途中の部分値を書き込みながら進めるため、6タスクの並列初回計算が重なると別タスクが部分値を読んで順序が不定になる（単体テストの全件実行で1回だけカテゴリ順位がずれて発覚した既存の競合）。タイブレークの決定的保証のために `Task.Run` 群の前で全件の `PointTotal` を読んで確定させる。読むだけなら競合しない。`CalcPoint` 自体へのロックは範囲が広がるため見送る
- **T31ブランチにも取り込む**: 本来#34は#31比較終了後に着手する建前だったが、順位が安定しないと1ヶ月比較自体がやりにくいため、developと `feature/t031-logofficial-prune-sohistory` の両方へマージする（#35と同一方式）

---

## Snapshot API v2更新チェック（Issue #38）

### Context

- スナップショット v2 のデータは AM5:00（JST）時点だが参照可能になる時刻はデータ蓄積とともに後ろ倒し（2026-09-20実測で `last_modified=07:08`）。更新完了の後ろ倒しに合わせ定期タスクの開始時刻を見直してきた経緯があるが、それでも前日データをつかむ危険があり、前日DBで後段の差分がすべてずれる
- 公式ガイドに切り替え日時エンドポイント（`.../api/v2/snapshot/version` → `{"last_modified": "..."}`）があるため推測ロジックは不要
- WinForm（手動）とCLI（無人・NASメール運用）で事後動作が異なる。「取得成功なのに異常終了」を作ると運用の切り分けが難しくなるため、タイムアウト時は取得自体をやめて異常終了する方式にした

### Decisions

- **判定と待機と取得を3分離**: `SnapShotVersionChecker`（取得→パース→JST日付比較の3値判定）・`SnapShotVersionPoller`（5分×最大1時間の待機）・`SnapController`（取得専任のまま）。待機と取得を分けると「タイムアウト時は取得せず終了」が呼び出し側に素直に書ける。取得成功＋遅延の畳み込み（終了コードの意味が二重になる案）は不採用
- **JST比較は `ToOffset(+09:00)` で寄せる**: `DateTime.Today` は実行環境TZ依存でNAS側設定次第で日付境界がずれる。`last_modified` のオフセットと実行時刻の両方をJST化してから `Date` 比較する
- **タイムアウト時は `InitilizeDB` に触れない**: 既存DB削除を伴うため、前日データでの上書きも当日ファイルの破壊も起きない。終了コード2でNASメールが飛び9時枠見直しのトリガーになる。DBエラーとの切り分けのため `nicorankerr.log` にリトライタイムアウトであることを記録する
- **version日時パースは `DateParseHandling.None`**: `JObject.Parse` 既定ではISO日時が `Date` トークンに化けて `+09:00` が落ちる（実装中に単体テスト8件失敗で発覚）。`JsonTextReader` で None を指定し文字列のまま `DateTimeOffset.TryParse` に回す。詳細は `pitfalls` 項目22
- **Winコンソールモードは対象外**: Linux CLIのみにリトライを実装し、Windows引数あり起動は従来通り。揃える場合は別タスクとする
- **待機の数え方は経過時間ベース**: 初回＋12回再チェックで約60分。精度不要のため超過分の延びは許容する。`RetryInterval`/`Timeout` は定数化し変更時はIssue見直しと判断するためテストで縛る

---

## ApiXML由来の削除判定を順位に影響させない（Issue #40）

### Context

- `ApiXML.db` は表示用キャッシュの位置づけだったが、`NicoApi.GetUserInfo` / `GetMovieInfo` が取得失敗・Status非ok・行なしで `isDelete` を立てていた
- `isDelete` は順位計算前の除外（SP・タグ検索）やポイント0化（週刊）に使われるため、取得タイミング次第で順位全体がずれた。再取得の有無は実行日で変わり、読む行も最新指定がなく不定だった
- SPは動画IDの一覧から出発し投稿日・タイトルを動画情報に頼るため、取れないだけで除外が起きていた

### Decisions

- **表示と存否の分離**: `GetUserInfo` / `GetMovieInfo` から `isDelete` 代入を除去し、失敗時は空欄・既定値のまま残す。各Reader（`MovieInfo` / `Genre` / `UserInfo` / `FavoriteTag`）も確保・読取の失敗で集計中断しない。なぜ中断しないか: キャッシュ扱い（最悪作り直し）のものを理由に集計全体を止めると、1件の取得失敗が全件失敗に見えるから。除外の判断は入力側（スナップショット差分・Sabun・Hidden）に残す
- **最新行読みに統一**: `GetLockedTags` と同じ `ORDER BY 取得日 DESC LIMIT 1` にする。複数行が残った場合の不定読みをなくすため
- **SPの欠落は案B（あるものを使う）**: `SpMovieInfoFallback` がタイトル空欄分だけ `LastResult` 最新タイトルと `LogOfficial` 期間内初見日（`MIN(集計日)`）で補う。新規取得はしない。初見日は本物の投稿日ではなく生成時刻の代わりの参考値であり、除外には使わない（方針は「残す」。検索下限が基準-7日のため除外分岐にも到達しない）。どちらもなければ空のまま残す。タイトル検索は `種別=Weekly` 限定で主キー経路を引き、なければ全体にフォールバックする（`LastResult` の主キーは種別・集計日・IDのためID単独では全表走査になる）。案A（スナップショットDBへの文字情報追加）は約900万行の肥大化と取得負荷のため見送り、Issue #41 に分離する。ジャンル空欄は許容し、影響が大きければ案Aを再検討する
- **週刊は事前取得で欠落自体を減らす**: oldlog週刊保存時に全ID約26000件を一括取得して日付フォルダへ `ApiXML.db` を置く。DB形式にした理由は、2019側が今のまま開けて読み書きコードが要らないから（json/tsvは取込コードが要る）。一時名で作ってから置き換え、2019側は一時置き場経由で新しい取得日だけ取り込む。運搬なしでも本地継続する
- **削除表示はタイトル欄の目印**: `Ranking.DeletedTitlePrefix`（【集計後削除】）を空欄タイトルに付ける。列追加・順序変更をしない理由は、手動アップロード先への互換を守るため。Status明言と行なしの区別はしない（区別のための再取得がタイミング依存を戻すため）

## 手動DB最適化（Issue #32）

### Context

- #31の日次pruneではVACUUMしないため、断片化が気になったときに手動で最適化する置き場所が必要になった
- 移行時（#28・#31）にVACUUMの実績はあるが、いずれも集計開始時の自動処理であり、手動実行の置き場所・サイズ表示・非同期化は新規設計になる
- UIは先にモック（32.1）で固めた。対象4DB・実行前後2列・#41予告枠・ログ欄なし・実行ボタン文言まで確定済みのため、中身実装ではレイアウトを変えない

### Decisions

- **`DbOptimizer` は `Util` の static クラス**: `DbMigrationCoordinator` と同層に置き、UIに依存させない。staticにしたのは状態を持たないため（`ApiUrlBuilder` と同型）。`Optimize(dbPath)`・`FormatFileSize`・`GetDefaultTargets()` を公開し、単体テストから直接呼べる
- **VACUUM手順は移行時と同一**: `SQLiteCtrl` で開いて `VACUUM;` を1発。トランザクションで包まない（VACUUMはトランザクション不可のため。移行時と同一の理由）。PRAGMA系は `Open()` 側で面倒を見るため呼び出し側では触らない
- **ファイル不在はスキップ扱い（失敗にしない）**: Dailylog.db等は未実行モードでは存在しないのが正常であり、不在自体は異常ではないため。UIはサイズ欄に「なし」と出す
- **サイズは `.db` 本体のみ**: `-wal` / `-shm` の合算はしない。接続クローズ時のチェックポイント後に本体サイズを測るため前後比較は成立する（VACUUM自体の効果ではなく測定順序が根拠のため、順序を変えると壊れる）。合算すると実行前後で測り方がぶれるため採用しない
- **非同期は `Task.Run` + `await`（`BackgroundWorker` 不使用）**: 既存の `ExecuteAnalyzeAsync` と同型にする。DB件数ステップで進捗を更新でき、UI更新はawait復帰後のUIスレッドに寄るため、pitfalls項目19（集計スレッドからのコントロール参照禁止）に触れない。条件の退避もタグ検索の `TagExecuteContext` と同一理由で行う
- **実行中は実行系ボタンを無効化**: 最適化ボタン・チェック4件・各集計ボタンを止め、集計との同時実行によるDBロック競合を防ぐ。VACUUM自体は原子性があるため、最悪でも失敗表示に留まり破損しない
- **ApiXML／Dailylogのパス定数は `DbOptimizer` に持つ**: `DB.cs` に定数がないため、`NicoApi`／`ApiXmlCacheImporter`／`TyukanAnalyze` と同一値をここに定義する。値ずれは最適化対象と集計参照先の食い違いになるため、変更時は同時更新すること（単体テスト `GetDefaultTargets_MatchesKnownPaths` で既知値との一致を縛る）
- **サイズ表示の進数は1024固定**: Windowsのエクスプローラ表示と合わせるため。`BytesPerUnit` として定数化する（マジックナンバー抑止）

---

## 実装済みの設計判断（要点）

詳細は `docs/knowledge/db.md`・`docs/knowledge/testing.md` を参照。

### SQLiteCtrl の接続設計（btreeInitPage 対策・2026-06-03）

- **接続文字列**: `Pooling=False`（環境によって問題を起こすため）、`JournalMode=Wal`、`DefaultTimeout=30`
- **PRAGMA**: `journal_mode=WAL` / `synchronous=NORMAL` / `temp_store=MEMORY` / `cache_size=-8000` を接続時に一律適用
- **グレースフルフォールバック**: PRAGMA 設定に失敗しても接続自体は維持（例外は握りつぶし、呼び出し側でログ出力）
- **根拠**: 単一の巨大トランザクションで WAL ファイルが肥大化し `btreeInitPage() returns error code 11`（SQLITE_CORRUPT）が発生していた。バッチコミットと併せて破損リスクを低減。

### SnapShotDB の大量データ登録設計（2026-06-03）

- **5000件ごとのバッチコミット**: 単一トランザクションでの WAL 肥大化を回避。1件ずつのコミットによるオーバーヘッドも回避（約200Byte/行 → 約1MB/バッチで I/O とメモリのバランスが良い）
- **INSERT OR IGNORE**: 従来の `INSERT ... WHERE NOT EXISTS` から変更。重複 ID 除外のセマンティクスを保ちつつ SQLite の最適化パスを利用
- **パラメータ事前生成・再利用**: `SQLiteParameter` をループ外で一度 `Add()` し、ループ内では `.Value` のみ代入。GC 圧力の削減
- **Rollback の例外処理**: `try { aCmd.Transaction?.Rollback(); } catch { }` でロールバック失敗を握りつぶし、元の例外を確実に上位へ伝播

### ISQLiteCtrl 抽象化とテスト容易性（2026-06-23）

- `SQLiteCtrl` から `ISQLiteCtrl` インターフェースを抽出し、`OpenInMemory()` を追加
- DB 操作クラスには `ISQLiteCtrl dbCtrl = null` のオプショナル引数でコンストラクタ注入（テスト時のみ注入、本番は従来通り `new SQLiteCtrl()`）
- **根拠**: SQLite 移行時に Microsoft.Data.Sqlite 版が同一インターフェースを実装すればテストコードの変更がゼロになる。DI コンテナは過剰設計。

### テスト観点は「操作パターン」単位（2026-06-23）

- クラス単位ではなく SELECT / INSERT / DDL / エラー の操作パターン単位でテストを整理
- **根拠**: 移行後のクラス構成が変わる可能性があり、操作パターンごとのテストがあればどの実装でも検証可能

### テスト基盤（2026-06-23）

- モック: Moq 4.x（.NET Framework 4.8 対応・MSTest との互換性・広く使われている）
- テストデータ: `UnitTest/Fixtures/` に配置し、ビルド時に出力ディレクトリへコピー（絶対パス依存の排除）
- 既存の実行不能テストは削除し、書き直し（`TestPointCalc_POINTALL_VOCACOLE2023` は維持）
