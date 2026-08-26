# Microsoft Store無料MSIX配布の準備

最終更新日: 2026年8月26日

## 目的と範囲

Microsoft Storeの新しいオンボーディングでは、個人・法人のDeveloper Accountに登録料がないとMicrosoft
公式文書で案内されている。Store審査を通過したMSIX/AppXはMicrosoftが再署名するため、Store版のために
CA証明書を購入する必要はない。

この無料署名はStoreから配布されるMSIX/AppXだけに適用される。GitHub ReleaseのSetup.exe、ZIP、
Store外でサイドロードするMSIXは署名されない。

## 現在の準備状況

Partner Centerで `XTimelineViewer Kotsume Edition` を予約し、発行された正式なIdentityを
`Package.appxmanifest`へ反映済みである。

- `Name="A470ACCD.XTimelineViewerKotsumeEdition"`
- `Publisher="CN=C5C81B3B-7437-40C6-8063-CFDF8AAF44EC"`
- `PublisherDisplayName="Kotsume Project"`
- `Store ID="9PMM81B377SD"`

これらはPartner CenterのProduct identity画面で確認した、この製品専用の値である。
派生元のStore Identityや、以前使用していた開発用Identityへ戻してはいけない。

## 利用者本人が行う必要がある手順

1. プライバシーポリシーURL、サポートURL、公開する開発者名を確認する。
2. Store掲載情報、スクリーンショット、年齢区分を入力する。
3. 提出用パッケージをアップロードし、警告とエラーを確認する。
4. 公開プレビューに本名、住所、電話番号などが表示されていないことを確認する。
5. 内容に問題がないことを確認してから、審査へ提出する。

本人確認資料、Microsoftアカウントの認証情報、回復コードはリポジトリやチャットへ貼り付けない。

## Identity取得後の実装と検証

1. [x] `Package.appxmanifest`のIdentityをPartner Centerの値へ更新する。
2. [x] DisplayName、Description、PublisherDisplayNameをKotsume Editionへ更新する。
3. [x] x64/arm64のStore upload packageをローカルで生成する。
4. [ ] Windows App Certification Kitで確認する。
5. [ ] Partner Centerへ提出し、認定結果を確認する。
6. [ ] Store版とGitHub版の更新経路・署名状態をREADMEで区別する。

提出前の全確認は `docs/store/SUBMISSION_CHECKLIST.md`、認証担当者向け説明は
`docs/store/CERTIFICATION_NOTES_TEMPLATE.md` を使う。

Partner Centerの正確なIdentityへ置き換えた後は、GitHub Actionsの
`Microsoft Store Package Candidate` を手動実行する。開発用Identityが残っている場合は
`scripts/test-store-readiness.ps1` が意図的に失敗し、提出候補を生成しない。生成したmsixuploadは
公開Releaseへ出さず、Partner Center提出とWACK確認だけに使う。Actions artifactも公開リポジトリの
閲覧者が取得できる場合があるため、名前に `UNSIGNED` と `NOT-FOR-DISTRIBUTION` を付ける。

## 審査前に説明する通信

- XをWebView2で表示すること
- Xの公式クライアントではないこと
- 翻訳は初期OFFで、同意後に投稿本文をGoogleの翻訳用エンドポイントへ送ること
- GitHub Releases APIによる更新確認は未パッケージ版だけであること
- ローカルのWebView2プロファイル、Cookie、設定、ログの保存と削除方法

## 未確認事項

- 現在の `runFullTrust`、WebView2、拡張読み込みがStore認定を通るか
- XおよびGoogle側の規約と、Storeの最新ポリシーに照らした最終的な許容範囲
- 他の利用者が書いた投稿をGoogleへ送る翻訳について、Storeポリシー10.5.3がどのように適用されるか

審査通過を事前に断定せず、実際のPartner Centerと認定結果を優先する。
