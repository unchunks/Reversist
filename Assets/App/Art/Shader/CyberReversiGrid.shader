Shader "Custom/CyberReversiGrid"
{
    Properties
    {
        [Header(Base Settings)]
        _BaseColor ("Background Color", Color) = (0.05, 0.05, 0.1, 1.0)
        [HDR] _GridColor ("Grid Neon Color", Color) = (0.0, 1.0, 1.0, 1.0) // HDRで発光させる
        
        [Header(Grid Settings)]
        _GridCount ("Grid Count", Float) = 8.0
        _LineWidth ("Line Width", Range(0.001, 0.1)) = 0.02
        _LineSoftness ("Line Glow", Range(0.0, 0.05)) = 0.01
        
        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0.0, 5.0)) = 1.5
        _PulseDensity ("Pulse Density", Range(1.0, 20.0)) = 5.0
        
        [Header(Scanline)]
        _ScanlineAlpha ("Scanline Alpha", Range(0.0, 1.0)) = 0.1
        _ScanlineCount ("Scanline Count", Float) = 100.0
        _ScanlineSpeed ("Scanline Speed", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
            };

            // Properties
            fixed4 _BaseColor;
            fixed4 _GridColor;
            float _GridCount;
            float _LineWidth;
            float _LineSoftness;
            float _PulseSpeed;
            float _PulseDensity;
            float _ScanlineAlpha;
            float _ScanlineCount;
            float _ScanlineSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            // グリッドラインの計算（Smoothstepによる発光表現付き）
            float gridFactor(float2 uv, float gridCount, float width, float softness)
            {
                float2 pos = uv * gridCount;
                float2 cellFrac = frac(pos);
                
                // 中心からの距離 (0.0 ~ 0.5)
                float2 distFromCenter = abs(cellFrac - 0.5);
                
                // 線の太さの半分
                float halfWidth = width * 0.5;
                
                // 線の端(0.5)に近いほど1.0になるようにsmoothstep計算
                // distFromCenter は 0.0(中心) -> 0.5(端)
                // 閾値: (0.5 - halfWidth) あたりから光り始める
                
                float2 lineGlow = smoothstep(0.5 - halfWidth - softness, 0.5 - halfWidth, distFromCenter);
                
                // X軸とY軸のグリッドを合成
                return max(lineGlow.x, lineGlow.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. グリッド描画
                float gridIntensity = gridFactor(i.uv, _GridCount, _LineWidth, _LineSoftness);

                // 外枠の強制描画（UVの端っこ処理）
                float2 border = step(i.uv, _LineWidth * 0.5) + step(1.0 - _LineWidth * 0.5, i.uv);
                gridIntensity = max(gridIntensity, max(border.x, border.y));

                // 2. パルスエフェクト (電脳的な明滅)
                // UV座標と時間を混ぜて波を作る
                //斜めに走る波
                float pulseWave = sin((i.uv.x + i.uv.y) * _PulseDensity - _Time.y * _PulseSpeed); 
                // 0~1の範囲に正規化し、少し鋭くする
                pulseWave = pow(pulseWave * 0.5 + 0.5, 4.0); 
                
                // グリッドの色をパルスで強化
                // ベースの明るさ(0.5) + パルス分
                float4 finalGridColor = _GridColor * (0.5 + pulseWave * 1.5);

                // 3. 背景の走査線 (Scanline)
                float scanline = sin(i.uv.y * _ScanlineCount - _Time.y * _ScanlineSpeed);
                scanline = scanline * 0.5 + 0.5;
                fixed4 bgColor = _BaseColor + (_BaseColor * scanline * _ScanlineAlpha);

                // 4. 合成
                // グリッド部分には背景を描画せず、加算合成のような見た目にする
                fixed4 finalColor = lerp(bgColor, finalGridColor, gridIntensity);

                return finalColor;
            }
            ENDCG
        }
    }
}
