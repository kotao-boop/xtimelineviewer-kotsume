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
- **フォーカス（メイン1列＋サブ2段）**: 左にメインタイムライン（広め）、右にサブ2段（狭め）

### ↔ マウスドラッグによるカラム幅のリサイズ
各タイムラインの境界線（右端）をマウスでドラッグするだけで、横幅を直感的に伸ばしたり縮めたりできます。調整した幅は自動的に保存されます。

### 🌐 ツイート自動翻訳機能
各タイムライン右上に自動翻訳ボタン（`🌐 翻訳: ON/OFF`）を配置。海外のツイートをタイムライン上で直接日本語に翻訳して表示します。翻訳は初期状態でOFFです。初回利用時に、投稿本文がGoogleの翻訳用エンドポイントへ送信されることを画面で確認してから有効になります。

### 🔒 WebView2によるXの表示
Xの非公式APIやスクレイピングは使わず、Microsoft Edgeのブラウザエンジン（WebView2）でXのWebページを表示します。ただし、本アプリはX Corp.の公式クライアントではなく、利用時にはXの規約が適用されます。任意の翻訳機能はXとは別にGoogleの翻訳用エンドポイントを利用します。詳しくは[プライバシーポリシー](PRIVACY.md)を確認してください。

### ⚡ その他の充実した機能
- **マルチアカウント対応**: アカウントごとに異なるプロファイル（Cookie）で独立管理
- **ホーム自動更新**: スクロールが先頭にある時だけ、新着ツイートを自動で取り込み（読んでいる途中は勝手に動かない設計）
- **定期ハードリロード**: 指定分ごとの自動再読み込み
- **キーボードショートカット**: `Ctrl+1〜9` でのタイムライン切り替え、`Ctrl+N` での高速投稿、いいね・リポストなど
- **テーマ切り替え**: Windows システム連動 / ダーク / ライトテーマ対応

---

## ダウンロードとインストール

[ダウンロード案内](DOWNLOADS.md)から、お好みの形式を確認してご利用いただけます。

> [!WARNING]
> 現在公開中のv2.1.0は未署名です。署名済みと誤認しないよう、Windowsのファイルプロパティで確認してください。SignPath Foundationへ無料コード署名を申請するための準備を進めています。

| 形式 | ファイル名 | 説明 |
|---|---|---|
| **インストーラー版 (EXE)** | `XTimelineViewer-Kotsume-v2.1.0-Setup.exe` | ダブルクリックで実行する標準インストーラー。デスクトップアイコン作成、スタートメニュー登録、アンインストールに対応。 |
| **ポータブル版 (ZIP)** | `XTimelineViewer-Kotsume-v2.1.0-win-x64-Portable.zip` | 解凍して `XTimelineViewer.exe` を起動するだけで即座に使える自己完結パッケージ。USBメモリ等に入れて持ち運ぶことも可能です。 |

---

## 動作要件

- **OS**: Windows 10 バージョン 19041 以降 / Windows 11
- **アーキテクチャ**: 64bit (x64)
- **ランタイム**: 必要なコンポーネントはすべてアプリ本体に同梱されているため、事前の追加インストールは不要です。

---

## 使い方

### 1. 初回起動とログイン
アプリを起動するとオンボーディング画面（初期設定案内）が表示されます。画面の指示に従って X アカウントにログインすると、ホームタイムラインが自動的に追加されます。

### 2. タイムラインの追加
- ツールバー右側のメニュー（`三`）→［タイムラインを追加］から「ホーム」「通知」「ブックマーク」「リスト」をワンクリックで追加できます。
- ブラウザのアドレスバーアイコンや、検索結果の URL をアプリ画面にドラッグ＆ドロップすることでも追加可能です。

### 3. レイアウトの変更
ツールバー右側にある **レイアウト切替ボタン（🗖）** をクリックし、お好みのテンプレート（クラシック、2×2、2×3、上下分割、フォーカス）を選択します。

### 4. カラム幅の調整（クラシックモード）
タイムラインの右端境界線にマウスポインターを合わせると左右矢印カーソルに変わります。そのまま左ドラッグすることで幅を自由に伸縮できます。

---

## 主なキーボードショートカット

| ショートカット | 動作 |
|---|---|
| `Ctrl + N` | 新規投稿ダイアログを開く |
| `Ctrl + 1` 〜 `9` | 左から N 番目のタイムラインをアクティブ化 |
| `Ctrl + →` / `Ctrl + ←` | 右 / 左のタイムラインへフォーカス移動 |
| `Ctrl + Shift + →` / `←` | アクティブなタイムラインを左右に並べ替え |
| `Ctrl + F` / `F3` | 検索ボックスにフォーカス |
| `F5` | アクティブなタイムラインを再読み込み |
| `Home` / `End` | タイムラインの最上部 / 最下部へスクロール |

---

## 免責事項 (Disclaimer)

- 本ソフトウェアは個人が開発したオープンソースの非公式デスクトップクライアントです。
- X Corp. および Twitter との提携、承認、スポンサーシップは一切受けていません。
- 「X」および「Twitter」は X Corp. の商標または登録商標です。

## Code signing policy

**Status:** Application preparation in progress. The current v2.1.0 release is not code-signed.

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

- **Committers and reviewers:** [@kotao-boop](https://github.com/kotao-boop)
- **Approvers:** [@kotao-boop](https://github.com/kotao-boop)
- **Privacy policy:** [PRIVACY.md](PRIVACY.md)
- **Source and build configuration:** this public repository and [GitHub Actions workflows](.github/workflows/)
- **Release approval:** every production signing request requires an explicit human approval. Unsigned artifacts must not be described as signed.
- **Third-party binaries:** only binaries built from this project's public source are submitted for this project's signature. Existing signatures on third-party and system components are preserved and verified instead of being replaced.

---

## ライセンス・クレジット

- **ライセンス**: [MIT License](LICENSE)
- **原作者**: [daruyanagi](https://github.com/daruyanagi)（[Original XTimelineViewer Repository](https://github.com/daruyanagi/XTimelineViewer)）
- **カスタマイズ & 拡張**: Kotsume Project
- **プライバシー**: [プライバシーポリシー](PRIVACY.md)
