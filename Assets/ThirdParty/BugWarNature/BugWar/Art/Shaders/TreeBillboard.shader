Shader "BugWar/TreeBillboard"
{
    // The far tree ring past SandboxTreeLayer.ImposterDistance: a single camera-facing, Y-axis-only
    // quad per tree, sampling one of the twenty-four angles (three trees times eight yaws)
    // SandboxTreeImposters bakes into TreeImposterTexture. One shared, GPU-instanced material
    // serves the whole ring — see TreeBillboardInput.hlsl for how _TreeIndex, the one thing that
    // differs per tree, rides the instancing buffer instead of breaking that batching.
    //
    // The normal atlas is a flat placeholder, not a true per-angle bake: URP's SRP does not honour
    // Camera.RenderWithShader, the usual way to swap every renderer to a normal-visualisation
    // material for a second render pass, so capturing real geometry normals would need a Renderer
    // Feature or a hand-rolled replacement shader — a lot of new surface for three trees. Every
    // pass below reads the card's own face direction as its normal instead, which is exactly what
    // the atlas's uniformly flat content encodes, so the two cannot disagree with each other.
    Properties
    {
        [Header(Atlas)]
        _TreeAtlas       ("Colour Atlas (RGB colour, A coverage)", 2D) = "white" {}
        _TreeAtlasNormal ("Normal Atlas (flat placeholder, see file header)", 2D) = "bump" {}
        _TreeIndex       ("Atlas Row (set per instance, not by hand)", Float) = 0

        [Header(Shading)]
        _Cutoff       ("Alpha Cutoff", Range(0, 1)) = 0.4
        _LightWrap    ("Light Wrap", Range(0, 1)) = 0.4
        _AmbientBoost ("Ambient Boost", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "AlphaTest"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Cull Off: the quad is built to always face the camera, so a back face should never
            // be visible, but a billboard that occasionally shows one from a bad angle is a hole
            // in the ring rather than a lit backside, which is worse.
            Cull Off

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
            // Set automatically by the LODGroup while a tree is crossfading between its mesh LOD
            // and this billboard — see SandboxTreeImposters.ApplyLodGroups.
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #include "TreeBillboardInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float3 positionWS    : TEXCOORD0;
                float2 uv            : TEXCOORD1;
                float3 cardRightWS   : TEXCOORD2;
                float3 cardForwardWS : TEXCOORD3;
                float  fogFactor     : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                TreeBillboardVertex card = ComputeTreeBillboardVertex(IN.positionOS.xyz, IN.uv);

                OUT.positionWS = card.positionWS;
                OUT.positionCS = TransformWorldToHClip(card.positionWS);
                OUT.uv = card.uv;
                OUT.cardRightWS = card.cardRightWS;
                OUT.cardForwardWS = card.cardForwardWS;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 atlasSample = SAMPLE_TEXTURE2D(_TreeAtlas, sampler_TreeAtlas, IN.uv);
                clip(atlasSample.a - _Cutoff);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(IN.positionCS);
            #endif

                // The atlas normal defaults to flat (0, 0, 1) — see the file header — which makes
                // this exactly the card's own face direction, the same fallback DepthNormals uses.
                half3 packedNormal = UnpackNormal(SAMPLE_TEXTURE2D(_TreeAtlasNormal, sampler_TreeAtlasNormal, IN.uv));
                float3 N = normalize(packedNormal.x * IN.cardRightWS + packedNormal.y * float3(0, 1, 0)
                                    + packedNormal.z * IN.cardForwardWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float wrap = _LightWrap;
                float NdotL = saturate((dot(N, mainLight.direction) + wrap) / (1.0 + wrap));
                float3 lit = mainLight.color * NdotL * mainLight.shadowAttenuation;

            #ifdef _ADDITIONAL_LIGHTS
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                uint addCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(addCount)
                    Light addLight = GetAdditionalLight(lightIndex, IN.positionWS);
                    float addNdotL = saturate((dot(N, addLight.direction) + wrap) / (1.0 + wrap));
                    lit += addLight.color * addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation;
                LIGHT_LOOP_END
            #endif

                float3 ambient = SampleSH(N) + _AmbientBoost.xxx;
                float3 color = atlasSample.rgb * (lit + ambient);

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
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BugWarPasses.hlsl"
            #include "TreeBillboardInput.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V ShadowVert(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                TreeBillboardVertex card = ComputeTreeBillboardVertex(IN.positionOS.xyz, IN.uv);
                OUT.positionCS = BugWarShadowPositionCS(card.positionWS, card.normalWS);
                OUT.uv = card.uv;
                return OUT;
            }

            half4 ShadowFrag(V IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_TreeAtlas, sampler_TreeAtlas, IN.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TreeBillboardInput.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V DepthVert(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                TreeBillboardVertex card = ComputeTreeBillboardVertex(IN.positionOS.xyz, IN.uv);
                OUT.positionCS = TransformWorldToHClip(card.positionWS);
                OUT.uv = card.uv;
                return OUT;
            }

            half4 DepthFrag(V IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_TreeAtlas, sampler_TreeAtlas, IN.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "TreeBillboardInput.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            V DepthNormalsVert(A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                TreeBillboardVertex card = ComputeTreeBillboardVertex(IN.positionOS.xyz, IN.uv);
                OUT.positionCS = TransformWorldToHClip(card.positionWS);
                OUT.normalWS = card.normalWS;
                OUT.uv = card.uv;
                return OUT;
            }

            half4 DepthNormalsFrag(V IN) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_TreeAtlas, sampler_TreeAtlas, IN.uv).a;
                clip(alpha - _Cutoff);
                return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
