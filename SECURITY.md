# セキュリティポリシー (Security Policy)

## サポートされているバージョン

現在、以下のバージョンに対してセキュリティ更新プログラムが提供されています。

| バージョン | サポート状況 |
| ------- | ---------- |
| 2.3.x   | :white_check_mark: |
| < 2.3.0 | :x: |

## 脆弱性の報告

本プロジェクトにおけるセキュリティ上の脆弱性を発見した場合は、公開Issueへ詳細や再現コードを
書かず、GitHubのSecurityタブにあるプライベート脆弱性報告（Private vulnerability reporting）を
利用してください。この機能が表示されない場合は、管理者のGitHubプロフィールから、機密情報を
含めずに安全な連絡方法を問い合わせてください。

公開前に、リポジトリ管理者はGitHubのPrivate vulnerability reportingが実際に有効であることを
ログアウト状態または別アカウントから確認します。

報告いただいた内容を速やかに調査し、修正とアップデートを実施します。

## ビルドとコード署名について

本プロジェクトの第一者実行ファイルは、GitHub-hosted Windows Runner上で公開ソースからビルドします。
第三者・Microsoft製バイナリを本プロジェクトの証明書で再署名しません。

Microsoft Store版はMicrosoftが署名して配布します。GitHubで配布するEXE・ZIPは未署名です。GitHub版の
最終成果物にはSHA-256とGitHub Artifact Attestationを付けていますが、これらはコード署名や安全性の
保証そのものではありません。SignPathの検討経緯と署名パイプラインの設計は
[コード署名ランブック](docs/CODE_SIGNING_RUNBOOK.md)を参照してください。Store署名をGitHub版の署名と
説明したり、署名検証に失敗した成果物を「署名済み」と表示したりしてはなりません。

