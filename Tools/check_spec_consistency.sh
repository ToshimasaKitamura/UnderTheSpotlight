#!/usr/bin/env bash
#
# 仕様ドキュメントの整合性チェック（矛盾検出）
# 「仕様を矛盾なく記述する」ための自動検査。以下を検出する:
#   [違反] ドキュメント内の相対リンク切れ（存在しないファイルを指す）
#   [違反] Specifications が言及するシーン名が Assets/Scenes に実在しない
#   [警告] Specifications が言及するクラス名(`Xxx`)が Assets/Src に実在しない
#   [警告] 同じ H2 見出しが複数の Specifications 文書にある（単一所有違反＝矛盾源）
#
#   使い方: bash Tools/check_spec_consistency.sh [--quiet]
#     --quiet : 違反(error)が無ければ無音・exit 0 / 違反あれば stderr に出し exit 2（フック用）
#   終了コード: 違反1件以上で 1（通常）/ 2（--quiet）、それ以外 0
#
set -u
cd "$(dirname "$0")/.." || exit 0
[ -d Docs ] || exit 0

QUIET=0
[ "${1:-}" = "--quiet" ] && QUIET=1

errmsgs=(); warnmsgs=()
err()  { errmsgs+=("  [違反] $1"); }
warn() { warnmsgs+=("  [警告] $1"); }

docs=()
while IFS= read -r d; do docs+=("$d"); done < <(find Docs README.md -name "*.md" 2>/dev/null)

# 1) 相対リンク切れ
for f in "${docs[@]}"; do
  dir=$(dirname "$f")
  # [text](target) の target を抽出
  while IFS= read -r link; do
    [ -n "$link" ] || continue
    case "$link" in
      http*|\#*|mailto:*) continue ;;      # 外部/アンカーは対象外
    esac
    target="${link%%#*}"                     # アンカー除去
    [ -z "$target" ] && continue
    case "$target" in
      /*) path=".${target}" ;;               # ルート相対
      *)  path="$dir/$target" ;;             # ファイル相対
    esac
    if [ ! -e "$path" ]; then
      err "リンク切れ: $f → $link"
    fi
  done < <(grep -oE '\]\([^)]+\)' "$f" 2>/dev/null | sed -E 's/^\]\(//; s/\)$//')
done

# 2) Specifications が言及するシーンが実在するか
specs=()
while IFS= read -r s; do specs+=("$s"); done < <(find Docs/Specifications -name "*.md" 2>/dev/null)
if [ "${#specs[@]}" -gt 0 ]; then
  while IFS= read -r scene; do
    [ -n "$scene" ] || continue
    if [ ! -f "Assets/Scenes/$scene.unity" ]; then
      err "仕様が参照するシーンが存在しない: $scene（Assets/Scenes/$scene.unity 無し）"
    fi
  done < <(grep -rhoE '[A-Za-z]+Scene\b' "${specs[@]}" 2>/dev/null | sort -u)

  # 3) Specifications が言及するクラス(`Xxx`)が実在するか（警告）
  while IFS= read -r cls; do
    [ -n "$cls" ] || continue
    grep -rqlE "class\s+$cls\b|enum\s+$cls\b|interface\s+$cls\b" Assets/Src --include="*.cs" 2>/dev/null \
      || warn "仕様が参照する型が Assets/Src に見つからない: $cls"
  done < <(grep -rhoE '`[A-Z][A-Za-z0-9_]*(Controller|Phase|Manager|View|System|Machine|Data|Definition)`' "${specs[@]}" 2>/dev/null | tr -d '`' | sort -u)

  # 4) 同一 H2 見出しが複数 Specifications 文書にある（単一所有違反）
  while IFS= read -r h; do
    [ -n "$h" ] || continue
    cnt=$(grep -rlE "^##[[:space:]]+$h$" "${specs[@]}" 2>/dev/null | wc -l | tr -d ' ')
    [ "$cnt" -gt 1 ] && warn "見出し「$h」が $cnt 個の仕様書に重複（正典を1つに）"
  done < <(grep -rhE '^##[[:space:]]+' "${specs[@]}" 2>/dev/null | sed -E 's/^##[[:space:]]+//' | sort | uniq -d)
fi

ne=${#errmsgs[@]}; nw=${#warnmsgs[@]}

if [ "$QUIET" -eq 1 ]; then
  if [ "$ne" -gt 0 ]; then
    { echo "仕様ドキュメントの矛盾 ${ne} 件:"; printf '%s\n' "${errmsgs[@]}"; } >&2
    exit 2
  fi
  exit 0
fi

if [ "$ne" -eq 0 ] && [ "$nw" -eq 0 ]; then
  echo "✓ 仕様ドキュメント整合性OK（リンク・シーン・型・単一所有）"
  exit 0
fi
[ "$ne" -gt 0 ] && printf '%s\n' "${errmsgs[@]}"
[ "$nw" -gt 0 ] && printf '%s\n' "${warnmsgs[@]}"
echo "── 違反 ${ne} 件 / 警告 ${nw} 件（正典と所有の考え方: Docs/README.md）──"
[ "$ne" -gt 0 ] && exit 1
exit 0
