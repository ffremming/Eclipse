#ifndef SPACEGAME_LIGHT_PALETTE_INCLUDED
#define SPACEGAME_LIGHT_PALETTE_INCLUDED

// The single source of truth for what light looks like in this game.
//
// Every light effect — the orb, the swipe arc, the beam, and anything added later —
// resolves its colour through LightEnergyColour() rather than through its own tint
// property. That is deliberate: a per-material colour is how a family of effects
// drifts apart over a production, one inspector tweak at a time. Here an effect
// chooses how much ENERGY it has at a given fragment, and the palette decides what
// that energy looks like. Retuning the game's light is then one edit to this file.
//
// Why these three colours, and why it does not read as fire:
//
// Fire is a blackbody radiator. It runs deep red, orange, yellow, white as it gets
// hotter, and it never leaves that curve — there is no cyan anywhere in a flame.
// So a cyan-turquoise fringe is the strongest available signal for "this is not
// combustion". The ramp below spends its low end on turquoise for exactly that
// reason: the thin, low-density edge of every effect fringes cyan, which the eye
// reads as light dispersing through a prism rather than as something burning.
//
// The orange mid-band is what keeps it warm and readable against a dark world
// instead of looking like generic sci-fi plasma. The white core is where the
// energy is dense enough to blow out, and it is pushed above 1.0 so bloom picks
// it up as the actual light source.

// Anchor colours of the ramp, low energy to high. Above 1.0 on purpose: these are
// HDR values feeding an additive blend and a bloom pass, not surface albedo.
#define LIGHT_COLOUR_FRINGE float3(0.16, 0.92, 0.88)   // turquoise — the anti-fire cue
#define LIGHT_COLOUR_BODY   float3(1.00, 0.46, 0.10)   // orange — the warm mass
#define LIGHT_COLOUR_CORE   float3(1.60, 1.35, 1.05)   // blown white — the source

// Where along the 0..1 energy axis the ramp crosses from fringe to body, and from
// body to core. The body band is deliberately narrow: a wide orange band starts to
// look like flame no matter what the endpoints are.
#define LIGHT_RAMP_FRINGE_END 0.34
#define LIGHT_RAMP_CORE_START 0.72

/// Energy density in 0..1 to the HDR colour that density emits.
///
/// The two segments are smoothstepped rather than lerped so the bands read as
/// distinct zones with soft seams, instead of one continuous gradient that muddies
/// into brown where orange meets cyan.
float3 LightEnergyColour(float energy)
{
    energy = saturate(energy);

    float toBody = smoothstep(0.0, LIGHT_RAMP_FRINGE_END, energy);
    float toCore = smoothstep(LIGHT_RAMP_CORE_START, 1.0, energy);

    float3 c = lerp(LIGHT_COLOUR_FRINGE, LIGHT_COLOUR_BODY, toBody);
    return lerp(c, LIGHT_COLOUR_CORE, toCore);
}

/// The same ramp, premultiplied by the energy itself.
///
/// Additive blending wants a fragment with no energy to contribute nothing. Returning
/// the raw ramp colour at energy 0 would paint full-strength turquoise over the whole
/// quad, which is where a "glowing box" artefact comes from. Effects should call this
/// unless they have a reason to separate hue from brightness.
float3 LightEmission(float energy, float intensity)
{
    energy = saturate(energy);
    return LightEnergyColour(energy) * energy * intensity;
}

// --- Detail ------------------------------------------------------------------
//
// Fire detail curls and rises. Light detail does neither: it travels along the
// direction the energy is already going, in thin straight filaments, and it does
// not loop back on itself. The helpers below are shaped for that.

/// Cheap hash, 3D in, 0..1 out. No texture dependency so an effect can be dropped
/// onto any mesh without a noise atlas travelling with it.
float LightHash(float3 p)
{
    p = frac(p * float3(127.1, 311.7, 74.7));
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

/// Value noise over the hash, trilinear.
float LightNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = LightHash(i + float3(0, 0, 0));
    float n100 = LightHash(i + float3(1, 0, 0));
    float n010 = LightHash(i + float3(0, 1, 0));
    float n110 = LightHash(i + float3(1, 1, 0));
    float n001 = LightHash(i + float3(0, 0, 1));
    float n101 = LightHash(i + float3(1, 0, 1));
    float n011 = LightHash(i + float3(0, 1, 1));
    float n111 = LightHash(i + float3(1, 1, 1));

    float x00 = lerp(n000, n100, f.x);
    float x10 = lerp(n010, n110, f.x);
    float x01 = lerp(n001, n101, f.x);
    float x11 = lerp(n011, n111, f.x);

    return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
}

