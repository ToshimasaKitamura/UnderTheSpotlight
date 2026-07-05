using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移エフェクトを管理するシングルトン
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    private static SceneTransitionManager instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SceneTransitionManager");
                instance = go.AddComponent<SceneTransitionManager>();
                DontDestroyOnLoad(go);
                instance.Initialize();
            }
            return instance;
        }
    }

    [Header("遷移設定")]
    [Tooltip("フェード時間")]
    public float fadeDuration = 0.5f;
    [Tooltip("スライド時間")]
    public float slideDuration = 0.6f;

    private Canvas transitionCanvas;
    private Image fadeImage;
    private RectTransform slidePanel;
    private bool isTransitioning = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        // キャンバス作成
        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(transform);
        transitionCanvas = canvasObj.AddComponent<Canvas>();
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = 9999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // フェード用パネル作成
        GameObject fadeObj = new GameObject("FadePanel");
        fadeObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = fadeObj.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        RectTransform fadeRect = fadeImage.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.sizeDelta = Vector2.zero;

        // スライド用パネル作成
        GameObject slideObj = new GameObject("SlidePanel");
        slideObj.transform.SetParent(canvasObj.transform, false);
        Image slideImage = slideObj.AddComponent<Image>();
        slideImage.color = new Color(0.05f, 0.02f, 0.1f, 1f);
        slidePanel = slideImage.GetComponent<RectTransform>();
        slidePanel.anchorMin = Vector2.zero;
        slidePanel.anchorMax = Vector2.one;
        slidePanel.sizeDelta = Vector2.zero;
        slidePanel.anchoredPosition = new Vector2(-1920, 0);

        Debug.Log("[SceneTransitionManager] 初期化完了");
    }

    /// <summary>
    /// フェード遷移でシーンをロード
    /// </summary>
    public void LoadSceneWithFade(string sceneName, Action onComplete = null)
    {
        if (isTransitioning) return;
        StartCoroutine(FadeTransition(sceneName, onComplete));
    }

    /// <summary>
    /// スライド遷移でシーンをロード
    /// </summary>
    public void LoadSceneWithSlide(string sceneName, Action onComplete = null)
    {
        if (isTransitioning) return;
        StartCoroutine(SlideTransition(sceneName, onComplete));
    }

    /// <summary>
    /// ワイプ遷移でシーンをロード（円形拡大）
    /// </summary>
    public void LoadSceneWithWipe(string sceneName, Action onComplete = null)
    {
        if (isTransitioning) return;
        StartCoroutine(WipeTransition(sceneName, onComplete));
    }

    private IEnumerator FadeTransition(string sceneName, Action onComplete)
    {
        isTransitioning = true;

        // フェードアウト
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1);

        // シーンロード
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 少し待つ
        yield return new WaitForSeconds(0.1f);

        // フェードイン
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            fadeImage.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);

        isTransitioning = false;
        onComplete?.Invoke();
    }

    private IEnumerator SlideTransition(string sceneName, Action onComplete)
    {
        isTransitioning = true;

        // スライドイン（左から右へ画面を覆う）
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            // イージング: EaseInOutCubic
            t = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            slidePanel.anchoredPosition = new Vector2(Mathf.Lerp(-1920, 0, t), 0);
            yield return null;
        }
        slidePanel.anchoredPosition = Vector2.zero;

        // シーンロード
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 少し待つ
        yield return new WaitForSeconds(0.1f);

        // スライドアウト（右へ抜ける）
        elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slideDuration;
            t = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            slidePanel.anchoredPosition = new Vector2(Mathf.Lerp(0, 1920, t), 0);
            yield return null;
        }
        slidePanel.anchoredPosition = new Vector2(1920, 0);

        // 元の位置に戻す
        slidePanel.anchoredPosition = new Vector2(-1920, 0);

        isTransitioning = false;
        onComplete?.Invoke();
    }

    private IEnumerator WipeTransition(string sceneName, Action onComplete)
    {
        isTransitioning = true;

        // 簡易的なワイプエフェクト（フェードで代用）
        // 本格的な円形ワイプには専用シェーダーが必要

        // ワイプアウト（素早くフェードアウト）
        float elapsed = 0f;
        float wipeDuration = fadeDuration * 0.8f;

        while (elapsed < wipeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / wipeDuration;
            // 加速カーブ
            t = t * t;
            fadeImage.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1);

        // シーンロード
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // ワイプイン（素早くフェードイン）
        elapsed = 0f;
        while (elapsed < wipeDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / wipeDuration);
            // 減速カーブ
            t = 1f - (1f - t) * (1f - t);
            fadeImage.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);

        isTransitioning = false;
        onComplete?.Invoke();
    }
}
