Shader "SpaceGame/Light/LightArc"
{
    // The swipe — a blade of light thrown around the player by a spin or a slash.
    //
    // Drawn on a ribbon mesh whose UVs run u = 0 at the oldest point of the sweep to
    // u = 1 at the leading edge, and v = 0 at the inner radius to v = 1 at the outer.
    // A TrailRenderer produces exactly this layout, so the effect can ride the weapon
    // socket without any custom mesh work.
    //
    // The read is: a hard bright leading edge that the eye tracks, decaying backwards
    // into a turquoise wisp. That decay is what sells the arc as a path something
    // travelled rather than a static crescent pasted over the player. _SweepProgress
    // is driven from the animation so the blade grows with the swing instead of
    // existing whole for the duration of the clip.
    Properties
    {
        _Intensity      ("Intensity",                Range(0, 8))    = 3.0
        _EdgeSharpness  ("Leading Edge Sharpness",   Range(1, 24))   = 7.0
        _TrailDecay     ("Trail Decay",              Range(0.2, 8))  = 2.2
        _WidthProfile   ("Width Falloff Power",      Range(0.5, 8))  = 2.4
        _SweepProgress  ("Sweep Progress",           Range(0, 1))    = 1.0
        _SweepFeather   ("Sweep Feather",            Range(0.01, 0.5)) = 0.08
        _FilamentScale  ("Filament Scale",           Range(1, 60))   = 22.0
        _FilamentStretch("Filament Stretch",         Range(1, 40))   = 14.0
        _FilamentSpeed  ("Filament Drift Speed",     Range(0, 6))    = 1.1
        _FilamentAmount ("Filament Amount",          Range(0, 1))    = 0.45
        _FilamentSharp  ("Filament Sharpness",       Range(1, 16))   = 8.0
        _DepthFade      ("Soft Depth Fade (m)",      Range(0, 4))    = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+10"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "LightArc"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
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
                float4 screenPos   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _EdgeSharpness;
                float _TrailDecay;
                float _WidthProfile;
                float _SweepProgress;
                float _SweepFeather;
                float _FilamentScale;
                float _FilamentStretch;
                float _FilamentSpeed;
                float _FilamentAmount;
                float _FilamentSharp;
                float _DepthFade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.uv          = IN.uv;
                OUT.screenPos   = ComputeScreenPos(pos.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Along the sweep. The leading edge is a narrow power curve so it stays
                // a thin hot line, and the rest of the ribbon decays behind it on a
                // separate, much gentler curve. Two curves rather than one because a
                // single falloff cannot be both a crisp edge and a long tail.
                float lead  = pow(saturate(IN.uv.x), _EdgeSharpness);
                float trail = pow(saturate(IN.uv.x), _TrailDecay) * 0.45;

                // Across the blade. Peaks at the middle of the ribbon and thins to
                // nothing at both rails, so the outer rail fringes turquoise the same
                // way the orb's silhouette does.
                float across = 1.0 - abs(IN.uv.y * 2.0 - 1.0);
                float width = pow(saturate(across), _WidthProfile);

                // The swing has only reached _SweepProgress. Everything ahead of that
                // is not drawn yet. Feathered so the growing tip is soft rather than a
                // scissor cut travelling along the ribbon.
                float sweep = 1.0 - smoothstep(_SweepProgress,
                                               _SweepProgress + _SweepFeather,
                                               IN.uv.x);

                // Filaments stretched hard along u so they streak in the direction of
                // travel. This is the axis that separates the effect from fire: flame
                // detail curls across its own motion, light detail runs with it.
                float3 samplePos = float3(IN.uv.x * _FilamentScale,
                                          IN.uv.y * _FilamentScale,
                                          _Time.y * _FilamentSpeed);
                float filaments = LightFilaments(samplePos, _FilamentStretch, _FilamentSharp);

                float body = (lead + trail) * width * sweep;
                float energy = saturate(body + filaments * _FilamentAmount * body);

                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float softFade = LightDepthFade(sceneEyeDepth, IN.screenPos.w, _DepthFade);

                return half4(LightEmission(energy, _Intensity) * softFade, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
