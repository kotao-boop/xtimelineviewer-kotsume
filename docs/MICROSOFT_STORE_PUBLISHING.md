# Microsoft Store無料MSIX配布の準備

最終更新日: 2026年8月23日

## 目的と範囲

Microsoft Storeの新しいオンボーディングでは、個人・法人のDeveloper Accountに登録料がないとMicrosoft
公式文書で案内されている。Store審査を通過したMSIX/AppXはMicrosoftが再署名するため、Store版のために
CA証明書を購入する必要はない。

この無料署名はStoreから配布されるMSIX/AppXだけに適用される。GitHub ReleaseのSetup.exe、ZIP、
Store外でサイドロードするMSIXは署名されない。

## 現在止めている理由

`Package.appxmanifest`には派生元のStore Identityが残っている。

- `Name="4275.XTimelineViewer"`
- `Publisher="CN=B73FDB0C-06E5-4824-9E7F-3AF969921DF4"`
- `PublisherDisplayName="だるやなぎ"`

これらをKotsume Editionが推測で再利用してはいけない。Partner CenterでKotsume Edition用の製品名を
予約した後、Product managementに表示される正確なIdentity値へ置き換える。

## 利用者本人が行う必要がある手順

1. Microsoft Store Developer Accountのページで、個人または法人の種類を選ぶ。
2. 個人の場合は、Microsoftの案内に従って身分証明書とセルフィーによる本人確認を行う。
3. Partner CenterでKotsume Edition用の製品名を予約する。
4. Product identityに表示されたName、Publisher、Publisher display nameを控える。
5. プライバシーポリシーURL、サポートURL、公開する開発者名を確認する。

本人確認資料、Microsoftアカウントの認証情報、回復コードはリポジトリやチャットへ貼り付けない。

## Identity取得後に行う実装

1. `Package.appxmanifest`のIdentityをPartner Centerの値へ更新する。
2. DisplayName、Description、PublisherDisplayNameをKotsume Editionへ更新する。
3. x64/arm64のStore upload packageを生成する。
4. Windows App Certification Kitで確認する。
5. Partner Centerへ提出し、認定結果を確認する。
6. Store版とGitHub版の更新経路・署名状態をREADMEで区別する。

## 審査前に説明する通信

- XをWebView2で表示すること
- Xの公式クライアントではないこと
- 翻訳は初期OFFで、同意後に投稿本文をGoogleの翻訳用エンドポイントへ送ること
- GitHub Releases APIによる更新確認は未パッケージ版だけであること
- ローカルのWebView2プロファイル、Cookie、設定、ログの保存と削除方法

## 未確認事項

- Kotsume Editionという製品名がPartner Centerで予約可能か
- 現在の `runFullTrust`、WebView2、拡張読み込みがStore認定を通るか
- XおよびGoogle側の規約と、Storeの最新ポリシーに照らした最終的な許容範囲

審査通過を事前に断定せず、実際のPartner Centerと認定結果を優先する。