/// Thin bright filaments running along one axis of the sample space.
///
/// The sample position is scaled anisotropically before the noise lookup — hard
/// stretch along the travel direction, tight across it. That is what turns a blob
/// of noise into streaks. Taking 1 - abs(2n - 1) and raising it to a power keeps
/// only the ridges, so the result is mostly dark with sparse bright lines, which is
/// what reads as filamentary rather than cloudy.
float LightFilaments(float3 samplePos, float travelStretch, float sharpness)
{
    float3 p = samplePos * float3(1.0 / max(travelStretch, 1e-3), 1.0, 1.0);
    float n = LightNoise(p);
    float ridge = 1.0 - abs(2.0 * n - 1.0);
    return pow(saturate(ridge), sharpness);
}

// --- Shaping -----------------------------------------------------------------

/// Rim term. Zero facing the camera, one at the silhouette.
///
/// Used to put the turquoise fringe where the geometry thins out, which is what
/// makes a solid mesh read as a volume of light rather than a painted surface.
float LightRim(float3 normalWS, float3 viewDirWS, float power)
{
    return pow(1.0 - saturate(dot(normalize(normalWS), normalize(viewDirWS))), power);
}

/// Soft-particle fade against the depth buffer.
///
/// Without it, every one of these effects cuts a hard line where it intersects
/// terrain, which on a sea stack happens constantly. Takes the raw scene depth and
/// the fragment's own eye depth so the caller controls how it sampled the buffer.
float LightDepthFade(float sceneEyeDepth, float fragmentEyeDepth, float fadeDistance)
{
    return saturate((sceneEyeDepth - fragmentEyeDepth) / max(fadeDistance, 1e-4));
}

/// Pulse in 0..1 with a fast attack and a slow decay.
///
/// A symmetric sine reads as "throbbing", which is a fire behaviour. Light that is
/// being CAST should snap on and ease off, so the asymmetry here is the point.
float LightPulse(float phase, float attack)
{
    float t = frac(phase);
    float rise = saturate(t / max(attack, 1e-4));
    float fall = saturate((1.0 - t) / max(1.0 - attack, 1e-4));
    return min(rise, fall);
}

// --- Slash spectrum ------------------------------------------------------------
//
// The swing trail is the one light effect that is NOT an energy ramp. The ramp above is one
// substance getting denser; a struck slash is the opposite — it wants to look like light being
// split, with each colour a separate layer that lives and dies on its own. So it is four discrete
// hues, laid across the ribbon from the hilt side to the tip side, and LightSlashBands() says how
// much of each is present at a given position across the blade.
//
// Blue and red are deliberately saturated and far apart. Between them white and orange sit as
// the warm centre, so the ribbon reads as a spectrum rather than as one colour with a fringe.
// HDR for the same reason as the ramp: additive blend into bloom.
#define LIGHT_SLASH_BLUE   float3(0.15, 0.45, 2.00)
#define LIGHT_SLASH_WHITE  float3(1.80, 1.75, 2.00)
#define LIGHT_SLASH_ORANGE float3(2.20, 0.85, 0.12)
#define LIGHT_SLASH_RED    float3(2.00, 0.07, 0.12)

/// Weight of each slash hue at position v across the ribbon (0 hilt side, 1 tip side), as
/// (blue, white, orange, red). Overlapping soft tents, so neighbours blend into each other and
/// blue never has to blend with red directly, which is where a muddy purple would come from.
float4 LightSlashBands(float v, float width)
{
    const float4 centres = float4(0.12, 0.38, 0.62, 0.88);
    return smoothstep(0.0, 1.0, saturate(1.0 - abs(v - centres) / max(width, 1e-3)));
}

/// The four band weights to the colour they emit, before intensity.
float3 LightSlashColour(float4 weights)
{
    return weights.x * LIGHT_SLASH_BLUE
         + weights.y * LIGHT_SLASH_WHITE
         + weights.z * LIGHT_SLASH_ORANGE
         + weights.w * LIGHT_SLASH_RED;
}

#endif // SPACEGAME_LIGHT_PALETTE_INCLUDED
