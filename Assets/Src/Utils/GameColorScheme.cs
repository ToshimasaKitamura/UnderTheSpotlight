using UnityEngine;

/// <summary>
/// ゲーム全体のカラースキームを管理する静的クラス。
/// テーマ「アンダーザスポットライト」= 舞台/劇場 × 8bit。
/// 正典（値・用途）は Docs/Specifications/デザインテーマ.md。変更時は両方を同期すること。
///
/// 3テーマ色:
///   Main   = ステージパープル（劇場の主色・面/背景寄り）
///   Sub    = トワイライトブルー（補助・味方/情報系）
///   Accent = スポットライトゴールド（強調・CTA・ハイライト）
/// </summary>
public static class GameColorScheme
{
    // ===== テーマ3色（正典） =====
    public static readonly Color Main = new Color(0.353f, 0.180f, 0.549f, 1f);    // #5A2E8C ステージパープル
    public static readonly Color Sub = new Color(0.180f, 0.294f, 0.549f, 1f);     // #2E4B8C トワイライトブルー
    public static readonly Color Accent = new Color(1.000f, 0.788f, 0.235f, 1f);  // #FFC93C スポットライトゴールド

    // ===== 基本パレット（正典） =====
    public static readonly Color Background = new Color(0.071f, 0.039f, 0.118f, 1f); // #120A1E カーテンブラック（最背面）
    public static readonly Color Surface = new Color(0.141f, 0.078f, 0.212f, 1f);    // #241436 パネル/カード面
    public static readonly Color TextOnDark = new Color(0.949f, 0.933f, 1.000f, 1f); // #F2EEFF 本文テキスト
    public static readonly Color TextMuted = new Color(0.702f, 0.651f, 0.800f, 1f);  // #B3A6CC 副次テキスト

    // ===== セマンティック（正典） =====
    public static readonly Color Success = new Color(0.400f, 0.851f, 0.498f, 1f); // #66D97F 成功/回復
    public static readonly Color Warning = new Color(1.000f, 0.690f, 0.125f, 1f); // #FFB020 警告
    public static readonly Color Danger = new Color(1.000f, 0.349f, 0.302f, 1f);  // #FF594D 危険/被弾

    // ===== プレイヤーカラー P1〜P4（正典） =====
    public static readonly Color[] Players =
    {
        new Color(1.000f, 0.349f, 0.349f, 1f), // P1 #FF5959 レッド
        new Color(0.349f, 0.651f, 1.000f, 1f), // P2 #59A6FF ブルー
        new Color(0.349f, 1.000f, 0.400f, 1f), // P3 #59FF66 グリーン
        new Color(1.000f, 0.851f, 0.349f, 1f), // P4 #FFD959 イエロー
    };

    // ===== 背景グラデ用（ディザ背景の上/下・テーマから導出） =====
    public static Color BackgroundTop => Color.Lerp(Background, Main, 0.22f);          // 上: 紫みを少し
    public static Color BackgroundBottom => Color.Lerp(Background, Color.black, 0.55f); // 下: より暗く

    // === ベースカラー（紫系） ===
    public static readonly Color PrimaryDark = new Color(0.12f, 0.05f, 0.18f, 1f);       // 深い紫
    public static readonly Color PrimaryMid = new Color(0.25f, 0.10f, 0.35f, 1f);        // 中間紫
    public static readonly Color PrimaryLight = new Color(0.45f, 0.25f, 0.55f, 1f);      // 明るい紫
    public static readonly Color PrimaryBright = new Color(0.65f, 0.40f, 0.75f, 1f);     // 鮮やかな紫

    // === アクセントカラー（黄色系） ===
    public static readonly Color AccentDark = new Color(0.6f, 0.5f, 0.1f, 1f);           // 暗い黄
    public static readonly Color AccentMid = new Color(0.9f, 0.75f, 0.2f, 1f);           // 中間黄
    public static readonly Color AccentBright = new Color(1f, 0.9f, 0.3f, 1f);           // 明るい黄
    public static readonly Color AccentGold = new Color(1f, 0.85f, 0.4f, 1f);            // ゴールド

    // === 味方ゾーン（青緑系） ===
    public static readonly Color AllyZoneDark = new Color(0.08f, 0.12f, 0.2f, 1f);       // 深い青
    public static readonly Color AllyZoneMid = new Color(0.15f, 0.25f, 0.35f, 1f);       // 中間青
    public static readonly Color AllyZoneLight = new Color(0.3f, 0.5f, 0.6f, 1f);        // 明るい青緑

    // === 敵ゾーン（赤紫系） ===
    public static readonly Color EnemyZoneDark = new Color(0.2f, 0.05f, 0.1f, 1f);       // 深い赤紫
    public static readonly Color EnemyZoneMid = new Color(0.35f, 0.08f, 0.15f, 1f);      // 中間赤紫
    public static readonly Color EnemyZoneLight = new Color(0.55f, 0.15f, 0.25f, 1f);    // 明るい赤

    // === 中立/フィールド ===
    public static readonly Color FieldDark = new Color(0.06f, 0.04f, 0.08f, 1f);         // 暗い紫黒
    public static readonly Color FieldMid = new Color(0.12f, 0.08f, 0.15f, 1f);          // 中間
    public static readonly Color FieldBorder = new Color(0.5f, 0.35f, 0.6f, 1f);         // 枠線(紫)

    // === UI カラー ===
    public static readonly Color TextPrimary = new Color(0.95f, 0.92f, 1f, 1f);          // 白っぽい紫
    public static readonly Color TextSecondary = new Color(0.7f, 0.65f, 0.8f, 1f);       // 薄紫
    public static readonly Color TextHighlight = AccentBright;                            // ハイライト(黄)
    public static readonly Color TextDanger = new Color(1f, 0.35f, 0.3f, 1f);            // 危険(赤)

    // === HP バー ===
    public static readonly Color HpFull = new Color(0.4f, 0.85f, 0.5f, 1f);              // 緑
    public static readonly Color HpDanger = new Color(1f, 0.35f, 0.3f, 1f);              // 赤
    public static readonly Color HpBackground = new Color(0.1f, 0.08f, 0.12f, 0.9f);     // 暗紫

    // === 弾幕フレーム ===
    public static readonly Color FrameOuter = new Color(0.5f, 0.35f, 0.6f, 1f);          // 外枠(紫)
    public static readonly Color FrameInner = new Color(0.3f, 0.2f, 0.4f, 1f);           // 内枠(暗紫)
    public static readonly Color FrameAccent = AccentMid;                                 // アクセント(黄)
}
