#ifndef SPACEGAME_DARK_PALETTE_INCLUDED
#define SPACEGAME_DARK_PALETTE_INCLUDED

// The single source of truth for what darkness looks like in this game — the mirror of
// LightPalette.hlsl, and deliberately built the same way so the two families stay
// recognisably opposite rather than merely different.
//
// The light palette answers "how much energy is here, and what colour does that emit".
// This one answers "how much energy is here, and how much of what is behind it does
// that TAKE". So every effect built on it is multiplied into the frame rather than
// added to it: a fragment with no energy returns 1 and changes nothing, and a fragment
// at full energy returns the void colour and leaves almost nothing of the scene.
//
// Why these colours:
//
// The light ramp fringes turquoise to say "not fire". This one fringes violet for the
// same kind of reason — a shadow is grey, and the eye knows it. Dark that is tinted,
// and tinted towards the one hue the player's light never produces, does not read as an
// absence of light but as a substance with an opinion. The core keeps a little blue
// alive at the bottom for the same reason: a pure black core reads as a hole in the
// rendering, and a hole is a bug, not a threat.
//
// It eats warm light hardest, which is what makes it the player's enemy specifically:
// the torch, the lantern and every light weapon are white-orange, so a violet-black
// absorber takes exactly what the player brought and leaves the world colder.

// The multiply factors, low energy to high. At or below 1 everywhere on purpose: these
// scale the frame, they do not tint it.
#define DARK_COLOUR_FRINGE float3(0.82, 0.74, 0.95)   // violet — the anti-shadow cue
#define DARK_COLOUR_BODY   float3(0.28, 0.20, 0.42)   // bruise — the mass of it
#define DARK_COLOUR_VOID   float3(0.02, 0.01, 0.05)   // almost nothing, but not nothing

// Where along the 0..1 energy axis the ramp crosses from fringe to body, and from body
// to void. The body band is wide where the light ramp's is narrow: light gets its
// character from its hot core, dark from the murk on the way in.
#define DARK_RAMP_FRINGE_END 0.28
#define DARK_RAMP_VOID_START 0.86

/// Energy density in 0..1 to the factor the frame behind it is multiplied by.
///
/// The two segments are smoothstepped rather than lerped for the same reason the light
/// ramp's are: bands with soft seams read as zones of a substance, a single gradient
/// reads as a vignette.
float3 DarkAbsorptionColour(float energy)
{
    energy = saturate(energy);

    float toBody = smoothstep(0.0, DARK_RAMP_FRINGE_END, energy);
    float toVoid = smoothstep(DARK_RAMP_VOID_START, 1.0, energy);

    float3 c = lerp(float3(1.0, 1.0, 1.0), DARK_COLOUR_FRINGE, toBody);
    c = lerp(c, DARK_COLOUR_BODY, toBody * toBody);
    return lerp(c, DARK_COLOUR_VOID, toVoid);
}

/// The same ramp, scaled by how hard the effect is currently drinking.
///
/// Multiplicative blending wants a fragment with no absorption to contribute nothing,
/// and "nothing" for a multiply is 1, not 0 — which is the one place this file cannot
/// simply mirror the light palette's premultiply. An effect with `absorption` at 0 is
/// invisible however much energy it has.
float3 DarkAbsorption(float energy, float absorption)
{
    return lerp(float3(1.0, 1.0, 1.0),
                DarkAbsorptionColour(energy),
                saturate(energy) * saturate(absorption));
}

/// Soft-particle fade for a multiply: hand back 1 where the effect meets geometry.
///
/// `LightDepthFade` returns 0 at the intersection because an additive effect
/// disappears by contributing nothing. A multiplicative one disappears by contributing
/// one, so the fade has to be applied to the factor rather than to the colour.
float3 DarkDepthFade(float3 factor, float fade)
{
    return lerp(float3(1.0, 1.0, 1.0), factor, saturate(fade));
}

#endif // SPACEGAME_DARK_PALETTE_INCLUDED
