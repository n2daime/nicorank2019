## なぜ変更するのか

RankingHistory::GetRankingSabunDataLogOfficial メソッド内で、BETWEEN 句の構文が不正な SQLite クエリが存在します。WHERE 句の前にカラム名が欠落しているため、LogOfficial.db へのクエリ実行時に SQL ロジックエラーが発生します。この問題により、特定の動画 ID (sm46256876, sm46270709) のランキングデータ取得が失敗し、公式チャンネルのランキング分析機能に影響が出ています。

## 変更内容

- RankingHistory.cs の BETWEEN 句の前に不足しているカラム名（集計日）を追加
- GetRankingSabunDataLogOfficial メソッド内の不正な SQL クエリを修正
- 日付範囲クエリのパラメータバインディングを適切に維持

## 機能一覧

### 新機能

なし

### 変更機能

- `ranking-data-retrieval`: BETWEEN 句を使用した日付範囲クエリの SQL 構文エラーを修正

## 影響範囲

- nicorankLib\Analyze\Official\RankingHistory.cs: 287行目
- LogOfficial.db のデータベースクエリ信頼性
- 公式チャンネルランキング分析機能