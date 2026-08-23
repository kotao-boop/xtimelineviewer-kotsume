# XTimelineViewer Kotsume Edition 開発ガイド

この文書では、XTimelineViewer Kotsume Editionの開発に必要な技術構成、ビルド方法、設計上の決まり、配布手順を説明する。本アプリは、複数のX（旧Twitter）タイムラインをMicrosoft Edge WebView2で表示するWindowsデスクトップアプリであり、Xの公式APIは使用しない。

## 技術構成

- UIにはWinUI 3とWindows App SDK 1.8を使用する。Windows App SDKは自己完結方式で配布物に含める。
- 実行基盤には.NET 8（`net8.0-windows10.0.26100.0`）を採用し、対応する最小Windowsバージョンは`10.0.19041.0`とする。
- 対応アーキテクチャはx64とARM64である。
- MVVMには`CommunityToolkit.Mvvm`、設定画面には`CommunityToolkit.WinUI.Controls.SettingsControls`を使用する。
- Xの表示にはMicrosoft Edge WebView2 Runtimeを使用する。
- NuGet依存関係はロックファイルで固定する。

正確なバージョンとビルド設定は、常に`XTimelineViewer.csproj`と`packages.lock.json`を正とする。

## ビルドとテスト

ロックファイルに記録されたバージョンで依存関係を復元する。

```powershell
dotnet restore XTimelineViewer.sln --locked-mode
```

x64のデバッグ版をビルドする。

```powershell
dotnet build XTimelineViewer.csproj -c Debug -p:Platform=x64
```

出力先は`bin/x64/Debug/net8.0-windows10.0.26100.0/win-x64/`である。デバッグ時は、ビルドした構成とアーキテクチャに対応する実行ファイルを起動し、古いビルドを誤って使わないようにする。

ARM64版をビルドするときは、WebView2のx64バイナリが混入しないよう、`PlatformTarget`と`EffectivePlatform`も指定する。

```powershell
dotnet build XTimelineViewer.csproj -c Release `
  -p:Platform=arm64 -p:PlatformTarget=arm64 -p:EffectivePlatform=arm64
