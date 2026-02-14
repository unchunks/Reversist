Shader "Custom/CyberWindowUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        
        _Aspect ("Aspect Ratio", Float) = 1.0
        
        [Header(Border)]
        [HDR] _BorderColor ("Border Color", Color) = (0, 1, 1, 1)
        _BorderWidth ("Border Width", Range(0, 0.5)) = 0.02
        _CornerSize ("Corner Size", Range(0, 0.5)) = 0.1
        
        [Header(Background)]
        [HDR] _BgColor ("Background Color", Color) = (0, 0.1, 0.1, 0.8)
        _GridSize ("Grid Size", Float) = 20.0
        _GridAlpha ("Grid Visibility", Range(0, 1)) = 0.1
        
        [Header(Animation)]
        _ScanSpeed ("Scan Speed", Float) = 1.0
        _ScanColor ("Scanline Color", Color) = (1, 1, 1, 0.2)
        _ScanWidth ("Scanline Width", Range(0.01, 0.5)) = 0.05
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            float4 _ClipRect;

            float _Aspect;
            float4 _BorderColor;
            float _BorderWidth;
            float _CornerSize;
            float4 _BgColor;
            float _GridSize;
            float _GridAlpha;
            float _ScanSpeed;
            float4 _ScanColor;
            float _ScanWidth; // プロパティに追加せず内部定数でも良いが調整用に

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // --- 1. 背景グリッド ---
                float2 gridUV = uv * float2(_GridSize * _Aspect, _GridSize);
                float2 gridDeriv = fwidth(gridUV);
                float2 gridLine = smoothstep(1.0 - gridDeriv * 1.5, 1.0, frac(gridUV));
                float gridPattern = max(gridLine.x, gridLine.y) * _GridAlpha;
                
                fixed4 bg = _BgColor + (gridPattern * _BorderColor * 0.5);

                // --- 2. 枠線 (Pixel-Perfect Safe Border) ---
                float2 dist = abs(uv - 0.5) * 2.0;
                float2 dDist = fwidth(dist);
                float2 minBorderWidth = dDist * 3;

                float2 targetBorderWidth;
                targetBorderWidth.y = _BorderWidth;
                targetBorderWidth.x = _BorderWidth / _Aspect;

                float2 actualWidth = max(targetBorderWidth, minBorderWidth);

                float2 border = smoothstep(1.0 - actualWidth - dDist, 1.0 - actualWidth, dist);
                float borderShape = max(border.x, border.y);

                // --- 3. コーナー強調 ---
                float2 targetCornerSize;
                targetCornerSize.y = _CornerSize;
                targetCornerSize.x = _CornerSize / _Aspect;
                
                float2 cornerDist = smoothstep(1.0 - targetCornerSize - dDist, 1.0 - targetCornerSize, dist);
                float cornerShape = min(cornerDist.x, cornerDist.y) * borderShape;
                
                // --- 4. 走査線 (修正版) ---
                // 上から下へ流れる位置 (0~1)
                float scanPos = frac(_Time.y * _ScanSpeed);
                // 現在のピクセルY座標との距離 (UVは下から上が0->1なので反転考慮)
                // 上(1.0)から下(0.0)へ流したい場合: 1.0 - scanPos がラインの中心Y
                float lineCenterY = 1.0 - scanPos;
                float distY = abs(uv.y - lineCenterY);
                
                // 距離が _ScanWidth 以内なら発光 (中心が1.0、端が0.0)
                // smoothstep(max, min, t) の順で渡すと逆転してフェードアウトを作れる
                float scanLine = smoothstep(_ScanWidth, 0.0, distY);
                
                // 少し鋭さを出すために二乗
                scanLine = pow(scanLine, 2.0);

                // --- 合成 ---
                fixed4 finalColor = bg;
                finalColor += _ScanColor * scanLine;

                fixed4 borderCol = _BorderColor;
                finalColor = lerp(finalColor, borderCol * 1.5, cornerShape);
                finalColor = lerp(finalColor, borderCol, borderShape - cornerShape);

                finalColor *= IN.color;
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

                return finalColor;
            }
            ENDCG
        }
    }
}
