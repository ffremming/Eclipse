#ifndef BUGWAR_PASSES_INCLUDED
#define BUGWAR_PASSES_INCLUDED

// The parts of a URP shadow pass that never differ between BugWar shaders. Chitin, GardenTerrain
// and GrassWind all need the same normal-biased, near-plane-clamped clip position; only what they
// do to positionWS beforehand (grass sways, the others do not) and what their fragment stage clips
// against differ, so those stay in the shaders and only this arrives from here.
//
// Include it after URP's Core.hlsl. It pulls in Shadows.hlsl itself for ApplyShadowBias.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Set by URP's shadow drawing: a direction for the sun, a position for a punctual light.
float3 _LightDirection;
float3 _LightPosition;

/// Clip-space position for a shadow caster vertex already in world space. The bias pushes the
/// surface along its normal so it does not shadow itself, and the clamp keeps geometry behind the
/// shadow camera's near plane from being clipped away — a caster that vanishes there leaves a hole
/// in the shadow map rather than a missing shadow.
float4 BugWarShadowPositionCS(float3 positionWS, float3 normalWS)
{
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

#endif
