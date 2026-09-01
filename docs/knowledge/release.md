# リリース手順・ブランチ運用（release.md）

## ブランチ運用

- **main はリリース用（保護ブランチ）** — 常にリリース可能な状態を保つ。直接コミット禁止。`develop` 経由のマージ、またはユーザーの明示指示による直接マージのみ許可。
- **develop は開発本流** — 日常の開発はすべて `develop` までで完結する。`feature` ブランチの統合先。
- **feature/tXXX-* は develop から分岐 → develop へマージ**（`--no-ff`）。
- **hotfix も feature と同列** — 緊急修正であっても `develop` 経由が原則。`main` への直接修正が必要な場合もユーザーの明示指示が必須（AIの独自判断で `main` を操作しない）。
- **旧ブランチ整理の経緯** — 2026-06 に旧 `develop` / `release/v1.0.0` / `snapshotBugFix` 等を削除したが、ユーザー意図しない削除だったため本設計で `develop` を復活させた。
- origin（GitHub: n2daime/nicorank2019）と同期する。

### 日常開発フロー（feature → develop）

```powershell
# 1. develop を最新化
git checkout develop
git pull origin develop

# 2. feature ブランチを作成
git checkout -b feature/tXXX-kebab-case-description
# ... 作業・コミット ...

# 3. テスト・ビルド成功を確認（AGENTS.md §0 MUST 5）
$log = "$env:TEMP\opencode\dotnet_log.txt"
dotnet test UnitTest/UnitTest.csproj *> $log
$code = $LASTEXITCODE
if ($code -eq 0) { Write-Output "TEST_RESULT=SUCCESS" } else { Write-Output "TEST_RESULT=FAILED"; Get-Content $log -Tail 15 }
Write-Output "EXIT_CODE=$code"

# 4. develop へマージ（--no-ff）
git checkout develop
git pull origin develop
git merge --no-ff feature/tXXX-kebab-case-description -m "Merge branch 'feature/tXXX-...' into develop (#xxx)"
git branch -d feature/tXXX-kebab-case-description

# 5. develop 上で tasks.md / archive.md / knowledge を更新（AGENTS.md §2 完了ゲート）
```

### docs のみの変更は develop 直可

`README` / `AGENTS.md` / `docs` のみの変更は `develop` 直可（`feature` ブランチ不要）。ただし `main` 直は禁止（リリース時のみ例外）。ソース変更を含む場合はコード変更扱い（`feature` ブランチ必須）。

## タグ・リリースの慣習

- タグ形式: `vYYYYMMDD_<名前>`（例: `v20260603_nicorank` / `v20260603_snapshot_preview`）
- タグは `main` にのみ打つ。`develop` には打たない。

## リリース成果物（添付ファイル）のルール

GitHub Release に添付する zip の内容は以下を厳守する。**ユーザーの個人データ（DB / 設定）を上書きしない**ことが目的。`bin/Release` を丸ごと含めない（散らかりや一時ファイルの混入を防ぐため、以下に列挙するもののみを含める — ホワイトリスト方式）。

### 配布しないもの（念のため明記）

- **DBファイル（`*.db`）** — `依存ファイル/DB/*.db`（初期サンプル）も `bin/Release/DB/*.db`（個人の運用データ）も zip に含めない。DBは各ユーザーの個人管理であり、リリース時に上書きすると蓄積データが失われるため。
- **設定ファイル本体** — `nicorank.xml` / `config.json` / `cookie.txt` はそのまま含めない（`.org` にリネームして配布）。
- 上記以外でも、以下に列挙しないもの（`*.pdb` / `*.xml` / `Output/` / `last*.json` / `*.log` / `System.*.dll` 直下残骸 / 一時ファイル等）は含めない。

### 配布するもの（ホワイトリスト — 以下に列挙するもののみを含める）

#### パターンA: nicorank2019（.NET Framework 4.8 / 要lib）

