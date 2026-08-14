# オンライン対戦（ロビー / 部屋コード）設計仕様書

ステージセレクト画面を入口に、**部屋コード方式**でオンライン対戦のプレイヤーを募集する。
実トランスポートは **Unity Netcode for GameObjects + Unity Relay(UGS)**。Relay の Join Code を
そのまま「部屋コード」として使う。

> このトピックの正典はこのファイル。フロー全体は [GAME_FLOW.md](../Specifications/GAME_FLOW.md)、
> 募集ロビーの操作は [StageMap_Design.md](StageMap_Design.md#6-ui) を参照（再掲しない）。

---

## 1. ユーザー提示仕様

- ステージセレクト画面に **ネットワーク設定用アイコン**（右上「NET」）。
- 押すと 2 メニュー: **部屋を建てる** / **部屋に入る**。
- 部屋を建てる → **コードが発行**され、**コピー**できる。
- 部屋に入る → **コード入力画面**でコードを入れて参加。
- 参加すると、**参加した順番ごとに 2P → 3P → 4P** が割り当たる（ホスト=1P）。
- ネット対戦・ローカル対戦のどちらもこの画面で募集する。

---

## 2. アーキテクチャ（パッケージ非依存で壊れない構造）

| 層 | ファイル | コンパイル条件 | 役割 |
|---|---|---|---|
| 抽象 | [INetworkLobby.cs](../../Assets/Src/Network/INetworkLobby.cs) | 常時 | `INetworkLobby` / `NetworkLobby`(provider) / `UnavailableLobby` |
| 実装 | [NetcodeRelayLobby.cs](../../Assets/Src/Network/NetcodeRelayLobby.cs) | `#if UGS_NETCODE` | Netcode+Relay 実通信・スロット同期 |
| UI | [NetworkLobbyPanel.cs](../../Assets/Src/Network/NetworkLobbyPanel.cs) | 常時 | NETアイコン→メニュー→建てる/入る→ロスター表示 |
| 統合 | [WorldMapSceneController.cs](../../Assets/Src/System/WorldMapSceneController.cs) | 常時 | パネル生成・ネット稼働中の募集枠反映 |

**なぜ `#if UGS_NETCODE` か**: 未導入のまま `using Unity.Netcode;` 等を書くと全スクリプトが
コンパイル不能になり（[CLAUDE.md] の警告）プロジェクトが丸ごと止まる。実装は丸ごとガードし、
未導入時は `UnavailableLobby` が **明確なエラー**を返す（フォールバックで握りつぶさない）。

---

## 3. 実通信を有効化する手順（一度きり）

1. **パッケージ導入**: `Packages/manifest.json` に追加済み（Editor 起動で解決）。
   - `com.unity.netcode.gameobjects` / `com.unity.services.core` /
     `com.unity.services.authentication` / `com.unity.services.relay`
   - ※ Package Manager が「バージョンが見つからない」と出たら、Unity Registry から各パッケージを
     最新で入れ直す（**Window > Multiplayer > Multiplayer Center** が推奨版を自動選択）。
2. **Scripting Define 追加**: Project Settings > Player > Scripting Define Symbols に **`UGS_NETCODE`** を追加。
   - これで [NetcodeRelayLobby.cs](../../Assets/Src/Network/NetcodeRelayLobby.cs) が有効化される。
3. **UGS 連携**: Project Settings > Services でプロジェクトを Unity 組織にリンク。
4. **Relay 有効化**: Unity Dashboard で Relay（および Authentication の匿名サインイン）を有効化。

以上が済むと NET アイコン→「部屋を建てる」でコードが発行され、別インスタンスから
「部屋に入る」でそのコードを入れて参加できる。

---

## 4. スロット割当（参加順 → 2P/3P/4P）

- **ホスト = スロット0（1P）**。
- クライアントは**接続順**にスロット1,2,3（2P,3P,4P）へ。
- サーバが参加順リスト `ordered`（先頭=ホスト）を保持し、接続/切断のたびに
  `CustomMessagingManager` で全員へ配信（`NetworkObject` の spawn は不要）。
- 各クライアントは受信リスト内の自分の `LocalClientId` の位置を自分のスロットとする。
- 上限は 4 人（Relay の `maxConnections` は「自分以外」= 最大3）。

---

## 5. ダウンストリームへの引き継ぎ

- ネット稼働中、`NetworkLobbyPanel` が `SlotOccupied` を `GameSceneManager.joinedPlayers` に反映。
- ステージセレクトの募集枠（下部 P1〜P4）にも反映される。
- キャラ選択は `joinedPlayers` を尊重（参加スロットのみカーソル）。

---

## 6. カーソル同期（実装済み）

- 各クライアントは**自分のスロットのカーソル位置を画面正規化(0..1)**で 30Hz 送信
  （`SetLocalCursor`）。解像度・アスペクトが違っても位置が合う。
- サーバが全スロット分を集約し `CustomMessaging`(Unreliable) で全員へ配信。
  各クライアントは受信値で他プレイヤーのカーソルを復元し、**リアルタイム表示**する
  （自分の位置はローカル入力を優先＝ジッタ回避）。
- メッセージ: `UTS_Cur`(client→server) / `UTS_Curs`(server→all)。

## 7. 既知の範囲（今回は未実装＝今後）

- **オンラインの同期出撃**（全員集合→全員同時にステージ遷移）は未実装。今回は
  「部屋の作成/参加/スロット割当＋カーソル同期」までを担う。集合出撃（約3秒）はオフライン時のみ。
- ゲームプレイ本体の状態同期（弾幕・カード・アクションコマンド）は範囲外。

---

## 8. 関連ドキュメント

- [GAME_FLOW.md](../Specifications/GAME_FLOW.md) — シーン遷移と各画面の操作
- [StageMap_Design.md](StageMap_Design.md) — ステージセレクト（募集ロビー）の操作・出撃
- [GAME_SPECIFICATION.md](../Specifications/GAME_SPECIFICATION.md) — プレイヤー数・全体像
