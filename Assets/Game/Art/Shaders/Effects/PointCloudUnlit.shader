// Draws a point-topology mesh as additive, vertex-coloured dots.
//
// The supernova remnant arrives from Sketchfab as a scanned point cloud: positions and
// vertex colours, no triangles, no UVs and no normals. Nothing in the URP shader set
// renders that — Lit and Unlit both expect surfaces — so this is the minimum that does:
// pass the vertex colour through, tint it, and add it to the frame.
//
// Point size is a hardware point size (PSIZE), which Metal and OpenGL honour and D3D
// ignores; on D3D the cloud falls back to single-pixel dots.
Shader "Eclipse/Effects/Point Cloud Unlit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
        _PointSize ("Point Size", Range(1, 16)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PointCloudUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float size : PSIZE;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Intensity;
                float _PointSize;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor * _Intensity;
                output.size = _PointSize;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return half4(input.color.rgb * input.color.a, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
