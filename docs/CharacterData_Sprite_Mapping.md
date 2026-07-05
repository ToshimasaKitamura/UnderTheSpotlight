# CharacterData とスプライトの対応表

## 完全な対応関係

| ファイル名 | m_Name | displayName | CharacterType | CharacterType値 | characterSprite | スプライト名 |
|-----------|--------|-------------|---------------|----------------|----------------|-------------|
| Iruka.asset | Iruka | イルカ | Iruka | 0 | 1700000001 | Player_0 |
| Neko.asset | Neko | ネコ | Neko | 1 | 1700000002 | Player_1 |
| Hitsuji.asset | Hitsuji | ヒツジ | Hitsji | 2 | 1700000003 | Player_2 |
| Nishikawa.asset | Nishikawa | ニシカワ | Nishikawa | 3 | 1700000004 | Player_3 |
| Ratel.asset | Ratel | ラーテル | Ratel | 4 | 1700000005 | Player_4 |
| Pierrot.asset | Pierrot | ピエロ | Pierrot | 5 | 1700000006 | Player_5 |
| Akuma.asset | Akuma | アクマ | Akuma | 6 | 1700000007 | Player_6 |
| SugakuSensei.asset | SugakuSensei | スガク先生 | Teacher | 7 | 1700000008 | Player_7 |

## デフォルトパーティ（バトルシーン）

BattleSceneSetup.csで使用されるデフォルトパーティ:

| Player | CharacterType | displayName | スプライト | 説明 |
|--------|--------------|-------------|-----------|------|
| Player1 | Iruka (0) | イルカ | Player_0 | 青いイルカ |
| Player2 | Neko (1) | ネコ | Player_1 | ピンクのネコ |
| Player3 | Hitsji (2) | ヒツジ | Player_2 | 紫のヒツジ |
| Player4 | Nishikawa (3) | ニシカワ | Player_3 | 黄色のニシカワ |

## スプライトシート: Player.png

### スプライト一覧

| インデックス | スプライト名 | fileID | 対応CharacterData |
|------------|-------------|--------|-------------------|
| 0 | Player_0 | 1700000001 | Iruka |
| 1 | Player_1 | 1700000002 | Neko |
| 2 | Player_2 | 1700000003 | Hitsuji |
| 3 | Player_3 | 1700000004 | Nishikawa |
| 4 | Player_4 | 1700000005 | Ratel |
| 5 | Player_5 | 1700000006 | Pierrot |
| 6 | Player_6 | 1700000007 | Akuma |
| 7 | Player_7 | 1700000008 | SugakuSensei |
| 8 | Player_8 | 1700000009 | (未使用) |

### スプライト座標

| スプライト | x | y | width | height | 備考 |
|----------|---|---|-------|--------|------|
| Player_0 | 19 | 515 | 240 | 442 | イルカ |
| Player_1 | 323 | 508 | 242 | 499 | ネコ |
| Player_2 | 565 | 496 | 284 | 470 | ヒツジ |
| Player_3 | 850 | 507 | 270 | 460 | ニシカワ |
| Player_4 | 1120 | 509 | 283 | 487 | ラーテル |
| Player_5 | 265 | 0 | 276 | 453 | ピエロ |
| Player_6 | 541 | 0 | 334 | 467 | アクマ |
| Player_7 | 875 | 2 | 233 | 439 | スガク先生 |
| Player_8 | 1112 | 13 | 261 | 427 | (未使用) |

## CharacterType enum定義

```csharp
public enum CharacterType
{
    Iruka,      // 0 - イルカ - 基本的な攻撃キャラ
    Neko,       // 1 - ネコ - 基本的な防御キャラ
    Hitsji,     // 2 - ヒツジ - 基本的な回復キャラ (スペル注意!)
    Nishikawa,  // 3 - ニシカワ - デッキ破壊特殊キャラ
    Ratel,      // 4 - ラーテル - ハイリスク攻撃キャラ
    Pierrot,    // 5 - ピエロ - 特殊防御キャラ
    Akuma,      // 6 - アクマ - 特殊回復キャラ
    Teacher     // 7 - スガクティーチャー - ランダム要素キャラ
}
```

## 検証コマンド

### CharacterDataアセットの確認

```bash
find Assets/Data/Characters -name "*.asset" -exec grep -H "m_Name:" {} \;
find Assets/Data/Characters -name "*.asset" -exec grep -H "characterType:" {} \;
find Assets/Data/Characters -name "*.asset" -exec grep -H "characterSprite:" {} \;
```

### スプライトシートの確認

```bash
grep "second: Player_" Assets/Images/Player.png.meta
```

## Unity内での検証

### 1. CharacterDataロードテスト

```
Tools > Test Character Data Loading
```

期待される出力:
```
検出されたCharacterData数: 8
パス: Assets/Data/Characters/Iruka.asset
  name: Iruka
  displayName: イルカ
  characterType: Iruka
  characterSprite: Player_0
  sprite.texture: Player
```

### 2. デフォルトパーティ選択テスト

```
Tools > Test Default Party Selection
```

期待される出力:
```
デフォルトパーティ (CharacterTypeで検索):
  P1: イルカ (CharacterType: Iruka)
    name: Iruka
    sprite: Player_0
  P2: ネコ (CharacterType: Neko)
    name: Neko
    sprite: Player_1
  P3: ヒツジ (CharacterType: Hitsji)
    name: Hitsuji
    sprite: Player_2
  P4: ニシカワ (CharacterType: Nishikawa)
    name: Nishikawa
    sprite: Player_3
```

### 3. 完全セットアップ＆検証

```
Tools > Full Setup and Validation Test
```

期待される出力:
```
[BattleSceneSetup] Player1にイルカのスプライトを設定 (sprite=Player_0)
[BattleSceneSetup] Player2にネコのスプライトを設定 (sprite=Player_1)
[BattleSceneSetup] Player3にヒツジのスプライトを設定 (sprite=Player_2)
[BattleSceneSetup] Player4にニシカワのスプライトを設定 (sprite=Player_3)

✅ Player1: sprite = Player_0
✅ Player2: sprite = Player_1
✅ Player3: sprite = Player_2
✅ Player4: sprite = Player_3
```

## トラブルシューティング

### 問題: スプライトがnullと表示される

**原因:** CharacterData.characterSpriteがnull

**解決策:**
1. UnityエディタでCharacterDataアセットを開く
2. Character Spriteフィールドを確認
3. Player.pngから対応するスプライトを設定

### 問題: 仮スプライト（四角形）が表示される

**原因1:** CharacterDataのロードに失敗

**解決策:**
```
Tools > Test Character Data Loading
```
で確認

**原因2:** CharacterTypeの検索に失敗

**解決策:**
```
Tools > Test Default Party Selection
```
で確認

### 問題: m_Nameとファイル名が一致しない

**解決策:**
```bash
cd Assets/Data/Characters
for file in *.asset; do
    basename="${file%.asset}"
    sed -i '' "s/m_Name: [^$]/m_Name: $basename/" "$file"
done
```

---

更新日: 2026-06-20
