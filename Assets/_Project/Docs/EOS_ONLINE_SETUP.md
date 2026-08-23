# EOSネット対戦セットアップ

> 現在ネット対戦は保留中で、公開リポジトリにはEOSパッケージと認証情報を含めない。再開時はEOS Plugin for Unity 6.1.1を導入し、`PRIDE_COURT_EOS`コンパイルシンボルを有効にしてから以下を設定する。

## 実装構成

- Epic Online ServicesのConnectをDevice ID方式で使用し、プレイヤーへEpic Gamesアカウントのログインを要求しない。
- Lobbyは2人用コートの作成と6桁コード検索だけを担当する。
- 試合中の入力・スナップショット・設定ポーズ同期はEOS P2Pの`Reliable Ordered`チャンネルで送る。
- `LanBattleProtocol`はLAN TCPとEOS P2Pで共用し、ルールやカード処理を通信方式ごとに分岐させない。
- ホストがルールとボール判定を担当する既存方式を維持する。専用ゲームサーバーは不要。
- Android IL2CPPでEOS設定型が削除されないよう、公式プラグイン指定の`Assets/link.xml`を同梱する。

## 初回設定

1. Epic Games Developer PortalでOrganizationとProductを作成する。
2. Product内にSandbox、Deployment、Client Credentialsを作成する。
3. Client PolicyでConnect、Lobbies、P2Pを利用できるようにする。匿名接続にはDevice IDを使用する。
4. Unityを開き、ネット対戦画面右下の`EOS設定を開く`を押す。メニューから開く場合は`EOS Plugin > EOS Configuration`を選ぶ。
5. Product Name、Product Version、Product ID、Sandbox ID、Deployment ID、Client ID、Client Secretを入力する。
6. WindowsとAndroidの両タブで同じDeploymentとClient Credentialsを選び、`Save All Changes`を押す。

`Client Secret`をチャットや公開場所へ貼る必要はない。Epic Developer PortalからUnityのEOS Configurationへ直接コピーする。

設定が不足している間もソロとLAN対戦は使用できる。ネット対戦画面では開始ボタンを無効にし、不足している設定を表示する。

## 動作確認

1. 同じプロトコル版・同じEOS Deploymentを使うWindows／Androidビルドを2台で起動する。
2. 両方で16枚デッキとキャラクターを選ぶ。
3. 片方で「コートを開く」を押し、表示された6桁コードをもう片方へ伝える。
4. もう片方がコードを入力して「コードで参加」を押す。
5. サーブ待機、ラリー、カード選択、再戦、切断を双方で確認する。

## 受け入れ条件

- Windows同士とWindows／Android間で、異なるインターネット回線から参加できる。
- サーブ待機中とカード選択中だけ設定を開け、プレイ中は設定ボタンが無効になる。
- 切断時は試合を継続せず、通信エラーを表示して安全にロビーへ戻れる。
- バックグラウンド移行時は通信対戦を切断する。

EOSのProduct設定はこのリポジトリでは生成できない。Developer Portalの実値を設定した後に、2台実機で最終確認する。
