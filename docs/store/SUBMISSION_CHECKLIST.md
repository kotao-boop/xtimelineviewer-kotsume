# Microsoft Store 提出チェックリストと進捗

このチェックリストは、Partner Centerへの提出前に使います。製品名の予約と正式なIdentityの
`Package.appxmanifest`への反映は完了しています。

> **現在の状態（2026年8月26日）:** x64版・ARM64版をPartner Centerへ提出済みで、現在は`In certification`
> （認定審査中）です。認定合格、Store公開、WACK合格は、結果が表示されるまで完了扱いにしません。

## 1. アカウントと製品Identity

- [x] Partner Centerの本人確認が完了している
- [x] `XTimelineViewer Kotsume Edition` を予約した
- [x] Product identityの `Name` を記録した
- [x] Product identityの `Publisher` を記録した
- [x] 公開する `Publisher display name` が `Kotsume Project` であることを確認した
- [x] 認証情報、回復コード、本人確認資料をリポジトリへ保存していない

## 2. manifest

- [x] `Name` をPartner Centerの正式な値へ置き換えた
- [x] `Publisher` をPartner Centerの正式な値へ置き換えた
- [x] アプリ版と4桁のパッケージ版を一致させた
- [x] x64とARM64の提出用パッケージをローカルで生成した
- [ ] `internetClient` が実際のHTTPS通信に必要であることを説明できる
- [x] `runFullTrust` がWinUI 3デスクトップ機能に必要であることをPartner Centerへ説明した
- [x] `scripts/test-store-readiness.ps1` が `-AllowDevelopmentIdentity` なしで成功する

## 3. プライバシー

- [x] `PRIVACY.md`を一般公開のHTTPS URLで閲覧できる
- [ ] X、Google、GitHub、Microsoftへの通信を掲載文でも説明した
- [ ] 翻訳が初期OFFであることを確認した
- [ ] 同意前に翻訳通信が発生しないことを通信ログで確認した
- [ ] 「同意設定」から同意を取り消せることを確認した
- [ ] 取り消した後、再同意するまで翻訳通信が発生しないことを確認した
- [ ] 他の利用者が書いた投稿をGoogleへ送る設計について、Storeポリシー10.5.3の扱いをMicrosoftへ確認した

## 4. セキュリティと動作確認

- [ ] 細工した `.url` がタイムラインとして追加されないことを確認した
- [ ] X以外のオリジンからWebViewネイティブメッセージを呼べないことを確認した
- [ ] `file:`、`javascript:`、独自URIスキームを外部起動しないことを確認した
- [ ] MSIX版が同梱済み拡張だけを読み込むことを確認した
- [ ] Windows 10 22H2、Windows 11、Windows 11 ARM64で実機確認した
- [ ] WebView2 Runtimeがない環境で、分かりやすい案内または導入処理が動くことを確認した
- [ ] Windows App Certification Kitがx64/ARM64の両方で合格した

## 5. Partner Center掲載情報

- [x] 日本語・英語の説明文
- [x] 個人情報が写っていないスクリーンショット
- [x] パッケージ内のアプリアイコンをStore表示に使用
- [x] カテゴリと年齢区分
- [x] プライバシーポリシーURL
- [x] サポートURL
- [ ] Windows App SDKの再配布条件を満たすStoreの使用許諾条件を確認した
- [x] 非公式Xクライアントであり、X Corp.の提供・承認製品ではない旨
- [x] 翻訳時に投稿本文をGoogleへ送る旨
- [ ] 更新経路はStoreであり、GitHub版の自己更新とは別である旨

## 6. 認証担当者向けノート

- [ ] 専用のテスト用Xアカウントを用意した
- [ ] パスワードをリポジトリや提出文書へ保存していない
- [ ] 認証担当者へ安全なPartner Center欄だけで資格情報を渡した
- [x] ログイン、ペイン追加、翻訳同意、同意撤回、メディア保存の確認手順を記載した
- [ ] 多要素認証や地域制限がある場合の手順を記載した

## 7. 最終提出ゲート

- [ ] ソース、manifest、掲載版、パッケージ版が一致している
- [ ] LICENSE、THIRD-PARTY-NOTICES、`licenses`フォルダーがパッケージ内にある
- [ ] WACK結果と手動確認結果をリリース記録へ保存した
- [ ] 既知の未解決ブロッカーが0件である
- [x] Partner Centerの提出内容を別の目で確認した