- `nicorank2019.exe`
- `nicorank2019.exe.config` — `lib/` フォルダ参照用
- `nicorank.xml.org` — `依存ファイル/nicorank.xml` または `bin/Release/nicorank.xml` をリネームしたもの
- `lib\*.*` — `lib/` 配下すべて（`Microsoft.Data.Sqlite.dll` 等4件 + `lib/runtimes/win-{x64,x86,arm}/native/e_sqlite3.dll`）

#### パターンB: nicorank_SnapShot（.NET Framework 4.8 / 要lib）

- `nicorank_SnapShot.exe`
- `nicorank_SnapShot.exe.config`
- `lib\*.*` — 同上（`nicorank.xml` は使用しないため `.org` 不要）

#### パターンC: nicorank_oldlog（.NET 8.0 / DB参照なし / lib不要）

- `nicorank_oldlog.exe`
- `nicorank_oldlog.dll` — .NET Core系は `exe` と `dll` の両方が必要
- `nicorank_oldlog.runtimeconfig.json`
- `config.json.org` — `nicorank_oldlog/config.json.org`（配布元）をリネームせずそのまま同梱。ユーザーは初回のみ `config.json.org` を `config.json` にコピーして利用
- `cookie.txt.org` — `nicorank_oldlog/cookie.txt.org`（配布元）をそのまま同梱。同上
- ※ `nicorank_oldlog.deps.json` / `lib/` は含めない（推移的コピーのため）

> 初期ユーザー向けの `*.org → *` 自動コピー等のフォローは別Issueで対応。

### 作成時の注意

- `bin/Release` をそのまま zip 化しない。空の一時フォルダに必要なものだけをコピーしてから zip 化する（ホワイトリスト方式）。`DB/` 除外や `Output/` 除外のブラックリスト方式は使わない。
- 例（PowerShell）:

  ```powershell
  # パターンA: nicorank2019
  $ver = "20250903"
  $src = "nicorank2019/bin/Release"
  $dst = "nicorank2019_$ver.zip"
  $tmp = "$env:TEMP\nicorank_release_$ver"
  Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
  New-Item $tmp -ItemType Directory | Out-Null
  Copy-Item "$src\nicorank2019.exe" $tmp -Force
  Copy-Item "$src\nicorank2019.exe.config" $tmp -Force
  Copy-Item "$src\lib" "$tmp\lib" -Recurse -Force
  if (Test-Path "$src\nicorank.xml") { Copy-Item "$src\nicorank.xml" "$tmp\nicorank.xml.org" -Force }
  Compress-Archive -Path "$tmp\*" -DestinationPath $dst -Force

  # パターンB: nicorank_SnapShot
  $srcSnap = "nicorank_SnapShot/bin/Release"
  $dstSnap = "nicorank_SnapShot_$ver.zip"
  $tmpSnap = "$env:TEMP\nicorank_snap_release_$ver"
  Remove-Item $tmpSnap -Recurse -Force -ErrorAction SilentlyContinue
  New-Item $tmpSnap -ItemType Directory | Out-Null
  Copy-Item "$srcSnap\nicorank_SnapShot.exe" $tmpSnap -Force
  Copy-Item "$srcSnap\nicorank_SnapShot.exe.config" $tmpSnap -Force
  Copy-Item "$srcSnap\lib" "$tmpSnap\lib" -Recurse -Force
  Compress-Archive -Path "$tmpSnap\*" -DestinationPath $dstSnap -Force

  # パターンC: nicorank_oldlog（.NET 8.0 / lib不要）
  $srcOld = "nicorank_oldlog/bin/Release/net8.0"
  $dstOld = "nicorank_oldlog_$ver.zip"
  $tmpOld = "$env:TEMP\nicorank_oldlog_release_$ver"
  Remove-Item $tmpOld -Recurse -Force -ErrorAction SilentlyContinue
  New-Item $tmpOld -ItemType Directory | Out-Null
  Copy-Item "$srcOld\nicorank_oldlog.exe" $tmpOld -Force
  Copy-Item "$srcOld\nicorank_oldlog.dll" $tmpOld -Force
  Copy-Item "$srcOld\nicorank_oldlog.runtimeconfig.json" $tmpOld -Force
  Copy-Item "nicorank_oldlog/config.json.org" "$tmpOld\config.json.org" -Force
  Copy-Item "nicorank_oldlog/cookie.txt.org" "$tmpOld\cookie.txt.org" -Force
  Compress-Archive -Path "$tmpOld\*" -DestinationPath $dstOld -Force
  ```

