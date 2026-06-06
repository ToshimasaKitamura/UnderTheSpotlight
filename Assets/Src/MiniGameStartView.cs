using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ミニゲーム選定演出。文字だけのシンプルな表示。
/// 全員が決定キー(Enter/Space/Z) を順に押すと <see cref="AllReady"/> = true。
/// </summary>
public class MiniGameStartView : MonoBehaviour
{
    private Canvas canvas;
    private Text readyLabel;
    private int playerCount;
    private int readyIndex;

    public bool AllReady => readyIndex >= playerCount;

    public void Show(string gameName, int count, Color[] playerColors)
    {
        playerCount = Mathf.Max(1, count);
        readyIndex = 0;

        canvas = AcUi.CreateOverlayCanvas(transform, "MiniGameStartCanvas", 120);

        // 暗幕は薄く(文字を読みやすくする最低限)
        var bg = AcUi.Image(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        AcUi.Stretch(bg.rectTransform);

        // タイトル
        var title = AcUi.Label(canvas.transform, "Title", 84, new Color(1f, 0.95f, 0.4f, 1f),
            TextAnchor.MiddleCenter, true, true);
        AcUi.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1600f, 130f));
        title.text = "M I N I   G A M E";

        // ゲーム名
        var nameLabel = AcUi.Label(canvas.transform, "GameName", 70, Color.white,
            TextAnchor.MiddleCenter, true, true);
        AcUi.Place(nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1700f, 120f));
        nameLabel.text = gameName;

        // 決定数表示(文字のみ)
        readyLabel = AcUi.Label(canvas.transform, "Ready", 40, new Color(0.85f, 0.9f, 1f, 1f),
            TextAnchor.MiddleCenter, false, true);
        AcUi.Place(readyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(1600f, 70f));

        // 操作ヒント
        var hint = AcUi.Label(canvas.transform, "Hint", 28, new Color(0.7f, 0.75f, 0.85f, 1f),
            TextAnchor.MiddleCenter, false, true);
        AcUi.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1600f, 50f));
        hint.text = "全員順に Enter / Space / Z で決定";

        RefreshReadyLabel();
    }

    private void RefreshReadyLabel()
    {
        if (readyLabel == null) return;
        readyLabel.text = AllReady
            ? "全員決定!"
            : $"決定者  {readyIndex} / {playerCount}";
    }

    /// <summary>毎フレーム呼ぶ。全員決定済みなら true。</summary>
    public bool Tick()
    {
        if (AllReady) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame
            || kb.numpadEnterKey.wasPressedThisFrame
            || kb.spaceKey.wasPressedThisFrame
            || kb.zKey.wasPressedThisFrame))
        {
            readyIndex++;
            RefreshReadyLabel();
        }
        return AllReady;
    }

    public void Close()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        canvas = null;
    }
}
