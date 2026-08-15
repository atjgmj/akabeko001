Shader "Akabeko/ScanlineHalftone"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScanlineDensity ("Scanline Density", Float) = 180.0
        _Contrast ("Contrast", Float) = 1.3
        _LineThickness ("Line Thickness", Float) = 0.85
        _MotionShift ("Motion Shift", Float) = 0.0
        _BgColor ("Background Color", Color) = (0.95, 0.95, 0.95, 1.0)
        _LineColor ("Line Color", Color) = (0.05, 0.05, 0.05, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ScanlineDensity;
            float _Contrast;
            float _LineThickness;
            float _MotionShift;
            float4 _BgColor;
            float4 _LineColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // 輝度（Luminance）の算出
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                lum = pow(lum, _Contrast);

                // モーションに応じた水平ラインの微細な横ズレ/ジッター
                float lineY = i.uv.y * _ScanlineDensity;
                float shift = sin(lineY * 0.5 + _Time.y * 12.0) * _MotionShift * 0.003;
                float pattern = sin((i.uv.y + shift) * _ScanlineDensity * 3.14159265);
                
                // 陰影に応じたスキャンラインの太さ制御 (暗いエリアほど線が太くなる)
                float lineThreshold = lerp(0.9, -0.6, saturate(1.0 - lum));
                float lineMask = step(pattern, lineThreshold * _LineThickness);

                // 背景オフホワイト色と黒スキャンラインの合成
                fixed3 finalColor = lerp(_BgColor.rgb, _LineColor.rgb, lineMask);

                // 完全な白（背景）はスキャンラインなしでオフホワイトに透過
                if (lum > 0.96)
                {
                    finalColor = _BgColor.rgb;
                }

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
