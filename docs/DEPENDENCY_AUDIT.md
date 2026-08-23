# 署名対象・依存コンポーネント監査

監査日: 2026年8月23日

この文書は、SignPath Foundation申請とArtifact Configuration作成のための技術監査である。法的判断を
断定するものではない。依存関係を更新したら、`obj/project.assets.json` と各NuGetパッケージ内の
ライセンス文書を再確認する。

## 第一者ソース

- Kotsume Editionの変更: この公開リポジトリ
- ネイティブランチャー: `tools/launcher/xtv.cpp`, `xtv.rc`, `build-launcher.ps1`
- 翻訳拡張: `extensions/xtv-translator/`
- 派生元: `daruyanagi/XTimelineViewer`（MIT、Git履歴とREADMEで表示）

第一者の署名対象は、公開ソースからGitHub Actions内で生成した `XTimelineViewer.exe`、`xtv.exe`、
Setup.exeに限定する。

## NuGet依存関係の確認結果

ローカルの復元済みNuGetメタデータで確認した主な項目:

| パッケージ群 | 確認したライセンス表示 | 署名時の扱い |
|---|---|---|
| CommunityToolkit.Mvvm / Common | MIT | 公開OSS依存。生成された第一者アセンブリ以外を再署名しない |
| CommunityToolkit.WinUI.* | MIT | 公開OSS依存。第三者DLLをプロジェクト証明書で再署名しない |
| System.Security.Permissions | MIT | .NET OSS依存 |
| System.Numerics.Tensors | MIT | .NET OSS依存 |
| System.Windows.Extensions | MIT | .NET OSS依存 |
| Microsoft.Web.WebView2 SDK | パッケージ内LICENSE.txt（BSD 3-Clause相当の条件文） | WebView2/Edgeの第三者・System Libraryとして扱う |
| Microsoft.WindowsAppSDK.* | Microsoft Software License Terms、再頒布可能ファイルを含む | Microsoft再頒布/System Libraryとして扱い、再署名しない |
| Microsoft.Windows.SDK.BuildTools.* | Microsoft SDK license | ビルド時依存。第三者ツール・ファイルを再署名しない |

## 実ファイルの署名方針

自己完結publishには、多数のMicrosoft/.NETコンポーネントが含まれる。ファイル拡張子だけで一括署名すると、
第三者のEXE/DLLへSignPath Foundationの署名を付けてしまう危険がある。そのためArtifact Configurationでは
第一者ファイルをパスで明示する。

既存のMicrosoft署名があるファイルは `authenticode-verify` 相当の検証対象とし、署名を置き換えない。
未署名の第三者DLLも、第一者証明書で署名せず、そのまま含めるか、必要性とライセンスを個別確認する。

## 追加確認が必要な境界

- Windows App SDKの自己完結再頒布ファイルを、SignPathがSystem Librariesとして受け入れるか
- MIT派生版で、現在の上流GitHubリリースが未署名である場合のmodified upstream条件
- Inno Setup EXEの内部ファイルを深い署名対象として扱えるか

これらは推測で「適合」とせず、SignPath申請またはArtifact Configuration作成時に確認する。
