# Microsoft Store提出候補の検証記録 v2.3.1

検証日: 2026年8月28日

## 結果の要約

- Store Identity検査: 合格
- x64 MSIXUpload生成: 成功、警告0、エラー0
- ARM64 MSIXUpload生成: 成功、警告0、エラー0
- 単体テスト: 210件すべて合格
- 翻訳拡張JavaScript構文: 合格
- パッケージ内部の版数、CPU、Identity: 合格
- 新アイコン5種類の一致: x64 / ARM64ともに合格
- LICENSE、THIRD-PARTY-NOTICES、`licenses`: x64 / ARM64ともに同梱
- Windows App Certification Kit（WACK）: 未実行

WACKはこのPCにインストールされていますが、起動に管理者権限が必要でした。現在のタスクでは
管理者権限を要求できないため、未実行です。未実行を合格として扱いません。

## 正式Identity

- Name: `A470ACCD.XTimelineViewerKotsumeEdition`
- Publisher: `CN=C5C81B3B-7437-40C6-8063-CFDF8AAF44EC`
- Version: `2.3.1.0`
- Architectures: `x64 / ARM64`

## ローカルで生成した提出候補

| CPU | ファイル | SHA-256 |
|---|---|---|
| x64 | `XTimelineViewer_2.3.1.0_x64.msixupload` | `33cdbdd10ae728afae83d8145e7a07777be9253e5b42f76afc5ac81a411261c8` |
| ARM64 | `XTimelineViewer_2.3.1.0_arm64.msixupload` | `7ba8021fbc6b8baa05e3014a5cd1639b31b58f803de8077641fec84428420a24` |

これらはローカル検証用に生成したMicrosoft Store提出専用の未署名候補です。GitHub Releaseや一般配布へ
置いてはいけません。実際の提出では、mainからGitHub ActionsのStoreワークフローで生成した成果物を使い、
そのSHA-256を別途確認します。Partner Centerへアップロードした後は、Partner Center側の検査結果と
最終提出内容を改めて確認します。

## パッケージ内部

両アーキテクチャで、次のファイルを確認しました。

- `Assets/StoreLogo.png`
- `Assets/Square44x44Logo.png`
- `Assets/Square150x150Logo.png`
- `Assets/Wide310x150Logo.png`
- `Assets/SplashScreen.png`
- `LICENSE`
- `THIRD-PARTY-NOTICES.md`
- `licenses`内の37ファイル

5種類のパッケージ内画像は、リポジトリの正式画像とバイト単位で一致しました。

## スクリーンショット

- ファイル: `docs/store/screenshots/ja-JP/01-multiple-timelines.png`
- サイズ: `3838 × 2082`
- SHA-256: `5F555F9141FF08F2C06AC5F416EA34EB17BCB3C52E72A71BD34612BA705F1804`

左上の個人アカウント名とハンドルは、利用者本人が画像生成を使わず手作業で削除しました。
