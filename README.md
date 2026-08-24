# PRIDE COURT / プライド・コート

カードで流れを変えながら戦う、Windows／Android向け3Dテニスゲームです。
このリポジトリは **Beta版（v0.1.1-beta）** の公開ページです。仕様、操作感、データ、対応端末はBeta期間中に変更される場合があります。

## Beta版で遊べる範囲

- CPUと戦うソロプレイ
- 同じLAN／Wi-Fi内のローカル対戦（Windows／Android）
- キャラクターごとのカードデッキと、試合中のカード発動
- キーボード・マウス、コントローラー、Androidタッチ操作
- 音量、画質、表示、CPU難度の設定・ポーズ

インターネット対戦（EOS）はBeta版では無効です。ゲーム内でも選択できません。

## ダウンロード

配布ビルドはGitHubの [Releases](https://github.com/Kuru99/tennis/releases) に掲載します。
Windows版はReleaseページにある `PrideCourt-Windows-Setup.exe` を実行し、Android版はReleaseページにあるAPKを端末へインストールしてください。

- Windows：Windows x64。インストーラーがゲームフォルダ一式を配置します。インストール後はスタートメニューまたはデスクトップのショートカットから起動できます。
- Android：Android 8.0（API 26）以上、横画面を対象としています。署名付きストア配布版ではない場合、OSの警告が表示されることがあります。
- 配布ビルドのハッシュ、変更点、既知の問題は各Release本文を確認してください。

## ソースから起動する場合

1. Unity `6000.5.4f1` でこのリポジトリを開きます。
2. `Assets/_Project/Scenes/MVP_Prototype.unity` を開きます。
3. Unity EditorのPlayで実行します。

操作の詳細は [Assets/_Project/Docs/PROTOTYPE_CONTROLS.md](Assets/_Project/Docs/PROTOTYPE_CONTROLS.md) を参照してください。

## 基本操作（PC）

- 移動・狙い：`WASD`／矢印キー
- 強打：マウス左ボタン／`J`
- 安定打：マウス右ボタン／`K`
- ダッシュ：`Shift`
- ダイブ：方向入力＋`Space`
- 必殺技：`E`
- カード：`1`・`2`・`3`
- 設定・ポーズ：画面右上の設定ボタン

## このリポジトリに含めないもの

- `Library`、`Temp`、`Builds` などのUnity生成フォルダ
- Android keystore、証明書、秘密鍵、EOS認証情報
- Epic Online Services Pluginのローカルパッケージ
- 個人用の作業ログ、配布前の検証ログ、内部引き継ぎ資料

EOSを再開する場合の設定方針は [EOS_ONLINE_SETUP.md](Assets/_Project/Docs/EOS_ONLINE_SETUP.md) に記載しています。認証情報をIssue、Pull Request、README、公開ログへ貼り付けないでください。

## ライセンスと権利表記

### Pride Court固有のコード・アート・音声

本リポジトリには、Pride Court Studioが制作したゲームコード、シーン、3Dモデル、キャラクター、カードアート、UI、画像、音声、設定資料が含まれます。これらにはオープンソースライセンスを付与していません（All rights reserved）。

Beta版を遊ぶためのダウンロード・個人評価は許可しますが、コードや素材の再配布、素材だけの抜き出し、改変版の公開、商用利用、他作品への転用は、Pride Court Studioの書面による許可なしに行わないでください。GitHubで公開されていることは、再利用許諾を意味しません。

プロジェクト固有素材の一覧と個別の出典は [THIRD_PARTY_ASSETS.md](Assets/_Project/Docs/THIRD_PARTY_ASSETS.md) にも記載しています。

### 同梱される第三者ライセンス素材

- **Dela Gothic One、Noto Sans JP、Big Shoulders Display、IBM Plex Sans Condensed**：SIL Open Font License 1.1。各フォントのライセンス本文を `Assets/_Project/Resources/Fonts/*-OFL.txt` に同梱しています。フォント単体の販売、予約フォント名の扱いなど、OFLの条件に従ってください。公式ライセンスは [SIL OFL 1.1](https://openfontlicense.org/open-font-license-official-text/) を参照してください。
- **ボールの効果音**：Joseph Sardin／BigSoundBankのCC0素材を編集したものです。出典と利用条件は [Assets/_Project/Resources/Audio/Ball/README.md](Assets/_Project/Resources/Audio/Ball/README.md) に記載しています。
- **Unity**：Unity EditorおよびUnityのランタイム／組み込みモジュールは同梱していません。Unityを利用した開発・配布は [Unity Terms of Service](https://unity.com/legal/terms-of-service) および適用されるUnityの追加条件に従ってください。
- **Epic Online Services Plugin for Unity**：MITライセンスの任意依存です。現在の公開リポジトリとBeta配布ビルドには含めていません。再導入時は、プラグイン本体のライセンスと第三者通知を確認してください。

第三者素材を追加する場合は、出典、著作者、ライセンス、改変・再配布条件、必要な帰属表示を登録してから追加してください。

## 免責

Beta版は無保証です。セーブデータ、通信、端末互換性、パフォーマンス、表示や入力の不具合が発生する可能性があります。問題報告時は、使用ビルド、OS／端末、再現手順、ログに秘密情報が含まれていないことを確認してからIssueへ記載してください。
