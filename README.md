# アンダーザスポットライト (Under the Spotlight)

ターン制カードバトル × アクションコマンド × 弾幕STGを融合した、レトロな8bitピクセルアート風ゲーム。

## ゲーム概要

最大4人のキャラクターでパーティを編成し、ステージごとに異なるボス敵と戦うゲームです。
カード選択、部位選択、アクションコマンド、弾幕回避の4つのフェーズで構成されるバトルシステムが特徴です。

### 主な特徴
- **ホットシート方式**: 1台のデバイスで最大4人がプレイ
- **フェーズ制バトル**: カード→部位→アクション→弾幕の4段階
- **8bitピクセルアート**: レトロな美咲ゴシックフォント使用
- **シーン遷移エフェクト**: フェード、スライド、ワイプの3種類

## クイックスタート

### 必要環境
- Unity 2022.3 LTS 以降
- Universal Render Pipeline (URP)
- Input System パッケージ

### プロジェクトを開く
1. Unityでプロジェクトを開く
2. `Assets/Scenes/TitleScene.unity` を開く
3. Playボタンを押してゲーム開始

### 操作方法
- **移動/選択**: 矢印キー or WASD
- **決定**: Enter / Space / Z
- **キャンセル**: Escape / X

## プロジェクト構成

```
アンダーザスポットライト/
├── Assets/
│   ├── Data/          # ゲームデータ（ScriptableObject）
│   ├── Editor/        # エディター拡張
│   ├── Images/        # 画像・スプライト
│   ├── Prefabs/       # プレハブ
│   ├── Resources/     # 動的読み込みリソース
│   ├── Scenes/        # シーンファイル
│   ├── Settings/      # プロジェクト設定
│   └── Src/           # ソースコード
│       ├── Battle/    # バトルシステム
│       ├── Card/      # カードシステム
│       ├── Character/ # キャラクター
│       ├── Dodge/     # 回避フェーズ
│       ├── Enemy/     # 敵システム
│       ├── MicroGame/ # ミニゲーム
│       ├── System/    # シーン管理
│       ├── UI/        # UIコンポーネント
│       └── Utils/     # ユーティリティ
├── Docs/              # ドキュメント・仕様書
│   ├── Specifications/    # 仕様書
│   ├── DEVELOPMENT_GUIDE.md  # 開発ガイド
│   └── README.md      # ドキュメント概要
└── README.md          # このファイル
```

## ドキュメント

開発を始める前に、必ず以下のドキュメントを確認してください：

### 必読ドキュメント
1. **[開発ガイドライン](Docs/DEVELOPMENT_GUIDE.md)** - 開発ルールとベストプラクティス
2. **[ゲーム全体仕様](Docs/Specifications/GAME_SPECIFICATION.md)** - ゲームシステムの完全仕様
3. **[ゲームフロー](Docs/Specifications/GAME_FLOW.md)** - シーン遷移とゲームフロー
4. **[フォルダ構成](Docs/Specifications/FOLDER_STRUCTURE.md)** - プロジェクト構造

### 開発者向けルール
- 実装前に必ず仕様書を確認
- 実装後に仕様との整合性を照合
- 仕様変更時は仕様書を更新

詳細は [Docs/DEVELOPMENT_GUIDE.md](Docs/DEVELOPMENT_GUIDE.md) を参照。

## ゲームフロー

```
タイトル → ワールドマップ → キャラ選択 → バトル → リザルト
  ↑                                              ↓
  └──────────────────────────────────────────────┘
```

### バトルフェーズ
1. **カード選択** - 各プレイヤーがアクションカードを選ぶ
2. **部位選択** - 攻撃対象の敵部位を選ぶ
3. **アクションコマンド** - ミニゲームでダメージ倍率を決定
4. **弾幕回避** - 敵の弾幕を回避して生き残る

## 開発

### 新機能を追加する場合

1. `Docs/Specifications/` の関連仕様を確認
2. 実装計画を立てる
3. `Assets/Src/` の適切なフォルダに実装
4. 仕様との整合性を確認
5. 必要に応じて仕様書を更新

### コーディング規約
- クラス名・メソッド名: PascalCase
- フィールド名: camelCase
- 日本語コメント推奨
- `[SerializeField]` で Inspector公開

詳細は [DEVELOPMENT_GUIDE.md](Docs/DEVELOPMENT_GUIDE.md) を参照。

## ビルド

### シーン構成
以下の順序でビルドに含まれます：
1. TitleScene
2. WorldMapScene
3. CharacterSelectScene
4. BattleScene
5. ResultScene

### ビルド設定
`File > Build Settings` で上記シーンが正しく登録されていることを確認してください。

## トラブルシューティング

### コンパイルエラー
- Input System パッケージがインストールされているか確認
- URP パッケージがインストールされているか確認

### シーンが見つからない
- `ProjectSettings/EditorBuildSettings.asset` にシーンが登録されているか確認

### 画像が表示されない
- Sprite の Import Settings が正しいか確認
- Resources フォルダ内のアセットが正しく配置されているか確認

## ライセンス

このプロジェクトで使用しているフォント：
- **美咲ゴシック** - 門真なむ氏作成、フリーフォント

## 連絡先

プロジェクト管理者: [情報追加予定]

---

制作: Under the Spotlight プロジェクト  
最終更新: 2024-06-20
