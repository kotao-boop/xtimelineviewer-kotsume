# xtv.exe — コマンドライン起動用ランチャー（#264）

`XTimelineViewer.exe`（.NET self-contained）を起動するための、**依存 DLL を持たない極小ネイティブ exe** です。

## なぜ必要か

winget の portable (ZIP) インストールは `PortableCommandAlias` を **symlink** で作成します。
しかし .NET self-contained の apphost は **symlink の場所を基準に DLL を探す**ため、本体
`XTimelineViewer.exe` を symlink 経由で直接実行すると DLL 解決に失敗します（ターミナルから
`xtimelineviewer` が起動できない）。

`xtv.exe` は依存 DLL を持たないため symlink 経由でも問題なく起動し、自分の**実体パスを
symlink 越しに解決**して、隣にある `XTimelineViewer.exe` を**正しい作業ディレクトリ**で
起動します。コマンドライン引数はそのまま本体へ転送します。

## 配布

- ZIP（GitHub リリース）に `XTimelineViewer.exe` と並べて同梱（CI: `.github/workflows/release.yml`）。
- winget マニフェスト（microsoft/winget-pkgs）の `NestedInstallerFiles` は、エイリアス
  `xtv` / `xtimelineviewer` の両方を **`RelativeFilePath: xtv.exe`** に向ける。
- Store(MSIX) 版は `Package.appxmanifest` の `appExecutionAlias`（#262）で対応済みのため、
  このランチャーは不要。

## ビルド（リリースごとに公開ソースから実行）

Visual Studio C++ Build Tools があるPowerShellで:

```powershell
.\build-launcher.ps1 -Architecture x64
.\build-launcher.ps1 -Architecture arm64
```

- 成果物：`build\x64\xtv.exe` / `build\arm64\xtv.exe`
- CIも同じスクリプトを使い、コミット済みバイナリには依存しない。
- `xtv.rc` の製品バージョンは `Release.ps1 -Version x.y.z` が更新する。
- `rc`：`xtv.rc`（`../../Assets/AppIcon.ico` を参照）からアイコンリソース `xtv.res` を生成し、exe に埋め込む（#270）。エクスプローラー／タスクバーで本体と同じアイコンが出る。
- `/MT`：CRT を静的リンク＝VC ランタイム DLL に非依存。
- `/SUBSYSTEM:WINDOWS`：コンソール窓を出さない。
- `/utf-8`：日本語コメントを含むソースを正しく読ませる。
- 依存は `SHELL32.dll` / `KERNEL32.dll`（いずれも OS 標準）のみ。
- 中間生成物と `xtv.exe` はコミットしない。GitHub Actionsの各リリース実行で生成する。

## アーキテクチャ

x64 ZIPにはx64ランチャー、arm64 ZIPにはarm64ランチャーを入れる。GitHub-hosted Windows Runnerの
Visual Studio Build Toolsを使い、x64ホストからそれぞれをビルドする。
