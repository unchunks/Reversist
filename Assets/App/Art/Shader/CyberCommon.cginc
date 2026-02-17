// CyberCommon.cginc
#ifndef CYBER_COMMON_INCLUDED
#define CYBER_COMMON_INCLUDED

[cite_start] // 擬似乱数 [cite: 80]
float random(float2 st)
{
    return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
}

[cite_start] // 六角形グリッドの距離関数 [cite: 81-85]
float hexDist(float2 p)
{
    p = abs(p);
    float c = dot(p, normalize(float2(1, 1.73)));
    return max(c, p.x);
}

// 六角形座標変換
float4 calculateHexCoords(float2 uv, float scale)
{
    float2 pUV = uv * scale;
    pUV.x *= 1.15;
    float2 r = float2(1, 1.73);
    float2 h = r * 0.5;
    float2 a = fmod(pUV, r) - h;
    float2 b = fmod(pUV - h, r) - h;
    float2 gv = dot(a, a) < dot(b, b) ? a : b;
    return float4(gv, pUV - gv);
}

[cite_start] // 走査線エフェクト (UI/World共通) [cite: 23-26, 108-109]
float scanlineEffect(float2 uv, float speed, float width, float alpha)
{
    float scanPos = frac(_Time.y * speed);
    float lineCenterY = 1.0 - scanPos;
    float distY = abs(uv.y - lineCenterY);
    return smoothstep(width, 0.0, distY) * alpha;
}

[cite_start] // グリッチ変位計算 [cite: 94-96]
float2 applyGlitch(float2 uv, float intensity, float speed)
{
    float t = _Time.y * speed;
    float glitchTrig = step(0.99, sin(t * 5.0 + uv.y * 10.0));
    uv.x += glitchTrig * intensity * 0.05 * sin(t * 50.0);
    return uv;
}

#endif
