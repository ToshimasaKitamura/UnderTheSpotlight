#!/usr/bin/env bash
#
# 修正PDCAの「Check」ゲート: 自動検査を一括実行する。
#   - フォルダ構成の逸脱     (Tools/check_folder_structure.sh)
#   - 仕様ドキュメントの矛盾 (Tools/check_spec_consistency.sh)
#   - Unity コンパイルエラー (Editor.log の直近 error CS。古いアセンブリで動く罠の検出)
#
#   使い方: bash Tools/check_all.sh
#   終了コード: いずれかに違反(error)があれば 1、無ければ 0
#
# ※ これは自動化できる部分のみ。PDCAのCheckでは加えて「症状を再現→解消を確認」
#   「デグレが無いか」を人/AIが確認すること（Docs/修正PDCA.md 参照）。
#
set -u
cd "$(dirname "$0")/.." || exit 0

fail=0

echo "▶ フォルダ構成チェック"
bash Tools/check_folder_structure.sh || fail=1

echo ""
echo "▶ 仕様整合性チェック"
bash Tools/check_spec_consistency.sh || fail=1

echo ""
echo "▶ Missing Script チェック"
bash Tools/check_missing_scripts.sh || fail=1

echo ""
echo "▶ Unity コンパイルエラー（直近ログ）"
LOG="$HOME/Library/Logs/Unity/Editor.log"
if [ -f "$LOG" ]; then
  last_err=$(grep -nE "error CS[0-9]+" "$LOG" | tail -1)
  last_reload=$(grep -nE "Reloading assemblies|Begin MonoManager ReloadAssembly" "$LOG" | tail -1)
  el=${last_err%%:*}; rl=${last_reload%%:*}
  if [ -n "$last_err" ] && { [ -z "$last_reload" ] || [ "${el:-0}" -gt "${rl:-0}" ]; }; then
    echo "  [注意] 直近ログにコンパイルエラーの形跡: $last_err"
    echo "         → 古いアセンブリで動作している可能性。Unity で再コンパイルを確認（CLAUDE.md 参照）"
  else
    echo "  ✓ 直近ログに未解決のコンパイルエラーは見当たらない"
  fi
else
  echo "  （Editor.log 無し。スキップ）"
fi

echo ""
if [ "$fail" -eq 0 ]; then
  echo "═══ Check: 自動検査は合格。あとは『症状の再現→解消』と『デグレ無し』を確認 ═══"
else
  echo "═══ Check: 違反あり。Plan に戻って直す（Docs/修正PDCA.md）═══"
fi
exit $fail
