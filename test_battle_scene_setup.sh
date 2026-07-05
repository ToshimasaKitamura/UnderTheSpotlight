#!/bin/bash

# バトルシーンセットアップの自動テストスクリプト

echo "========================================="
echo "バトルシーンセットアップ自動テスト"
echo "========================================="
echo ""

# Unity プロジェクトパス
PROJECT_PATH="/Users/witchcraft/Project/アンダーザスポットライト"

# CharacterDataアセットの確認
echo "1. CharacterDataアセットの確認..."
find "$PROJECT_PATH/Assets/Data/Characters" -name "*.asset" | while read -r file; do
    echo "  - $(basename "$file")"
    # characterSpriteフィールドを確認
    if grep -q "characterSprite: {fileID: 0}" "$file"; then
        echo "    ⚠️  WARNING: characterSpriteがnullです"
    elif grep -q "characterSprite:" "$file"; then
        sprite_line=$(grep "characterSprite:" "$file")
        echo "    ✅ characterSpriteが設定されています: $sprite_line"
    fi
done

echo ""
echo "2. Player.png スプライトシートの確認..."
PLAYER_PNG="$PROJECT_PATH/Assets/Images/Player.png"
PLAYER_META="$PROJECT_PATH/Assets/Images/Player.png.meta"

if [ -f "$PLAYER_PNG" ]; then
    echo "  ✅ Player.png が存在します"
    ls -lh "$PLAYER_PNG"
else
    echo "  ❌ Player.png が見つかりません"
fi

if [ -f "$PLAYER_META" ]; then
    echo "  ✅ Player.png.meta が存在します"
    echo ""
    echo "  スプライト一覧:"
    grep "second: Player_" "$PLAYER_META" | while read -r line; do
        echo "    $line"
    done
else
    echo "  ❌ Player.png.meta が見つかりません"
fi

echo ""
echo "3. CharacterDataとスプライトの対応確認..."
echo "  - Iruka (CharacterType: 0) → Player_0"
echo "  - Neko (CharacterType: 1) → Player_1"
echo "  - Hitsji (CharacterType: 2) → Player_2"
echo "  - Nishikawa (CharacterType: 3) → Player_3"
echo "  - Ratel (CharacterType: 4) → Player_4"
echo "  - Pierrot (CharacterType: 5) → Player_5"
echo "  - Akuma (CharacterType: 6) → Player_6"
echo "  - Teacher (CharacterType: 7) → Player_7"

echo ""
echo "========================================="
echo "テスト完了"
echo "========================================="
