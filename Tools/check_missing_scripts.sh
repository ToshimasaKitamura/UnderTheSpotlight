#!/usr/bin/env bash
#
# Missing Script 検出
# シーン/プレハブ/アセットの m_Script guid が、実在するスクリプト(.cs.meta / .dll.meta)に
# 解決するかを検査する。解決しない＝Missing Script（偽/旧 guid）で、画面が出ない・データが空
# といった不具合の典型原因（このプロジェクトで多発）。
#
#   使い方: bash Tools/check_missing_scripts.sh [--quiet]
#     --quiet : 違反が無ければ無音・exit 0 / あれば stderr に出し exit 2（フック用）
#   終了コード: Missing Script が1件以上で 1（通常）/ 2（--quiet）、無ければ 0
#
set -u
cd "$(dirname "$0")/.." || exit 0
[ -d Assets ] || exit 0

QUIET=0
[ "${1:-}" = "--quiet" ] && QUIET=1

# 実在スクリプトの guid 集合（プロジェクト Assets ＋ パッケージキャッシュの .cs/.dll メタ）
known="$(mktemp)"
{
  grep -rh "guid:" Assets --include="*.cs.meta" --include="*.dll.meta" 2>/dev/null
  grep -rh "guid:" Library/PackageCache --include="*.cs.meta" --include="*.dll.meta" 2>/dev/null
} | grep -oE "[a-f0-9]{32}" | sort -u > "$known"

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
  echo "✓ Missing Script なし（全 m_Script guid が実スクリプトに解決）"
  exit 0
fi
printf '%s\n' "${msgs[@]}"
echo "── Missing Script ${n} 件 ──"
echo "  対処: 該当 .cs.meta の guid に m_Script を修正（正典: 開発ナレッジ.md「Missing Script」）"
exit 1
