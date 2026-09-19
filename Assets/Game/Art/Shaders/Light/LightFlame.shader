Shader "SpaceGame/Light/LightFlame"
{
    // The flame on the torch.
    //
    // Drawn on a cone or a stack of billboards whose UVs run v = 0 at the wick to v = 1 at the tip.
    // Unlike the rest of the family this one IS allowed to look like fire — it is a torch, and a
    // torch that does not look lit is a stick. What keeps it inside the same design language is
    // that it still resolves its colour through the shared palette, so the flame's core is the same
    // blown white as a beam's core and its thinnest edges fringe the same turquoise. A flame that
    // fringes cyan is the one detail that says this fire is not ordinary fire.
    //
    // Stylisation comes from posterising the flame into a few bands rather than smoothing it. A
    // smooth gradient plus turbulence reads photographic; three or four hard steps read as painted,
    // and they hold up at the small size a torch head actually occupies on screen.
    Properties
    {
        _Intensity      ("Intensity",                 Range(0, 12))   = 3.4
        _Height         ("Flame Height (frac)",       Range(0.1, 1))  = 0.85
        _Taper          ("Taper Power",               Range(0.5, 8))  = 2.2
        _CoreWidth      ("Core Width",                Range(0.02, 1)) = 0.34

        [Header(Motion)]
        _RiseSpeed      ("Rise Speed",                Range(0, 12))   = 3.2
        _TurbulenceScale("Turbulence Scale",          Range(1, 30))   = 7.0
        _TurbulenceAmount("Turbulence Amount",        Range(0, 1))    = 0.42
        _Lick           ("Tongue Sharpness",          Range(1, 16))   = 5.0

        [Header(Stoking)]
        _Stoke          ("Stoke (0..1)",              Range(0, 1))    = 0
        _StokeHeight    ("Stoke Height Gain",         Range(0, 2))    = 0.7
        _StokeIntensity ("Stoke Intensity Gain",      Range(0, 8))    = 3.5

        [Header(Stylisation)]
        _Bands          ("Posterise Bands (0 = off)", Range(0, 16))   = 5
        _BandSoftness   ("Band Softness",             Range(0, 1))    = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+20"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "LightFlame"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LightPalette.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Height;
                float _Taper;
                float _CoreWidth;
                float _RiseSpeed;
                float _TurbulenceScale;
                float _TurbulenceAmount;
                float _Lick;
                float _Stoke;
                float _StokeHeight;
                float _StokeIntensity;
                float _Bands;
                float _BandSoftness;
            CBUFFER_END

            /// Quantise into bands with a soft seam. The stylisation lever.
            float Posterise(float t, float bands, float softness)
            {
                if (bands < 1.0) return t;

                float scaled = t * bands;
                float index = floor(scaled);
                float frac0 = scaled - index;
                float stepped = index + smoothstep(0.5 - softness * 0.5, 0.5 + softness * 0.5, frac0);
                return saturate(stepped / bands);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.uv = IN.uv;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Stoking makes the flame taller as well as brighter. Height alone reads as the
                // torch growing, brightness alone reads as a lamp being turned up; together they
                // read as the thing catching, which is what a swing should look like.
                float height = saturate(_Height * (1.0 + _Stoke * _StokeHeight));

                float up = saturate(IN.uv.y);
                float across = abs(IN.uv.x * 2.0 - 1.0);

                // Turbulence scrolling DOWNWARD in sample space, which makes the pattern appear to
                // travel up the flame. This is the one effect in the family allowed to curl across
                // its own motion, because curling is what separates fire from a jet.
                float3 samplePos = float3(IN.positionOS.xz * _TurbulenceScale,
                                          IN.positionOS.y * _TurbulenceScale - _Time.y * _RiseSpeed);
                float turbulence = LightNoise(samplePos) * 0.65 + LightNoise(samplePos * 2.1) * 0.35;

                // The tongue: the flame's silhouette wanders with the turbulence, so the top edge
                // licks rather than sitting at a fixed height.
                float reach = height * (1.0 + (turbulence - 0.5) * _TurbulenceAmount);
                float along = 1.0 - smoothstep(reach * 0.55, reach, up);

                // Narrows towards the tip. Without the taper the flame is a column, and a column of
                // light is a beam, which is a different weapon.
                float taper = pow(saturate(1.0 - up / max(reach, 1e-4)), 1.0 / _Taper);
                float width = 1.0 - smoothstep(_CoreWidth * taper, taper, across);

                float body = saturate(along * width);
                body = pow(body, 1.0 / _Lick);

                // Energy is highest at the wick and falls towards the tip, so the palette puts the
                // white core down in the flame and lets the tips fringe turquoise — the opposite of
                // a real flame, and the detail that keeps this inside the light family.
                float energy = saturate(body * (1.0 - up * 0.55));
                energy = Posterise(energy, _Bands, _BandSoftness);

                float intensity = _Intensity + _Stoke * _StokeIntensity;
                return half4(LightEmission(energy, intensity), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
