# UnitTest 環境セットアップ

## テスト実行コマンド
```powershell
dotnet test UnitTest\UnitTest.csproj --verbosity minimal
```

## 環境
- .NET Framework 4.8 ターゲット
- .NET 10 SDK でビルド・実行
- MSTest テストフレームワーク (MSTest.TestAdapter 3.5.2 + MSTest.TestFramework 3.5.2)
- モック: Moq 4.20.72
- テストプロジェクト: SDK-style csproj

## 重要: csproj は SDK-style を使用すること

### 背景
旧 `UnitTest.csproj` は old-style csproj（packages.config + Reference）で、.NET 10 SDK 上の
MSTest.TestAdapter 3.5.2 と組み合わせると testhost 起動時に StackOverflow が発生する。

### 解決策
SDK-style csproj に変換する。以下の設定が必須：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="3.5.2" />
    <PackageReference Include="MSTest.TestFramework" Version="3.5.2" />
    <PackageReference Include="MSTest.TestAdapter.ExternalAssemblies" Version="3.5.2" />
    <PackageReference Include="Moq" Version="4.20.72" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\nicorankLib\nicorankLib.csproj" />
  </ItemGroup>
</Project>
```

ポイント:
- `TargetFramework` は `net48`（.NET Framework 4.8）
- `<IsTestProject>true</IsTestProject>` を設定
- パッケージは `PackageReference` 形式で追加（packages.config は使わない）
- `MSTest.TestAdapter.ExternalAssemblies` が必要な場合がある（.NET Framework ターゲット時）

### 注意点
- SDK-style csproj の出力先は `bin\Debug\net48\`（old-style は `bin\Debug\`）
- フィクスチャファイルのコピー設定は出力先のパスに合わせる
- `app.config` は残してよい（binding redirect 用）

## フィクスチャファイル

テスト用データファイルは `UnitTest\Fixtures\` に配置。

```xml
<!-- csproj でのコピー設定例 -->
<ItemGroup>
  <None Update="Fixtures\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
  <None Update="Fixtures\nicorank.xml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <TargetPath>nicorank.xml</TargetPath>
  </None>
</ItemGroup>
```

- `Fixtures\nicorank.xml` → 出力直下の `nicorank.xml` としてもコピー（Config.GetInstance() がカレントディレクトリから読むため）
- `Fixtures\*.csv` → `Fixtures\` サブフォルダにコピー

## 既知の問題と対処

### 1. テスト実行時に StackOverflow
- 原因: old-style csproj + .NET 10 SDK の MSTest 互換性問題
- 対処: SDK-style csproj に書き換える

### 2. Config テストが失敗する（nicorank.xml が見つからない）
- 原因: `Config.GetInstance().Initilize()` がカレントディレクトリの `nicorank.xml` を読む
- 対処: 出力直下に `nicorank.xml` を配置する

### 3. TextUtil.ReadCsv の戻り値
- ファイル不在時、戻り値は `false` で `out` 引数は **空の List**（null ではない）
- `ReadCsv` 内で `rankingList = new List<Ranking>()` と初期化した後、`File.Exists` チェックで false を返す
- テストアサートは `Assert.IsNotNull(rankingList)` + `Assert.AreEqual(0, rankingList.Count)` とすること

### 4. nicorankLib の Costura.Fody
- テスト実行の StackOverflow とは無関係
- 削除しないこと

## テスト構成

```
UnitTest\
  UnitTest.csproj          # SDK-style csproj
  AGENTS.md                # このファイル
  Fixtures\
    nicorank.xml            # Config テスト用設定ファイル
    test_ranking.csv        # CSV読み取りテスト用データ
  nicorankLib\
    Util\
      UnitTestSQLiteCtrl.cs # SQLiteCtrl テスト
      UnitTestTextUtil.cs   # TextUtil テスト
    Analyze\
      UnitTestRanking.cs    # Ranking テスト
    output\
      UnitTestOutput.cs     # 出力テスト
    Common\
      UnitTestConfig.cs     # Config テスト
```

## 新規テスト追加の作法
1. `nicorankLib\` 以下に対象クラスと同じ名前空間パスでファイルを作成
2. 既存テストを参考にクラス・メソッドを記述
3. SQLite を使用するテストは `OpenInMemory()` でインメモリDBを作成する
