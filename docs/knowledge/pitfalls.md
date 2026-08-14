# 実装ノウハウ・再発防止（pitfalls.md）

過去に発生した問題とその対策の記録。**同じ失敗を繰り返さないために**、実装・修正時に必ず確認する。

---

## SQLite 関連

### 1. 巨大トランザクションで btreeInitPage() returns error code 11（SQLITE_CORRUPT）

- **症状**: スナップショット API から数十万件を登録する際、`btreeInitPage() returns error code 11`（SQLITE_CORRUPT）が断続発生し、DB が破損して以後の読み書きが不可能になる
- **原因**: 単一の巨大トランザクションにより WAL ファイルが肥大化し、ディスク I/O やページ管理の限界を超える
- **対策（実装済み）**:
  - 5000件ごとのバッチコミット（`SnapShotDB.RegistDB`）
  - `INSERT OR IGNORE` 化（重複 ID スキップ）
  - SQLiteCtrl で WAL モード + PRAGMA（synchronous=NORMAL / temp_store=MEMORY / cache_size=-8000）
  - パラメータの事前生成・再利用
- **詳細**: `db.md` を参照

### 2. WAL モードの副作用

- `.db-wal` / `.db-shm` ファイルが別途作成される（通常は自動管理）
- 不要になった場合は `PRAGMA wal_checkpoint(TRUNCATE)` で手動整理可能
- バッチコミットの途中でクラッシュした場合、未コミット分（最大5000件未満）は失われるが、`LogSnapshot*.db` は日次作成されるため再実行で復元可能

### 3. PRAGMA 設定のグレースフルフォールバック

- PRAGMA 設定（WAL 等）に失敗しても接続自体は開いているため、**例外を握りつぶして継続する**（`SQLiteCtrl.Open()`）。呼び出し側でログを出す
- PRAGMA 設定の失敗で接続を閉じてしまうと、エラー処理が複雑化するため

### 4. 接続文字列の Pooling

- プーリングは**環境によって問題を起こすことがある**ため無効化（`Pooling=False`）

---

## ニコニコ API 関連

### 5. 連続アクセスによる 403 エラー

- **症状**: 大量の動画情報取得（getthumbinfo 等）で連続アクセスすると 403（AccessDenied）になり、XML を取得できない
- **対策（実装済み・NicoApi）**: 取得失敗時に `ManualResetEventSlim` で**全スレッドを一時停止**し、指数バックオフ（`1.5^retry × 1000ms`、最大100回）で再試行 → 成功したら再開
  - 各スレッド冒頭に `Sleep(rnd.Next(50,200))` のジッターを付与して同時アクセスを分散
- **TxtDownLoad（InternetUtil）**: 最大20回リトライ。`ContentType == "application/xml"` の ProtocolError（403 相当）は**即断念**（再試行しない）。それ以外は `2^retry×100ms`（上限30秒）の指数バックオフ + ランダム遅延

### 6. ニコ動の仕様変更への追従

- 過去に発生した仕様変更と対応（コミット履歴より）:
  - **RSS 廃止**（2025-04）: RSS 取得を廃止し、ニコ動 API 経由でランキングを取得するよう変更。取得元は設定ファイルで切替可能（`URL_JSON_TARGET`）
  - **FQDN 変更**（2024-03）: snapshot API のエンドポイント URL を変更
  - **サイバー攻撃対策**（2024-06）: 公式過去ログ提供停止 → `2daime.myds.me/old-ranking/` を自前運用
  - **24/08/14 の更新**（2024-08）: 中間ランキングの集計対象が正しく産出されない不具合を修正
  - ニコ動側の仕様変更はコミット履歴に情報が残っている。仕様変更を疑ったら `git log` を確認する

### 7. スナップショット API の仕様

- **1年以上前のデータは 1000 再生制限**（`flgLimit1000` で URL 切替）
- **5万件を超える期間は 1日ずつ狭めて再取得**（自主規制）
- レスポンスの `":null"` を `":0"` に置換してからデシリアライズ（null 対策）

---

## テスト関連

### 8. old-style csproj + .NET 10 SDK で StackOverflow

- **症状**: MSTest 3.5.2 の testhost 起動時に StackOverflow が発生
- **原因**: old-style csproj（packages.config + Reference）と .NET 10 SDK の組み合わせ問題
- **対策（実装済み）**: SDK-style csproj に変換。**UnitTest.csproj は SDK-style を維持すること**（詳細: `testing.md`）

### 9. Config テストの nicorank.xml 依存

- `Config.GetInstance().Initilize()` がカレントディレクトリの `nicorank.xml` を読むため、テストでは出力直下にフィクスチャをコピーする
- `TestConfigBuilder` で非公開フィールドを差し替える方式もある

### 10. TextUtil.ReadCsv の戻り値仕様

- ファイル不在時、戻り値は `false` で out 引数は**空の List**（null ではない）
- テストアサートは `Assert.IsNotNull(rankingList)` + `Assert.AreEqual(0, rankingList.Count)`

---

## 運用・その他

### 11. 週刊ランキングの差分の平等性

- 公式データは取得件数に制限があり、半年/1年の差分で「片方の情報がない」ことがある
- 差分計算できた動画とできなかった動画を同じ数字としてランキング計算するのは結果の平等性に問題がある
- 過去は人間が差分情報を手入力していたが、**スナップショット DB で自動化**した（SP 集計）

### 12. 公式チャンネル動画（so）の再公開問題

- so 動画は非公開→再公開で**投稿日時が更新される**（更新条件は不明）
- 本ツールの DB は 2019 年以前の情報を持たないため、その補完に nicochart.jp を利用（`CheckSoMovieNeedSabun`）
- 補完がないと「2026年に100万再生した」という実態と合わない扱いになる

### 13. ビルド・環境の注意

- `nicorankLib` の Costura.Fody は削除しない（単一 EXE 化のため）
- nicorank_oldlog は net8.0 から net48 ライブラリを参照するハイブリッド構成
- 設定ファイル（`config.json` / `cookie.txt`）はリポジトリに含めない（手動配置）
