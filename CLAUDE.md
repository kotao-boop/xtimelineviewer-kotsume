# XTimelineViewer Kotsume Edition — 開発ガイド

複数の X（旧 Twitter）タイムラインを WebView2 ペインで横並び表示する Windows デスクトップアプリ。X の Web ページを細長い WebView2 に並べているだけで、公式 API は使わない。

## 技術スタック

- **WinUI 3 / Windows App SDK 1.8**（`WindowsAppSDKSelfContained` = 自己完結）、**.NET 8**（`net8.0-windows10.0.26100.0`、最小 OS 10.0.19041.0）
- ターゲット: **x64 / arm64**
- MVVM: `CommunityToolkit.Mvvm`、設定 UI に `CommunityToolkit.WinUI.Controls.SettingsControls`
- 描画コンテンツ: `Microsoft.Web.WebView2`（Edge Dev/WebView2 Runtime を利用）

## ビルド・実行

```powershell
dotnet build XTimelineViewer.csproj -c Debug -p:Platform=x64
```
出力: `bin/x64/Debug/net8.0-windows10.0.26100.0/win-x64/XTimelineViewer.exe`

- デバッグ時は、直前にビルドした構成・アーキテクチャーと、起動する exe の出力先を必ず一致させる。古い exe を誤って起動しない。
- テスト: `dotnet test XTimelineViewer.Tests/XTimelineViewer.Tests.csproj`（xUnit）。CI（`ci.yml`）で自動実行される。
- **起動スモークテスト**: `.\ui-smoke.ps1`（#346）。実際に exe を起動し、winapp CLI の UI Automation で
  ツールバー・メニュー・設定ウィンドウが出ることだけを確認する。CI では別ジョブ `ui-smoke` として
  `continue-on-error` 付きで実行し、スクリーンショットを artifact に残す。**UI の些細な変更で落ちて
  保守されなくなるのを避けるため、意図的に「壊滅的に壊れていないこと」だけに絞っている**（旧 `ui-tests.ps1`
  は旧 UI 前提のまま放置され CI でも動いていなかったので置き換えた）。
  ペインの検査（番号バッジが 1..N の連番か）も含むが、**`timelines.json` があるときだけ**
  実行される。CI ランナーには X ログインもタイムライン設定も無いのでペインは 0 件になり、
  この部分は実質ローカル実行専用。
- **構造ドリフトテスト**: `XTimelineViewer.Tests/TimelinePaneStructureTests.cs`。
  ペインを消す経路が 2 つあり、両方が同じ後始末をしているかをソースの文字列走査で固定する。
  ペイン単位の辞書を追加したら、このテストの一覧にも追加すること（#359 / #362 の再発防止）。
  UI 依存が無いので CI でも効く。

## アーキテクチャ

- **`MainWindow` は機能ごとに分割した partial クラス**:
  - `MainWindow.xaml.cs` … 初期化・フィールド・共通処理（`ShowDialogAsync` など）
  - `MainWindow.Timeline.cs` … ペイン UI 構築、⚙ 設定ダイアログ、番号バッジ、フォーカス移動
  - `MainWindow.WebView2.cs` … WebView2 初期化、拡張機能読み込み、ホーム自動更新の JS 注入
  - `MainWindow.Post.cs` … 投稿ダイアログ（プリロード、アカウント切替、ESC/Ctrl+Enter 制御）
  - `MainWindow.HardReload.cs` … 定期ハードリロード（#49）と UI 更新タイマー
  - `MainWindow.Settings.cs` / `.Search.cs` / `.Profiles.cs` / `.Theme.cs` / `.Updates.cs` / `.Onboarding.cs`
- **WebView2 環境はプロファイル単位**: `GetOrCreateProfileEnvAsync(profileId)` が `CoreWebView2Environment` を生成・キャッシュ。設定とプロファイル一覧は `SettingsService`、WebView2 のプロファイルデータは `GetProfilesDataDir()` が決める保存先で管理する。
- **MVVM**: `SettingsViewModel` が `AppSettings` をラップ。設定ウィンドウの `SettingsChanged` で各 WebView / タイマーへ即時反映。
- **ホーム自動更新（#207）**: `x.com/home` に JS を注入し、先頭付近かつ非入力・非検索時のみ新着を取り込む。

## 規約（重要）

- **UI 文字列をコードに直接埋め込まない**。`Strings/ja-JP/Resources.resw` と `Strings/en-US/Resources.resw` に追加し、`R.Get("Key")` で参照する。**両言語のキーは常に一致**させる。
- **言語切り替え**: unpackaged では `PrimaryLanguageOverride`（MSIX パッケージ ID 必須）が使えないため、resw を直接パースする方式（#117）。
- **コード生成の設定 UI ではラベルを `Header` に持たせる**。別立ての `TextBlock` を並べると UI Automation 上でコントロールと関連付かず、Narrator で「何の設定か」が伝わらない。`NumberBox` / `ToggleSwitch` / `ComboBox` には `Header` があるのでそれを使う（#344）。`Header` を持たないコントロールは `AutomationProperties.SetName` で名前を与える。
- **PUA グリフ**（Segoe Fluent Icons 等）は `\uXXXX` エスケープで書く。リンターが生グリフに変換すると Edit で扱いづらい。
- イシュー着手時は **作業ブランチを切る**。担当者を設定する場合は、このリポジトリの現在のメンテナーを指定する。実装前に **コメントまで読む**（`gh issue view <n> --comments`）。

## 配布・リリース

- **GitHub Releases**: 現在は、未署名の `Setup.exe` と x64 / ARM64 の Portable ZIP を公開する。Kotsume Edition 用の winget パッケージは、現時点では公開していない。
- `.github/workflows/release.yml` は、`main` に含まれる `v*` タグでビルド・テスト・SHA-256 作成・GitHub Artifact Attestation・GitHub Release 公開を行う。手動実行は成果物の検証用で、Release は公開しない。既存の Release と資産は上書きしない。
- **未署名の表示**: SignPath の承認と署名が完了するまで、GitHub 版を署名済みと表示してはならない。`UNSIGNED-RELEASE.txt`、SHA-256、Artifact Attestation を維持する。署名方針は `docs/CODE_SIGNING_RUNBOOK.md` を参照する。
- **Microsoft Store**: Store 配布は廃止しておらず、現在は提出準備中。`.github/workflows/store-package.yml` は提出候補を手動作成するが、Partner Center の正式 Identity、WACK、ポリシー確認が完了するまで公開・提出しない。手順は `docs/MICROSOFT_STORE_PUBLISHING.md` と `docs/store/SUBMISSION_CHECKLIST.md` を参照する。
- **更新経路を混同しない**: GitHub 版と Store 版では、署名と更新の経路が異なる。未パッケージ版は GitHub Releases の最新版を確認し、Store/MSIX 版の更新は Microsoft Store / Windows に任せる。
- **コマンドライン起動**: GitHub 配布物には C++ の極小ランチャー **`xtv.exe`**（`tools/launcher/`、依存 DLL ゼロ）を同梱する。`xtv` が主、`xtimelineviewer` は後方互換。
- **arm64 の落とし穴**: arm64 ビルド/publish には必ず **`-p:EffectivePlatform=arm64`** を渡す。付けないと WebView2 SDK がビルドホスト RID(win-x64) を見て x64 の `Microsoft.Web.WebView2.Core.dll` を arm64 パッケージに混入させ、arm64 で `BadImageFormatException` になる（#267）。
- `Package.appxmanifest` は自己完結ビルドに必要なため残す。`<Logo>StoreLogo.png</Logo>` は appx 必須要素かつ `AboutPage` で使用するので削除しない。
