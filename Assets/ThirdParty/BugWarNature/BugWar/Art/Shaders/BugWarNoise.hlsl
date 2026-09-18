#ifndef BUGWAR_NOISE_INCLUDED
#define BUGWAR_NOISE_INCLUDED

// Cheap hash/value noise shared by every BugWar shader. Ported from SpaceGame's StylizedTerrain
// and ClothWind, kept in one place so the terrain, the chitin cracks and the acid puddles all
// break up along the same grain.

float BugWarHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float BugWarHash31(float3 p)
{
    p = frac(p * float3(127.1, 311.7, 74.7));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

// 0..1 smooth value noise.
float BugWarValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = BugWarHash21(i);
    float b = BugWarHash21(i + float2(1, 0));
    float c = BugWarHash21(i + float2(0, 1));
    float d = BugWarHash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float BugWarValueNoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = BugWarHash31(i + float3(0, 0, 0));
    float n100 = BugWarHash31(i + float3(1, 0, 0));
    float n010 = BugWarHash31(i + float3(0, 1, 0));
    float n110 = BugWarHash31(i + float3(1, 1, 0));
    float n001 = BugWarHash31(i + float3(0, 0, 1));
    float n101 = BugWarHash31(i + float3(1, 0, 1));
    float n011 = BugWarHash31(i + float3(0, 1, 1));
    float n111 = BugWarHash31(i + float3(1, 1, 1));

    float x00 = lerp(n000, n100, f.x);
    float x10 = lerp(n010, n110, f.x);
    float x01 = lerp(n001, n101, f.x);
    float x11 = lerp(n011, n111, f.x);
    return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
}

// Two octaves is all a stylised look needs and keeps the fragment cost flat.
float BugWarFbm(float2 p)
{
    return BugWarValueNoise(p) * 0.65 + BugWarValueNoise(p * 2.17 + 11.3) * 0.35;
}

// Analytic gradient of BugWarValueNoise, for a bumped normal that costs one noise evaluation
// instead of the three (centre, +dx, +dy) a finite-difference version would need.
// BugWarValueNoise bilinear-blends the four corner hashes through u = smoothstep(f); writing that
// blend as n = a + k0*u.x + k1*u.y + k2*u.x*u.y (k0=b-a, k1=c-a, k2=a-b-c+d) makes it a plain
// product rule away from a closed-form derivative — no sampling a neighbour to see which way the
// field is leaning.
float2 BugWarNoiseGradient(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    float a = BugWarHash21(i);
    float b = BugWarHash21(i + float2(1, 0));
    float c = BugWarHash21(i + float2(0, 1));
    float d = BugWarHash21(i + float2(1, 1));

    float2 u = f * f * (3.0 - 2.0 * f);
    float2 du = 6.0 * f * (1.0 - f); // d/df of the smoothstep weight, chained in below

    float k0 = b - a;
    float k1 = c - a;
    float k2 = a - b - c + d;

    return float2((k0 + k2 * u.y) * du.x,
                   (k1 + k2 * u.x) * du.y);
}

// Interleaved gradient noise: a stable per-pixel threshold in 0..1 with no lookup table and no
// dynamic indexing, so it compiles everywhere. Used by the camouflage fade, which clips pixels
// below _Alpha and so reads as a shimmer instead of a hard alpha edge.
float BugWarDither(float2 screenPosition)
{
    return frac(52.9829189 * frac(0.06711056 * screenPosition.x + 0.00583715 * screenPosition.y));
}

#endif
