// Height fog as a full-screen pass, run by URP's Full Screen Pass Renderer Feature after the
// skybox and before transparents. Reads the depth texture, rebuilds the world position, and
// integrates an exponential-with-height density along the eye ray — analytically, so it costs
// one texture read per pixel. The sky itself is left alone (its horizon haze is painted into the
// skybox in the same colour), which is what makes the two agree at the far clip.
//
// Every parameter is a shader global written by WorldAtmosphere, so the one scene component is
// the single place fog is tuned and this material carries nothing.
Shader "SpaceGame/HeightFog"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "HeightFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _AirFogColor;      // rgb
            float  _AirFogDensity;    // extinction per metre at the base height
            float  _AirFogBaseHeight; // metres; density is _AirFogDensity here
            float  _AirFogFalloff;    // 1 / scale height, per metre
            float  _AirFogStart;      // metres of clear air in front of the eye

            // Optical depth of an exp(-(y - base) * k) density along a segment of length d that
            // climbs dy.
            //
            // The closed form is atEye * d * (1 - exp(-dy k)) / (dy k). That quotient is a
            // removable singularity at dy k = 0 — the level-flight case, where the answer is just
            // atEye * d — and the guard has to be on dy k itself, not on the slope: a nearly level
            // ray over a long distance has a small slope but a perfectly well-behaved dy k, and
            // guarding on the slope put a hard seam straight across the view at eye level.
            // Below the threshold the first two terms of the series are used, which meets the
            // quotient smoothly instead of stepping to 1.
            float opticalDepth(float eyeY, float dy, float d)
            {
                float k = max(_AirFogFalloff, 1e-5);
                float atEye = _AirFogDensity * exp(-(eyeY - _AirFogBaseHeight) * k);
                float x = dy * k;
                float ratio = abs(x) < 1e-3 ? 1.0 - 0.5 * x : (1.0 - exp(-x)) / x;
                return atEye * d * ratio;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv    = IN.texcoord;
                half4  scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0001) return scene;   // sky: painted by the skybox itself
                #else
                    if (rawDepth >= 0.9999) return scene;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 eye  = _WorldSpaceCameraPos;
                float3 ray  = positionWS - eye;
                float  dist = length(ray);
                float  d    = max(dist - _AirFogStart, 0.0);
                float  dy   = ray.y * (d / max(dist, 1e-3));
                float  eyeY = eye.y + ray.y * (_AirFogStart / max(dist, 1e-3));

                float transmittance = exp(-opticalDepth(eyeY, dy, d));
                return half4(lerp(_AirFogColor.rgb, scene.rgb, transmittance), scene.a);
            }
            ENDHLSL
        }
    }
}
