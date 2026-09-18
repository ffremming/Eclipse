#ifndef BUGWAR_GRASS_WIND_INPUT_INCLUDED
#define BUGWAR_GRASS_WIND_INPUT_INCLUDED

// Shared by every pass of GrassWind.shader. Field order matches the Properties block so a diff
// against it is easy to eyeball, and the block stays byte-identical across passes, which is what
// lets the SRP batcher take a field of these without falling back to per-draw uniforms.
//
// LowPolyWindAssets builds these materials as copies of the vendor Low Poly Wind material via
// Material.CopyPropertiesFromMaterial, which matches by property name: _Color, _MBAmplitude,
// _MBFrequency, _MBMaxHeight, _NoiseTextureTilling and _NoisePannerSpeed keep the vendor's own
// names for exactly that reason. _NoiseTexture/_NoiseTextureTilling/_NoisePannerSpeed are accepted
// here purely so the copy has somewhere to land without Unity warning about a missing property;
// GrassWind never samples the noise texture itself — BugWarWind.hlsl's own gust field replaces it.

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    // Not a vendor property. The vendor shader's own _Cutoff is declared [HideInInspector] by its
    // ASE-generated Lit template rather than living in the visible Properties block, but every
    // imported grass/flower material still carries a serialized value (0.5) for it, so
    // CopyPropertiesFromMaterial still finds a match and this is not left at some other default.
    float  _Cutoff;
    float  _MBAmplitude;
    float  _MBFrequency;
    float  _MBMaxHeight;
    float4 _NoiseTextureTilling;
    float4 _NoisePannerSpeed;
    float4 _TransColor;
    float  _TransStrength;
    float  _TransPower;
    float  _TransAmbient;
    float  _TransShadow;
    float4 _TipColor;
    float  _TipBlend;
    float  _RootOcclusion;
CBUFFER_END

// Textures live outside UnityPerMaterial: a texture bound there breaks SRP batching.
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// Accepted, unused — see the block comment above. Declared so the vendor material's noise texture
// copies onto this shader without Unity complaining that the destination has no matching property.
TEXTURE2D(_NoiseTexture);
SAMPLER(sampler_NoiseTexture);

/// Alpha-tests a grass/flower card against its own cutout mask. Every fragment-stage pass that
/// draws this shader has to run it: a pass that skips it draws a blade's fully rectangular quad
/// into the shadow map, the depth buffer or the motion buffer instead of the cutout silhouette the
/// forward pass shows, which reads as a solid card ghosting behind or in front of the real blade.
void GrassAlphaClip(float2 uv)
{
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * _Color.a;
    clip(alpha - _Cutoff);
}

#endif
