Shader "Custom/CyberUI_Pro"
{
    Properties
    {
        [Header(Base)]
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Tint Color", Color) = (1,1,1,1)
        
        [Header(Interaction)]
        _Hover ("Hover Intensity (0-1)", Range(0, 1)) = 0.0

        [Header(Border)]
        [Toggle(ENABLE_BORDER)] _EnableBorder ("Enable Border", Float) = 1
        [HDR] _BorderColor ("Border Color", Color) = (0, 1, 1, 1)
        _BorderWidth ("Border Width", Range(0, 1)) = 0.02
        _CornerSize ("Corner Cut Size", Range(0, 1)) = 0.05

        [Header(Background Pattern)]
        [KeywordEnum(None, Grid, Hexagon, Circuit, Stripe)] _PatternType ("Pattern Type", Float) = 1
        [HDR] _PatternColor ("Pattern Color", Color) = (0, 0.2, 0.2, 0.5)
        _PatternScale ("Pattern Scale", Float) = 20.0
        _PatternSpeed ("Animation Speed", Float) = 0.5
        _PatternAlpha ("Pattern Visibility", Range(0, 1)) = 1.0

        [Header(Effects)]
        [Toggle(ENABLE_SCANLINE)] _EnableScanline ("Enable Scanline", Float) = 1
        _ScanSpeed ("Scanline Speed", Float) = 1.0
        [Toggle(ENABLE_GLITCH)] _EnableGlitch ("Enable Glitch", Float) = 0
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.1

        [cite_start]// UI Masking (Required) [cite: 70]
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (0,0,0,0)
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
            
            #pragma multi_compile _PATTERNTYPE_NONE _PATTERNTYPE_GRID _PATTERNTYPE_HEXAGON _PATTERNTYPE_CIRCUIT _PATTERNTYPE_STRIPE
            #pragma shader_feature ENABLE_BORDER
            #pragma shader_feature ENABLE_SCANLINE
            #pragma shader_feature ENABLE_GLITCH

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "CyberCommon.cginc" // 作成した共通ライブラリ

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
                float2 sizeRatio : TEXCOORD3; // アスペクト比補正用
            };

            fixed4 _Color;
            float4 _ClipRect;
            float _Hover;

            // Border
            float4 _BorderColor;
            float _BorderWidth;
            float _CornerSize;

            // Pattern
            float4 _PatternColor;
            float _PatternScale;
            float _PatternSpeed;
            float _PatternAlpha;

            // Effects
            float _ScanSpeed;
            float _GlitchIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                
                // キャンバス上のRectTransformサイズ比率を簡易計算（ddx/ddyでフラグメントでやるほうが正確だが負荷軽減のため）
                // 注: UIのScaleを変えると変動する可能性があります
                OUT.sizeRatio = float2(1.0, 1.0); 
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float t = _Time.y;

                // 1. Glitch
                #if ENABLE_GLITCH
                    uv = applyGlitch(uv, _GlitchIntensity + _Hover * 0.1, 1.0);
                #endif

                // 2. Base Pattern Generation
                float pattern = 0.0;
                
                #if defined(_PATTERNTYPE_GRID)
                    [cite_start]// CyberWindowUIのグリッドロジック [cite: 15-17]
                    float2 gridUV = uv * _PatternScale;
                    float2 gridDeriv = fwidth(gridUV);
                    float2 gridLine = smoothstep(1.0 - gridDeriv * 1.5, 1.0, frac(gridUV));
                    pattern = max(gridLine.x, gridLine.y);

                #elif defined(_PATTERNTYPE_HEXAGON)
                    float4 hc = calculateHexCoords(uv, _PatternScale);
                    float d = hexDist(hc.xy);
                    float edge = smoothstep(0.45, 0.5, d);
                    float pulse = sin(dot(floor(hc.zw), float2(12.9, 78.2)) + t * _PatternSpeed);
                    pattern = edge + step(0.9 - (_Hover * 0.2), pulse) * (1.0 - edge);

                #elif defined(_PATTERNTYPE_STRIPE)
                    float stripe = frac((uv.x + uv.y) * _PatternScale * 0.5 - t * _PatternSpeed);
                    pattern = step(0.5, stripe);
                
                #elif defined(_PATTERNTYPE_CIRCUIT)
                    // 簡易回路ノイズ
                    float2 cUV = uv * _PatternScale + float2(0, t * 0.1);
                    float2 i = floor(cUV);
                    float2 f = frac(cUV);
                    float r = random(i);
                    float lineVal = (r > 0.5) ? step(abs(f.x - f.y), 0.1) : step(abs(f.x + f.y - 1.0), 0.1);
                    pattern = lineVal * step(0.2, random(i + 1.0));
                #endif

                // パターン合成
                float pAlpha = _PatternAlpha + (_Hover * 0.5);
                fixed4 finalColor = IN.color * 0.1; // ベースは暗く
                finalColor += _PatternColor * pattern * pAlpha;

                // 3. Scanline
                #if ENABLE_SCANLINE
                    float scan = scanlineEffect(uv, _ScanSpeed, 0.1, 0.3 + _Hover * 0.2);
                    finalColor += fixed4(1,1,1,1) * scan;
                #endif

                [cite_start]// 4. Border & Corner (Pixel Perfect logic from CyberWindowUI) [cite: 18-21]
                #if ENABLE_BORDER
                    float2 dist = abs(uv - 0.5) * 2.0;
                    // fwidthを使用してピクセル単位の均一な幅を計算（重要: アスペクト比依存解消）
                    float2 dDist = fwidth(dist); 
                    // _BorderWidthをUV空間での見た目の太さに変換
                    float2 targetWidth = _BorderWidth / (dDist / min(dDist.x, dDist.y)); 
                    
                    float2 borderAlpha = smoothstep(1.0 - _BorderWidth - dDist, 1.0 - _BorderWidth, dist);
                    float borderShape = max(borderAlpha.x, borderAlpha.y);

                    // コーナーカット
                    // UV距離(マンハッタン距離)で計算
                    float cornerDist = (abs(uv.x - 0.5) + abs(uv.y - 0.5));
                    if (cornerDist > (1.0 - _CornerSize)) discard; // 単純なClip

                    // 枠線の着色 (ホバーで発光)
                    fixed4 bCol = _BorderColor * (1.0 + _Hover);
                    finalColor = lerp(finalColor, bCol, borderShape);
                #endif

                // Unity UI Clipping
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                
                return finalColor;
            }
            ENDCG
        }
    }
}
