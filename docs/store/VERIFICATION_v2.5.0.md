# Microsoft Store提出候補の検証記録 v2.5.0

最終更新日: 2026年8月31日

## 対象

- アプリ版: `2.5.0`
- パッケージ版: `2.5.0.0`
- Store ID: `9PMM81B377SD`
- Identity Name: `A470ACCD.XTimelineViewerKotsumeEdition`
- Publisher: `CN=C5C81B3B-7437-40C6-8063-CFDF8AAF44EC`
- 対象: `x64 / ARM64`

## ローカル検証

- Store Identityとアプリ版の整合性: 合格
- 翻訳拡張JavaScript構文: 合格
- 単体テスト: 258件合格、失敗0件
- x64 Releaseビルド: 合格、警告0件、エラー0件
- ARM64 Releaseビルド: 合格、警告0件、エラー0件
- Git差分形式検査: 提出コミット前に実施

## GitHub Actions

Pull Request #23のソースコミット `8934ae9` で確認した。

- CI / build-and-test: 合格（3分17秒）
- CI / ui-smoke: 合格（2分17秒）
- CI / dependency-review: 合格（7秒）
- CodeQL / Analyze C#: 合格（4分48秒）
- GitHub code scanning result: 合格

## Store提出パッケージ

mainへマージ後、`Microsoft Store Package Candidate`ワークフローで生成し、次を記録する。

- Gitコミット: 未確定
- GitHub Actions run: 未実施
- x64 MSIXUpload: 未生成
- ARM64 MSIXUpload: 未生成
- Partner Centerパッケージ検証: 未実施

## 手動確認

- 自動翻訳ボタンがXのページを覆わず、列ヘッダーに表示されること: 実機確認待ち
- 設定画面が1100×760を基準に開き、小さい画面では収まること: 実機確認待ち
- ワークスペースタブ、列追加、検索条件、新着件数、列操作: 実機確認待ち
- 日本語・英語掲載情報: Partner Center入力前
- 個人情報を含まないスクリーンショット4枚以上: 準備中
- WACK x64／ARM64: 未実施

未実施項目を合格扱いしない。Partner Centerへ提出した後は、認定結果と公開パッケージ版を追記する。
