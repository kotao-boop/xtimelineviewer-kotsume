# XTimelineViewer Kotsume Edition (xTV-Kotsume)

X（旧 Twitter）の複数タイムラインを並列表示できる、軽量で高機能な Windows デスクトップクライアントです。

![XTimelineViewer Kotsume Edition アプリアイコン](Assets/Square150x150Logo.png)

> [!NOTE]
> 本ソフトウェアは、[daruyanagi 氏](https://github.com/daruyanagi)が開発したオープンソース「XTimelineViewer」をベースに、縦横グリッド分割レイアウト、手動ドラッグリサイズ、自動翻訳機能などを追加・強化した **Kotsume Edition** です。

---

## 主な特徴

### 🗖 多彩なレイアウトテンプレート（縦横グリッド分割）
大型モニター（4K、ウルトラワイド）や縦置きモニター、ノートPCなど、あらゆる画面環境に最適な配置をワンクリックで選択できます。
- **クラシック**: 従来の横スクロール型マルチカラム（ドラッグで幅を自由調整可能）
- **2 × 2 グリッド（4分割）**: 4つのタイムラインを均等に正方形配置（ホーム、通知、検索、リスト等）
- **2 × 3 グリッド（6分割）**: ウルトラワイド画面に最適な6画面ダッシュボード
- **上下 2 分割**: 縦置き（ピボット）モニター向けにタイムラインを上下に2段積み
- **集中表示**: 選んだタイムラインだけを一時的に大きく表示。終了すると直前の配置へ戻ります

### ↔ マウスとキーボードによるサイズ変更
現在の配置で動かせる境界線をマウスでドラッグし、列の幅や行の高さを直感的に調整できます。`Alt + Shift + 矢印キー`でも少しずつ変更でき、調整結果は自動的に保存されます。

### 🌐 ツイート自動翻訳機能
各タイムラインの三点メニューから自動翻訳を切り替えられます。必要な場合は設定で列ヘッダーに地球ボタンを表示できます。Xの画面や投稿を覆わず、海外のツイートをタイムライン上で直接日本語に翻訳して表示します。翻訳は初期状態でOFFです。初回利用時に、投稿本文がGoogleの翻訳用エンドポイントへ送信されることを画面で確認してから有効になります。列の詳細設定から同意をいつでも取り消せます。

### 🔒 WebView2によるXの表示
Xの非公式APIやスクレイピングは使わず、Microsoft Edgeのブラウザエンジン（WebView2）でXのWebページを表示します。ただし、本アプリはX Corp.の公式クライアントではなく、利用時にはXの規約が適用されます。任意の翻訳機能はXとは別にGoogleの翻訳用エンドポイントを利用します。詳しくは[プライバシーポリシー](PRIVACY.md)を確認してください。

### ⚡ その他の充実した機能
- **マルチアカウント対応**: アカウントごとに異なるプロファイル（Cookie）で独立管理
- **ホーム自動更新**: スクロールが先頭にある時だけ、新着ツイートを自動で取り込み（読んでいる途中は勝手に動かない設計）
- **定期ハードリロード**: 指定分ごとの自動再読み込み
- **キーボードショートカット**: `Ctrl+1〜9` でのタイムライン切り替え、`Ctrl+N` での高速投稿、いいね・リポストなど
- **タイムライン管理**: 名前の変更、表示・非表示、並べ替え、複製、削除を1画面で管理
- **ワークスペース**: 用途ごとのタイムライン構成とレイアウトを保存して切り替え
- **操作検索**: `Ctrl+K` から、タイムライン追加、レイアウト変更、設定などをすばやく実行
- **テーマ切り替え**: Windowsシステム連動、ライト、ダークに加え、サイバーパンク、ネオン・コントラスト、ミッドナイト・オーシャン、フォレスト、サクラから選択可能

---

## ダウンロードとインストール

[ダウンロード案内](DOWNLOADS.md)から、お好みの形式を確認してご利用いただけます。

> [!WARNING]
> Microsoft Store版はMicrosoftが署名して配布します。GitHubで配布するv2.6.0のEXE・ZIPはコード署名されていないため、Windowsで「不明な発行元」やSmartScreenの警告が表示される場合があります。実行前に、リリースに添付された`SHA256SUMS.txt`とGitHub Artifact Attestationでファイルを確認してください。これらは改ざん確認の助けになりますが、コード署名や安全性の保証そのものではありません。

| 形式 | ファイル名 | 説明 |
|---|---|---|
| **インストーラー版 (EXE)** | `XTimelineViewer-Kotsume-v2.6.0-Setup.exe` | ダブルクリックで実行する標準インストーラー。デスクトップアイコン作成、スタートメニュー登録、アンインストールに対応。 |
| **ポータブル版 (ZIP)** | `XTimelineViewer-Kotsume-v2.6.0-win-x64-Portable.zip` / `...-win-arm64-Portable.zip` | 解凍して `XTimelineViewer.exe` を起動するだけで使える自己完結パッケージ。CPUに合う版を選びます。 |

### Microsoft Store版の状況

Microsoft Store版は2026年8月27日に初回公開されました。次回は、固定レイアウトの重なり防止、集中表示からの復元、ワークスペース削除の確認、高DPI対応、境界線による縦横サイズ変更などを含むv2.6.0を、GitHub版での確認後に提出する予定です。Google／Appleログインがアプリ内で完了できない場合は、X用パスワードによる確実なログイン方法を画面内で案内します。

Store版のMSIXはMicrosoftが署名し、Microsoft Storeからインストールと更新を行います。GitHub Releasesで配布するEXE・ZIPとは別の配布経路であり、現在のGitHub版はコード署名されていません。

申請の詳しい記録は、[Microsoft Store提出状況](docs/MICROSOFT_STORE_PUBLISHING.md)を確認してください。

---

## 動作要件

- **OS**: Windows 10 バージョン 19041 以降 / Windows 11
- **アーキテクチャ**: 64bit (x64) / ARM64
- **.NET / Windows App SDK**: 自己完結パッケージに同梱
- **Microsoft Edge WebView2 Runtime**: Windows 11には標準搭載。Windows 10で入っていない場合は、Microsoft公式の[WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)が必要

---

## 使い方

### 1. 初回起動とログイン
アプリを起動すると、3段階の初期設定案内が表示されます。画面の指示に従って X アカウントにログインすると、ホームタイムラインが自動的に追加されます。まず画面を確認したい場合は［あとで設定］を選び、メイン画面から後でアカウントを追加できます。

### 2. タイムラインの追加
- ツールバー左側の［＋タイムライン］から「ホーム」「通知」「ブックマーク」「リスト」をワンクリックで追加できます。
- ブラウザのアドレスバーアイコンや、検索結果の URL をアプリ画面にドラッグ＆ドロップすることでも追加可能です。

### 3. レイアウトの変更
ツールバー右側にある **レイアウト切替ボタン（🗖）** をクリックし、お好みのテンプレート（クラシック、2×2、2×3、上下分割、自動配置、集中表示）を選択します。固定配置に収まらない数のタイムラインがある場合は、重なりを防ぐため自動配置へ切り替わります。

### 4. 列・行のサイズ調整
タイムラインの境界線にマウスポインターを合わせると、左右または上下の矢印カーソルに変わります。そのままドラッグすると、現在の配置に合った方向へサイズを変更できます。キーボードでは、対象のタイムラインを選んで`Alt + Shift + 矢印キー`を押します。

### 5. タイムラインとワークスペースの管理
右上のメニューから［タイムラインを管理］を開くと、分かりやすい表示名への変更、表示・非表示、並べ替え、複製、削除ができます。［ワークスペース］では、現在のタイムライン、並び順、表示状態、レイアウトを「仕事」「ニュース」などの名前で保存できます。

検索結果は画面右側のパネルに表示されます。元のタイムラインを見たまま内容を確認し、必要な検索だけ［タイムラインとして追加］で固定できます。

---

## 主なキーボードショートカット

| ショートカット | 動作 |
|---|---|
| `Ctrl + N` | 新規投稿ダイアログを開く |
| `Ctrl + 1` 〜 `9` | 左から N 番目のタイムラインをアクティブ化 |
| `Ctrl + →` / `Ctrl + ←` | 右 / 左のタイムラインへフォーカス移動 |
| `Ctrl + Shift + →` / `←` | アクティブなタイムラインを左右に並べ替え |
| `Alt + Shift + ←` / `→` / `↑` / `↓` | アクティブなタイムラインの境界をキーボードで調整 |
| `Ctrl + F` / `F3` | 検索ボックスにフォーカス |
| `Ctrl + K` | 操作検索を開く |
| `F1` | キーボードショートカット一覧を開く |
| `F5` | アクティブなタイムラインを再読み込み |
| `Home` / `End` | タイムラインの最上部 / 最下部へスクロール |

---

## 開発・貢献

ビルド方法、テスト、設計上の決まり、リリース手順は[開発ガイド](DEVELOPMENT.md)にまとめています。不具合の報告や修正に参加する前に、[行動規範](CODE_OF_CONDUCT.md)と[セキュリティポリシー](SECURITY.md)も確認してください。

---

## 免責事項 (Disclaimer)

- 本ソフトウェアは個人が開発したオープンソースの非公式デスクトップクライアントです。
- X Corp. および Twitter との提携、承認、スポンサーシップは一切受けていません。
- 「X」および「Twitter」は X Corp. の商標または登録商標です。

## Code signing policy

**Status:** Microsoft Store distribution is live and Store packages are signed by Microsoft. The GitHub v2.6.0 EXE/ZIP release remains unsigned while the separate SignPath application is deferred.

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

- **Committers and reviewers:** [@kotao-boop](https://github.com/kotao-boop)
- **Approvers:** [@kotao-boop](https://github.com/kotao-boop)
- **Privacy policy:** [PRIVACY.md](PRIVACY.md)
- **Source and build configuration:** this public repository and [GitHub Actions workflows](.github/workflows/)
- **Release approval:** every production signing request requires an explicit human approval. An unsigned release may be published only when its unsigned status is prominent and its final files have SHA-256 checksums and GitHub Artifact Attestations. Unsigned artifacts must never be described as signed.
- **Third-party binaries:** only binaries built from this project's public source are submitted for this project's signature. Existing signatures on third-party and system components are preserved and verified instead of being replaced.

---

## ライセンス・クレジット

- **ライセンス**: [MIT License](LICENSE)
- **第三者ライセンス**: [Third-party software notices](THIRD-PARTY-NOTICES.md)（配布物には正確なLICENSE/NOTICE一式を同梱）
- **原作者**: [daruyanagi](https://github.com/daruyanagi)（[Original XTimelineViewer Repository](https://github.com/daruyanagi/XTimelineViewer)）
- **カスタマイズ & 拡張**: Kotsume Project
- **プライバシー**: [プライバシーポリシー](PRIVACY.md)
