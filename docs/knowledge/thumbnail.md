# ニコニコ動画のサムネイルURL取得（thumbnail.md）

このドキュメントは、ニコニコ動画のサムネイルURLを取得する方法と、実測で確認した解像度・制限・注意点をまとめたものである。2026-11-14 にこのリポジトリの実行環境から実際にHTTP取得して確認した。外部仕様は予告なく変わるため、実装時は必ず再確認すること。

関連Issue: #56（EDサムネイル・ユーザーアイコンの直接ダウンロード機能）

## 1. 現状の実装

- `nicorankLib/api/NicoApi.cs` が `https://ext.nicovideo.jp/api/getthumbinfo/{動画ID}` を呼び、XML の `<thumbnail_url>` を `Ranking.ThumbnailURL` に保存している。
- `ResultImagegetMovieIcon` が `queue.irv` に、`ResultImagegetUserIcon` が `queue_UserIcon.irv` にダウンロードキューを書き、Irvine などの外部ツールが画像を取得する。
- そのため、アプリ内で直接ダウンロードする機能を追加するときは、既存の `ThumbnailURL`（130x100）をそのまま使うのではなく、必要に応じて高解像度URLを解決する必要がある。
- `ThumbnailURL` は `HiddenMovieDelete` と `RankingHistory` の `/video_deleted` 判定にも使われている。削除判定を壊さないよう、高解像度URLは別フィールドに持つ。

## 2. 実測した取得方法と解像度

| 方法 | 取得元 | 解像度 | キー | 備考 |
|---|---|---|---|---|
| getthumbinfo | `https://ext.nicovideo.jp/api/getthumbinfo/{ID}` の `<thumbnail_url>` | 130x100 | 不要 | 非公開API。既存実装で使用中 |
| getthumbinfo + `.M` | 上記URLの末尾に `.M` | 320x180 | 不要 | 2015年以降の動画で確認。2010年は404 |
| getthumbinfo + `.L` | 上記URLの末尾に `.L` | 360x270（4:3固定） | 不要 | 同上。16:9動画は上下に黒帯が入る（実測） |
| スナップショット検索API v2 | `https://snapshot.search.nicovideo.jp/api/v2/snapshot/video/contents/search` の `thumbnailUrl` | 130x100（`.L` 付与可） | 不要 | 公式API。高解像度は返さない |
| 視聴ページHTML | `https://www.nicovideo.jp/watch/{ID}` の `server-response` または `og:image` | 960x540 / 1280x720 / 405x720 | あり | Cookie不要。本命 |
| `v3_guest` JSON | `https://www.nicovideo.jp/api/watch/v3_guest/{ID}` | 同上 | あり | `nicosid` Cookie必須のため不採用 |

## 3. URLパターンの詳細

`{n}` は動画IDの数字部分（例: `sm47042567` なら `47042567`）、`{rand}` はサムネイル変更時に付く数字（無い動画もある）。

### 3.1 通常サムネイル（キー不要）

```
https://nicovideo.cdn.nimg.jp/thumbnails/{n}/{n}          # 130x100
https://nicovideo.cdn.nimg.jp/thumbnails/{n}/{n}.M        # 320x180
https://nicovideo.cdn.nimg.jp/thumbnails/{n}/{n}.L        # 360x270
https://nicovideo.cdn.nimg.jp/thumbnails/{n}/{n}.{rand}   # 130x100（サムネ変更あり）
https://nicovideo.cdn.nimg.jp/thumbnails/{n}/{n}.{rand}.L # 360x270
```

`getthumbinfo` の `thumbnail_url` は、サムネイル変更がある動画では `{n}.{rand}` まで返る。そこへ `.L` を付ければよい。

### 3.2 高画質サムネイル（キー必須）

視聴ページの `server-response` JSON の `data.response.$watchV4.data.video.thumbnail` に、次のURLが入る。

| フィールド | URL例 | 実解像度 | 備考 |
|---|---|---|---|
| `normal` | `https://nicovideo.cdn.nimg.jp/thumbnails/{n}/{n}.{rand}` | 130x100 | |
| `middle` | 同上 + `.M` | 320x180 | 無い動画あり |
| `large` | 同上 + `.L` | 360x270 | 無い動画あり |
| `player` | `https://img.cdn.nimg.jp/s/nicovideo/thumbnails/{n}/{n}.{rand}.original/a960x540l?key=...` | 960x540 | 2007年の動画でも取得可。4:3動画は左右黒帯 |
| `ogp` | `https://img.cdn.nimg.jp/s/nicovideo/thumbnails/{n}/{n}.{rand}.original/r1280x720l?key=...` | 1280x720 | 16:9向け。4:3動画では960x720などになることがある |
| `ogp`（別系統） | `https://goptim.video.nimg.jp/thumbnail/1280x720?b=p&bs=54&i={n}.{rand}&s=blur&key=...` | 1280x720 | 4:3/ショートはぼかし背景で16:9に埋める |
| `short` | `https://img.cdn.nimg.jp/s/nicovideo/thumbnails/{n}/{n}.{rand}.original/r405x720l?key=...` | 405x720 | ニコニコショート（`ss`）用。無い動画では null |

`ogp` は動画によって `img.cdn.nimg.jp` の `r1280x720l` と `goptim.video.nimg.jp` の2系統がある。どちらも1280x720だが、`goptim` は4:3やショートのときにぼかし背景で16:9を埋める。16:9動画では `player` とほぼ同一内容であることを画素比較で確認した（MAE=3.13）。

### 3.3 `.L` の年代制限（実測）

| 投稿年 | `.L` | 備考 |
|---|---|---|
| 2010 | 404 | `.M` も404 |
| 2015以降 | 200 | 360x270 |
| 2007（sm9） | 404 | ただし `player` 960x540 は取得可 |

