# Microsoft Store掲載更新案 v2.5.0

最終更新日: 2026年8月31日

## 方針

利用者が検索しそうな言葉を自然な日本語・英語で説明し、公式アプリと誤認させないことを優先する。
製品名 `XTimelineViewer Kotsume Edition` は維持し、短い説明の冒頭で
`X（旧Twitter）` / `X (formerly Twitter)` と明示する。

Microsoft公式資料では、検索キーワードは最大7件、各40文字、合計21語までで、無関係な語や
自社以外のブランド名をキーワード欄へ入れないよう案内されている。このため `Twitter` は隠し
キーワードへ入れず、アプリの対象を説明する本文で事実に沿って使用する。

- [Store掲載情報の公式仕様](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-and-edit-store-listing-info)
- [キーワード欄の公式仕様](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/add-additional-information)
- [カテゴリの公式仕様](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/categories-and-subcategories)
- [獲得分析の公式仕様](https://learn.microsoft.com/en-us/windows/apps/publish/analyze-app-performance/acquisitions-report)

## 現在の確認結果

- 公開ページの短い説明は `X` のみで、`Twitter` / `ツイッター` を含まない。
- 長い説明には `X（旧Twitter）` が入っている。
- 公開スクリーンショットは1枚。Microsoftは対応デバイス種別ごとに4枚以上を推奨している。
- 公開ページに「機能」の箇条書きが表示されていない。
- 2026年8月31日の日本向けStore Web検索で `Twitter` の上位40件に本アプリは表示されなかった。
- 主カテゴリの「ソーシャル」はアプリの対象に合っている。

検索順位は利用者、地域、時刻、Store側の変更で変わるため、この順位は調査時点の基準値として扱う。

## 日本語

### 製品名

`XTimelineViewer Kotsume Edition`

製品名へ `Twitter` を追加しない。現在の利用者が同じアプリだと認識できる名前を保ち、X Corp.の
公式製品と誤認される危険を避ける。

### 短い説明

> X（旧Twitter）のホーム・通知・検索・リストを、1つの画面に複数列で並べて見渡せるWindows向け非公式タイムラインビューアー。自動整列、一時非表示、集中表示、ワークスペース保存、自動翻訳に対応します。

### 説明

> XTimelineViewer Kotsume Editionは、X（旧Twitter）の複数のタイムラインやページを、1つのウィンドウに見やすく並べて表示するWindows向けの非公式ビューアーです。
>
> ホーム、通知、検索結果、リストなどを複数列で同時に確認できます。上部のワークスペースタブから用途ごとの構成を切り替え、各列の幅と高さをドラッグで調整できます。使わない列の一時非表示、選んだ列への集中表示、残った列の自動整列、新着件数の確認にも対応しています。
>
> 統一された列追加画面と高度な検索条件ビルダーにより、必要な情報をすばやく追加できます。設定とワークスペースは、ログイン情報を含めずにバックアップ・復元できます。キーボードショートカット、操作検索、ダーク／ライトテーマ、任意の画像へ切り替えるボスモードも利用できます。
>
> 本アプリはX Corp.およびTwitterの公式・承認アプリではありません。XのページはMicrosoft Edge WebView2で表示します。本アプリがEdgeのCookieを読み取ったり、コピーしたりすることはありません。
>
> 自動翻訳は初期状態でオフです。利用者が画面の説明に同意した場合だけ、表示中の投稿本文をGoogleの翻訳用エンドポイントへ送信します。同意は列の詳細設定からいつでも取り消せます。保存するデータや通信先については、プライバシーポリシーをご覧ください。

### 製品の機能

Partner Centerでは行頭の記号を付けず、1項目ずつ入力する。

1. X（旧Twitter）の複数タイムラインを1画面に表示
2. ホーム・通知・検索・リストを統一画面から追加
3. ワークスペースタブで用途ごとの構成を切り替え
4. 列の自動整列・集中表示・一時非表示
5. 列の幅と高さをドラッグで調整
6. 各列の新着件数とクイック操作
7. 高度な検索条件ビルダー
8. 同意制の投稿翻訳
9. 設定とワークスペースのバックアップ・復元
10. x64／ARM64、ダーク／ライトテーマ対応

### 検索キーワード候補

1. `マルチカラム`
2. `複数タイムライン`
3. `タイムライン整理`
4. `SNSビューアー`
5. `ソーシャルメディア`
6. `ワークスペース`
7. `投稿翻訳`

`Twitter`、`ツイッター`、`TweetDeck` は他社ブランドのため、隠しキーワード欄には入力しない。
短い説明と本文の `X（旧Twitter）` で検索意図を拾う。

### このバージョンの新機能

> 複数タイムラインをより速く整理できるようになりました。ワークスペースタブ、統一された列追加画面、高度な検索条件、各列の新着件数とクイック操作を追加しました。列の自動整列、一時非表示、集中表示、幅・高さのドラッグ調整にも対応しています。自動翻訳ボタンはXのページを覆わない列ヘッダーへ移動しました。設定画面も広くし、小さい画面では自動的に収まるよう改善しました。

## English

### Product name

`XTimelineViewer Kotsume Edition`

### Short description

> A Windows multi-column viewer for X (formerly Twitter). Arrange Home, notifications, searches, and lists in one workspace, then resize, focus, hide, restore, and translate timelines with quick column controls. Not affiliated with X Corp.

### Description

> XTimelineViewer Kotsume Edition is an unofficial Windows viewer for displaying multiple X (formerly Twitter) timelines and pages side by side in one window.
>
> View Home, notifications, search results, and lists at the same time. Switch layouts from workspace tabs, resize each column in both directions, temporarily hide unused columns, focus on one column, automatically arrange the remaining columns, and see new-item counts at a glance.
>
> A unified column creator and advanced search builder make it easier to add the information you need. Back up and restore settings and workspace layouts without including sign-in credentials. Keyboard shortcuts, command search, light and dark themes, and a user-selected boss-mode image are also available.
>
> This app is not an official or endorsed app of X Corp. or Twitter. X pages are displayed with Microsoft Edge WebView2. The app does not read or copy Microsoft Edge cookies.
>
> Automatic translation is off by default. Visible post text is sent to Google's translation endpoint only after the user accepts the on-screen explanation. Consent can be withdrawn from the column settings at any time. See the privacy policy for details about communications and locally stored data.

### Product features

1. Multiple X (formerly Twitter) timelines in one window
2. Unified creator for Home, notifications, searches, and lists
3. Workspace tabs for task-specific layouts
4. Auto arrange, focus, and temporarily hide columns
5. Drag to resize columns horizontally and vertically
6. New-item counts and quick column actions
7. Advanced search query builder
8. Opt-in post translation
9. Credential-free settings and workspace backup
10. x64 and ARM64 with light and dark themes

### Keyword candidates

1. `multi column timeline`
2. `social media viewer`
3. `timeline dashboard`
4. `workspace organizer`
5. `multiple timelines`
6. `post translator`
7. `desktop social client`

### What's new in this version

> Organize multiple timelines faster with workspace tabs, a unified column creator, advanced search conditions, new-item counts, and quick actions in every column header. Columns can now be automatically arranged, temporarily hidden, focused, and resized in both directions. The automatic translation control has moved into the native column header so it no longer covers X content. The Settings window is also larger and automatically fits smaller displays.

## カテゴリ

- 主カテゴリ: `ソーシャル` / `Social`
- 副カテゴリ候補: `仕事効率化` / `Productivity`

主カテゴリは交流サービスを表示する性質に合う。副カテゴリは複数の情報を整理して効率よく確認する
用途に合うため追加する。

## スクリーンショット計画

Microsoftの推奨に合わせ、最低4枚を用意する。個人名、ハンドル、メールアドレス、通知内容、非公開
投稿を写さない。公開情報を使う場合も、投稿者の表示がStore紹介に必要な範囲を超えないようにする。

1. 3列のタイムラインとワークスペースタブ
2. 列の新着件数、翻訳、集中表示、一時非表示のクイック操作
3. 統一された列追加画面と高度な検索条件
4. 広くなった設定画面とバックアップ機能

## 公開後の測定

Partner Centerの獲得レポートで、更新前後28日を比較する。

- Microsoft Store on Windowsからのページ表示数
- ページ表示からインストールへの転換率
- インストール成功率
- 初回起動数
- 日本市場と英語市場の差
- パッケージ版 `2.5.0.0` の新規インストール数

順位だけでなく、検索結果からページを開いてもらえるか、ページを見た人がインストールするかを
分けて評価する。
