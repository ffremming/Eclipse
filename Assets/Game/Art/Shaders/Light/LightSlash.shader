Shader "SpaceGame/Light/LightSlash"
{
    // The trail a swing leaves: a ribbon of light between the hilt and the tip of whatever was swung.
    //
    // Drawn on the mesh SlashRibbon builds. u runs 0 at the oldest point of the sweep to 1 at the
    // leading edge; v runs 0 at the hilt side to 1 at the tip side.
    //
    // The look is a spectrum being split. Four layers — blue, white, orange, red — lie side by side
    // across the ribbon, and each one has its own tail length along it. The red at the tip side is
    // the longest, the blue at the hilt side dies first, so as the trail ages it separates into its
    // colours and finally leaves only a red thread, the way a prism spreads what it is given.
    // A blown-white edge sits at the head and a hot line runs along the tip's path, because the
    // tip is the fastest thing in the swing and the eye follows it.
    //
    // With _OrbPalette on, the four layers are replaced by the orb's own energy ramp — turquoise
    // fringe, orange body, white core, from LightPalette — for a weapon that carries a ball of light
    // and should not throw any colour the ball does not have. Same shape, same cel steps, same
    // streaks; only what the energy is turned into changes.
    //
    // Cel-stepped rather than smooth: the brightness is quantised into a few bands with soft seams,
    // which is what turns a gradient into something that reads as inked and stylised.
    Properties
    {
        [Toggle] _OrbPalette ("Orb Palette (ball colours only)", Float)   = 0
        _Intensity       ("Intensity",                    Range(0, 8))    = 3.2
        _BandWidth       ("Colour Band Width",            Range(0.1, 0.6)) = 0.3
        _TailDecay       ("Tail Decay  Blue/White/Orange/Red", Vector)    = (3.4, 2.3, 1.5, 0.8)
        _EdgeSharpness   ("Leading Edge Sharpness",       Range(1, 32))   = 12.0
        _EdgeAmount      ("Leading Edge Amount",          Range(0, 4))    = 1.6
        _RimWidth        ("Tip Rim Width",                Range(0.01, 0.3)) = 0.08
        _RimAmount       ("Tip Rim Amount",               Range(0, 4))    = 1.5
        _Steps           ("Cel Steps",                    Range(2, 12))   = 5.0
        _FilamentScale   ("Filament Scale",               Range(1, 80))   = 36.0
        _FilamentStretch ("Filament Stretch",             Range(1, 60))   = 16.0
        _FilamentSpeed   ("Filament Drift Speed",         Range(0, 12))   = 4.0
        _FilamentAmount  ("Filament Amount",              Range(0, 3))    = 1.1
        _FilamentSharp   ("Filament Sharpness",           Range(1, 16))   = 6.0
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
            Name "LightSlash"
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
            };

            CBUFFER_START(UnityPerMaterial)
                float  _OrbPalette;
                float  _Intensity;
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

            // Quantise to _Steps levels, with the seam between levels softened so the bands read
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

                // Each hue's own tail. pow on the vector: four different decay rates in one call.
                float4 bands = LightSlashBands(v, _BandWidth);
                float4 tails = pow(u.xxxx, _TailDecay);

                // Streaks running along the direction of travel — speed lines inside the light.
                // They only brighten what is already there, so a gap between bands stays dark.
                float streaks = LightFilaments(float3(u * _FilamentScale, v * _FilamentScale, _Time.y * _FilamentSpeed),
                                               _FilamentStretch, _FilamentSharp);
                float4 layers = bands * tails * (1.0 + streaks * _FilamentAmount);

                float3 colour;
                float3 hot;
                if (_OrbPalette > 0.5)
                {
                    // One energy field instead of four layers: densest towards the tip's path and
                    // the head, thinning to the fringe at the hilt side and down the tail. The
                    // orb's ramp then decides what each level looks like.
                    float energy = tails.z * lerp(0.28, 0.7, v) * (1.0 + streaks * _FilamentAmount * 0.5);
                    energy = CelStep(saturate(energy).xxxx, _Steps).x;
                    colour = LightEmission(energy, 1.0);
                    hot = LIGHT_COLOUR_CORE;
                }
                else
                {
                    colour = LightSlashColour(CelStep(saturate(layers), _Steps));
                    hot = LIGHT_SLASH_WHITE;
                }

                // The blown-white head. Across the whole width, but eased in from the hilt side so
                // it does not put a hard white bar where the blade is thickest.
                float head = pow(u, _EdgeSharpness) * smoothstep(0.0, 0.2, v);
                colour += hot * head * _EdgeAmount;

                // The hot line the tip itself drew, riding the very edge of the red.
                float rim = smoothstep(1.0 - _RimWidth, 1.0 - _RimWidth * 0.3, v) * tails.w;
                colour += hot * rim * _RimAmount;

                // Soft on the hilt side, where the ribbon has no business having an edge; crisp on
                // the tip side, where a hard edge is the blade.
                colour *= smoothstep(0.0, 0.06, v);

                return half4(colour * _Intensity, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
