Shader "Custom/SpriteGrayscale"
{
    // 標準スプライトシェーダーに「脱色(グレースケール)」処理を加えたもの。
    // _GrayAmount = 1 で完全グレー。SpriteRenderer.color(頂点カラー)による
    // 減光もそのまま効くので、暗さは color 側で、脱色は本シェーダーで制御する。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GrayAmount ("Gray Amount", Range(0,1)) = 1
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            float _GrayAmount;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                // 輝度を計算して RGB を灰色へ寄せる
                float gray = dot(c.rgb, fixed3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, fixed3(gray, gray, gray), _GrayAmount);
                // スプライト標準のプリマルチプライドアルファ
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
