// The sea the sea stacks stand in, built to be read from altitude.
//
// Everything is procedural in world space on a flat plane — no textures, no vertex
// displacement — because the ornithopter looks at this ocean from fifty to three hundred
// metres up, where displaced geometry is invisible but moving light is everything. What
// sells water from up there is exactly three things: the colour deepening where the sea
// gets deep, the sun glittering across the swell, and foam collaring the rock bases. Each
// has its own block below, and nothing else earned a place.
//
// Depth is read from the camera depth texture (enabled on PC_RPAsset), so the shallows,
// the foam line and the soft shoreline all come from how much water the eye looks through
// rather than from authored masks — drop a new stack anywhere and its foam collar is
// already there.
Shader "SpaceGame/OceanWater"
{
    Properties
    {
        [Header(Colour by depth)]
        _ShallowColor ("Shallow Colour", Color) = (0.22, 0.60, 0.60, 1)
        _DeepColor    ("Deep Colour",    Color) = (0.03, 0.16, 0.27, 1)
        _DepthFade    ("Depth Fade Distance (m of water to reach deep colour)", Range(0.5, 40)) = 9

        [Header(Waves  Scrolling procedural normals)]
        _WaveScale    ("Wave Scale (smaller = broader swell)", Range(0.005, 1)) = 0.055
        _WaveSpeed    ("Wave Drift Speed", Range(0, 2)) = 0.28
        _WaveStrength ("Wave Normal Strength", Range(0, 1)) = 0.35
        _DetailMix    ("High-Frequency Ripple Mix", Range(0, 1)) = 0.5

        [Header(Sun and sky)]
        _Smoothness   ("Glitter Tightness (spec power source)", Range(0.5, 1)) = 0.92
        _SpecStrength ("Sun Glitter Strength", Range(0, 4)) = 1.6
        _SkyTint      ("Horizon Reflection Tint", Color) = (0.63, 0.75, 0.82, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4

        [Header(Foam at the waterline)]
        _FoamDepth    ("Foam Depth (m of water that still foams)", Range(0.05, 6)) = 1.6
        _FoamColor    ("Foam Colour", Color) = (0.92, 0.96, 0.95, 1)
        _FoamScale    ("Foam Breakup Scale", Range(0.02, 2)) = 0.35
        _FoamSpeed    ("Foam Churn Speed", Range(0, 2)) = 0.5

        [Header(Body)]
        _Opacity      ("Deep Water Opacity", Range(0, 1)) = 0.94
        _EdgeSoftness ("Shoreline Softness (m)", Range(0.02, 3)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "OceanForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor;
                float  _DepthFade;
                float  _WaveScale, _WaveSpeed, _WaveStrength, _DetailMix;
                float  _Smoothness, _SpecStrength;
                float4 _SkyTint;
                float  _FresnelPower;
                float  _FoamDepth;
                float4 _FoamColor;
                float  _FoamScale, _FoamSpeed;
                float  _Opacity, _EdgeSoftness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
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
                return lerp(lerp(hash12(i),                  hash12(i + float2(1, 0)), u.x),
                            lerp(hash12(i + float2(0, 1)),   hash12(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int i = 0; i < 3; i++) { v += a * vnoise(p); p *= 2.11; a *= 0.5; }
                return v;
            }

            // The swell as a height field: two fbm layers drifting against each other so the
            // pattern never visibly tiles or marches in one direction, plus a finer ripple
            // layer that keeps the surface alive near the camera.
            float waveHeight(float2 p, float t)
            {
                float broad  = fbm(p * _WaveScale + t * _WaveSpeed * float2(1.0, 0.6));
                float cross  = fbm(p * _WaveScale * 1.7 - t * _WaveSpeed * float2(0.7, 1.0) + 41.7);
                float ripple = fbm(p * _WaveScale * 6.3 + t * _WaveSpeed * float2(1.3, -0.9));
                return broad + cross + ripple * _DetailMix;
            }

            // Normal from finite differences of the height field. The step is fixed in metres,
            // not texels, so wave shading stays consistent however the plane is scaled.
            float3 waveNormal(float2 p, float t)
            {
                const float e = 0.8;
                float hC = waveHeight(p, t);
                float hX = waveHeight(p + float2(e, 0), t);
                float hZ = waveHeight(p + float2(0, e), t);
                float3 n = float3((hC - hX) / e, 1.0 / max(_WaveStrength, 1e-4), (hC - hZ) / e);
                return normalize(n);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vpi.positionCS;
                OUT.positionWS = vpi.positionWS;
                OUT.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float  t = _Time.y;
                float2 p = IN.positionWS.xz;
                float3 N = waveNormal(p, t);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // ---- How much water the eye is looking through ----
                // Scene depth minus the surface's own depth, both in eye space. This is a view
                // distance, not a vertical depth, which is what we want: a grazing look across
                // the shallows reads deep, exactly as real water does.
                float2 screenUV   = IN.positionCS.xy / _ScaledScreenParams.xy;
                float  sceneEye   = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  surfaceEye = IN.positionCS.w; // clip w IS the eye depth under perspective
                float  thickness  = max(sceneEye - surfaceEye, 0.0);

                // ---- Colour by depth ----
                float  absorb    = 1.0 - exp(-thickness / max(_DepthFade, 1e-3));
                float3 water     = lerp(_ShallowColor.rgb, _DeepColor.rgb, absorb);

                // The wave field also shifts the body colour a touch, so the swell is visible
                // even where the sun is not — from high up this is most of the texture.
                float swell = waveHeight(p, t);
                water *= 0.88 + 0.24 * swell;

                // ---- Sky reflection by fresnel ----
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                water = lerp(water, _SkyTint.rgb, fresnel * 0.75);

                // ---- Sun glitter ----
                Light  sun  = GetMainLight();
                float3 H    = normalize(sun.direction + V);
                float  spec = pow(saturate(dot(N, H)), exp2(_Smoothness * 11.0));
                water += sun.color * spec * _SpecStrength;

                // ---- Foam where the water thins against rock ----
                float foamBand = 1.0 - smoothstep(0.0, _FoamDepth, thickness);
                float churn    = fbm(p * _FoamScale * 6.0 + t * _FoamSpeed * float2(0.9, -1.1));
                float foam     = foamBand * smoothstep(0.35, 0.75, churn + foamBand * 0.35);
                water = lerp(water, _FoamColor.rgb, saturate(foam));

                // ---- Shoreline blend ----
                // Fade out over the last centimetres of thickness so the surface meets the
                // rock and the seabed as a waterline, not as a hard clipped edge.
                float alpha = _Opacity * smoothstep(0.0, _EdgeSoftness, thickness);
                alpha = max(alpha, foam * 0.9);

                water = MixFog(water, IN.fogFactor);
                return half4(water, alpha);
            }
            ENDHLSL
        }
    }
}
