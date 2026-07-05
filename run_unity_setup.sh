#!/bin/bash

# Unityバッチモードでセットアップを実行

UNITY_PATH="/Applications/Unity/Hub/Editor/2022.3.30f1/Unity.app/Contents/MacOS/Unity"
PROJECT_PATH="/Users/witchcraft/Project/アンダーザスポットライト"
LOG_FILE="$PROJECT_PATH/unity_setup.log"

echo "========================================="
echo "Unity バトルシーンセットアップ実行"
echo "========================================="
echo "プロジェクト: $PROJECT_PATH"
echo "ログファイル: $LOG_FILE"
echo ""

# Unityバッチモードで実行
"$UNITY_PATH" \
    -batchmode \
    -quit \
    -projectPath "$PROJECT_PATH" \
    -executeMethod BattleSceneSetup.SetupBattleScene \
    -logFile "$LOG_FILE"

EXIT_CODE=$?

echo ""
echo "========================================="
echo "実行完了 (終了コード: $EXIT_CODE)"
echo "========================================="
echo ""

# ログの最後100行を表示
echo "ログ（最後100行）:"
echo "---"
tail -n 100 "$LOG_FILE"
echo "---"

exit $EXIT_CODE
