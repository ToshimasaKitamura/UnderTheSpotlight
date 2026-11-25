#!/usr/bin/env bash
#
# Missing Script 検出
# シーン/プレハブ/アセットの m_Script guid が、実在するスクリプト(.cs.meta / .dll.meta)に
# 解決するかを検査する。解決しない＝Missing Script（偽/旧 guid）で、画面が出ない・データが空
# といった不具合の典型原因（このプロジェクトで多発）。
#
#   使い方: bash Tools/check_missing_scripts.sh [--quiet | --update-package-list]
#     --quiet : 違反が無ければ無音・exit 0 / あれば stderr に出し exit 2（フック用）
#     --update-package-list : Library/PackageCache から、アセットが使っているパッケージ側スクリプトの guid を
#                             Tools/known_package_script_guids.txt に書き出す(Unity で開いた環境で実行)
#
#   パッケージ(URP/uGUI/Input System 等)のスクリプトは Library/PackageCache にある。Unity で開いていない環境
#   (クラウド/CI)には無いため、そこでは既知リスト Tools/known_package_script_guids.txt で代わりに判定する。
#   終了コード: Missing Script が1件以上で 1（通常）/ 2（--quiet）、無ければ 0
#
set -u
cd "$(dirname "$0")/.." || exit 0
[ -d Assets ] || exit 0

QUIET=0
UPDATE_LIST=0
[ "${1:-}" = "--quiet" ] && QUIET=1
[ "${1:-}" = "--update-package-list" ] && UPDATE_LIST=1
PKG_LIST="Tools/known_package_script_guids.txt"
HAS_CACHE=0
[ -d Library/PackageCache ] && HAS_CACHE=1

# 実在スクリプトの guid 集合（プロジェクト Assets ＋ パッケージキャッシュの .cs/.dll メタ）
known="$(mktemp)"
{
  grep -rh "guid:" Assets --include="*.cs.meta" --include="*.dll.meta" 2>/dev/null
  if [ "$HAS_CACHE" -eq 1 ]; then
    grep -rh "guid:" Library/PackageCache --include="*.cs.meta" --include="*.dll.meta" 2>/dev/null
  elif [ -f "$PKG_LIST" ]; then
    grep -oE "^[a-f0-9]{32}" "$PKG_LIST"   # Unity で開いていない環境: 既知のパッケージ側 guid で代用
  fi
} | grep -oE "[a-f0-9]{32}" | sort -u > "$known"

# 既知リストの更新(パッケージ側 guid のうち、アセットが実際に使っているものだけ)
if [ "$UPDATE_LIST" -eq 1 ]; then
  if [ "$HAS_CACHE" -ne 1 ]; then
    echo "Library/PackageCache がありません。Unity でプロジェクトを一度開いてから実行してください。" >&2
    rm -f "$known"; exit 1
  fi
  pkg="$(mktemp)"
  grep -rh "guid:" Library/PackageCache --include="*.cs.meta" --include="*.dll.meta" 2>/dev/null | grep -oE "[a-f0-9]{32}" | sort -u > "$pkg"
  used="$(mktemp)"
  find Assets \( -name "*.asset" -o -name "*.prefab" -o -name "*.unity" \) -not -path "*/TextMesh Pro/*" -print0 2>/dev/null \
    | xargs -0 grep -ohE "m_Script: \{fileID: [0-9-]+, guid: [a-f0-9]{32}" 2>/dev/null | grep -oE "[a-f0-9]{32}$" | sort -u > "$used"
  {
    echo "# Unity パッケージ側スクリプトの guid 一覧(check_missing_scripts.sh 用)"
    echo "# Library/PackageCache が無い環境(クラウド/CI)でのみ使う。Unity で開いた環境では PackageCache から正確に判定する。"
    echo "# 更新: Unity でプロジェクトを開いた後に  bash Tools/check_missing_scripts.sh --update-package-list"
    echo "# 形式: guid"
    comm -12 "$used" "$pkg"
  } > "$PKG_LIST"
  echo "✓ $PKG_LIST を更新しました($(comm -12 "$used" "$pkg" | wc -l) 件)"
  rm -f "$known" "$pkg" "$used"; exit 0
fi

msgs=()
while IFS= read -r file; do
  [ -n "$file" ] || continue
  # m_Script の guid をすべて取り出して解決確認
  while IFS= read -r g; do
    [ -n "$g" ] || continue
    grep -q "^$g$" "$known" || msgs+=("  [Missing] $file  (m_Script guid=$g)")
  done < <(grep -oE "m_Script: \{fileID: [0-9-]+, guid: [a-f0-9]{32}" "$file" 2>/dev/null | grep -oE "[a-f0-9]{32}$" | sort -u)
done < <(find Assets \( -name "*.asset" -o -name "*.prefab" -o -name "*.unity" \) -not -path "*/TextMesh Pro/*" 2>/dev/null)

rm -f "$known"
n=${#msgs[@]}

if [ "$QUIET" -eq 1 ]; then
  if [ "$n" -gt 0 ]; then
    { echo "Missing Script ${n} 件（.asset/.unity/.prefab の m_Script guid が実スクリプトに解決しない）:"; printf '%s\n' "${msgs[@]}"; } >&2
    exit 2
  fi
  exit 0
fi

if [ "$n" -eq 0 ]; then
  if [ "$HAS_CACHE" -eq 1 ]; then
    echo "✓ Missing Script なし（全 m_Script guid が実スクリプトに解決）"
  else
    echo "✓ Missing Script なし（Library/PackageCache が無いため、パッケージ側は既知リスト $PKG_LIST で判定）"
  fi
  exit 0
fi
printf '%s\n' "${msgs[@]}"
echo "── Missing Script ${n} 件 ──"
echo "  対処: 該当 .cs.meta の guid に m_Script を修正（正典: 開発ナレッジ.md「Missing Script」）"
[ "$HAS_CACHE" -eq 1 ] || echo "  ※ Library/PackageCache が無い環境です。新しく使い始めたパッケージのスクリプトなら、Unity で開いた環境で --update-package-list を実行して $PKG_LIST を更新してください"

exit 1