古い動画では `.L` が存在しないため、**404時のフォールバックが必須**である。`player` / `ogp` は古い動画でも生成されていることが多い。

## 4. 視聴ページHTMLからの取得（Cookie不要）

`https://www.nicovideo.jp/watch/{ID}` を通常のUser-Agentで取得すると、次のメタタグにJSONが入っている。

```html
<meta name="server-response" content="{...}" />
```

`content` はHTMLエスケープされているので、`WebUtility.HtmlDecode` してからJSONとして読む。`$watchV4` は `$` を含むため、`JObject` のインデクサで参照する。

```csharp
var m = Regex.Match(html, "<meta name=\"server-response\" content=\"([^\"]*)\"");
var json = WebUtility.HtmlDecode(m.Groups[1].Value);
var thumb = JObject.Parse(json)["data"]["response"]["$watchV4"]["data"]["video"]["thumbnail"];
var player = (string)thumb["player"];
var ogp = (string)thumb["ogp"];
var shortUrl = (string)thumb["short"];
```

`og:image` だけなら次のようにも取れる。ただし `og:image` は `goptim` の1280x720 URLだけなので、`player` や `short` を使いたい場合は `server-response` を読む。

```csharp
var m = Regex.Match(html, "<meta[^>]+property=\"og:image\"[^>]+content=\"([^\"]+)\"");
```

- 視聴ページHTMLは**Cookieなし**で取得できる。トップページで `nicosid` を取る必要はない。
- レスポンスは約49KB。`v3_guest` のJSONは約12KBだが、Cookieが必要なので本アプリではHTMLを採用する。
- `server-response` が無い/パースできない場合は、`.L` へフォールバックする。

## 5. キー付きURLの注意

- `player` / `ogp` / `short` の `key` は必須。**サイズ部分だけ変えると400**になる（例: `a960x540l` を `a1280x720l` に変える、`r405x720l` を `r720x1280l` に変える）。
- `key` は同じ動画ならUser-Agentを変えても同一だった。取得から30分以上経過しても有効だったが、恒久保証はない。
- **キー付きURLをDBに長期保存しない**。必要なときに視聴ページから再取得する。
- 画像本体のダウンロードにCookieは不要。

## 6. 採用しなかった方法

### 6.1 `v3_guest`

```
GET https://www.nicovideo.jp/api/watch/v3_guest/{ID}?_frontendId=6&_frontendVersion=0&actionTrackId={10英数}_{13桁ミリ秒}
```

必須ヘッダは `X-Frontend-Id: 6`、`X-Frontend-Version: 0`、`Referer`、`Origin`、`X-Requested-With: XMLHttpRequest`、`Accept: application/json`。`data.video.thumbnail` は視聴ページと同じ形で返る。

ただし、**`nicosid` Cookie が無い状態でランダムな `actionTrackId` を送ると406**になる。トップページを1回取得してCookieを取れば動くが、外部仕様の対Bot挙動に依存するため、本アプリのサムネDL機能では採用しない。詳細を知りたい場合だけ、Cookie付きの実験用経路として扱う。

### 6.2 `nvapi`

`https://nvapi.nicovideo.jp/v1/videos?videoIds=...` は空配列、`/v1/search/video` は404で、サムネイル取得には使えなかった。

### 6.3 旧 `tn.smilevideo.jp`

廃止済み。`nicovideo.cdn.nimg.jp` へリダイレクトされる。

## 7. このアプリでの採用方針（Issue #56）

- **既定（360x270）**: `ThumbnailURL + ".L"`。404なら `ThumbnailURL`（130x100）。
- **高画質ON（1280x720）**: 視聴ページHTMLの `thumbnail.ogp` → `thumbnail.player`（960x540）→ `.L` → base。
- **ショート**: `thumbnail.short`（405x720）→ `ogp` → `player` → `.L` → base。
- **ユーザーアイコン**: `getthumbinfo` の `GetUserIconUrl()` をそのまま使う。高画質オプションの対象外。
- **取得間隔**: HTTPリクエスト間に0.5秒（`DownloadIntervalMs = 500`）。並列にしない。ニコ動への負荷を抑えるため。
- **失敗時**: 403/429はその画像をスキップ。404はフォールバック。タイムアウト/5xxは最大2回リトライし、それでも失敗ならスキップして処理を続ける。
- **削除判定**: `ThumbnailURL` は従来どおりbaseのまま残し、`/video_deleted` 判定を壊さない。

## 8. 実測例（2026-11-14）

| 動画ID | 投稿年 | `.L` | `player` | `ogp` | `short` |
|---|---|---|---|---|---|
| `sm9` | 2007 | 404 | 960x540（4:3は左右黒帯） | 1280x720（goptim） | null |
| `so11004625` | 2010 | 404 | 960x540 | 1280x720（goptim） | null |
| `sm26335920` | 2015 | 200 / 360x270 | 960x540 | 960x720（r1280x720l） | null |
| `sm47042567` | 2026 | 200 / 360x270 | 960x540 | 1280x720（goptim） | null |
| `ss47042739` | 2026 | 200 / 360x270 | 960x540 | 1280x720（goptim） | 405x720 |

## 9. 更新ルール

- ニコ動側のURLパターン・解像度・キー仕様が変わったら、このファイルを最初に更新する。
- 実測日と確認した動画IDを残す。Issue には仕様の長文を貼らず、このファイルへのリンクと今回のスコープだけを書く。
- `docs/specs.md` にはアプリの振る舞い（ボタン、出力先、ファイル名、フォールバック順）を、`docs/design.md` には設計判断（なぜ `v3_guest` を使わないか、なぜ0.5秒か）を書く。
