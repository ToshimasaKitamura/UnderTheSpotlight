using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カード選択フェーズの舞台演出。
/// カメラを主人公側へ寄せ(ズームイン)、アクティブキャラを画面左に巨大表示する。
/// カードは右側(CardHandView, スクリーン空間)で選ぶ前提なので、左をキャラ・右をカードに分離。
///
/// 使い方:
///   var stage = obj.AddComponent&lt;CardSelectStage&gt;();
///   stage.Initialize(playerTransforms);
///   stage.FocusPlayer(i);   // i 番にカメラ寄せ + 強調
///   stage.PlayReaction(i);  // 選択時リアクション
///   stage.Restore();        // 終了でカメラ/キャラ復帰
/// </summary>
public class CardSelectStage : MonoBehaviour
{
    [Header("カメラ")]
    [Tooltip("動かすカメラ。未設定なら Camera.main → 最初に見つかった Camera。")]
    public Camera targetCamera;
    [Tooltip("元 orthographicSize に対するズーム率(小さいほど寄る)。")]
    public float zoomFactor = 0.5f;
    [Tooltip("アクティブキャラを画面横どこに置くか(0=左端,1=右端)。左に大きく出すので小さめ。")]
    [Range(0f, 1f)] public float activeScreenX = 0.26f;
    [Tooltip("アクティブキャラを画面縦どこに置くか(0=下,1=上)。")]
    [Range(0f, 1f)] public float activeScreenY = 0.5f;
    [Tooltip("カメラ遷移の秒数。")]
    public float cameraMoveDuration = 0.6f;

    [Header("キャラ強調")]
    [Tooltip("アクティブキャラの追加拡大率(カメラズームに上乗せ)。")]
    public float focusScaleMul = 1.15f;
    [Tooltip("非アクティブキャラの暗転倍率(0=黒,1=元色)。")]
    public float dimMultiplier = 0.3f;

    [Header("演出")]
    [Tooltip("背景暗幕のアルファ。")]
    public float backdropAlpha = 0.4f;
    [Tooltip("スポットライトのワールド径。")]
    public float spotlightSize = 400f;
    [Tooltip("スポットライトの色。")]
    public Color spotlightColor = new Color(1f, 1f, 1f, 0.15f);
    [Tooltip("スポットライト演出を無効化する")]
    public bool disableSpotlight = true;

    [Header("グレースケール")]
    [Tooltip("非アクティブキャラを脱色する量(0=元色,1=完全グレー)。")]
    [Range(0f, 1f)]
    public float grayAmount = 1f;

    private const string GrayShaderName = "Custom/SpriteGrayscale";
    private Material grayMaterial;

    private readonly List<Target> targets = new List<Target>();
    private GameObject backdrop;
    private SpriteRenderer backdropSr;
    private GameObject spotlight;
    private SpriteRenderer spotlightSr;
    private Coroutine running;

    private Vector3 camHomePos;
    private float camHomeOrtho;
    private bool camSaved;

    public void Initialize(IList<Transform> players)
    {
        targets.Clear();
        if (players != null)
        {
            foreach (var t in players)
            {
                if (t == null) continue;
                var srs = t.GetComponentsInChildren<SpriteRenderer>(true);
                var baseColors = new Color[srs.Length];
                var baseMaterials = new Material[srs.Length];
                for (int i = 0; i < srs.Length; i++)
                {
                    baseColors[i] = srs[i] != null ? srs[i].color : Color.white;
                    baseMaterials[i] = srs[i] != null ? srs[i].sharedMaterial : null;
                }
                targets.Add(new Target
                {
                    tr = t,
                    basePos = t.position,
                    baseScale = t.localScale,
                    srs = srs,
                    baseColors = baseColors,
                    baseMaterials = baseMaterials,
                });
            }
        }

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) targetCamera = FindObjectOfType<Camera>();
        if (targetCamera != null && !camSaved)
        {
            camHomePos = targetCamera.transform.position;
            camHomeOrtho = targetCamera.orthographic ? targetCamera.orthographicSize : 5f;
            camSaved = true;
        }

