# コード署名・SignPath申請ランブック

最終更新日: 2026年8月26日

## 現在の状態

- GitHubで公開するv2.3.2のEXE・ZIPは未署名。Windowsの警告が出る可能性をREADME、ダウンロード案内、リリースノートで明記する。Microsoft Store版のMicrosoft署名とは区別する。
- SignPath Foundationへの申請準備中。
- READMEの署名状態、コード署名ポリシー、担当者、プライバシーポリシーを公開済みの差分として準備した。
- .NET本体とネイティブランチャーは、同じ製品名・製品バージョンを持つ。
- ネイティブランチャーはGitHub Actionsでソースからビルドし、コミット済みEXEを利用しない。
- SignPath承認前でも、利用者が未署名だと明確に分かり、テスト、最終SHA-256、GitHub Artifact Attestation、
  ライセンス同梱の確認に合格した成果物はGitHub Releaseへ公開できる。
- 手動実行では公開直前までの全工程を試験し、タグ実行だけがReleaseを新規作成する。公開済みReleaseや資産を
  `--clobber`で上書きしない。
- SHA-256とGitHub Artifact Attestationは、署名の有無にかかわらず、すべての適用対象ゲートに合格した
  最終成果物にだけ付与する。これらをコード署名や安全性の保証として説明しない。

SignPathの承認と実際の署名完了までは、成果物を「署名済み」と表示してはならない。

## 申請に記載するプロジェクト情報

| 項目 | 内容 |
|---|---|
| Project name | XTimelineViewer Kotsume Edition |
| Short name | xTV-Kotsume |
| Source repository | https://github.com/kotao-boop/xtimelineviewer-kotsume |
| Download page | https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/DOWNLOADS.md |
| License | MIT |
| Platforms | Windows 10/11, x64 and arm64 |
| Artifact types | Portable ZIP, PE/EXE, Inno Setup EXE installer |
| Build system | GitHub Actions, GitHub-hosted Windows runner |
| Maintainer | https://github.com/kotao-boop |
| Privacy policy | https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md |
| Security policy | https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/SECURITY.md |

## 申請用の説明案

> XTimelineViewer Kotsume Edition is a public MIT-licensed Windows desktop application that displays multiple
> X timelines through Microsoft Edge WebView2. It is a maintained derivative of daruyanagi/XTimelineViewer and
> preserves the upstream Git history and attribution. Kotsume Edition adds grid layouts, column resizing,
> explicit-consent translation and other usability features. Release artifacts are built from public source on
> GitHub-hosted Windows runners. First-party binaries are built in the workflow; third-party and Microsoft system
> components are not re-signed with the project certificate. Translation is disabled by default and requires an
> in-app disclosure before post text is sent to a Google translation endpoint.

## SignPath申請フォームへの入力案

2026年8月23日に実際の申請フォームを確認した。次の公開情報は、READMEとプライバシーポリシーを
`main`へ公開し、各URLがログアウト状態でも閲覧できることを確認してから入力する。

| フォーム項目 | 入力案 |
|---|---|
| Project Name | `XTimelineViewer Kotsume Edition` |
| Repository URL | `https://github.com/kotao-boop/xtimelineviewer-kotsume` |
| Homepage URL | `https://github.com/kotao-boop/xtimelineviewer-kotsume` |
| Download URL | `https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/DOWNLOADS.md` |
| Privacy Policy URL | `https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md` |
| Wikipedia URL | 空欄 |
| Tagline | `A Windows desktop app for viewing and arranging multiple X timelines side by side.` |
| Maintainer Type | `Individual maintainer(s)` |
| Build System | `GitHub Actions` |
| Company Name | 組織として申請しない場合は空欄 |

### Description

> XTimelineViewer Kotsume Edition is a public MIT-licensed Windows desktop application for displaying and
> arranging multiple X timelines through Microsoft Edge WebView2. It is a maintained derivative of
> daruyanagi/XTimelineViewer with preserved Git history and attribution. Release artifacts are built from
> public source on GitHub-hosted Windows runners. Optional translation is disabled by default and requires an
> in-app disclosure and user consent before visible post text is sent to a Google translation endpoint.

### Reputation

Kotsume Edition自体の実績と派生元の実績を混同しない。2026年8月23日のGitHub APIで、Kotsume Editionは
2026年8月22日公開、1リリース、リリース資産の合計ダウンロード3、Star 0、Fork 0だった。派生元の
`daruyanagi/XTimelineViewer`はStar 43、Fork 4、33リリース、リリース資産の合計ダウンロード5,637だった。
申請時には最新値へ更新し、次のように透明性を保って記載する。

> Kotsume Edition is newly published and does not yet have an independent track record: as of 2026-08-23 it
> has one release with 3 total asset downloads, 0 stars and 0 forks. It is a maintained derivative of
> daruyanagi/XTimelineViewer, whose public GitHub repository has 43 stars, 4 forks and 5,637 total release-asset
> downloads across 33 releases as of the same date. Upstream attribution and Git history are preserved. We are
> disclosing these figures separately and ask SignPath Foundation to confirm whether the derivative is eligible.

この実績の弱さは審査上の主要な不確実性である。数字を大きく見せたり、派生元の実績をKotsume Edition
自身の実績として記載したりしない。

## 本人が入力・確認する項目

- First Name、Last Name、Email
- Primary Discovery Channelと正確な発見元
- 必須のCode of Conduct同意
- 必須の個人情報保存・処理同意
- reCAPTCHA
- Submitボタンによる最終送信

