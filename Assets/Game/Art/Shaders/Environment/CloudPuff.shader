// The clouds you fly past: clusters of low-poly spheres wearing this.
//
// Shape comes from the mesh (CloudMeshBuilder), and this shader's job is to make a lump of
// spheres read as one soft body: a sun-facing gradient with a wide wrap so the shadow side is
// still bright, a rim that goes translucent so the silhouette has no hard polygon edge, a slow
// vertex breathe so the lump is never quite still, and fog so far puffs sit in the haze.
Shader "SpaceGame/CloudPuff"
{
    Properties
    {
        [Header(Colour)]
        _LitColor    ("Sunlit Colour", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadowed Colour", Color) = (0.66, 0.74, 0.86, 1)
        _SunWrap     ("Sun Wrap", Range(0, 1)) = 0.6
        _Bottom      ("Underside Darkening", Range(0, 1)) = 0.35

        [Header(Edge)]
        _EdgePower ("Rim Softness Power", Range(0.5, 6)) = 2.2
        _EdgeFade  ("Rim Fade", Range(0, 1)) = 0.85

        [Header(Breathe)]
        _Wobble      ("Vertex Wobble (m)", Range(0, 6)) = 1.5
        _WobbleScale ("Wobble Scale", Range(0.005, 0.2)) = 0.03
        _WobbleSpeed ("Wobble Speed", Range(0, 1)) = 0.12

        _Opacity ("Opacity", Range(0, 1)) = 0.97
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-60" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "CloudPuffForward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // Depth is written so the spheres of one cluster occlude each other correctly; the
            // price is a slightly harder rim where two lobes cross, which the wobble hides.
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LitColor, _ShadowColor;
                float  _SunWrap, _Bottom, _EdgePower, _EdgeFade;
                float  _Wobble, _WobbleScale, _WobbleSpeed, _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            float hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise3(float3 p)
            {
                float3 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i), n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0)), n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1)), n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1)), n111 = hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                            lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                float breathe = vnoise3(positionWS * _WobbleScale + _Time.y * _WobbleSpeed) - 0.5;
                positionWS += normalWS * breathe * _Wobble;

                OUT.positionWS = positionWS;
                OUT.normalWS   = normalWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 L = normalize(_MainLightPosition.xyz);

                float wrap  = saturate((dot(N, L) + _SunWrap) / (1.0 + _SunWrap));
                float under = saturate(-N.y) * _Bottom;
                float3 colour = lerp(_ShadowColor.rgb, _LitColor.rgb, wrap) * (1.0 - under);

                float rim   = pow(1.0 - saturate(dot(N, V)), _EdgePower);
                float alpha = (1.0 - rim * _EdgeFade) * _Opacity;

                colour = MixFog(colour, IN.fogFactor);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
