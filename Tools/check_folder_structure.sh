#!/usr/bin/env bash
#
# フォルダ構成チェック
# Docs/フォルダ構成.md の配置ルールに対する逸脱を検出する。
#
#   使い方: bash Tools/check_folder_structure.sh [--quiet]
#     （引数なし）: 全結果を stdout に出力。違反があれば終了コード 1
#     --quiet     : 違反(error)が無ければ無音・exit 0。違反があれば stderr に出し exit 2
#                   （Claude Code の PostToolUse フック用。exit 2 で Claude に通知される）
#
set -u

cd "$(dirname "$0")/.." || exit 0
[ -d Assets ] || exit 0   # Assets が無い環境では何もしない

QUIET=0
[ "${1:-}" = "--quiet" ] && QUIET=1

errmsgs=()
warnmsgs=()
err()  { errmsgs+=("  [違反] $1"); }
warn() { warnmsgs+=("  [警告] $1"); }

# 1) Assets 直下に .cs を置かない
while IFS= read -r f; do [ -n "$f" ] && err ".cs が Assets 直下 → Assets/Src/<カテゴリ>/ へ: $f"; done \
  < <(find Assets -maxdepth 1 -name '*.cs' 2>/dev/null)

# 2) Assets/Src 直下に .cs を置かない（カテゴリ分け必須）
while IFS= read -r f; do [ -n "$f" ] && err ".cs が Assets/Src 直下 → サブフォルダへ分類: $f"; done \
  < <(find Assets/Src -maxdepth 1 -name '*.cs' 2>/dev/null)

# 3) エディタ専用スクリプトは Editor/ 配下必須（ビルドが壊れるため）
while IFS= read -r f; do
  [ -n "$f" ] || continue
  case "$f" in */Editor/*) continue;; esac
  if grep -qE '^\s*using\s+UnityEditor\s*;|:\s*EditorWindow\b|:\s*Editor\b|\[\s*MenuItem|\[\s*CustomEditor|\[\s*InitializeOnLoad' "$f" 2>/dev/null; then
    err "エディタ専用APIを使う .cs が Editor/ 配下に無い → Assets/Editor/ へ: $f"
  fi
done < <(find Assets -name '*.cs' 2>/dev/null)

# 4) .unity は Assets/Scenes/ のみ（テンプレ/バックアップは除外）
while IFS= read -r f; do
  [ -n "$f" ] || continue
  case "$f" in
    Assets/Scenes/*) : ;;
    Assets/_Recovery/*) : ;;                # Unity バックアップ残骸
    Assets/Settings/*) : ;;                  # URP のシーンテンプレート
    "Assets/TextMesh Pro/"*) : ;;           # TMP パッケージ標準
    *) warn ".unity が Assets/Scenes 以外にある: $f" ;;
  esac
done < <(find Assets -name '*.unity' 2>/dev/null)

# 5) Assets/Scenes に .cs / .prefab を置かない
while IFS= read -r f; do [ -n "$f" ] && err "Scenes フォルダにスクリプト/プレハブがある: $f"; done \
  < <(find Assets/Scenes -maxdepth 1 \( -name '*.cs' -o -name '*.prefab' \) 2>/dev/null)

# 6) .prefab は原則 Assets/Prefabs/（Resources 内の意図的配置は許容→警告のみ）
while IFS= read -r f; do
  [ -n "$f" ] || continue
  case "$f" in
    Assets/Prefabs/*) : ;;
    Assets/Resources/*) : ;;
    *) warn ".prefab が Assets/Prefabs 以外にある: $f" ;;
  esac
done < <(find Assets -name '*.prefab' 2>/dev/null)

ne=${#errmsgs[@]}
nw=${#warnmsgs[@]}

# --quiet: 違反のみ stderr、無ければ無音
if [ "$QUIET" -eq 1 ]; then
  if [ "$ne" -gt 0 ]; then
    {
      echo "フォルダ構成の違反 ${ne} 件（Docs/フォルダ構成.md 準拠に修正してください）:"
      printf '%s\n' "${errmsgs[@]}"
    } >&2
    exit 2
  fi
  exit 0
fi

# 通常モード: 全部 stdout
if [ "$ne" -eq 0 ] && [ "$nw" -eq 0 ]; then
  echo "✓ フォルダ構成OK（逸脱なし）— Docs/フォルダ構成.md 準拠"
  exit 0
fi
[ "$ne" -gt 0 ] && printf '%s\n' "${errmsgs[@]}"
[ "$nw" -gt 0 ] && printf '%s\n' "${warnmsgs[@]}"
echo "── 違反 ${ne} 件 / 警告 ${nw} 件（詳細: Docs/フォルダ構成.md）──"
[ "$ne" -gt 0 ] && exit 1
exit 0
