# プライバシーポリシー / Privacy Policy

最終更新日: 2026年8月23日

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

## 2. 翻訳機能

翻訳機能を初めて使うとき、本アプリは次の内容を画面に表示し、利用者の同意を求めます。

- 翻訳対象となる投稿本文
- 翻訳先の言語（現在は日本語）
- 通信に通常付随する情報（IPアドレス、User-Agent等）

同意後、これらは `https://translate.googleapis.com/` の翻訳用エンドポイントへ送信されます。
Xのパスワードや本アプリの保存済みCookieを翻訳本文として送る処理はありません。ただし、投稿本文に
個人情報や秘密情報が含まれる場合、その文字列も翻訳先へ送られます。秘密情報を含む投稿では翻訳を
使用しないでください。

自動翻訳は初期状態で無効です。画面右上の切替ボタンでいつでも無効にできます。個別の
「翻訳を表示」操作も、初回同意後にのみ通信します。同意状態とON/OFF設定は、WebView2の
ローカルストレージに保存されます。

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

MSIX版では、Windowsが本アプリへ割り当てたLocalStateフォルダー以下へ保存します。エラーログは
不具合の内容や例外メッセージを含む場合がありますが、自動送信はしません。

## 6. 削除方法

本アプリ内のプロファイル削除機能で、該当するWebView2プロファイルデータを削除できます。
本アプリを終了したうえで上記の保存フォルダーを削除すると、ローカル設定とキャッシュを消去できます。
MSIX版は、Windowsのアプリ設定にあるリセットまたはアンインストールも利用できます。

## 7. 第三者サービス

本アプリから利用する第三者サービスには、それぞれの利用規約とプライバシーポリシーが適用されます。

- X: `https://x.com/`
- Google翻訳用エンドポイント: `https://translate.googleapis.com/`
- GitHub Releases API: `https://api.github.com/`
- Microsoft Edge WebView2 / Microsoft Store / winget

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
updates. See the Japanese sections above for the complete current data-flow description.
