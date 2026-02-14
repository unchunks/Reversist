Shader "Custom/CyberCursor"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 0, 1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True"
        }
        
        // ★重要: 加算合成 (Additive)
        // 背景色に自分の色を足し算する。黒い部分は透明になり、明るい部分は発光して見える。
        // Alpha値が下がると加算量が減り、自然に消える。
        Blend SrcAlpha One
        
        // 深さバッファには書き込まない（透過物の基本）
        ZWrite Off
        Cull Off

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
            fixed4 _BaseColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // テクスチャカラー * 指定色
                fixed4 col = tex2D(_MainTex, i.uv) * _BaseColor;
                
                // アルファが0なら描画しない（微小な値をクリップ）
                clip(col.a - 0.01);
                
                // 加算合成用に、AlphaをRGBに乗算しておく
                // (Blend SrcAlpha One なので、RGB値がそのまま加算される強さになる)
                col.rgb *= col.a;
                
                return col;
            }
            ENDCG
        }
    }
}
