// The sky over the archipelago, built to be looked at from altitude.
//
// Three colours and a haze band do almost all of the work: a zenith, a horizon, a nadir (the
// sea's own colour, so the sky below the horizon reads as more sea rather than as a void), and
// a haze band at the horizon in the SAME colour the fog uses, so distant stacks dissolve into the
// sky instead of ending at a fog curtain with a different sky behind it. The sun is a disc and a
// glow that takes its direction from the scene's directional light, and a single band of high
// cirrus gives the sky a slow drift without pretending to be weather. No stars, no night: the
// flying world is always day.
Shader "SpaceGame/AirSkybox"
{
    Properties
    {
        [Header(Sky)]
        _ZenithColor  ("Zenith",  Color) = (0.20, 0.42, 0.80, 1)
        _HorizonColor ("Horizon", Color) = (0.72, 0.82, 0.92, 1)
        _NadirColor   ("Nadir (below the horizon)", Color) = (0.30, 0.42, 0.52, 1)
        _ZenithCurve  ("Zenith Curve (lower = bluer sooner)", Range(0.2, 3)) = 0.7

        [Header(Horizon haze  match the fog colour)]
        _HazeColor    ("Haze Colour", Color) = (0.78, 0.84, 0.90, 1)
        _HazeHeight   ("Haze Height (fraction of the sky)", Range(0.01, 0.6)) = 0.12
        _HazeStrength ("Haze Strength", Range(0, 1)) = 0.85

        [Header(Sun)]
        _SunColor     ("Sun Colour", Color) = (1, 0.97, 0.90, 1)
        _SunSize      ("Sun Size", Range(0.002, 0.1)) = 0.012
        _SunGlowColor ("Glow Colour", Color) = (1, 0.85, 0.65, 1)
        _SunGlow      ("Glow Strength", Range(0, 2)) = 0.6
        _SunGlowPower ("Glow Tightness", Range(1, 64)) = 12

        [Header(High cirrus)]
        _CirrusColor    ("Cirrus Colour", Color) = (1, 1, 1, 1)
        _CirrusCoverage ("Cirrus Coverage", Range(0, 1)) = 0.35
        _CirrusScale    ("Cirrus Scale", Range(0.5, 12)) = 3
        _CirrusSpeed    ("Cirrus Drift", Range(0, 0.2)) = 0.01
        _CirrusStrength ("Cirrus Strength", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor, _HorizonColor, _NadirColor;
                float  _ZenithCurve;
                float4 _HazeColor;
                float  _HazeHeight, _HazeStrength;
                float4 _SunColor, _SunGlowColor;
                float  _SunSize, _SunGlow, _SunGlowPower;
                float4 _CirrusColor;
                float  _CirrusCoverage, _CirrusScale, _CirrusSpeed, _CirrusStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirWS      : TEXCOORD0;
            };

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash12(i),                hash12(i + float2(1, 0)), u.x),
                            lerp(hash12(i + float2(0, 1)), hash12(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.07 + 17.3; a *= 0.5; }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // The skybox mesh is centred on the eye, so object position IS the view direction.
                OUT.dirWS = TransformObjectToWorldDir(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dirWS);
                float  y   = dir.y;

                // ---- Gradient: horizon up to zenith, horizon down to the sea's colour ----
                float3 above = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(y), _ZenithCurve));
                float3 below = lerp(_HorizonColor.rgb, _NadirColor.rgb, saturate(-y * 4.0));
                float3 sky   = y >= 0.0 ? above : below;

                // ---- Haze: strongest AT the horizon, either side of it ----
                float haze = exp(-abs(y) / max(_HazeHeight, 1e-3)) * _HazeStrength;
                sky = lerp(sky, _HazeColor.rgb, haze);

                // ---- Cirrus: a noise band projected onto a high plane ----
                if (y > 0.02)
                {
                    float2 uv = dir.xz / (y + 0.15) * _CirrusScale + _Time.y * _CirrusSpeed;
                    float  n  = fbm(uv);
                    float  c  = smoothstep(1.0 - _CirrusCoverage, 1.0 - _CirrusCoverage + 0.25, n);
                    float  fade = smoothstep(0.02, 0.25, y); // thins toward the horizon, where the haze is
                    sky = lerp(sky, _CirrusColor.rgb, c * fade * _CirrusStrength);
                }

                // ---- Sun ----
                float3 sunDir = normalize(_MainLightPosition.xyz);
                float  cosSun = dot(dir, sunDir);
                float  disc   = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.55, cosSun);
                float  glow   = pow(saturate(cosSun), _SunGlowPower) * _SunGlow;
                sky += _SunGlowColor.rgb * glow;
                sky  = lerp(sky, _SunColor.rgb, disc);

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
