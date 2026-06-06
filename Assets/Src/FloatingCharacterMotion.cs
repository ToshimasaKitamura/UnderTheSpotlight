using UnityEngine;

namespace UnderTheSpotlight
{
    /// <summary>
    /// キャラを常時ふわふわ浮かせる視覚演出。
    /// Sine波で localPosition.y にオフセットを加える方式。
    ///
    /// 重要: 親オブジェクトの座標を直接揺らすと移動・ノックバック処理と干渉するので、
    /// 推奨は「視覚用の子オブジェクト」にこのスクリプトをアタッチする構成:
    ///
    ///   CharacterRoot           ← 当たり判定・論理位置(別スクリプトが動かす)
    ///    └ Visual               ← SpriteRenderer + このスクリプト(浮遊だけ担当)
    ///       └ Shadow (任意)     ← 影は浮かさず地面に固定
    ///
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

        [Header("一時停止")]
        [Tooltip("trueにすると浮遊を止めて基準位置に戻る。被弾時などに切れる用")]
        public bool paused = false;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float phaseOffset;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            phaseOffset = randomizePhase ? UnityEngine.Random.Range(0f, Mathf.PI * 2f) : 0f;
        }

        private void Update()
        {
            if (paused)
            {
                transform.localPosition = basePosition;
                transform.localRotation = baseRotation;
                return;
            }

            float t = Time.time * frequency * Mathf.PI * 2f + phaseOffset;
            float yOffset = Mathf.Sin(t) * amplitude;
            transform.localPosition = basePosition + new Vector3(0f, yOffset, 0f);

            if (enableRotation)
            {
                // 回転は浮きの半分の周波数にすると有機的に見える
                float angle = Mathf.Sin(t * 0.5f) * rotationAmplitude;
                transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// 基準位置を再設定する。スプライト差し替え後にlocalPositionが変わった時など。
        /// </summary>
        public void RebaseToCurrentPosition()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
        }
    }
}