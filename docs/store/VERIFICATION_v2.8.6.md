# Microsoft Store提出候補の検証記録 v2.8.6

最終更新日: 2026年9月10日

## 対象

- アプリ版: `2.8.6`
- パッケージ版: `2.8.6.0`
- Store ID: `9PMM81B377SD`
- Identity Name: `A470ACCD.XTimelineViewerKotsumeEdition`
- Publisher: `CN=C5C81B3B-7437-40C6-8063-CFDF8AAF44EC`
- 対象: `x64 / ARM64`
- Gitコミット: `5a06e2f5a27171b7911719f0cdb44eb90ba3a848`

## ローカル検証

- `scripts/test-store-readiness.ps1`: 合格
- manifest Identity、Publisher、Version `2.8.6.0`: 整合
- GitHub Actions提出候補生成: 成功
- x64 MSIXUpload内部検査: 合格
- ARM64 MSIXUpload内部検査: 合格

## GitHub Actions

- Workflow: `Microsoft Store Package Candidate`
- Run: `34435371931`
- URL: `https://github.com/kotao-boop/xtimelineviewer-kotsume/actions/runs/34435371931`
- readiness、legal notices、x64 package、ARM64 package、artifact upload: すべて成功

## 提出パッケージ

- `XTimelineViewer_2.8.6.0_x64.msixupload`
  - SHA-256: `810eb8276bdee11d0be970acaac12e5cc53324f0db1f2926cc55ee7cc3f8ed20`
- `XTimelineViewer_2.8.6.0_arm64.msixupload`
  - SHA-256: `ae4cc6d441752b0352d087fad0b01a22a2cf92975756d5366aa43bf48c7c4fdf`

## Partner Center

- Submission: `13`
- x64 package: `Validated`
- ARM64 package: `Validated`
- 日本語Store掲載情報: 保存済み、v2.8.6の説明・短い説明・機能一覧・新機能へ更新
- 英語Store掲載情報: 保存済み、v2.8.6の説明・短い説明・機能一覧・新機能へ更新
- 旧版のリリースノート: 現行の提出欄から削除
- 提出状態: `In certification`
- 進行状態: Submission完了、Pre-processing進行中、Certification／Publishing待ち

認定完了まではSubmission 12のv2.8.5.0がStoreで公開される。認定後にPartner Centerで
公開版のパッケージと掲載情報を再確認し、この記録を更新する。

## 未完了の確認

- Windows App Certification Kit（WACK）: 未実施
- Microsoftの認定結果: 待機中
- v2.8.6公開後のStore表示確認: 公開後に実施
