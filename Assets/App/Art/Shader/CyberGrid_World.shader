Shader "Custom/CyberGrid_World"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.05, 0.05, 0.1, 1.0)
        [HDR] _GridColor ("Grid Color", Color) = (0, 1, 1, 1)

        [Header(Grid Settings)]
        [Toggle(USE_WORLD_POS)] _UseWorldPos ("Use World Position", Float) = 0
        _GridScale ("Grid Scale", Float) = 1.0
        _LineWidth ("Line Width", Range(0.001, 0.1)) = 0.02
        _LineFalloff ("Line Glow", Range(0.0, 0.1)) = 0.01

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Float) = 1.0
        _ScanlineSpeed ("Scanline Speed", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature USE_WORLD_POS
            #include "UnityCG.cginc"
            #include "CyberCommon.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _BaseColor;
            fixed4 _GridColor;
            float _GridScale;
            float _LineWidth;
            float _LineFalloff;
            float _PulseSpeed;
            float _ScanlineSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                #if USE_WORLD_POS
                    // ワールド座標のXZ平面を使用（床用）
                    o.uv = o.worldPos.xz * _GridScale;
                #else
                    // 通常のUV
                    o.uv = v.uv * _GridScale; [cite_start]// [cite: 51]
                #endif
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                [cite_start]// グリッド計算 (CyberReversiGridのロジックを改良) [cite: 51-55]
                float2 pos = i.uv;
                float2 cellFrac = frac(pos);
                float2 distFromCenter = abs(cellFrac - 0.5);
                // 滑らかなライン描画
                float2 lineGlow = smoothstep(0.5 - _LineWidth * 0.5 - _LineFalloff, 0.5 - _LineWidth * 0.5, distFromCenter);
                float gridVal = max(lineGlow.x, lineGlow.y);

                [cite_start]// パルスアニメーション [cite: 58-60]
                float pulse = sin((i.uv.x + i.uv.y) * 2.0 - _Time.y * _PulseSpeed);
                pulse = pow(pulse * 0.5 + 0.5, 4.0);

                // 走査線
                float scan = scanlineEffect(i.uv * 0.1, _ScanlineSpeed, 0.2, 0.5);

                // 合成
                fixed4 finalGrid = _GridColor * (1.0 + pulse * 2.0);
                fixed4 outColor = lerp(_BaseColor, finalGrid, gridVal);
                outColor += _GridColor * scan * 0.2;

                return outColor;
            }
            ENDCG
        }
    }
}
