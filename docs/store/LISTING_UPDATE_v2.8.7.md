# Microsoft Store掲載情報 v2.8.7

最終更新日: 2026年10月25日

Partner Centerへ入力する日本語・英語の掲載情報を、このファイルで管理する。v2.8.7では、
.NET、Windows App SDK、WebView2 SDKを更新し、自己完結型配布物とStore提出候補を再ビルドした。

## 日本語

### 製品名

`XTimelineViewer Kotsume Edition`

### 短い説明

> X（旧Twitter）のホーム・通知・検索・リストを、1つの画面に複数列で並べて見渡せるWindows向け非公式タイムラインビューアー。自動整列、一時非表示、集中表示、ワークスペース保存、自動翻訳に対応します。

### 説明

> XTimelineViewer Kotsume Editionは、X（旧Twitter）の複数のタイムラインやページを、1つのウィンドウに見やすく並べて表示するWindows向けの非公式ビューアーです。
>
> ホーム、通知、検索結果、リストなどを縦横に配置し、列の幅と高さをドラッグで調整できます。使わない列を一時的に隠す、選んだ列だけに集中する、残った列を自動整列する、あとでまとめて元に戻す、といった日常操作にも対応しています。
>
> タイムライン構成はワークスペースとして保存できます。設定と構成はログイン情報を含めずにバックアップ・復元できます。キーボードショートカット、操作検索、ダーク／ライトテーマ、任意の画像へすばやく切り替えるボスモードも利用できます。
>
> 本アプリはXの公式アプリではありません。XのページはMicrosoft Edge WebView2で表示します。本アプリがEdgeのCookieを読み取ったり、コピーしたりすることはありません。Google／Appleの外部認証がアプリ内で完了できない場合は、X用パスワードによるログイン方法を画面内で案内します。
>
> 翻訳機能は初期状態でオフです。利用者が画面の説明に同意した場合だけ、表示中の投稿本文をGoogleの翻訳用エンドポイントへ送信します。翻訳方式は設定から無効化でき、同意は列の詳細設定から取り消せます。保存するデータや通信先については、プライバシーポリシーをご覧ください。

### 製品の機能

Partner Centerでは、次の項目を1件ずつ入力する。

1. X（旧Twitter）の複数タイムラインを1画面に表示
2. ホーム・通知・検索・リストを縦横に配置
3. 列の幅と高さをドラッグで調整
4. 列の自動整列・集中表示・一時非表示・一括復元
5. ワークスペースの保存・切り替え
6. 設定とタイムライン構成のバックアップ・復元
7. 利用者が追加したChromium拡張機能のオン・オフ
8. 同意制の投稿翻訳と翻訳方式の切り替え
9. キーボードショートカット・操作検索・ダーク／ライトテーマ
10. 任意の画像にすばやく切り替えるボスモード

### 検索キーワード候補

1. `マルチカラム`
2. `複数タイムライン`
3. `タイムライン整理`
4. `SNSビューアー`
5. `ソーシャルメディア`
6. `ワークスペース`
7. `投稿翻訳`

### このバージョンの新機能

> v2.8.7では、配布物に含まれる実行環境とWindowsの画面基盤を更新しました。
>
> ・.NET SDKを更新し、自己完結型配布物へ.NET 8.0.32を含めるようにしました。
> ・Windows App SDKをサポート中の2.5.1へ更新しました。
> ・WebView2 SDKとWindows SDK BuildToolsを更新しました。
> ・x64とARM64のGitHub版・Microsoft Store版で、同じ依存関係を使ってビルドできるようにしました。
> ・翻訳は初期状態でオフのままです。翻訳の同意、Google方式のオン／オフ、同意の取り消し方法は変更していません。

## English

### Product name

`XTimelineViewer Kotsume Edition`

### Short description

> A Windows multi-column viewer for X (formerly Twitter). Arrange Home, notifications, searches, and lists in one workspace, then resize, focus, hide, restore, and translate timelines with quick column controls. Not affiliated with X Corp.

### Description

> XTimelineViewer Kotsume Edition is an unofficial Windows viewer for displaying multiple X (formerly Twitter) timelines and pages in one window.
>
> Arrange Home, notifications, search results, and lists vertically or horizontally, then drag to resize each column. Temporarily hide columns you do not need, focus on selected columns, automatically arrange the remaining columns, and restore them together later.
>
> Save timeline arrangements as workspaces. Back up and restore settings and layouts without including sign-in credentials. Keyboard shortcuts, command search, light and dark themes, and a user-selected boss-mode image are also available.
>
> This app is not an official app of X and is not affiliated with X Corp. X pages are displayed with Microsoft Edge WebView2. The app does not read or copy Microsoft Edge cookies. If Google or Apple external authentication cannot finish in the app, the app explains how to sign in with an X password.
>
> Translation is off by default. Visible post text is sent to Google's translation endpoint only after the user accepts the on-screen explanation. Translation methods can be disabled in Settings, and consent can be withdrawn from the column settings. See the privacy policy for details about communications and locally stored data.

### Product features

1. Multiple X (formerly Twitter) timelines in one window
2. Home, notifications, searches, and lists arranged in a workspace
3. Drag to resize columns horizontally and vertically
4. Auto arrange, focus, temporarily hide, and restore columns
5. Save and switch workspaces
6. Back up and restore settings and timeline layouts
7. Turn user-added Chromium extensions on or off
8. Opt-in post translation with selectable translation methods
9. Keyboard shortcuts, command search, and light or dark themes
10. Switch quickly to a user-selected boss-mode image

### Keyword candidates

1. `multi column timeline`
2. `social media viewer`
3. `timeline dashboard`
4. `workspace organizer`
5. `multiple timelines`
6. `post translator`
7. `desktop social client`

### What's new in this version

> v2.8.7 updates the runtime and Windows UI components included in the distribution packages.
>
> ・The .NET SDK was updated so self-contained packages include .NET 8.0.32.
> ・Windows App SDK was updated to the supported 2.5.1 release.
> ・The WebView2 SDK and Windows SDK BuildTools were updated.
> ・GitHub and Microsoft Store packages for x64 and ARM64 are built from the same dependency set.
> ・Translation remains off by default. Translation consent, the Google method switch, and consent withdrawal are unchanged.

## 共通の確認事項

- 旧版の説明文や古い「このバージョンの新機能」は使用しない。
- 日本語・英語の長い説明、短い説明、機能一覧、「新機能」を同じv2.8.7の内容に更新する。
- 公開前にPartner Centerのプレビューで、本文が途中で切れていないことと、古いバージョン番号が残っていないことを確認する。
- パッケージのバージョンは `2.8.7.0`、アプリ表示バージョンは `2.8.7` とする。
- v2.8.6の提出履歴と認定記録は変更せず、v2.8.7の提出時に新しい候補として記録する。
