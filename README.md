# アンダーザスポットライト (Under the Spotlight)

ターン制カードバトル × アクションコマンド × 弾幕STGを融合した、レトロな8bitピクセルアート風ゲーム。

最大4人でパーティを組み、ステージごとに異なるボスと戦う。バトルは
**カード選択 → 部位選択 → アクションコマンド(ミニゲーム) → 弾幕回避** を繰り返して進む。
1台のキーボード/ゲームパッドで遊ぶローカル多人数(ホットシート)と、部屋コードによるオンライン参加に対応。

## クイックスタート

### 必要環境
- **Unity 6000.3.13f1**(Unity 6.3。バージョンは `ProjectSettings/ProjectVersion.txt` が正)
- 必要なパッケージ(URP / Input System / Netcode for GameObjects / Unity Relay など)は `Packages/manifest.json` に記載済み。Unity で開くと自動で入る

### 遊ぶ
1. Unity Hub からプロジェクトを開く
2. `Assets/Scenes/TitleScene.unity` を開いて Play
3. タイトル → ワールドマップ → キャラ選択 → バトル → リザルト

> BattleScene を直接再生すると、ステージ・ボスのデータが読み込まれない。動作確認はタイトルから始めるか、
> デバッグ室(バトル中に Esc。エディタ / Development Build のみ)を使う。

操作方法は [docs/Specifications/GAME_FLOW.md](docs/Specifications/GAME_FLOW.md) の各画面を参照。

## ドキュメント

仕様・設計の索引は **[docs/README.md](docs/README.md)**(どのトピックをどの文書が決めているか=正典の一覧)。主なもの:

| 内容 | 文書 |
|---|---|
| ゲーム全体の仕様 | [docs/Specifications/GAME_SPECIFICATION.md](docs/Specifications/GAME_SPECIFICATION.md) |
| シーン遷移と各画面の操作 | [docs/Specifications/GAME_FLOW.md](docs/Specifications/GAME_FLOW.md) |
| フォルダ構成・ファイルの置き場所 | [docs/フォルダ構成.md](docs/フォルダ構成.md) |
| 数値調整(CSV / Excel) | [docs/Design/CsvWorkflow.md](docs/Design/CsvWorkflow.md) |
| Git の運用(ブランチ・リリース) | [docs/Git運用.md](docs/Git運用.md) |
| 修正の経緯と既知の罠 | [docs/開発ナレッジ.md](docs/開発ナレッジ.md) |
| AI(Claude Code)向けのルール | [CLAUDE.md](CLAUDE.md) |

## 開発

- 作業は `develop` から `feature/<内容>` / `fix/<内容>` を切って行い、PR で `develop` に取り込む。`main` はリリース専用([docs/Git運用.md](docs/Git運用.md))
- 変更したら次の自動チェックを通す(フォルダ構成・仕様の整合性・Missing Script):

```bash
bash Tools/check_all.sh
```

- ゲームの挙動を変えたら、対応する正典(仕様書)も更新する([docs/README.md](docs/README.md))

## ライセンス(使用フォント)

- **美咲ゴシック**(`Assets/Resources/Fonts/misaki_gothic.ttf`) — 門真なむ 氏
- **PixelMplus12**(`Assets/Resources/Fonts/PixelMplus12-Regular.ttf`) — itouhiro 氏(M+ FONTS ライセンス)

各フォントの配布元のライセンスに従う。
