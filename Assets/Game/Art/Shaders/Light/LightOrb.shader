Shader "SpaceGame/Light/LightOrb"
{
    // A travelling ball of light — the projectile the player throws.
    //
    // Drawn on a plain sphere mesh. The sphere is never seen as a surface: the shader
    // reads its normal only to work out how much of the volume the view ray passes
    // through, so the middle of the silhouette is dense and the edge is thin. Dense
    // resolves to the blown-white core through the shared palette, thin resolves to
    // the turquoise fringe, and the orange sits between them. That gradient across a
    // single ball is the whole reason the family reads as one material.
    //
    // Nothing here rises, curls, or flickers randomly. Those are fire behaviours. The
    // orb spins its detail around its own axis and pulses on a fixed period, which
    // reads as something powered rather than something burning.
    Properties
    {
        _Intensity      ("Intensity",                 Range(0, 8))    = 2.5
        _Density        ("Volume Density",            Range(0.5, 6))  = 2.2
        _FringePower    ("Fringe Thickness",          Range(0.5, 6))  = 2.0
        _FilamentScale  ("Filament Scale",            Range(1, 40))   = 12.0
        _FilamentSpeed  ("Filament Spin Speed",       Range(0, 8))    = 1.6
        _FilamentAmount ("Filament Amount",           Range(0, 1))    = 0.35
        _FilamentSharp  ("Filament Sharpness",        Range(1, 16))   = 6.0
        _PulsePeriod    ("Pulse Period (s)",          Range(0.1, 4))  = 0.9
        _PulseAttack    ("Pulse Attack (frac)",       Range(0.02, 1)) = 0.15
        _PulseDepth     ("Pulse Depth",               Range(0, 1))    = 0.25
        _DepthFade      ("Soft Depth Fade (m)",       Range(0, 4))    = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }
        LOD 100

        Pass
        {
            Name "LightOrb"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "LightPalette.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Density;
                float _FringePower;
                float _FilamentScale;
                float _FilamentSpeed;
                float _FilamentAmount;
                float _FilamentSharp;
                float _PulsePeriod;
                float _PulseAttack;
                float _PulseDepth;
                float _DepthFade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.screenPos   = ComputeScreenPos(pos.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 viewDirWS = GetWorldSpaceViewDir(IN.positionWS);

                // How much of the ball the view ray crosses. The rim term is near 1 at
                // the silhouette, so inverting it gives thickness through the middle.
                // Raising it to _Density controls how fast the core blows out, which is
                // the difference between a soft glow and a hard bright pip.
                float rim = LightRim(IN.normalWS, viewDirWS, _FringePower);
                float thickness = pow(saturate(1.0 - rim), _Density);

                // Detail spins around the orb's own Y axis in object space, so it stays
                // attached to the ball as it travels instead of swimming through world
                // space. Stretch of 1 keeps the filaments isotropic here — on a sphere
                // there is no single travel direction to align them to.
                float spin = _Time.y * _FilamentSpeed;
                float3 samplePos = IN.positionOS * _FilamentScale;
                samplePos.xz = float2(
                    samplePos.x * cos(spin) - samplePos.z * sin(spin),
                    samplePos.x * sin(spin) + samplePos.z * cos(spin));
                float filaments = LightFilaments(samplePos, 1.0, _FilamentSharp);

                // Filaments ADD energy rather than multiplying it. Multiplying would
                // punch dark holes through the core, which looks like soot.
                float energy = saturate(thickness + filaments * _FilamentAmount * thickness);

                // Asymmetric pulse: snaps up, eases down. Scaled so the orb never drops
                // to nothing — a projectile that blinks out mid-flight is unreadable.
                float pulse = LightPulse(_Time.y / max(_PulsePeriod, 1e-3), _PulseAttack);
                float brightness = _Intensity * lerp(1.0 - _PulseDepth, 1.0, pulse);

                // Soft fade so the orb does not cut a hard disc into terrain it passes.
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float softFade = LightDepthFade(sceneEyeDepth, IN.screenPos.w, _DepthFade);

                return half4(LightEmission(energy, brightness) * softFade, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
