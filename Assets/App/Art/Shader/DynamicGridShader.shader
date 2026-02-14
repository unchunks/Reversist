Shader "Custom/ReversiGridShader_UV"
{
    Properties
    {
        _BoardColor ("Board Color", Color) = (0.0, 0.45, 0.0, 1.0)
        _LineColor ("Line Color", Color) = (0.0, 0.0, 0.0, 1.0)
        
        // 線の太さ（1マスのサイズに対する割合。0.02なら2%）
        _LineWidth ("Line Width Ratio", Range(0.001, 0.2)) = 0.02
        
        // ここを「8」にすると8x8になります
        _GridCount ("Grid Count", Float) = 8.0
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
                float2 uv : TEXCOORD0; // UV座標を使用
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _BoardColor;
            float4 _LineColor;
            float _LineWidth;
            float _GridCount;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // UV座標をそのまま渡す（通常 0.0 ～ 1.0 の範囲）
                o.uv = v.uv;
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV(0~1) に 分割数(8) を掛ける -> 範囲が (0~8) になる
                float2 pos = i.uv * _GridCount;

                // fracで小数部分を取り出すことで 0~1 の繰り返しを作る
                float2 cellFrac = frac(pos);

                // 線の描画（アンチエイリアスなし）
                // _LineWidth はセルの大きさに対する割合として機能します
                float2 lines = step(1.0 - _LineWidth, cellFrac) + step(cellFrac, _LineWidth);
                
                // x方向またはy方向に線があるか
                float isLine = min(1.0, lines.x + lines.y);
                
                // 外枠（0.0と1.0の境界）を綺麗に描画するための補正（UVの端っこ対策）
                // これがないと一番端の線が半分の太さに見えることがあります
                float2 border = step(pos, _LineWidth) + step(_GridCount - _LineWidth, pos);
                isLine = max(isLine, min(1.0, border.x + border.y));

                return lerp(_BoardColor, _LineColor, isLine);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}