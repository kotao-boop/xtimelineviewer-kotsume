# アプリアイコン

XTimelineViewer Kotsume Editionでは、本家XTimelineViewerのアイコンを使用しません。
現在のアイコンは、Kotsume Editionのために新しく作成したオリジナルデザインです。

## デザインの意味

- 中央のコツメカワウソは、複数の情報を落ち着いて見渡す案内役です。
- 青・緑・橙の3つの面は、同時に表示できる複数のタイムラインやワークスペースを表しています。
- XやKの文字、Xの公式ロゴ、本家XTimelineViewerの銀色のマークは使用していません。

## 制作方法

デザインの方向を決める段階では画像生成AIを補助的に使用しました。
製品で使用する図形は、その案を参考に、このリポジトリ内でSVGとして新しく組み直しています。
本家アイコンや第三者の画像を下敷き、トレース、画像入力として使用していません。

## 原画と書き出しファイル

- `Assets/AppIcon.svg`：48ピクセル以上を想定した原画
- `Assets/AppIconSmall.svg`：16～50ピクセル向けの簡略版
- `Assets/AppIcon.png`：確認・文書掲載用の1024ピクセル版
- `Assets/AppIcon.ico`：Windowsアプリ、ランチャー、インストーラー用
- `Assets/StoreLogo.png`：Microsoft Storeのロゴ
- `Assets/Square44x44Logo.png`：Windowsの小さいタイル用
- `Assets/Square150x150Logo.png`：Windowsの通常タイル用
- `Assets/Wide310x150Logo.png`：Windowsの横長タイル用
- `Assets/SplashScreen.png`：起動画面用

SVGを原画として扱い、PNGやICOだけを直接描き替えないでください。
小さい画像は単純な縮小ではなく、顔を識別しやすい簡略版から書き出します。

## 背景と余白

StoreやWindows側の角丸処理で白い隙間が出ないよう、画像の四隅まで濃紺の背景を敷いています。
四隅を白く塗ったり、白い正方形の台紙を追加したりしないでください。

このアイコンは、特に記載がない限り、リポジトリ本体と同じMIT Licenseの対象です。
