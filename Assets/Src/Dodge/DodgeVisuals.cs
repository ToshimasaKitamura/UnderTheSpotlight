using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 回避フェーズの演出（アニメーション）を担当するクラス。
/// 敵の上昇、プレイヤーの退避、フィールドの出現などのコルーチンを管理。
/// </summary>
public class DodgeVisuals : MonoBehaviour
{
    [Header("参照")]
    public Transform enemiesRoot;
    public GameObject centralField;
    public Transform[] playerTransforms;

    [Header("設定")]
    public float enemyRiseDistance = 8f;   // 常駐ボスを画面外(上)まで退避させる(弾幕ボスと二重に見えないように)
    public float enemyRiseDuration = 0.6f;
    public float playerMoveDuration = 0.6f;
    public float fieldScaleDuration = 0.4f;
    public float centralFieldAppearScale = 1f;
    public float playerExitDuration = 0.4f;
    // 退避時に下へ下げるビューポート量。下段の四隅窓に届かない安全範囲に留める。
    // (SpriteMask は全窓に作用するため、下げ過ぎると上段キャラが下段窓に写り込む。
    //  静止時スプライト下端は下段窓の上端から innerH*0.15≈0.05vp 上にあるので、それ未満にする)
    public float exitDropViewport = 0.035f;
    public float playerSideEntryDuration = 0.5f;
    public float playerReturnDuration = 0.6f;
    public float playerSmallScale = 0.75f;   // 左右に控える時の縮小率(大きめ)
    public float fieldFadeOutDuration = 0.4f;

    // 内部状態
    private Vector3 enemiesOriginalPos;
    private Vector3[] playersOriginalPosArr;
    private Vector3[] playersOriginalScaleArr;
    private List<Transform> individualEnemies;
    private List<Vector3> individualEnemiesOriginalPos;
    private Vector3 centralFieldOriginalScale;

    // 左右入場・浮遊用
    private Vector3[] playerSidePositions;
    private bool sideFloatActive;
    [Tooltip("弾幕中の左右浮遊の振幅(ワールド)")]
    public float sideFloatAmplitudeX = 0.15f;
    [Tooltip("弾幕中の上下浮遊の振幅(ワールド)")]
    public float sideFloatAmplitudeY = 0.05f;
    [Tooltip("弾幕中の浮遊の速さ")]
    public float sideFloatSpeed = 1.3f;

    // 手番キャラが「光になって」自機の丸に入り込む演出用
    private bool[] charFloating;       // true = 左右で浮遊中
    private Color[] charColor;         // 各キャラ(=プレイヤー)の色
    private Coroutine[] charRoutine;   // 各キャラの入場/退場エフェクト
    private int activeCharIndex = -1;
    private Transform onFieldPlayer;

    [Tooltip("光が球体へ流れ込む秒数(小さいほど速い)")]
    public float lightTravelDuration = 0.18f;
    [Tooltip("光の玉のワールドサイズ")]
    public float lightSize = 0.9f;

