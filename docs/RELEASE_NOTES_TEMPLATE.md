# XTimelineViewer Kotsume Edition vX.Y.Z

## 署名状態 / Code-signing status

次のどちらか一方だけを残し、事実と異なる表示をしないでください。

- **署名済み:** SignPath Foundationの証明書で第一者EXE/DLLを署名し、タイムスタンプと証明書チェーンを検証しました。
- **未署名:** このリリースはコード署名されていません。SignPathで承認された成果物として扱わないでください。

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

## ダウンロード

- `XTimelineViewer-Kotsume-vX.Y.Z-Setup.exe`
- `XTimelineViewer-Kotsume-vX.Y.Z-win-x64-Portable.zip`
- `XTimelineViewer-Kotsume-vX.Y.Z-win-arm64-Portable.zip`

`SHA256SUMS.txt`は、署名の有無を明記した後、すべての適用対象ゲートに合格した最終ファイルから生成します。
GitHub Artifact Attestationも同じ最終ファイルを対象にします。署名済みリリースでは署名後、未署名
リリースでは未署名警告の同梱後に生成します。SHA-256と来歴証明をコード署名や安全性の保証そのものとして
説明しないでください。

## プライバシーと通信の変更

- `[変更内容を記入。変更がない場合も「変更なし」と明記]`
- プライバシーポリシー: https://github.com/kotao-boop/xtimelineviewer-kotsume/blob/main/PRIVACY.md

## 主な変更

- `[利用者向け変更を記入]`

## 既知の問題

- `[既知の問題を記入。ない場合も「なし」と明記]`

## ライセンス

各ZIPとインストーラーには、プロジェクトの `LICENSE`、`THIRD-PARTY-NOTICES.md`、依存コンポーネントの
正確なライセンスを収めた `licenses` フォルダーが含まれます。

## 検証記録

- ソースコミット: `[40文字SHA]`
- CI実行: `[Actions URL]`
- SignPath signing request: `[承認済みの場合だけURL。未署名なら「対象外」と記入]`
- x64/ARM64ビルド: `[成功/失敗]`
- 単体テスト: `[件数と結果]`
- Authenticode検証: `[成功/未署名を確認]`
- WACK: `[Store提出時だけ結果]`