        EnsureGrayMaterial();
        BuildBackdrop();
        BuildSpotlight();
    }

    /// <summary>
    /// 脱色用マテリアルを用意する。シェーダーが無ければ明確にエラーを出す。
    /// </summary>
    private void EnsureGrayMaterial()
    {
        if (grayMaterial != null) return;

        var sh = Shader.Find(GrayShaderName);
        if (sh == null)
        {
            Debug.LogError($"[CardSelectStage] シェーダー '{GrayShaderName}' が見つかりません。SpriteGrayscale.shader が Assets 内にあるか確認してください。");
            return;
        }
        grayMaterial = new Material(sh);
        grayMaterial.SetFloat("_GrayAmount", grayAmount);
    }

    /// <summary>
    /// 対象キャラの SpriteRenderer をグレースケール表示/通常表示に切り替える。
    /// </summary>
    private void ApplyGrayscale(Target tg, bool gray)
    {
        bool useGray = gray && grayMaterial != null;
        for (int i = 0; i < tg.srs.Length; i++)
        {
            var sr = tg.srs[i];
            if (sr == null) continue;
            sr.sharedMaterial = useGray ? grayMaterial : tg.baseMaterials[i];
        }
    }

    private void BuildBackdrop()
    {
        if (disableSpotlight) return;

        backdrop = new GameObject("CardSelect_Backdrop", typeof(SpriteRenderer));
        backdrop.transform.SetParent(transform, false);
        backdropSr = backdrop.GetComponent<SpriteRenderer>();
        backdropSr.sprite = SolidSprite();
        backdropSr.color = new Color(0f, 0f, 0f, 0f);
        // sortingOrderを低く設定してプレイヤーの後ろに表示
        backdropSr.sortingOrder = -100;
        backdrop.transform.localScale = new Vector3(20000f, 20000f, 1f);
    }

    private void BuildSpotlight()
    {
        if (disableSpotlight) return;

        spotlight = new GameObject("CardSelect_Spotlight", typeof(SpriteRenderer));
        spotlight.transform.SetParent(transform, false);
        spotlightSr = spotlight.GetComponent<SpriteRenderer>();
        spotlightSr.sprite = RadialSprite();
        spotlightSr.color = new Color(spotlightColor.r, spotlightColor.g, spotlightColor.b, 0f);
        // sortingOrderを低く設定してプレイヤーの後ろに表示
        spotlightSr.sortingOrder = -99;
        spotlight.transform.localScale = Vector3.one * (spotlightSize / 256f);
        spotlight.SetActive(false);
    }

    public Coroutine FocusPlayer(int index)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FocusRoutine(index));
        return running;
    }

    private IEnumerator FocusRoutine(int index)
    {
        if (index < 0 || index >= targets.Count) yield break;
        var act = targets[index];

        // カメラ目標を算出(アクティブキャラを画面の activeScreenX/Y に、zoom 率で寄せる)
        Vector3 camFrom = targetCamera != null ? targetCamera.transform.position : Vector3.zero;
        float orthoFrom = (targetCamera != null && targetCamera.orthographic) ? targetCamera.orthographicSize : camHomeOrtho;
        float orthoTo = camHomeOrtho * Mathf.Max(0.05f, zoomFactor);
        Vector3 camTo = ComputeCameraPos(act.basePos, orthoTo);

        // スポットライト演出（無効化されていない場合のみ）
        Color slFrom = Color.clear;
        Color slTo = Color.clear;
        Color bdFrom = Color.clear;
        Color bdTo = Color.clear;

        if (!disableSpotlight && spotlight != null && spotlightSr != null)
        {
            spotlight.transform.position = act.basePos;
            spotlight.SetActive(true);
            slFrom = spotlightSr.color;
            slTo = spotlightColor;
        }
        if (!disableSpotlight && backdropSr != null)
        {
            bdFrom = backdropSr.color;
            bdTo = new Color(0f, 0f, 0f, backdropAlpha);
        }

        int n = targets.Count;
        var fromScale = new Vector3[n];
        var toScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            fromScale[i] = targets[i].tr != null ? targets[i].tr.localScale : Vector3.one;
            toScale[i] = targets[i].baseScale * (i == index ? focusScaleMul : 1f);
            // アクティブキャラは通常表示、それ以外はグレースケールで目立たなくする
            ApplyGrayscale(targets[i], i != index);
        }

        float t = 0f;
        while (t < cameraMoveDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / cameraMoveDuration));

            if (targetCamera != null)
            {
                targetCamera.transform.position = Vector3.Lerp(camFrom, camTo, p);
                if (targetCamera.orthographic)
                    targetCamera.orthographicSize = Mathf.Lerp(orthoFrom, orthoTo, p);
            }

            if (!disableSpotlight)
            {
                if (backdropSr != null) backdropSr.color = Color.Lerp(bdFrom, bdTo, p);
                if (spotlightSr != null) spotlightSr.color = Color.Lerp(slFrom, slTo, p);
                FollowCameraVisuals(act.basePos);
            }

            for (int i = 0; i < n; i++)
            {
                var tg = targets[i];
                if (tg.tr == null) continue;
                tg.tr.localScale = Vector3.Lerp(fromScale[i], toScale[i], p);
                ApplyTint(tg, i == index ? 1f : dimMultiplier);
            }
            yield return null;
        }

        if (targetCamera != null)
        {
            targetCamera.transform.position = camTo;
            if (targetCamera.orthographic) targetCamera.orthographicSize = orthoTo;
        }

        if (!disableSpotlight)
        {
            if (backdropSr != null) backdropSr.color = bdTo;
            if (spotlightSr != null) spotlightSr.color = slTo;
            FollowCameraVisuals(act.basePos);
        }

        for (int i = 0; i < n; i++)
        {
            var tg = targets[i];
            if (tg.tr == null) continue;
            tg.tr.localScale = toScale[i];
            ApplyTint(tg, i == index ? 1f : dimMultiplier);
        }
        running = null;
    }

    /// <summary>アクティブキャラを画面の activeScreenX/Y へ収めるカメラ位置を算出。</summary>
    private Vector3 ComputeCameraPos(Vector3 charPos, float ortho)
    {
        if (targetCamera == null) return camHomePos;
        float halfH = ortho;
        float halfW = ortho * targetCamera.aspect;
        // 画面比率→NDC(-1..1)
        float ndcX = -1f + 2f * activeScreenX;
        float ndcY = -1f + 2f * activeScreenY;
        float camX = charPos.x - ndcX * halfW;
        float camY = charPos.y - ndcY * halfH;
        return new Vector3(camX, camY, camHomePos.z);
    }

    private void FollowCameraVisuals(Vector3 activePos)
    {
        if (targetCamera == null) return;
        Vector3 c = targetCamera.transform.position;
        // 暗幕はカメラ正面・少し奥
        backdrop.transform.position = new Vector3(c.x, c.y, 0f);
        // スポットはアクティブキャラ位置(ワールド固定)
        spotlight.transform.position = new Vector3(activePos.x, activePos.y, 0f);
    }

    /// <summary>
    /// カメラだけを全体表示(ホーム)へ戻す。キャラの強調/位置はそのまま維持する。
    /// カード確定直後の「ボスに部位マーカーを出して狙う」ためにボスを画面へ収める用途。
    /// </summary>
    public IEnumerator MoveCameraHome(float dur)
    {
        if (targetCamera == null || !camSaved) yield break;
        if (running != null) { StopCoroutine(running); running = null; }

        Vector3 fromPos = targetCamera.transform.position;
        float fromOrtho = targetCamera.orthographic ? targetCamera.orthographicSize : camHomeOrtho;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            targetCamera.transform.position = Vector3.Lerp(fromPos, camHomePos, p);
            if (targetCamera.orthographic)
                targetCamera.orthographicSize = Mathf.Lerp(fromOrtho, camHomeOrtho, p);
            yield return null;
        }
        targetCamera.transform.position = camHomePos;
        if (targetCamera.orthographic) targetCamera.orthographicSize = camHomeOrtho;
    }

    public void PlayReaction(int index)
    {
        if (index < 0 || index >= targets.Count) return;
        StartCoroutine(ReactionRoutine(targets[index]));
    }

    private IEnumerator ReactionRoutine(Target tg)
    {
        if (tg.tr == null) yield break;
        Vector3 baseS = tg.tr.localScale;
        float t = 0f, dur = 0.22f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = (p < 0.5f) ? Mathf.Lerp(1f, 1.12f, p * 2f) : Mathf.Lerp(1.12f, 1f, (p - 0.5f) * 2f);
            tg.tr.localScale = baseS * s;
            yield return null;
        }
        tg.tr.localScale = baseS;
    }

    public void Restore()
    {
        StopAllCoroutines();
        running = null;
        foreach (var tg in targets)
        {
            if (tg.tr == null) continue;
            tg.tr.position = tg.basePos;
            tg.tr.localScale = tg.baseScale;
            for (int i = 0; i < tg.srs.Length; i++)
            {
                if (tg.srs[i] == null) continue;
                tg.srs[i].color = tg.baseColors[i];
                // グレースケール用マテリアルを元に戻す
                tg.srs[i].sharedMaterial = tg.baseMaterials[i];
            }
        }
        if (targetCamera != null && camSaved)
        {
            targetCamera.transform.position = camHomePos;
            if (targetCamera.orthographic) targetCamera.orthographicSize = camHomeOrtho;
        }
        if (backdrop != null) Destroy(backdrop);
        if (spotlight != null) Destroy(spotlight);
        if (grayMaterial != null)
        {
            Destroy(grayMaterial);
            grayMaterial = null;
        }
    }

    private static void ApplyTint(Target tg, float mul)
    {
        for (int i = 0; i < tg.srs.Length; i++)
        {
            var sr = tg.srs[i];
            if (sr == null) continue;
            Color b = tg.baseColors[i];
            sr.color = new Color(b.r * mul, b.g * mul, b.b * mul, b.a);
        }
    }

    private static Sprite cachedSolid;
    private static Sprite SolidSprite()
    {
        if (cachedSolid != null) return cachedSolid;
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        cachedSolid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
        return cachedSolid;
    }

    private static Sprite cachedRadial;
    private static Sprite RadialSprite()
    {
        if (cachedRadial != null) return cachedRadial;
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        var px = new Color32[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float maxR = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        cachedRadial = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
        return cachedRadial;
    }

    private class Target
    {
        public Transform tr;
        public Vector3 basePos;
        public Vector3 baseScale;
        public SpriteRenderer[] srs;
        public Color[] baseColors;
        public Material[] baseMaterials;
    }
}