```

単体テストを実行する。

```powershell
dotnet test XTimelineViewer.Tests/XTimelineViewer.Tests.csproj -c Release
```

翻訳拡張のJavaScriptを変更した場合は、少なくとも文法検査を実行する。

```powershell
node --check extensions/xtv-translator/content.js
node --check extensions/xtv-translator/background.js
```

実際にアプリを起動して基本画面を確認する場合は、`./ui-smoke.ps1`を実行する。このテストは、ツールバー、メニュー、設定画面などが表示されることをWindows UI Automationで確認し、結果のスクリーンショットを`test-screenshots/`へ保存する。Xへのログインが必要なタイムライン検査は、ローカルに`timelines.json`がある場合だけ実行される。

## コードの構成

`MainWindow`は機能ごとのpartialクラスに分割されている。

| ファイル | 主な役割 |
|---|---|
| `MainWindow.xaml.cs` | 初期化、共有フィールド、共通処理 |
| `MainWindow.Timeline.cs` | タイムラインペインの構築、設定、並べ替え、フォーカス移動 |
| `MainWindow.WebView2.cs` | WebView2の初期化、拡張機能、ホーム自動更新 |
| `MainWindow.Post.cs` | 投稿画面、画像・動画の保存 |
| `MainWindow.HardReload.cs` | 定期的な再読み込みと表示更新 |
| `MainWindow.Settings.cs` | 設定とプロファイル一覧の読み書き |
| `MainWindow.Search.cs` | 検索タイムライン |
| `MainWindow.Profiles.cs` | 複数プロファイルの管理 |
| `MainWindow.Theme.cs` | テーマの適用 |
| `MainWindow.Updates.cs` | GitHub Releasesを使った更新確認 |
| `MainWindow.Onboarding.cs` | 初回起動時の案内 |

WebView2の実行環境はプロファイル単位で作成し、`GetOrCreateProfileEnvAsync(profileId)`で共有する。設定とプロファイル一覧は`SettingsService`、タイムライン構成は`TimelineStore`が保存する。Microsoft Store版ではWindowsがアプリに割り当てるLocalStateフォルダーを使い、未パッケージ版では主に`%LOCALAPPDATA%\XTimelineViewer`を使用する。

## 開発上の決まり

### UIとアクセシビリティ

- UI文字列をC#へ直接埋め込まず、`Strings/ja-JP/Resources.resw`と`Strings/en-US/Resources.resw`へ追加して`R.Get("Key")`で取得する。
- 日本語版と英語版のリソースキーを常に一致させる。
- 設定項目の名前は、可能な限りコントロールの`Header`へ設定する。`Header`を持たないコントロールには`AutomationProperties.SetName`で読み上げ名を設定する。
- Segoe Fluent IconsなどのPUAグリフは、生の文字ではなく`\uXXXX`形式で記述する。
- キーボードだけでも主要な操作を完了できるようにする。

### 通信とプライバシー

- 新しい外部通信を追加するときは、送信先、送信する情報、実行条件、保存期間を明確にする。必要に応じて、利用者への説明と同意取得、`PRIVACY.md`の更新も行う。
- WebView2からWindows側の機能を呼び出すメッセージは、信頼できるXのHTTPSオリジンから届いたものだけを受理する。
- 投稿本文、認証Cookie、投稿ID、ユーザー名、検索語などを診断ログへ不用意に残さない。
- 外部URL、ファイル名、プロファイルID、WebView2メッセージは、使用前に形式と長さを検証する。

### 保存データ

- 設定ファイルが存在しない場合と、壊れて読み込めない場合を区別する。
- 読み込みに失敗した状態で、既存データの上書きや関連フォルダーの削除を行わない。
- ファイル保存中の中断に備え、一時ファイルを使った安全な置き換えと復旧手段を用意する。
- フォルダーを削除する前に、解決後の絶対パスがアプリの管理対象ディレクトリ内にあることを確認する。

### Gitとプルリクエスト

- 作業は`main`から分けたブランチで行い、プルリクエストを通してマージする。
- 実装前に関連Issueの本文とコメントを確認する。
- 製品コードを変更した場合は、x64・ARM64のビルド、単体テスト、関係する追加検査を行う。
- GitHub Actionsの`build-and-test`、`dependency-review`、CodeQLを確認してからマージする。
- 関係のない変更を同じプルリクエストへ混ぜない。

## 配布とリリース

### GitHub版

現在のGitHubリリースでは、未署名のインストーラー（`Setup.exe`）と、x64・ARM64向けのポータブルZIPを公開する。Kotsume Editionのwingetパッケージは、現時点では公開していない。

`.github/workflows/release.yml`は、`main`に含まれる`v*`タグを起点として、ビルド、テスト、SHA-256チェックサムの作成、GitHub Artifact Attestationによる来歴証明、GitHubリリースの公開を行う。手動実行では成果物の検証だけを行い、リリースは公開しない。既存のリリースや添付ファイルは上書きしない。

SignPathの承認と実際の署名が完了するまでは、GitHub版を署名済みと表示してはならない。未署名版では`UNSIGNED-RELEASE.txt`、SHA-256チェックサム、Artifact Attestationを維持する。詳しい署名手順は`docs/CODE_SIGNING_RUNBOOK.md`を参照する。

### Microsoft Store版

Microsoft Store版は未公開であり、提出に向けて準備中である。`.github/workflows/store-package.yml`は提出候補を手動で作成するが、次の条件を満たすまで公開または提出しない。

- Partner Centerで予約した正式な製品ID（Identity）を設定する。
- x64版とARM64版の両方でWindows App Certification Kit（WACK）を実行する。
- プライバシー、外部通信、WebView2拡張機能、`runFullTrust`に関係する最新のStoreポリシーを確認する。
- `docs/store/SUBMISSION_CHECKLIST.md`の必須項目を完了する。

Store提出の詳しい手順は`docs/MICROSOFT_STORE_PUBLISHING.md`を参照する。

GitHub版とMicrosoft Store版では、署名と更新の経路が異なる。未パッケージ版はGitHub Releasesで最新版を確認し、Storeから配布するMSIX版の更新はMicrosoft StoreまたはWindows Updateに任せる。

### ランチャーとパッケージ

GitHub配布物には、C++で実装した小型ランチャー`xtv.exe`を同梱する。`xtv`を主なコマンド名とし、`xtimelineviewer`は後方互換のために維持する。

`Package.appxmanifest`は、自己完結ビルドとMicrosoft Store向けパッケージの両方で必要になる。`StoreLogo.png`はマニフェストとアプリ内の情報画面で使用するため、削除しない。

## 関連文書

- [README](README.md)
- [プライバシーポリシー](PRIVACY.md)
- [セキュリティポリシー](SECURITY.md)
- [依存関係とライセンスの監査](docs/DEPENDENCY_AUDIT.md)
- [GitHubリポジトリの保護手順](docs/GITHUB_REPOSITORY_HARDENING.md)
- [コード署名・SignPath申請手順](docs/CODE_SIGNING_RUNBOOK.md)
- [Microsoft Store提出手順](docs/MICROSOFT_STORE_PUBLISHING.md)
- [Microsoft Store提出チェックリスト](docs/store/SUBMISSION_CHECKLIST.md)
