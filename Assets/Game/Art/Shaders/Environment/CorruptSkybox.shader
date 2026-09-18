// The sky over the corrupted world.
//
// Built on the same three-colour-plus-haze skeleton as AirSkybox, because the two need to agree
// about where a horizon is, but inverted in intent. AirSkybox is a sky to fly in. This one is a
// lid: the brightest part is a low, bruised band near the horizon and it gets DARKER towards the
// zenith, which is the opposite of a real daytime sky and is most of why it reads as oppressive
// rather than merely overcast. The eye expects light overhead; taking it away is unsettling
// before the player can say why.
//
// The sun is there, and it is eclipsed. That is the one thing in the sky the player can navigate
// by, and it earns its place by explaining the dark instead of merely being dark: a world under a
// permanent eclipse is a world with a reason. It is also the only saturated red anywhere, so it
// pulls the eye hard — which is exactly what a fixed landmark in a black world should do.
//
// The disc gives no light. All of its brightness lives at the edge: a thin ring where light
// escapes past the occluding body, ragged corona streamers around that, and a wide weak bleed of
// red into the surrounding sky. A black centre ringed in fire reads as something enormous in the
// way, which is the feeling wanted, while leaving the player's own light the only light that
// actually falls on anything.
//
// The palette is deliberately almost colourless — ash greys pulled slightly green and slightly
// violet, nothing saturated anywhere. That is what leaves the player's white-orange-turquoise
// light as the only saturated thing on screen, so the eye goes to it and nowhere else. Draining
// the world is what makes the light legible; a colourful sky would compete with the one thing
// the player needs to read.
Shader "SpaceGame/CorruptSkybox"
{
    Properties
    {
        [Header(Sky  darkest at the zenith)]
        _ZenithColor  ("Zenith (the lid)",       Color) = (0.018, 0.020, 0.028, 1)
        _HorizonColor ("Horizon (the bruise)",   Color) = (0.115, 0.105, 0.098, 1)
        _NadirColor   ("Nadir (below horizon)",  Color) = (0.010, 0.011, 0.013, 1)
        _ZenithCurve  ("Zenith Curve (lower = darker sooner)", Range(0.2, 3)) = 1.35

        [Header(Horizon haze  match the fog colour)]
        _HazeColor    ("Haze Colour",            Color) = (0.135, 0.128, 0.120, 1)
        _HazeHeight   ("Haze Height (fraction)", Range(0.01, 0.6)) = 0.22
        _HazeStrength ("Haze Strength",          Range(0, 1))      = 0.9

        [Header(The eclipse)]
        _EclipseSize    ("Disc Size",              Range(0.005, 0.15)) = 0.045
        _DiscColor      ("Disc Colour",            Color) = (0.004, 0.003, 0.004, 1)
        _RingColor      ("Ring Colour",            Color) = (1.00, 0.13, 0.06, 1)
        _RingWidth      ("Ring Width (frac of disc)", Range(0.01, 0.6)) = 0.14
        _RingIntensity  ("Ring Intensity",         Range(0, 12)) = 5.0
        _CoronaColor    ("Corona Colour",          Color) = (0.62, 0.09, 0.05, 1)
        _CoronaStrength ("Corona Strength",        Range(0, 4))  = 1.5
        _CoronaPower    ("Corona Falloff",         Range(1, 24)) = 4.0
        _StreamerScale  ("Streamer Count",         Range(1, 40)) = 14
        _StreamerDepth  ("Streamer Depth",         Range(0, 1))  = 0.55
        _BloodSky       ("Blood Sky Bleed",        Range(0, 2))  = 0.7

        [Header(Overcast)]
        _PallColor    ("Pall Colour",            Color) = (0.055, 0.052, 0.058, 1)
        _PallCoverage ("Pall Coverage",          Range(0, 1))     = 0.62
        _PallScale    ("Pall Scale",             Range(0.5, 12))  = 2.2
        _PallSpeed    ("Pall Drift",             Range(0, 0.2))   = 0.006
        _PallStrength ("Pall Strength",          Range(0, 1))     = 0.7

        [Header(Stylisation)]
        _Bands        ("Posterise Bands (0 = off)", Range(0, 32)) = 12
        _BandSoftness ("Band Softness",          Range(0, 1))     = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Background"
            "RenderType"     = "Background"
            "PreviewType"    = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "CorruptSky"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Light/LightPalette.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dirWS       : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _NadirColor;
                float  _ZenithCurve;
                float4 _HazeColor;
                float  _HazeHeight;
                float  _HazeStrength;
                float  _EclipseSize;
                float4 _DiscColor;
                float4 _RingColor;
                float  _RingWidth;
                float  _RingIntensity;
                float4 _CoronaColor;
                float  _CoronaStrength;
                float  _CoronaPower;
                float  _StreamerScale;
                float  _StreamerDepth;
                float  _BloodSky;
                float4 _PallColor;
                float  _PallCoverage;
                float  _PallScale;
                float  _PallSpeed;
                float  _PallStrength;
                float  _Bands;
                float  _BandSoftness;
            CBUFFER_END

            // The scene's directional light, so the occluded glow sits where the sun would be.
            // Written by WorldAtmosphere alongside the fog globals rather than authored here, so
            // sky and fog cannot disagree about which way is sunward.
            float4 _CorruptSunDir;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirWS = IN.positionOS.xyz;
                return OUT;
            }

            /// Quantise a 0..1 value into bands with a soft seam.
            ///
            /// This is the stylisation lever. A fully smooth gradient reads as photographic; hard
            /// bands read as illustration. _BandSoftness sits between the two so the sky can be
            /// pushed from painterly to poster without touching anything else.
            float Posterise(float t, float bands, float softness)
            {
                if (bands < 1.0) return t;

                float scaled = t * bands;
                float index = floor(scaled);
                float frac0 = scaled - index;
                float stepped = index + smoothstep(0.5 - softness * 0.5, 0.5 + softness * 0.5, frac0);
                return saturate(stepped / bands);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dirWS);
                float height = dir.y;

                // Above the horizon the gradient runs horizon to zenith; below it runs horizon to
                // nadir. Posterising the parameter rather than the colour keeps the bands aligned
                // across every term that uses it.
                float up = saturate(height);
                float upCurved = Posterise(pow(up, _ZenithCurve), _Bands, _BandSoftness);
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upCurved);

                float down = saturate(-height);
                sky = lerp(sky, _NadirColor.rgb, saturate(down / 0.25));

                // The haze band. Sits tight to the horizon and is the colour the fog uses, so
                // distant terrain dissolves into the sky instead of ending at a visible curtain.
                float haze = 1.0 - smoothstep(0.0, _HazeHeight, abs(height));
                sky = lerp(sky, _HazeColor.rgb, haze * _HazeStrength);

                // --- The eclipse ---------------------------------------------------------
                //
                // Angular distance from the sky direction to the eclipse, measured as a chord
                // length rather than an arc. Over the few degrees the disc spans the two agree,
                // and the chord costs no inverse trig.
                float3 sunDir = normalize(_CorruptSunDir.xyz);
                float angular = length(dir - sunDir);
                float discT = angular / max(_EclipseSize, 1e-4);

                // The occluding body. Solid, and darker than any part of the sky behind it, so it
                // reads as a hole punched in the world rather than as a dark cloud. Everything
                // bright about the eclipse happens at its edge, which is what makes the black
                // centre feel like an absence of something enormous.
                float disc = 1.0 - smoothstep(0.96, 1.0, discT);

                // The ring. A thin band sitting just outside the disc's edge, in the hottest red
                // available. This is the only saturated thing in the sky and it stays tiny: a
                // wide ring reads as a sunset, a thin one reads as light escaping past an edge.
                float ringT = abs(discT - 1.0) / max(_RingWidth, 1e-4);
                float ring = pow(saturate(1.0 - ringT), 2.0) * _RingIntensity;

                // Corona streamers. The angle around the disc drives a noise lookup, so the falloff
                // is ragged rather than a clean radial gradient — spikes of light reaching out at
                // irregular intervals. Static, because a corona that animates starts to look like
                // a spell effect instead of a sky.
                float2 offset = dir.xz - sunDir.xz;
                float angle = atan2(offset.y, offset.x);
                float streamer = LightNoise(float3(angle * _StreamerScale, 0.0, 0.0));
                streamer = lerp(1.0, streamer, _StreamerDepth);

                float corona = pow(saturate(1.0 - saturate(discT / 6.0)), _CoronaPower)
                             * _CoronaStrength * streamer;
                corona *= 1.0 - disc;

                // The red bleeding into the sky around it. Wide, weak, and posterised with the rest
                // so it bands like everything else rather than being the one smooth gradient.
                float bleed = pow(saturate(1.0 - saturate(discT / 24.0)), 2.0) * _BloodSky;
                bleed = Posterise(bleed, _Bands, _BandSoftness);

                // Only the wide bleed goes down before the overcast, so the pall can dull the red
                // haze the way cloud dulls a sunset.
                sky += _CoronaColor.rgb * bleed;

                // The pall: one slow layer of overcast, drifting. Two octaves only — more detail
                // starts to look like weather, and weather implies a world that still works.
                float2 pallUV = dir.xz / max(abs(height) + 0.35, 1e-3);
                float3 pallPos = float3(pallUV * _PallScale, _Time.y * _PallSpeed);
                float pall = LightNoise(pallPos) * 0.65 + LightNoise(pallPos * 2.3) * 0.35;
                pall = smoothstep(1.0 - _PallCoverage, 1.0, pall);
                pall = Posterise(pall, _Bands * 0.5, _BandSoftness);
                pall *= saturate(up * 3.0) * _PallStrength;
                sky = lerp(sky, _PallColor.rgb, pall);

                // The eclipse itself lands on top of the overcast rather than under it. Physically
                // that is backwards, but it is the one shape in the sky the player has to be able
                // to find, and a cloud drifting over it would take it away with no warning. The
                // corona is drawn here too, so the disc and its edge stay one object.
                sky = lerp(sky, _DiscColor.rgb, disc);
                sky += _CoronaColor.rgb * corona;
                sky += _RingColor.rgb * ring * (1.0 - disc);

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
