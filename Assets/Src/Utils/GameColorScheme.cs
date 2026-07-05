using UnityEngine;

/// <summary>
/// ゲーム全体のカラースキームを管理する静的クラス。
/// ベースカラー: 紫系、アクセントカラー: 黄色系
/// </summary>
public static class GameColorScheme
{
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
