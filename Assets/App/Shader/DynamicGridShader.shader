Shader "Custom/DynamicGridShader"
{
    Properties
    {
        _BoardColor ("Board Color", Color) = (0.0, 0.45, 0.0, 1.0)  // 緑色の盤面
        _LineColor ("Line Color", Color) = (0.0, 0.0, 0.0, 1.0)     // 黒い線
        _LineWidth ("Line Width", Range(0.001, 0.1)) = 0.01         // 線の太さ
        _CellSize ("Cell Size", Float) = 1.0                        // 1マスの大きさ
    }
    SubShader
    {
        Tags {"Queue"="Geometry" "RenderType"="Opaque"}
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 worldPos : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _BoardColor;
            float4 _LineColor;
            float _LineWidth;
            float _CellSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // ワールド座標を取得
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xz; // XZ平面を使用
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 中心を(0,0)としたグリッド座標を計算
                // CellSizeに対して位置を計算し、0.5を足して中央揃えにする
                float2 cellPos = (i.worldPos / _CellSize) + 0.5;
                
                // 整数部分と小数部分に分割
                float2 cell = floor(cellPos);
                float2 cellFrac = frac(cellPos);
                
                // 線の幅を調整して描画
                float2 lines = step(1.0 - _LineWidth, cellFrac) + step(cellFrac, _LineWidth);
                float isLine = min(1.0, lines.x + lines.y);
                
                // 色を決定
                return lerp(_BoardColor, _LineColor, isLine);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}