    void Update()
    {
        if (!sideFloatActive || playerTransforms == null || playerSidePositions == null || charFloating == null) return;

        float time = Time.time;
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            var tr = playerTransforms[i];
            if (tr == null || i >= playerSidePositions.Length || i >= charFloating.Length) continue;
            if (!charFloating[i]) continue;   // 入り込み中/球体内のキャラはスキップ

            Vector3 b = playerSidePositions[i];
            float dx = Mathf.Sin(time * sideFloatSpeed + i * 0.9f) * sideFloatAmplitudeX;
            float dy = Mathf.Sin(time * (sideFloatSpeed * 0.7f) + i * 1.7f) * sideFloatAmplitudeY;
            tr.position = new Vector3(b.x + dx, b.y + dy, b.z);
            tr.localScale = SideScale(i);
        }
    }

    private Vector3 SideScale(int i)
    {
        Vector3 baseS = (playersOriginalScaleArr != null && i < playersOriginalScaleArr.Length) ? playersOriginalScaleArr[i] : Vector3.one;
        return baseS * playerSmallScale;
    }

    /// <summary>
    /// 手番プレイヤーのキャラを「光になって」自機の丸へ入り込ませ、丸をそのプレイヤー色にする。
    /// 前の手番キャラは光となって丸から出て、左右の待機位置へ戻る。
    /// </summary>
    public void SetActiveCharacterOnField(int index, Transform fieldPlayer, Color playerColor)
    {
        onFieldPlayer = fieldPlayer;
        if (charFloating == null) return;   // 左右入場前は無視

        if (index >= 0 && index < charColor.Length) charColor[index] = playerColor;

        // 前の手番キャラ: 丸から出て左右へ戻る
        if (activeCharIndex >= 0 && activeCharIndex != index && activeCharIndex < charFloating.Length)
            StartCharRoutine(activeCharIndex, ExitLight(activeCharIndex));

        activeCharIndex = index;
        // 新しい手番キャラ: 光になって丸へ入る
        if (index >= 0 && index < charFloating.Length)
            StartCharRoutine(index, EnterLight(index, playerColor));
    }

    private void StartCharRoutine(int i, IEnumerator routine)
    {
        if (charRoutine == null || i < 0 || i >= charRoutine.Length) return;
        if (charRoutine[i] != null) StopCoroutine(charRoutine[i]);
        charRoutine[i] = StartCoroutine(routine);
    }

    // キャラが光になって球体へ吸い込まれる → 球体がプレイヤー色に
    private IEnumerator EnterLight(int index, Color color)
    {
        var charTr = playerTransforms[index];
        if (charTr == null || onFieldPlayer == null) yield break;

        charFloating[index] = false;
        Vector3 startPos = charTr.position;

        // キャラは光になる(本体を消して光の玉を出す)
        SetCharVisible(index, false);
        var light = CreateGlow(color, lightSize);
        light.transform.position = startPos;

        float dur = Mathf.Max(0.05f, lightTravelDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float ease = p * p;   // 後半で加速して吸い込まれる
            Vector3 ballPos = onFieldPlayer != null ? onFieldPlayer.position : startPos;
            light.transform.position = Vector3.Lerp(startPos, ballPos, ease);
            light.transform.localScale = Vector3.one * Mathf.Lerp(lightSize, lightSize * 0.25f, p);
            SpawnTrail(light.transform.position, color, lightSize * 0.55f);
            yield return null;
        }
        if (light != null) Destroy(light);

        // 到着: 丸をフラッシュしてプレイヤー色にする
        yield return StartCoroutine(FlashBall(color));
        charRoutine[index] = null;
    }

    // 球体から光が出て → キャラが左右の待機位置で再形成
    private IEnumerator ExitLight(int index)
    {
        var charTr = playerTransforms[index];
        if (charTr == null) yield break;

        Color c = (charColor != null && index < charColor.Length) ? charColor[index] : Color.white;
        Vector3 ballPos = onFieldPlayer != null ? onFieldPlayer.position : charTr.position;
        Vector3 sidePos = (index < playerSidePositions.Length) ? playerSidePositions[index] : charTr.position;

        var light = CreateGlow(c, lightSize * 0.25f);
        light.transform.position = ballPos;

        float dur = Mathf.Max(0.05f, lightTravelDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            light.transform.position = Vector3.Lerp(ballPos, sidePos, p);
            light.transform.localScale = Vector3.one * Mathf.Lerp(lightSize * 0.25f, lightSize, p);
            SpawnTrail(light.transform.position, c, lightSize * 0.5f);
            yield return null;
        }
        if (light != null) Destroy(light);

        // キャラ再形成(待機位置で表示、浮遊再開)
        charTr.position = sidePos;
        charTr.localScale = SideScale(index);
        SetCharVisible(index, true);
        charFloating[index] = true;
        charRoutine[index] = null;
    }

    // 球体を一瞬ふくらませ白→プレイヤー色にする
    private IEnumerator FlashBall(Color color)
    {
        if (onFieldPlayer == null) yield break;
        var sr = onFieldPlayer.GetComponentInChildren<SpriteRenderer>();
        Vector3 baseScale = onFieldPlayer.localScale;
        float dur = 0.22f, t = 0f;
        while (t < dur)
        {
            if (onFieldPlayer == null) yield break;   // 球体が消えたら中断
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            if (sr != null) sr.color = Color.Lerp(Color.white, color, p);
            onFieldPlayer.localScale = baseScale * (1f + 0.5f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        if (onFieldPlayer == null) yield break;
        if (sr != null) sr.color = color;   // 以後この色を維持
        onFieldPlayer.localScale = baseScale;
    }

    private void SpawnTrail(Vector3 pos, Color color, float size)
    {
        var g = CreateGlow(color, size);
        g.transform.position = pos;
        StartCoroutine(FadeAndDestroy(g, 0.25f));
    }

    private IEnumerator FadeAndDestroy(GameObject g, float dur)
    {
        var sr = g != null ? g.GetComponent<SpriteRenderer>() : null;
        Color c0 = sr != null ? sr.color : Color.white;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = 1f - t / dur;
            if (sr != null) sr.color = new Color(c0.r, c0.g, c0.b, c0.a * a);
            if (g != null) g.transform.localScale *= 0.95f;
            yield return null;
        }
        if (g != null) Destroy(g);
    }

    private GameObject CreateGlow(Color color, float worldSize)
    {
        var go = new GameObject("EnterLight");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GlowSprite();
        // 発光風に少し明るくする
        sr.color = new Color(Mathf.Min(1f, color.r + 0.3f), Mathf.Min(1f, color.g + 0.3f), Mathf.Min(1f, color.b + 0.3f), 0.9f);
        sr.sortingOrder = 60;   // 弾(30)や自機(40)より前面
        go.transform.localScale = Vector3.one * worldSize;
        return go;
    }

    private static Sprite cachedGlow;
    private static Sprite GlowSprite()
    {
        if (cachedGlow != null) return cachedGlow;
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color[s * s];
        float c = (s - 1) * 0.5f;
        float r = s * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                float a = Mathf.Clamp01(1f - d);
                a = a * a;   // 中心ほど明るい放射状
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        cachedGlow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);   // PPU=s → 1ユニット
        return cachedGlow;
    }

    /// <summary>キャラの SpriteRenderer の表示/非表示を切り替える(光になる/再形成)。</summary>
    private void SetCharVisible(int i, bool visible)
    {
        if (playerTransforms == null || i < 0 || i >= playerTransforms.Length || playerTransforms[i] == null) return;
        foreach (var sr in playerTransforms[i].GetComponentsInChildren<SpriteRenderer>(true))
            sr.enabled = visible;
    }

    public void Initialize(Transform enemies, GameObject field, Transform[] players)
    {
        enemiesRoot = enemies;
        centralField = field;
        playerTransforms = players;

        if (enemiesRoot != null) enemiesOriginalPos = enemiesRoot.position;
        if (centralField != null) centralFieldOriginalScale = centralField.transform.localScale;

        if (playerTransforms != null)
        {
            playersOriginalPosArr = new Vector3[playerTransforms.Length];
            playersOriginalScaleArr = new Vector3[playerTransforms.Length];
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                if (playerTransforms[i] != null)
                {
                    playersOriginalPosArr[i] = playerTransforms[i].position;
                    playersOriginalScaleArr[i] = playerTransforms[i].localScale;
                }
            }
        }
    }

    public void SetIndividualEnemies(List<Transform> enemies, List<Vector3> positions)
    {
        individualEnemies = enemies;
        individualEnemiesOriginalPos = positions;
    }

    public IEnumerator PlayEnemyRise()
    {
        float t = 0f;
        while (t < enemyRiseDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / enemyRiseDuration);

            if (enemiesRoot != null)
            {
                enemiesRoot.position = Vector3.Lerp(enemiesOriginalPos, enemiesOriginalPos + Vector3.up * enemyRiseDistance, p);
            }
            else if (individualEnemies != null)
            {
                for (int i = 0; i < individualEnemies.Count; i++)
                {
                    if (individualEnemies[i] != null)
                        individualEnemies[i].position = Vector3.Lerp(individualEnemiesOriginalPos[i], individualEnemiesOriginalPos[i] + Vector3.up * enemyRiseDistance, p);
                }
            }
            yield return null;
        }
    }

    public IEnumerator PlayEnemyRestore(float duration, float fromAboveHeight)
    {
        Vector3 aboveOffset = Vector3.up * fromAboveHeight;
        if (enemiesRoot != null)
        {
            Vector3 from = enemiesOriginalPos + aboveOffset;
            enemiesRoot.position = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duration);
                enemiesRoot.position = Vector3.Lerp(from, enemiesOriginalPos, p);
                yield return null;
            }
            enemiesRoot.position = enemiesOriginalPos;
        }
        else if (individualEnemies != null)
        {
            int m = Mathf.Min(individualEnemies.Count, individualEnemiesOriginalPos.Count);
            for (int i = 0; i < m; i++)
                if (individualEnemies[i] != null) individualEnemies[i].position = individualEnemiesOriginalPos[i] + aboveOffset;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / duration);
                for (int i = 0; i < m; i++)
                {
                    if (individualEnemies[i] == null) continue;
                    individualEnemies[i].position = Vector3.Lerp(individualEnemiesOriginalPos[i] + aboveOffset, individualEnemiesOriginalPos[i], p);
                }
                yield return null;
            }
            for (int i = 0; i < m; i++)
                if (individualEnemies[i] != null) individualEnemies[i].position = individualEnemiesOriginalPos[i];
        }
    }

    public IEnumerator PlayFieldAppear()
    {
        if (centralField == null) yield break;
        centralField.SetActive(true);
        SetFieldAlpha(1f);
        float t = 0f;
        Vector3 targetScale = Vector3.one * centralFieldAppearScale;
        while (t < fieldScaleDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / fieldScaleDuration);
            centralField.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, p);
            yield return null;
        }
        centralField.transform.localScale = targetScale;
    }

    public IEnumerator PlayFieldExit()
    {
        if (centralField == null) yield break;
        float t = 0f;
        Vector3 startScale = centralField.transform.localScale;
        while (t < fieldFadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / fieldFadeOutDuration);
            centralField.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
            SetFieldAlpha(1f - p);
            yield return null;
        }
        centralField.SetActive(false);
    }

    public IEnumerator PlayPlayersExitDown()
    {
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        Camera cam = Camera.main;
        float t = 0f;
        while (t < playerExitDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerExitDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(playersOriginalPosArr[i]);
                    // 画面下まで下げず、自分の窓から出る分だけ下げる(下段窓への写り込み防止)
                    float targetY = vp.y - exitDropViewport;
                    vp.y = Mathf.Lerp(vp.y, targetY, p);
                    vp.z = GetSafeScreenZ(cam, playersOriginalPosArr[i]);
                    Vector3 world = cam.ViewportToWorldPoint(vp);
                    world.z = playersOriginalPosArr[i].z;
                    tr.position = world;
                }
                // 縮小＋フェードで「自分の枠の中に沈んで消える」ように見せる(枠外・他窓へ出さない)
                tr.localScale = Vector3.Lerp(playersOriginalScaleArr[i], playersOriginalScaleArr[i] * 0.45f, p);
                SetCharAlpha(i, 1f - p);
            }
            yield return null;
        }

        // 直後に枠を隠す(クリップ解除)ため、保険として描画を切る。
        // 次の PlayPlayersSideEntry が左右へ配置し直し、表示とアルファを戻す。
        if (playerTransforms != null)
            for (int i = 0; i < playerTransforms.Length; i++) SetCharVisible(i, false);
    }

    /// <summary>キャラの SpriteRenderer 群のアルファを設定する(フェード用)。</summary>
    private void SetCharAlpha(int i, float a)
    {
        if (playerTransforms == null || i < 0 || i >= playerTransforms.Length || playerTransforms[i] == null) return;
        foreach (var sr in playerTransforms[i].GetComponentsInChildren<SpriteRenderer>(true))
        {
            var c = sr.color; c.a = a; sr.color = c;
        }
    }

    /// <summary>
    /// プレイヤーを画面左右外から左右の待機位置へ入場させ、その後の浮遊を開始する。
    /// (PlayPlayersExitDown で一度下に消えた後に呼ぶ想定)
    /// </summary>
    public IEnumerator PlayPlayersSideEntry()
    {
        if (playerTransforms == null || playerTransforms.Length == 0) yield break;
        Camera cam = Camera.main;
        if (cam == null) yield break;

        int n = playerTransforms.Length;
        playerSidePositions = new Vector3[n];
        Vector3[] startPos = new Vector3[n];
        float depth = Mathf.Abs(cam.transform.position.z);

        // 全キャラを浮遊状態で初期化
        charFloating = new bool[n];
        charColor = new Color[n];
        charRoutine = new Coroutine[n];
        // 退避(PlayPlayersExitDown)で描画を切った/縮小・透明化したキャラをここで元に戻す。
        for (int m = 0; m < n; m++)
        {
            charFloating[m] = true;
            charColor[m] = Color.white;
            SetCharVisible(m, true);
            SetCharAlpha(m, 1f);
            if (playerTransforms[m] != null && playersOriginalScaleArr != null && m < playersOriginalScaleArr.Length)
                playerTransforms[m].localScale = playersOriginalScaleArr[m] * playerSmallScale;
        }
        activeCharIndex = -1;

        int leftCount = Mathf.CeilToInt(n / 2f);   // 前半を左、後半を右に振り分け
        int rightCount = n - leftCount;

        for (int i = 0; i < n; i++)
        {
            if (playerTransforms[i] == null) continue;
            bool left = i < leftCount;
            int idxInSide = left ? i : (i - leftCount);
            int cnt = left ? leftCount : rightCount;

            float vx = left ? 0.11f : 0.89f;
            // 画面下寄りに配置(2人なら中央〜下)
            float vy = (cnt <= 1) ? 0.34f : Mathf.Lerp(0.5f, 0.2f, idxInSide / (float)(cnt - 1));

            Vector3 target = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));
            float z = (playersOriginalPosArr != null && i < playersOriginalPosArr.Length) ? playersOriginalPosArr[i].z : 0f;
            target.z = z;
            playerSidePositions[i] = target;

            // 画面外(左右)に一旦配置してから滑り込ませる
            float startVx = left ? -0.18f : 1.18f;
            Vector3 s = cam.ViewportToWorldPoint(new Vector3(startVx, vy, depth));
            s.z = z;
            startPos[i] = s;
            playerTransforms[i].position = s;
        }

        float t = 0f;
        float dur = Mathf.Max(0.05f, playerSideEntryDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / dur);
            for (int i = 0; i < n; i++)
            {
                if (playerTransforms[i] == null) continue;
                playerTransforms[i].position = Vector3.Lerp(startPos[i], playerSidePositions[i], p);
            }
            yield return null;
        }
        for (int i = 0; i < n; i++)
            if (playerTransforms[i] != null) playerTransforms[i].position = playerSidePositions[i];

        // 以後は Update() で左右にゆらゆら浮遊
        sideFloatActive = true;
    }

    public IEnumerator PlayPlayersRestore()
    {
        sideFloatActive = false;   // 浮遊/入り込み演出を止めてから復帰
        activeCharIndex = -1;
        // 進行中の光エフェクトを止める
        if (charRoutine != null)
            for (int i = 0; i < charRoutine.Length; i++) if (charRoutine[i] != null) StopCoroutine(charRoutine[i]);
        charFloating = null;
        charColor = null;
        charRoutine = null;
        // 光になって非表示だったキャラを再表示
        if (playerTransforms != null)
            for (int i = 0; i < playerTransforms.Length; i++) SetCharVisible(i, true);
        if (playerTransforms == null || playersOriginalPosArr == null) yield break;
        float t = 0f;
        while (t < playerReturnDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, t / playerReturnDuration);
            for (int i = 0; i < playerTransforms.Length; i++)
            {
                var tr = playerTransforms[i];
                if (tr == null) continue;
                tr.position = Vector3.Lerp(tr.position, playersOriginalPosArr[i], p);
                tr.localScale = Vector3.Lerp(tr.localScale, playersOriginalScaleArr[i], p);
            }
            yield return null;
        }
    }

    public void SetFieldAlpha(float a)
    {
        if (centralField == null) return;
        foreach (var sr in centralField.GetComponentsInChildren<SpriteRenderer>(true)) { var c = sr.color; c.a = a; sr.color = c; }
        foreach (var img in centralField.GetComponentsInChildren<Image>(true)) { var c = img.color; c.a = a; img.color = c; }
        foreach (var r in centralField.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in r.materials) if (mat != null && mat.HasProperty("_Color")) { Color c = mat.color; c.a = a; mat.color = c; }
        }
    }

    private float GetSafeScreenZ(Camera cam, Vector3 worldPos)
    {
        if (cam == null) return 10f;
        float z = Vector3.Dot(worldPos - cam.transform.position, cam.transform.forward);
        if (z <= 0.01f) z = Vector3.Distance(cam.transform.position, worldPos);
        return Mathf.Max(z, 0.01f);
    }
}
