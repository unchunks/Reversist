Shader "Custom/CyberStone"
{
    Properties
    {
        [Header(Base)]
        [HDR] _Color ("Main Color", Color) = (0.1, 0.1, 0.1, 1)
        [HDR] _SubColor ("Sub Color", Color) = (0, 0, 0, 1)
        
        [Header(Pattern)]
        // 0:Normal, 1:Expander, 2:Fixed, 3:Phantom, 4:Bomb, 5:Spy
        _PatternMode ("Pattern Mode", Float) = 0 
        _PatternScale ("Pattern Scale", Float) = 5.0
        _AnimSpeed ("Animation Speed", Float) = 1.0
        
        [Header(Rim)]
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        [HDR] _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD1;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SubColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _PatternMode)
                UNITY_DEFINE_INSTANCED_PROP(float, _PatternScale)
                UNITY_DEFINE_INSTANCED_PROP(float, _AnimSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _RimPower)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RimColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            // --- ユーティリティ関数 ---
            float random(float2 st) { return frac(sin(dot(st.xy, float2(12.9898,78.233))) * 43758.5453123); }
            
            float noise(float2 st) {
                float2 i = floor(st); float2 f = frac(st);
                float a = random(i); float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0)); float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a)* u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // --- 図形描画用関数 ---
            float box(float2 st, float2 size) {
                size = float2(0.5, 0.5) - size * 0.5;
                float2 uv = step(size, st) * step(size, 1.0 - st);
                return uv.x * uv.y;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float4 subColor = UNITY_ACCESS_INSTANCED_PROP(Props, _SubColor);
                float patternMode = UNITY_ACCESS_INSTANCED_PROP(Props, _PatternMode);
                float patternScale = UNITY_ACCESS_INSTANCED_PROP(Props, _PatternScale);
                float animSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimSpeed);
                float rimPower = UNITY_ACCESS_INSTANCED_PROP(Props, _RimPower);
                float4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);

                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float dist = length(centered);
                float t = _Time.y * animSpeed;
                float pattern = 0.0;

                // --- パターン生成ロジック (Refined Visuals) ---
                
                // Mode 0: Normal (Stable Ring)
                if (patternMode < 0.5) {
                    pattern = smoothstep(0.4, 0.35, dist) * smoothstep(0.2, 0.25, dist);
                    pattern *= 0.8 + 0.2 * sin(t * 2.0); 
                } 
                // Mode 1: Expander (Outward Arrows)
                else if (patternMode < 1.5) {
                    float2 absUV = abs(centered);
                    float maxAxis = max(absUV.x, absUV.y);
                    float arrows = frac(maxAxis * 6.0 - t * 1.5);
                    arrows = step(0.6, arrows) * step(maxAxis, 0.45);
                    float mask = step(absUV.x, absUV.y) * step(0.1, absUV.y) + step(absUV.y, absUV.x) * step(0.1, absUV.x);
                    pattern = arrows * mask;
                    pattern += step(dist, 0.1);
                } 
                // Mode 2: Fixed (Shield & Cross)
                else if (patternMode < 2.5) {
                    float shield = smoothstep(0.45, 0.4, dist) * smoothstep(0.35, 0.4, dist);
                    float2 rotatedUV = float2(
                        centered.x * 0.707 - centered.y * 0.707,
                        centered.x * 0.707 + centered.y * 0.707
                    );
                    float xMark = box(rotatedUV + 0.5, float2(0.4, 0.1)) + box(rotatedUV + 0.5, float2(0.1, 0.4));
                    xMark = min(xMark, 1.0);
                    pattern = max(shield, xMark * 0.8);
                } 
                // Mode 3: Phantom (Digital Ash / Crumbling)
                // 崩れ落ちるようなグリッチ表現
                else if (patternMode < 3.5) {
                    // 1. 座標破壊（グリッチ）
                    float2 glitchUV = uv;
                    // ランダムな水平ズレ
                    float twitch = step(0.95, random(float2(floor(uv.y * 20.0), t * 10.0)));
                    glitchUV.x += twitch * 0.1 * sin(t * 50.0);

                    // 2. 基本形状（欠けたリング）
                    float d = length(glitchUV - 0.5);
                    float baseShape = smoothstep(0.4, 0.38, d) - smoothstep(0.25, 0.23, d);

                    // 3. 崩落エフェクト（粒子が下へ落ちる）
                    float2 particleUV = glitchUV * float2(20.0, 10.0); // 粗いピクセル
                    float rain = frac(particleUV.y + t * 2.0); // 落下アニメ
                    float noiseVal = random(floor(particleUV) + floor(t * 2.0)); // ランダムな欠け
                    
                    // ノイズと落下を組み合わせて形状を侵食させる
                    float erosion = step(0.3, noiseVal * rain); 
                    
                    pattern = baseShape * erosion;
                    
                    // 4. 幽霊のような不安定な明滅
                    pattern *= 0.5 + 0.5 * step(0.1, random(float2(t, 0)));
                } 
                // Mode 4: Bomb (Explosion Shockwave)
                else if (patternMode < 4.5) {
                    float wave = frac(dist * 4.0 - t * 3.0);
                    wave = smoothstep(0.0, 0.2, wave) * smoothstep(0.5, 0.2, wave);
                    float angle = atan2(centered.y, centered.x);
                    float spikes = step(0.5, sin(angle * 12.0));
                    pattern = wave + (spikes * step(dist, 0.3) * step(0.1, dist));
                    pattern += step(dist, 0.15) * (0.5 + 0.5 * sin(t * 20.0));
                } 
                // Mode 5: Spy (The Eye)
                else {
                    float2 eyeUV = centered;
                    float upperLid = step(length(eyeUV + float2(0, 0.3)), 0.55);
                    float lowerLid = step(length(eyeUV - float2(0, 0.3)), 0.55);
                    float eyeShape = upperLid * lowerLid;
                    float pupil = step(dist, 0.12);
                    float iris = step(dist, 0.22) * step(0.12, dist);
                    float scan = step(0.0, sin(uv.y * 20.0 - t * 5.0));
                    pattern = (eyeShape - step(dist, 0.22)) + pupil + (iris * scan);
                }

                // --- 合成 ---
                fixed4 finalCol = lerp(color, subColor, pattern);

                float3 normal = normalize(i.normal);
                float3 viewDir = normalize(i.viewDir);
                float rim = 1.0 - saturate(dot(normal, viewDir));
                rim = pow(rim, rimPower);
                
                finalCol += rimColor * rim;

                return finalCol;
            }
            ENDCG
        }
    }
}
