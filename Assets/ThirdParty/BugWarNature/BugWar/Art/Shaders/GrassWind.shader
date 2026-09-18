Shader "BugWar/GrassWind"
{
    // The sandbox's tall grass, ground cover and flowers. Same house shape as GardenTerrain and
    // Chitin — a Foo.shader paired with a FooInput.hlsl for the per-material properties — plus a
    // shared BugWarWind.hlsl bend so every pass here agrees on where a blade actually is.
    //
    // LowPolyWindAssets builds these materials as copies of the vendor Nicrom "Low Poly Wind"
    // material via Material.CopyPropertiesFromMaterial, so every property this shader shares a name
    // with the vendor's LPW_Vegetation.shader (see that shader's own Properties block) carries the
    // vendor's authored value across; everything else — translucency, tip colour, root occlusion —
    // is BugWar's own and simply falls back to the default below.
    Properties
    {
        _Color   ("Colour", Color) = (1, 1, 1, 1)
        _MainTex ("Main Tex", 2D) = "white" {}

        // Not a vendor Properties-block entry (see GrassWindInput.hlsl), but every imported
        // material still carries a serialized value for it, so it copies across like the rest.
        _Cutoff  ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Wind Bend)]
        _MBAmplitude ("MB Amplitude", Float) = 1.5
        _MBFrequency ("MB Frequency", Float) = 1.11
        _MBMaxHeight ("MB Max Height", Float) = 10

        // Declared only so CopyPropertiesFromMaterial from the vendor material finds a home for
        // these; the bend here is analytic and samples no noise texture. A ShaderLab Header cannot
        // carry a comma, so the explanation lives in this comment rather than in the label.
        [Header(World Space Noise vendor compatibility)]
        [NoScaleOffset] _NoiseTexture ("Noise Texture", 2D) = "bump" {}
        _NoiseTextureTilling ("Noise Tilling - Static (XY), Animated (ZW)", Vector) = (1, 1, 1, 1)
        _NoisePannerSpeed    ("Noise Panner Speed", Vector) = (0.05, 0.03, 0, 0)

        [Header(Translucency)]
        _TransColor   ("Translucency Colour", Color) = (0.65, 0.85, 0.35, 1)
        _TransStrength("Translucency Strength", Range(0, 4)) = 1.6
        _TransPower   ("Translucency Falloff", Range(1, 16)) = 4
        _TransAmbient ("Translucency Ambient", Range(0, 1)) = 0.15
        _TransShadow  ("Translucency In Shadow", Range(0, 1)) = 0.35

        [Header(Tip and Root)]
        _TipColor      ("Tip Colour", Color) = (0.72, 0.82, 0.34, 1)
        _TipBlend      ("Tip Blend", Range(0, 1)) = 0.6
        _RootOcclusion ("Root Occlusion", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GrassWindInput.hlsl"
            #include "BugWarWind.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;
                float3 normalWS      : TEXCOORD1;
                float2 uv            : TEXCOORD2;
                float  heightFraction: TEXCOORD3;
                float  fogFactor     : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // The effect this whole shader exists for: the sun rakes in at 34 deg elevation, so a
            // blade with the light behind it should glow at the tip rather than just going dark.
            half3 grassLighting(Light light, half3 N, half3 V, half heightFraction, half3 albedo)
            {
                half3 L = light.direction;
                half atten = light.shadowAttenuation * light.distanceAttenuation;

                // Wrapped diffuse: a blade is thin, so light reaches round it.
                half wrap = saturate((dot(N, L) + 0.4) / 1.4);
                half3 diffuse = albedo * light.color * wrap * atten;

                // Transmission. Strongest looking into the sun through the blade, and strongest at
                // the tip where the blade is thinnest.
                half back = pow(saturate(dot(-V, -L)), _TransPower);
                half thickness = lerp(0.25, 1.0, heightFraction);
                half3 trans = _TransColor.rgb * light.color * back * thickness * _TransStrength
                            * lerp(_TransShadow, 1.0, atten);

                return diffuse + trans;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate(IN.positionOS.y / max(0.001, _MBMaxHeight));
                positionWS = BugWarWindBend(positionWS, rootWS, h, _BugWarWindTime);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.positionWS = positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.heightFraction = h;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half alpha = tex.a * _Color.a;
                clip(alpha - _Cutoff);

                half3 N = normalize(IN.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                half3 albedo = lerp(_Color.rgb, _TipColor.rgb, IN.heightFraction * _TipBlend) * tex.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 color = grassLighting(mainLight, N, V, IN.heightFraction, albedo);

            #ifdef _ADDITIONAL_LIGHTS
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                uint addCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(addCount)
                    Light addLight = GetAdditionalLight(lightIndex, IN.positionWS);
                    color += grassLighting(addLight, N, V, IN.heightFraction, albedo);
                LIGHT_LOOP_END
            #endif

                color += _TransAmbient * _TransColor.rgb * SampleSH(N);

                // Root occlusion: blade bases in a dense stand sit in their own neighbours' shade,
                // so darken the whole result there rather than only the albedo — a base that still
                // caught full transmission or full ambient would not read as shaded at all.
                color *= lerp(1.0 - _RootOcclusion, 1.0, IN.heightFraction);

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BugWarPasses.hlsl"
            #include "GrassWindInput.hlsl"
            #include "BugWarWind.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V ShadowVert(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate(IN.positionOS.y / max(0.001, _MBMaxHeight));
                positionWS = BugWarWindBend(positionWS, rootWS, h, _BugWarWindTime);

                OUT.positionCS = BugWarShadowPositionCS(positionWS, TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 ShadowFrag(V IN) : SV_Target { GrassAlphaClip(IN.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GrassWindInput.hlsl"
            #include "BugWarWind.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V DepthVert(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate(IN.positionOS.y / max(0.001, _MBMaxHeight));
                positionWS = BugWarWindBend(positionWS, rootWS, h, _BugWarWindTime);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DepthFrag(V IN) : SV_Target { GrassAlphaClip(IN.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GrassWindInput.hlsl"
            #include "BugWarWind.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V DepthNormalsVert(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate(IN.positionOS.y / max(0.001, _MBMaxHeight));
                positionWS = BugWarWindBend(positionWS, rootWS, h, _BugWarWindTime);

                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DepthNormalsFrag(V IN) : SV_Target
            {
                GrassAlphaClip(IN.uv);
                return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0.0);
            }
            ENDHLSL
        }

        // Not optional: the camera runs AntialiasingMode.TemporalAntiAliasing, and TAA reprojects
        // every pixel using per-object motion vectors. A vertex-animated shader with no MotionVectors
        // pass falls back to camera-only motion, so the wind's own vertex displacement is invisible
        // to TAA's reprojection and every moving blade smears a short trail behind itself each frame
        // instead of resolving crisp — turning TAA into a downgrade on exactly the geometry (thousands
        // of instanced blades) it most needed to help.
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ColorMask RG

            HLSLPROGRAM
            #pragma vertex MotionVectorsVert
            #pragma fragment MotionVectorsFrag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
            #include "GrassWindInput.hlsl"
            #include "BugWarWind.hlsl"

            struct A
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V
            {
                float4 positionCS                 : SV_POSITION;
                float4 positionCSNoJitter         : TEXCOORD0;
                float4 previousPositionCSNoJitter : TEXCOORD1;
                float2 uv                         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V MotionVectorsVert(A IN)
            {
                V OUT = (V)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // The object itself does not move — every LowPolyWind prefab in the sandbox is a
                // static scatter instance — so the only source of motion is the wind bend, and the
                // only thing that needs to differ between "current" and "previous" is which instant
                // of the wind clock BugWarWindBend evaluates. positionWS/rootWS come from this
                // frame's (unmoving) object-to-world matrix for both samples.
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float h = saturate(IN.positionOS.y / max(0.001, _MBMaxHeight));

                float3 currentWS  = BugWarWindBend(positionWS, rootWS, h, _BugWarWindTime);
                float3 previousWS = BugWarWindBend(positionWS, rootWS, h, _BugWarWindTime - unity_DeltaTime.x);

                // _NonJitteredViewProjMatrix / _PrevViewProjMatrix (UnityInput.hlsl) are this URP
                // version's real names for what the brief called UNITY_MATRIX_UNJITTERED_VP /
                // UNITY_MATRIX_PREV_VP — those macros do not exist in this package. The on-screen
                // SV_POSITION still uses the ordinary jittered VP (TransformWorldToHClip), matching
                // every other pass and what URP's own ObjectMotionVectors.hlsl does.
                OUT.positionCS = TransformWorldToHClip(currentWS);
                OUT.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, float4(currentWS, 1.0));
                OUT.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, float4(previousWS, 1.0));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 MotionVectorsFrag(V IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                GrassAlphaClip(IN.uv);

                // CalcNdcMotionVectorFromCsPositions (MotionVectorsCommon.hlsl) is URP's own helper —
                // it does the perspective divide, the UNITY_UV_STARTS_AT_TOP flip and the NDC-to-UV
                // 0.5 scale that URP's TAA resolve expects, so the sign and scale come from there
                // rather than being reimplemented here. RG holds the UV-space velocity; BA is unused,
                // matching CameraMotionVectors/ObjectMotionVectors' own RG-only ColorMask.
                float2 motion = CalcNdcMotionVectorFromCsPositions(IN.positionCSNoJitter, IN.previousPositionCSNoJitter);
                return half4(motion, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
