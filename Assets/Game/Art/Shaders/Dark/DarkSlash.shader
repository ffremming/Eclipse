Shader "SpaceGame/Dark/DarkSlash"
{
    // The streak a creature's blade drags behind it: LightSlash read backwards.
    //
    // Drawn on the mesh SlashRibbon builds, with the same UV contract — u runs 0 at the
    // oldest point of the sweep to 1 at the leading edge, v runs 0 at the hilt side to 1
    // at the tip side — so the two trails are the same ribbon and can only be told apart
    // by what they do to the frame.
    //
    // The shape is LightSlash's, term for term. Four strands lie side by side across the
    // width, each with its own tail length along it, so as the trail ages the short ones
    // die and what is left is a handful of separate black LINES rather than one smear.
    // Filaments streak along the direction of travel, the head is the densest part, and a
    // hard line rides the tip's own path because the tip is the fastest thing in the swing
    // and the eye follows it. Cel-stepped, so the strands read as inked rather than as a
    // gradient.
    //
    // What is NOT LightSlash's is the blend and the colour. This multiplies the frame
    // instead of adding to it, and it multiplies straight to DARK_COLOUR_VOID — the
    // bottom of the dark palette, and the only part of that ramp this uses. The fringe
    // and the body are skipped on purpose: they are violet, which reads as a coloured
    // substance hanging in the air, and what is wanted from a swing is the world being
    // taken away. The void is the palette's own pitch black, a shade off zero for the
    // reason DarkPalette gives — an absolute zero reads as a hole in the rendering, and
    // a hole is a bug rather than a threat.
    //
    // The cost of black is that black on black cannot be seen, so the streak only reads
    // where the player's own light is already falling. That is where the player is
    // looking during a fight, and it is also what ties the effect to the light system:
    // an enemy's swing is visible exactly to the extent that you brought light to it.
    Properties
    {
        _Absorption      ("Absorption",                   Range(0, 1))     = 1.0
        _BandWidth       ("Strand Width",                 Range(0.1, 0.6)) = 0.3
        _TailDecay       ("Tail Decay  1/2/3/4",          Vector)          = (3.4, 2.3, 1.5, 0.8)
        _EdgeSharpness   ("Leading Edge Sharpness",       Range(1, 32))    = 12.0
        _EdgeAmount      ("Leading Edge Amount",          Range(0, 4))     = 1.6
        _RimWidth        ("Tip Rim Width",                Range(0.01, 0.3)) = 0.08
        _RimAmount       ("Tip Rim Amount",               Range(0, 4))     = 1.5
        _Steps           ("Cel Steps",                    Range(2, 12))    = 5.0
        _FilamentScale   ("Filament Scale",               Range(1, 80))    = 36.0
        _FilamentStretch ("Filament Stretch",             Range(1, 60))    = 16.0
        _FilamentSpeed   ("Filament Drift Speed",         Range(0, 12))    = 2.4
        _FilamentAmount  ("Filament Amount",              Range(0, 3))     = 1.1
        _FilamentSharp   ("Filament Sharpness",           Range(1, 16))    = 6.0
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
            Name "DarkSlash"
            Tags { "LightMode" = "UniversalForward" }

            // Multiply, where LightSlash is additive. Everything else about the two passes
            // is deliberately identical.
            Blend DstColor Zero
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Light/LightPalette.hlsl"
            #include "DarkPalette.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float  _Absorption;
                float  _BandWidth;
                float4 _TailDecay;
                float  _EdgeSharpness;
                float  _EdgeAmount;
                float  _RimWidth;
                float  _RimAmount;
                float  _Steps;
                float  _FilamentScale;
                float  _FilamentStretch;
                float  _FilamentSpeed;
                float  _FilamentAmount;
                float  _FilamentSharp;
            CBUFFER_END

            // Quantise to _Steps levels, with the seam between levels softened so the strands read
            // as inked layers rather than as banding artefacts.
            float4 CelStep(float4 x, float steps)
            {
                float4 scaled = x * steps;
                float4 level = floor(scaled);
                return (level + smoothstep(0.35, 0.65, scaled - level)) / steps;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float u = saturate(IN.uv.x);
                float v = saturate(IN.uv.y);

                // The light slash's four hue bands, borrowed for their SHAPE alone — overlapping
                // tents across the width, so the strands blend into their neighbours instead of
                // sitting as four hard stripes. Nothing here asks what colour they were.
                float4 strands = LightSlashBands(v, _BandWidth);
                float4 tails = pow(u.xxxx, _TailDecay);

                // Streaks running along the direction of travel. They only ever deepen what is
                // already there, so a gap between strands stays clear of the frame.
                float streaks = LightFilaments(float3(u * _FilamentScale, v * _FilamentScale, _Time.y * _FilamentSpeed),
                                               _FilamentStretch, _FilamentSharp);
                float4 layers = strands * tails * (1.0 + streaks * _FilamentAmount);

                // Summed rather than maxed: where two strands lie over one another the blade took
                // twice as much, and that overlap is what gives the ribbon a dense middle.
                float4 stepped = CelStep(saturate(layers), _Steps);
                float ink = stepped.x + stepped.y + stepped.z + stepped.w;

                // The head. Where the light one blows out to white, this one goes to nothing at
                // all. Eased in from the hilt side so it does not put a hard black bar across the
                // widest part of the ribbon.
                ink += pow(u, _EdgeSharpness) * smoothstep(0.0, 0.2, v) * _EdgeAmount;

                // The line the tip itself drew, riding the very edge of the longest strand.
                ink += smoothstep(1.0 - _RimWidth, 1.0 - _RimWidth * 0.3, v) * tails.w * _RimAmount;

                // Soft on the hilt side, where the ribbon has no business having an edge; crisp on
                // the tip side, where a hard edge is the blade.
                ink = saturate(ink) * smoothstep(0.0, 0.06, v) * saturate(_Absorption);

                // A multiply contributes nothing at 1 and everything at the void. No ink leaves the
                // frame exactly as it found it.
                return half4(lerp(float3(1.0, 1.0, 1.0), DARK_COLOUR_VOID, ink), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
