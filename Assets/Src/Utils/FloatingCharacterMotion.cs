using UnityEngine;

namespace UnderTheSpotlight
{
    /// <summary>
    /// キャラを常時ふわふわ浮かせる視覚演出。
    /// SpriteRendererを子オブジェクトに移動し、そのlocalPositionを上下に動かす。
    /// 親のtransform.positionは変更しない。
    /// </summary>
    public class FloatingCharacterMotion : MonoBehaviour
    {
        [Header("浮遊")]
        [Tooltip("上下に浮く幅(ワールド単位)")]
        public float amplitude = 0.12f;

        [Tooltip("1秒あたりのサイクル数。値が大きいほど速い呼吸")]
        public float frequency = 1.1f;

        [Tooltip("起動時に位相をランダム化する。複数キャラが同期して動かないようバラけさせる用。")]
        public bool randomizePhase = true;

        [Header("回転(任意)")]
        [Tooltip("浮きと連動した小さな揺れ回転を入れるとさらに生きてる感が出る")]
        public bool enableRotation = false;

        [Tooltip("最大回転角度(度)")]
        public float rotationAmplitude = 2.5f;

        private float phaseOffset;
        private Transform spriteHolder;
        private Quaternion baseRotation;

        private void Awake()
        {
            phaseOffset = randomizePhase ? UnityEngine.Random.Range(0f, Mathf.PI * 2f) : 0f;
            SetupSpriteHolder();
        }

        private void SetupSpriteHolder()
        {
            // まず既存の子オブジェクトにSpriteRendererがあるか確認
            var childRenderer = GetComponentInChildren<SpriteRenderer>();
            if (childRenderer != null && childRenderer.transform != transform)
            {
                // 子オブジェクトにSpriteRendererがある場合はそれを使用
                spriteHolder = childRenderer.transform;
                baseRotation = spriteHolder.localRotation;
                return;
            }

            // 自分自身にSpriteRendererがある場合、子オブジェクトを作成してそこに移動
            var myRenderer = GetComponent<SpriteRenderer>();
            if (myRenderer != null)
            {
                // スプライトホルダー用の子オブジェクトを作成
                var holder = new GameObject("SpriteHolder");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = Vector3.zero;
                holder.transform.localRotation = Quaternion.identity;
                holder.transform.localScale = Vector3.one;

                // SpriteRendererを子オブジェクトにコピー
                var newRenderer = holder.AddComponent<SpriteRenderer>();
                newRenderer.sprite = myRenderer.sprite;
                newRenderer.color = myRenderer.color;
                newRenderer.flipX = myRenderer.flipX;
                newRenderer.flipY = myRenderer.flipY;
                newRenderer.sortingLayerID = myRenderer.sortingLayerID;
                newRenderer.sortingOrder = myRenderer.sortingOrder;
                newRenderer.material = myRenderer.material;
                newRenderer.drawMode = myRenderer.drawMode;
                newRenderer.maskInteraction = myRenderer.maskInteraction;

                // 元のSpriteRendererを無効化（削除するとコンポーネント参照が壊れる可能性があるため）
                myRenderer.enabled = false;

                spriteHolder = holder.transform;
                baseRotation = Quaternion.identity;
            }
        }

        private void LateUpdate()
        {
            if (spriteHolder == null) return;

            float t = Time.time * frequency * Mathf.PI * 2f + phaseOffset;
            float yOffset = Mathf.Sin(t) * amplitude;

            // スプライトホルダーのlocalPositionを上下に動かす
            spriteHolder.localPosition = new Vector3(0f, yOffset, 0f);

            if (enableRotation)
            {
                float angle = Mathf.Sin(t * 0.5f) * rotationAmplitude;
                spriteHolder.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// 基準位置を再設定する。
        /// </summary>
        public void RebaseToCurrentPosition()
        {
            // オフセットベースなので特に何もしない
        }
    }
}