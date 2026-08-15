Shader "Akabeko/MonoLineScanline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScanlineDensity ("Scanline Density", Float) = 130.0
        _LineThickness ("Line Thickness", Float) = 0.85
        _MotionAmount ("Motion Amount", Float) = 0.0
        _BgColor ("Base Color", Color) = (0.95, 0.95, 0.95, 1.0)
        _LineColor ("Line Color", Color) = (0.05, 0.05, 0.05, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ScanlineDensity;
            float _LineThickness;
            float _MotionAmount;
            float4 _BgColor;
            float4 _LineColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // スクリーン座標に基づくラインパターン算出（水平スキャンライン）
                float2 screenUV = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                
                // 陰影計算（NdotL）
                float3 lightDir = normalize(float3(0.4, 0.9, -0.6));
                float NdotL = saturate(dot(i.worldNormal, lightDir));
                
                // モーション量に応じた横揺れジッター
                float lineY = screenUV.y * _ScanlineDensity;
                float shift = sin(lineY * 0.4 + _Time.y * 14.0) * _MotionAmount * 0.006;
                float pattern = sin((screenUV.y + shift) * _ScanlineDensity * 3.14159265);

                // 明暗に応じたスキャンラインの太さコントロール
                float threshold = lerp(0.85, -0.75, NdotL);
                float lineMask = step(pattern, threshold * _LineThickness);

                fixed3 col = lerp(_BgColor.rgb, _LineColor.rgb, lineMask);

                // 輪郭エッジ強調（黒縁ライン）
                float3 viewDir = float3(0, 0, 1);
                float rim = 1.0 - saturate(dot(i.worldNormal, viewDir));
                if (rim > 0.72)
                {
                    col = _LineColor.rgb;
                }

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