「その他のSignPath情報を受け取る」同意は任意なので、希望しなければ選択しない。氏名・メールを
チャットやリポジトリへ保存せず、申請画面へ本人が直接入力する。

## 申請時に隠さず確認する事項

### 1. 派生版と未署名の上流リリース

このGitHubリポジトリは、Git履歴上は `daruyanagi/XTimelineViewer` を基礎にしているが、GitHubの
repository metadataでは `fork: false` である。READMEでは派生元を明示している。

2026年8月23日に上流v2.0.3の公開x64 ZIPを確認した時点では、`XTimelineViewer.exe` と `xtv.exe` は
Authenticode未署名だった。SignPath Foundationの「modified upstream」条件に照らし、この状態で
Kotsume Editionを署名対象として受け入れ可能か、申請時に明示的に質問する。

### 2. 単独メンテナーの役割

現状は `@kotao-boop` がcommitter/reviewer/approverを担当する。単独メンテナーが複数役割を持つ構成を
受け入れ可能か確認する。SignPathとGitHubの両方で多要素認証を有効にする。

### 3. Microsoft再頒布コンポーネント

自己完結ビルドにはWindows App SDK、WebView2 SDK、.NET Runtime等のMicrosoft再頒布コンポーネントが
含まれる。これらはプロジェクト証明書で再署名せず、System Librariesまたは既存の第三者署名付き
コンポーネントとして扱う。詳細は `docs/DEPENDENCY_AUDIT.md` を参照する。

## SignPath承認後に設定する秘密情報

次の値はGitHub SecretsまたはSignPath/GitHubの保護された設定へ保存し、リポジトリへコミットしない。

- `SIGNPATH_API_TOKEN`
- SignPath organization ID
- SignPath project slug
- Artifact configuration slug
- Test signing policy slug
- Release signing policy slug

SignPath GitHub Appを対象リポジトリへインストールし、SignPath側でGitHub.comをTrusted Build Systemとして
関連付ける。APIトークンは署名要求の送信に必要な最小権限だけを持たせる。

## 推奨する署名済みリリースの順序

1. タグがmain上のコミットを指すことと、タグ・csproj・ランチャーの版数一致を検証する。
2. ユニットテスト、JavaScript構文検査、x64/arm64ビルドを実行する。
3. `XTimelineViewer.exe`、`XTimelineViewer.dll` と各アーキテクチャの `xtv.exe` をSignPathへ送る。
4. 署名済みx64ツリーからInno Setupインストーラーを作る。
5. 外側のSetup.exeをSignPathへ送る。
6. 全ての第一者EXE/DLLについて署名、証明書チェーン、タイムスタンプを検証する。
7. SHA-256とGitHub Artifact Attestationを生成する。
8. 署名検証が全件成功した場合だけGitHub Releaseへ公開する。

未署名リリースでは手順3から6を「成功」と見せかけず、署名なしと明記する。その代わり、テスト、
第三者の既存署名、第一者メタデータ、ライセンス、最終SHA-256、Artifact Attestationを確認する。
未署名リリースの公開可否と、将来のSignPath署名申請の可否は別の判断として記録する。

公開ノートは `docs/RELEASE_NOTES_TEMPLATE.md` を使用し、署名状態、SHA-256、attestation、
プライバシー変更、既知の問題をリリースごとに明記する。

Inno Setup内部のファイルをSignPathが直接深い署名の対象にできるかは、Artifact Configuration作成時に
SignPathへ確認する。確認できない場合は、上記の二段階方式を使う。

## 署名対象と対象外

### SignPathで署名する第一者ファイル

- `XTimelineViewer.exe`
- `XTimelineViewer.dll`（実際のマネージドアプリコード）
- `xtv.exe`（x64）
- `xtv.exe`（arm64）
- `XTimelineViewer-Kotsume-vX.Y.Z-Setup.exe`

### プロジェクト証明書で再署名しないもの

- Microsoftまたは第三者の署名があるEXE/DLL
- .NET Runtime、Windows App SDK、WebView2等の再頒布ファイル
- ソースをこのチームが保守していない第三者バイナリ

Artifact Configurationでは、ファイル名のワイルドカードだけで全DLLを署名せず、第一者ファイルを
明示的に指定する。製品名 `XTimelineViewer Kotsume Edition` とリリース版数も制約として設定する。

## 公開前の検証例

```powershell
$files = @(
  'publish\x64\XTimelineViewer.exe',
  'publish\x64\XTimelineViewer.dll',
  'publish\x64\xtv.exe',
  'publish\arm64\XTimelineViewer.exe',
  'publish\arm64\XTimelineViewer.dll',
  'publish\arm64\xtv.exe',
  'dist\XTimelineViewer-Kotsume-vX.Y.Z-Setup.exe'
)

foreach ($file in $files) {
  $signature = Get-AuthenticodeSignature -LiteralPath $file
  if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate) {
    throw "署名またはタイムスタンプが不正です: $file ($($signature.Status))"
  }
}
```

証明書のSubjectは、Kotsume Projectではなく、SignPath Foundationになる予定である。承認前にSubjectを
推測して検証コードへ固定せず、SignPath側で発行された証明書を確認してから制約を追加する。

## OSSignの後続確認

OSSignは、アカウント・組織・プロジェクトについて最低6か月の活動実績と、利用者コミュニティを要求して
いる。リポジトリ作成日が2026年8月22日のため、継続的な活動がある場合でも、最短の再確認日は
2027年2月22日頃とする。SignPathが不成立の場合に再評価する。
