# GitHubリポジトリ保護設定

この文書は、ワークフローをmainへ反映した後に、GitHubのSettings画面で行う管理者向けチェックリストです。
コードだけでは有効にできない設定を含みます。

## 1. mainブランチのruleset

`Settings > Rules > Rulesets` で、`main` を対象に次を設定します。

- [ ] Pull request経由の変更を必須にする
- [ ] 最低1人の承認を必須にする
- [ ] 新しいコミットで古い承認を無効にする
- [ ] 未解決のレビュー会話を残したままマージできないようにする
- [ ] required status checksを有効にする
- [ ] `build-and-test` を必須にする
- [ ] `dependency-review` をPull Requestで必須にする
- [ ] CodeQLの `Analyze C#` を必須にする
- [ ] force pushを禁止する
- [ ] branch deletionを禁止する
- [ ] 管理者にもルールを適用する

`ui-smoke` はGitHub-hosted runner上で不安定になり得るため、安定性を確認するまでは必須チェックにしません。

## 2. リリースタグのruleset

`v*` タグを対象に次を設定します。

- [ ] タグの削除を禁止する
- [ ] タグの更新・付け替えを禁止する
- [ ] タグ作成者をリリース担当者に制限する
- [ ] mainに含まれるコミットだけをタグ付けする運用にする

現在の `release.yml` も、タグのコミットがmainに含まれることを機械的に検査します。

## 3. Security設定

`Settings > Code security and analysis` で次を有効にします。

- [ ] Dependency graph
- [ ] Dependabot alerts
- [ ] Dependabot security updates
- [ ] Secret scanning
- [ ] Push protection
- [ ] Private vulnerability reporting
- [ ] CodeQL default setupを使わず、このリポジトリの固定SHAワークフローが正常に動くことを確認する

`.github/dependabot.yml` はNuGetとGitHub Actionsを毎週確認します。DependabotのPRでも、ロックファイル、
x64/ARM64ビルド、テスト、CodeQL、dependency reviewを省略しません。

## 4. SignPath用Environment

SignPath Foundationから承認を得た後に `release-signing` Environmentを作ります。

- [ ] required reviewerを設定する
- [ ] 可能ならcommitterとapproverを別の人にする
- [ ] deployment branch/tag ruleを `v*` に限定する
- [ ] `SIGNPATH_API_TOKEN` をEnvironment secretへ保存する
- [ ] Organization ID、Project slug、Artifact Configuration slug、Signing Policy slugをEnvironment variableへ保存する
- [ ] 認証情報をrepository、Issue、Actionsログへ書かない

単独メンテナーのまま役割を兼ねる場合は、SignPathに許容されるかを事前確認します。

## 5. 公開ゲート

### 未署名リリース

SignPath承認前でも、未署名であることを利用者へ明確に伝え、次をすべて満たした成果物は公開できます。

- [ ] main上の版数一致、単体テスト、JavaScript検査、x64/ARM64ビルドが成功した
- [ ] 第一者ファイルの製品情報と、第三者ファイルの既存署名を確認した
- [ ] `LICENSE`、`THIRD-PARTY-NOTICES.md`、正確な`licenses`フォルダーを同梱した
- [ ] Windowsで「不明な発行元」やSmartScreenの警告が出る可能性を目立つ場所に書いた
- [ ] 最終成果物からSHA-256を生成して、その場でもう一度照合した
- [ ] 最終成果物にGitHub Artifact Attestationを付けた
- [ ] SHA-256と来歴証明がコード署名や安全性の保証そのものではないと説明した
- [ ] 手動実行で公開直前までの全工程を試験した
- [ ] タグ実行だけがGitHub Releaseを新規作成し、`--clobber`上書きを使わない

### SignPath署名済みリリースで追加するゲート

- [ ] SignPathの適格性確認が完了した
- [ ] `XTimelineViewer.exe`、`XTimelineViewer.dll`、x64/ARM64の`xtv.exe`が署名対象になった
- [ ] インストーラー内部と外側の署名方法が確定した
- [ ] `scripts/verify-release-signatures.ps1`が全件成功した
- [ ] 署名検証後の最終成果物からSHA-256とGitHub Artifact Attestationを生成した
