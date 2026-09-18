// A cloud deck: one huge disc at altitude, procedural in world space, lit by the sun.
//
// Not a volume. From below it is a ceiling with sunlit and shadowed lobes that drift; from
// above it is a floor of the same. What it is NOT good at is the moment the camera passes
// through it, so the deck fades out within _NearFade metres of the eye — a pilot climbing
// through it sees it thin to mist and thicken again above, which reads as cloud better than
// a hard plane ever could. Coverage, softness and scale are the whole weather system.
Shader "SpaceGame/CloudDeck"
{
    Properties
    {
        [Header(Cloud)]
        _CloudColor  ("Sunlit Colour", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadowed Colour", Color) = (0.62, 0.70, 0.82, 1)
        _Coverage    ("Coverage", Range(0, 1)) = 0.5
        _Softness    ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _Scale       ("Scale (noise units per metre)", Range(0.0002, 0.01)) = 0.0011
        _Detail      ("Detail Mix", Range(0, 1)) = 0.5

        [Header(Drift)]
        _WindDir   ("Wind Direction (xz)", Vector) = (1, 0.3, 0, 0)
        _WindSpeed ("Wind Speed (m/s)", Range(0, 20)) = 3

        [Header(Lighting)]
        _SunWrap   ("Sun Wrap (how far light bleeds round a lobe)", Range(0, 1)) = 0.4
        _LightStep ("Lobe Shading Distance (noise units)", Range(0.005, 0.2)) = 0.05

        [Header(Fade)]
        _NearFade  ("Near Fade Distance (m)", Range(10, 400)) = 120
        _Opacity   ("Opacity", Range(0, 1)) = 0.96
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "CloudDeckForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor, _ShadowColor;
                float  _Coverage, _Softness, _Scale, _Detail;
                float4 _WindDir;
                float  _WindSpeed, _SunWrap, _LightStep, _NearFade, _Opacity;
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
                return lerp(lerp(hash12(i),                hash12(i + float2(1, 0)), u.x),
                            lerp(hash12(i + float2(0, 1)), hash12(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int i = 0; i < 5; i++) { v += a * vnoise(p); p = p * 2.03 + 31.7; a *= 0.5; }
                return v;
            }

            // Two octave stacks against each other, so the deck never marches in one direction.
            float density(float2 p)
            {
                float broad  = fbm(p);
                float detail = fbm(p * 3.1 + 7.9);
                float n = broad + (detail - 0.5) * _Detail * 0.5;
                return smoothstep(1.0 - _Coverage - _Softness, 1.0 - _Coverage + _Softness, n);
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
                float2 wind = normalize(_WindDir.xy + 1e-5) * _WindSpeed * _Time.y;
                float2 p    = (IN.positionWS.xz + wind) * _Scale;

                float d = density(p);
                if (d <= 0.001) discard;

                // Lobe shading: compare the density a little way toward the sun. Denser toward the
                // sun means this point is on a lobe's shadow side.
                float3 sunDir = normalize(_MainLightPosition.xyz);
                float  towardSun = density(p + normalize(sunDir.xz + 1e-5) * _LightStep);
                float  lit = saturate(0.5 + (d - towardSun) * 3.0 + _SunWrap * 0.5);
                float3 colour = lerp(_ShadowColor.rgb, _CloudColor.rgb, lit);

                float dist = distance(_WorldSpaceCameraPos, IN.positionWS);
                float nearFade = smoothstep(0.0, _NearFade, dist);

                colour = MixFog(colour, IN.fogFactor);
                return half4(colour, d * nearFade * _Opacity);
            }
            ENDHLSL
        }
    }
}
