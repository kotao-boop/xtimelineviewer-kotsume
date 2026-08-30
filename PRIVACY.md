# プライバシーポリシー / Privacy Policy

最終更新日: 2026年8月28日

この文書は、XTimelineViewer Kotsume Edition（以下「本アプリ」）が扱うデータと通信を説明します。
本アプリはオープンソースの非公式Xクライアントであり、X Corp.、Google LLC、Microsoft Corporation
およびGitHub, Inc.から承認・提供された製品ではありません。

## 要点

- 開発者が運営する収集サーバー、広告、利用状況テレメトリはありません。
- Xへのログインとタイムライン表示は、Microsoft Edge WebView2からXへ直接接続して行います。
- 自動翻訳は初期状態で無効です。有効にする前に、外部送信についてアプリ内で確認します。
- 翻訳を実行すると、表示中の投稿本文がGoogleの翻訳用エンドポイントへ送信されます。
- 設定、タイムライン構成、WebView2のCookie等は、原則として利用者のPC内に保存されます。

## 1. Xの表示とログイン

本アプリはMicrosoft Edge WebView2を使って `https://x.com/` を表示します。ログイン情報、Cookie、
閲覧履歴など、WebView2内でXが取り扱うデータには、Xのプライバシーポリシーが適用されます。
本アプリの開発者が運営するサーバーへ、XのパスワードやCookieを送信する処理はありません。

利用者がXのログイン画面で「Googleで続ける」または「Appleで続ける」を明示的に選んだ場合、
本アプリは同じWebView2プロファイルを使う認証用ウィンドウで、Googleの
`https://accounts.google.com/` またはAppleの `https://appleid.apple.com/` を表示します。
認証情報は各サービスとXの間で処理され、本アプリ独自の設定ファイルやログへパスワード、
認証Cookie、認証コードを保存する処理はありません。認証用ウィンドウは、X、Google、Appleの
正規HTTPSホストだけへ移動できるよう制限しています。

## 2. 翻訳機能

翻訳機能を初めて使うとき、本アプリは次の内容を画面に表示し、利用者の同意を求めます。

- 翻訳対象となる投稿本文
- 翻訳先の言語（現在は日本語）
- 通信に通常付随する情報（IPアドレス、User-Agent等）

同意後、これらは `https://translate.googleapis.com/` の翻訳用エンドポイントへ送信されます。
Xのパスワードや本アプリの保存済みCookieを翻訳本文として送る処理はありません。ただし、投稿本文に
個人情報や秘密情報が含まれる場合、その文字列も翻訳先へ送られます。秘密情報を含む投稿では翻訳を
使用しないでください。

自動翻訳は初期状態で無効です。各タイムラインの列ヘッダーにある地球ボタンでいつでも無効にできます。個別の
「翻訳を表示」操作も、初回同意後にのみ通信します。同意状態とON/OFF設定は、XのWebページから
読み書きできない拡張機能専用のローカルストレージに保存されます。画面右上の「同意設定」から、
外部送信への同意は、列の詳細設定からいつでも取り消せます。取り消すと自動翻訳も無効になり、次回使用時に再確認します。

投稿本文はHTTPSのPOST本文として送信し、URLのクエリ文字列には入れません。本アプリは翻訳結果を
メモリー内で一時的に再利用しますが、翻訳本文や結果を設定ファイル・ログへ永続保存しません。

このエンドポイントは本アプリ専用サービスではなく、仕様や利用可否が予告なく変わる可能性があります。
Googleによるデータ処理については、Googleのプライバシーポリシーも確認してください。

## 3. 更新確認

未パッケージ版は、最新版の有無を確認するため、
`https://api.github.com/repos/kotao-boop/xtimelineviewer-kotsume/releases/latest` を利用します。

更新確認では、GitHubへ通常のHTTPSリクエストに付随する情報（IPアドレス、User-Agent等）が
送信されます。本アプリの設定ファイルやXの認証情報は送信しません。MSIX版の更新はMicrosoft Store
またはWindowsの更新機能に任せます。

## 4. 画像・動画の保存

利用者が画像や動画の保存を明示的に操作した場合、本アプリはXのWebページが示すメディア配信先から
データを取得し、利用者の「ピクチャ」または「ビデオ」フォルダー内へ保存します。

## 5. PC内に保存するデータ

未パッケージ版では、主に `%LOCALAPPDATA%\XTimelineViewer` 以下へ次のデータを保存します。

- アプリ設定（`settings.json`）
- タイムライン構成（`timelines.json`）
- プロファイル名等（`profiles.json`）
- WebView2のプロファイルデータ、Cookie、キャッシュ
- エラーログ（`error.log`、`error.log.1`）

MSIX版では、設定・タイムライン構成・WebView2データの主な保存先として、Windowsが本アプリへ
割り当てたLocalStateフォルダーを使います。互換性維持のため、エラーログだけはパッケージ版でも
`%LOCALAPPDATA%\XTimelineViewer\error.log` と `error.log.1` に保存します。ログには不具合の内容、
処理名、例外メッセージ、応答サイズなどが含まれる場合があります。投稿本文、解析した投稿ID、
認証Cookieを意図的に記録する処理はなく、ログを自動送信する処理もありません。

## 6. 削除方法

本アプリ内のプロファイル削除機能で、該当するWebView2プロファイルデータを削除できます。
本アプリを終了したうえで上記の保存フォルダーを削除すると、ローカル設定とキャッシュを消去できます。
MSIX版は、Windowsのアプリ設定にあるリセットまたはアンインストールも利用できます。

## 7. 第三者サービス

本アプリから利用する第三者サービスには、それぞれの利用規約とプライバシーポリシーが適用されます。

- X: `https://x.com/` / プライバシー: `https://x.com/en/privacy`
- GoogleによるXへのログイン（利用者が選んだ場合）: `https://accounts.google.com/` / プライバシー: `https://policies.google.com/privacy`
- AppleによるXへのログイン（利用者が選んだ場合）: `https://appleid.apple.com/` / プライバシー: `https://www.apple.com/legal/privacy/`
- Google翻訳用エンドポイント: `https://translate.googleapis.com/` / プライバシー: `https://policies.google.com/privacy`
- GitHub Releases API: `https://api.github.com/` / プライバシー: `https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement`
- Microsoft Edge WebView2 / Microsoft Store / winget / プライバシー: `https://privacy.microsoft.com/privacystatement`

## 8. 変更と問い合わせ

本ポリシーを変更した場合は、このリポジトリの履歴とリリースノートで公開します。プライバシーに関する
一般的な問い合わせはGitHub Issuesを利用してください。パスワード、Cookie、秘密鍵、個人情報は
公開Issueへ書かないでください。脆弱性は `SECURITY.md` の非公開報告手順を利用してください。

---

## English summary

The application does not operate a developer-controlled analytics or collection server. X is displayed directly
through Microsoft Edge WebView2. Translation is disabled by default and requires an in-app disclosure before use.
When translation is requested, the visible post text is sent to `translate.googleapis.com`. Settings, WebView2
profiles and logs are stored locally. Unpackaged builds contact the GitHub Releases API to check for
updates. If the user explicitly chooses Google or Apple on X's sign-in page, the corresponding authentication
page is opened in an in-app WebView2 window that shares the same local profile with X. See the Japanese sections
above for the complete current data-flow description.
