Shader "SpaceGame/Light/LightBeam"
{
    // The shot — a bolt or sustained beam fired from the hand.
    //
    // Drawn on a cylinder whose local Z runs from the muzzle at v = 0 to the far end
    // at v = 1, and whose u wraps around the barrel. Scale the cylinder to the hit
    // distance and the shader handles the rest; _Travel drives the bolt head along it
    // for a projectile, or sits at 1 for a sustained beam.
    //
    // This is the effect most at risk of reading as a generic energy weapon, so the
    // shape work matters: a tight white core, a wide low-density sheath that fringes
    // turquoise, and filaments running strictly parallel to the barrel. Nothing
    // wobbles perpendicular to travel — that is what plasma does, and it is the read
    // we are avoiding.
    Properties
    {
        _Intensity      ("Intensity",                 Range(0, 12))    = 4.0
        _CoreWidth      ("Core Width (frac)",         Range(0.01, 1))  = 0.14
        _SheathWidth    ("Sheath Width (frac)",       Range(0.05, 1))  = 0.70
        _SheathPower    ("Sheath Falloff Power",      Range(1, 8))     = 3.0
        _Travel         ("Travel (0..1)",             Range(0, 1))     = 1.0
        _HeadSharpness  ("Head Sharpness",            Range(1, 40))    = 14.0
        _TailFade       ("Tail Fade Length (frac)",   Range(0.01, 1))  = 0.35
        _MuzzleBoost    ("Muzzle Boost",              Range(0, 4))     = 1.2
        _MuzzleLength   ("Muzzle Length (frac)",      Range(0.01, 0.5))= 0.08
        _FilamentScale  ("Filament Scale",            Range(1, 80))    = 30.0
        _FilamentStretch("Filament Stretch",          Range(1, 60))    = 26.0
        _FilamentSpeed  ("Filament Flow Speed",       Range(0, 20))    = 7.0
        _FilamentAmount ("Filament Amount",           Range(0, 1))     = 0.40
        _FilamentSharp  ("Filament Sharpness",        Range(1, 16))    = 9.0
        _DepthFade      ("Soft Depth Fade (m)",       Range(0, 4))     = 0.30
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
            Name "LightBeam"
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
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _CoreWidth;
                float _SheathWidth;
                float _SheathPower;
                float _Travel;
                float _HeadSharpness;
                float _TailFade;
                float _MuzzleBoost;
                float _MuzzleLength;
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
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.uv          = IN.uv;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = nrm.normalWS;
                OUT.screenPos   = ComputeScreenPos(pos.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float along = saturate(IN.uv.y);

                // Radial density. On a cylinder the silhouette is where the view ray
                // grazes the surface, so the rim term stands in for "how far off-axis
                // am I" without needing the ray-to-axis solve the flashlight beam does.
                // Cheaper, and accurate enough for something this thin.
                float3 viewDirWS = GetWorldSpaceViewDir(IN.positionWS);
                float offAxis = LightRim(IN.normalWS, viewDirWS, 1.0);

                float core   = 1.0 - smoothstep(0.0, _CoreWidth, offAxis);
                float sheath = pow(saturate(1.0 - offAxis / max(_SheathWidth, 1e-4)), _SheathPower);
                float radial = saturate(core + sheath * 0.35);

                // The bolt head is a sharp front at _Travel, with a fade trailing it.
                // At _Travel = 1 the head sits at the far end and the whole length is
                // lit, which is the sustained-beam case — same shader, no branch.
                float head = 1.0 - smoothstep(_Travel, _Travel + 0.02, along);
                float tail = smoothstep(_Travel - _TailFade, _Travel, along);
                float lengthMask = head * lerp(0.35, 1.0, pow(saturate(tail), 1.0 / _HeadSharpness));

                // Extra density right at the muzzle. This is what makes the shot look
                // like it came from the hand rather than appearing in mid-air, and it
                // is the part that lights the player's own arm once a real Light rides
                // along with it.
                float muzzle = (1.0 - smoothstep(0.0, _MuzzleLength, along)) * _MuzzleBoost;

                // Filaments flow from muzzle to tip, stretched hard along the barrel.
                float3 samplePos = float3(along * _FilamentScale - _Time.y * _FilamentSpeed,
                                          IN.uv.x * _FilamentScale,
                                          0.0);
                float filaments = LightFilaments(samplePos, _FilamentStretch, _FilamentSharp);

                float body = radial * lengthMask;
                float energy = saturate(body + filaments * _FilamentAmount * body + muzzle * radial);

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