- `.gitignore` で `bin/` / `DB/` は除外されているが、リリース成果物の除外は手動（上記手順）で行う。
- `nicorank_oldlog` の `config.json.org` / `cookie.txt.org` は `nicorank_oldlog/` 直下に git 管理する（`依存ファイル/` には置かない。`nicorank2019` の `PostBuildEvent: xcopy 依存ファイル\*.*` による混入を防ぐため）。実行時イベント（自動コピー）は設けない。

## リリース前チェックリスト

リリース前に以下をすべて確認する（AGENTS.md §2 のリリース後ゲートも参照）。

- [ ] `develop` の全変更が `main` に取り込む対象として意図通りか確認（`git log main..develop --oneline`）
- [ ] `docs/knowledge/` 配下の全ファイルを最新化（新しいクラス・依存関係・変更点）
- [ ] `docs/specs.md` の仕様を最新化（挙動の定義が変わった場合）
- [ ] `docs/tasks.md` の該当タスクに完了マーク（`develop` 上で完了済み）
- [ ] 関連する GitHub Issue の状態を確認（クローズ方針は `develop` 側の運用に従う）
- [ ] `develop` で `dotnet restore` + `dotnet test UnitTest/UnitTest.csproj` が全件 PASS
- [ ] `develop` で `bin\Release\lib\` に `Microsoft.Data.Sqlite.dll` 等 4件 + `lib\runtimes\win-{x64,x86,arm}\native\e_sqlite3.dll` が配置されていること
- [ ] `nicorank2019.exe.config` / `nicorank_SnapShot.exe.config` に `<loadFromRemoteSources enabled="true" />` が含まれていること（#26。旧 config が混入すると GitHub から DL した zip 展開時の MOTW で SQLiteCtrl のタイプ初期化が失敗する）
- [ ] `bin\Release\nicorank.xml` が `依存ファイル/nicorank.xml` と一致していること（PostBuildEvent の xcopy はビルド方式・タイミングによって反映が保証されない。廃止済み設定が残った古い版が zip に入った実例あり。差異があれば手動コピーしてから zip 化する）
- [ ] リリース成果物（zip）がホワイトリスト通りであること（パターンA: `nicorank2019.exe` / `nicorank.xml.org` / `nicorank2019.exe.config` / `lib\*.*` のみ、パターンB: `nicorank_SnapShot.exe` / `exe.config` / `lib\*.*` のみ、パターンC: `nicorank_oldlog.exe` / `dll` / `runtimeconfig.json` / `config.json.org` / `cookie.txt.org` のみ。`DB/*.db` / `*.org` でない設定本体 / `*.pdb` / `Output/` / `System.*.dll` 直下等が含まれていないこと。上記「リリース成果物のルール」参照）
- [ ] 実機で集計（週刊/中間/SP）が通ることの確認（`develop` のビルド成果物で確認）

## リリース手順（AIが実行する手順）

> **前提:** ユーザーから「`develop` を `main` にリリースして」という明示指示があったときのみ実行する。AIの独自判断で `main` を操作しない。

```powershell
# 0. ユーザーの明示指示を確認（この手順は指示があったときのみ実行）

# 1. develop と main を最新化（develop の未プッシュコミットがあれば先に push しておく）
git checkout develop
git push origin develop
git pull origin develop
git checkout main
git pull origin main

# 2. develop → main へマージ（--no-ff）
git merge --no-ff develop -m "Merge develop into main for release vYYYYMMDD_<名前>"

# 3. リリース前チェックリストをすべて確認（上記）

# 4. タグを打つ
git tag -a vYYYYMMDD_<名前> -m "vYYYYMMDD_<名前>: <変更内容の概要>"

# 5. push（ユーザーの指示があるときだけ push する — AGENTS.md §0 MUST 8）
git push origin main
git push origin vYYYYMMDD_<名前>

# 6. GitHub Release を作成（ノート本文は UTF-8 の md ファイルに書いて --notes-file で渡す。
#    PowerShell から --notes に本文を直接渡すとバッククォートがエスケープ文字として解釈され、
#    `n が改行に置換されて文字欠けする（2026-08-31 の初リリースで発生））
gh release create vYYYYMMDD_<名前> --title "vYYYYMMDD_<名前>" --notes-file "<ノートmdファイル>" "<リリースzip>"

# 7. main の内容を develop に同期（乖離防止）
git checkout develop
git merge --no-ff main -m "Sync develop with main after release vYYYYMMDD_<名前>"
git push origin develop

# 8. リリース後ゲート
#    - タグが main HEAD を指すこと（annotated tag は git rev-parse <tag>^{commit} でコミットを確認。
#      git rev-parse <tag> はタグオブジェクト自体の SHA を返す点に注意）
#    - main と develop の内容が一致すること（git diff main develop --stat が空）
#    - archive.md にリリース実績（タグ・Release URL・成果物・検証結果・判明した問題）を追記する
#      （docs のため develop 直可）
```

### Release ノートの記載指針

- 前回タグ以降の**全変更**を記載する（前回タグの確認: `git log v<前回タグ>..develop --oneline`）
- 導入手順は **「上書き更新する場合（既存ユーザー）」と「新規に導入する場合」に分離**する
- 旧環境の不要ファイルの削除指示を含める（例: 旧 SQLite ライブラリの `x64` / `x86` フォルダ、廃止した DB。**残っていても動作するものは「削除してかまわない」と表記**）
- 既存ユーザーの設定ファイルはそのまま使い続けられる旨を明記する（設定廃止時は「残っていても無視される」を添える）
- `exe` と `exe.config` は**セットで上書き**するよう案内する（`loadFromRemoteSources` は `exe.config` に含まれる。exe だけ差し替えて config が旧版のままだと、GitHub から DL した zip を展開した際の MOTW で `SQLiteCtrl` のタイプ初期化が失敗する — #26。旧版は DLL のプロパティ「許可する」または `Unblock-File` で回避可能）

### 補足

- `main` へのマージは `develop` からのみ行う。`feature` ブランチを直接 `main` にマージしない。
- タグ作成後の `develop` へのバックマージ（手順7）は `main` と `develop` の乖離を防ぐために必須。忘れると次回リリース時にコンフリクトする。
- 緊急の `hotfix` が必要な場合も、原則は `develop` で修正 → `develop → main` のリリース手順で対応する。`main` に直接 `hotfix` を当てる必要がある場合も、必ずユーザー指示を得てから `main` にコミットし、その後 `main → develop` へ同期する。

## 週刊ランキングの運用フロー（毎週）

```
土日: 中間集計 → 事前準備（紹介位置の決定など）
月曜: 週刊集計 → 動画作成 → ニコニコに投稿
     → ニコランWeb管理画面で情報を手動アップロード → ニコランWebサイト更新
```

- ニコランWeb（https://nicoranweb.com/）へのデータ提供は手動アップロード（自動連携なし）
- スナップショット取得（nicorank_SnapShot）は任意のタイミングで実行（引数ありでコンソールモード）

## 実行環境

| ツール | 用途 |
|---|---|
| Visual Studio 2026 Community | ビルド・デバッグ |
| dotnet SDK ≥ 8.0 | ビルド・テスト（nicorank_oldlog は net8.0、UnitTest は .NET 10 SDK でビルド） |
| .NET Framework 4.8 | ランタイム（nicorankLib / nicorank2019 / nicorank_SnapShot） |
