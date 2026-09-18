#ifndef BUGWAR_WIND_INCLUDED
#define BUGWAR_WIND_INCLUDED

// The one vertex bend every BugWar wind shader must share. GrassWind's five passes (forward,
// shadow, depth, depth-normals, motion vectors) all call BugWarWindBend with the same arguments
// for the same vertex, which is exactly what keeps a blade's shadow, its depth and its motion
// vector locked to the blade the forward pass actually draws — a pass that bends the position its
// own way, even slightly, detaches its output from what is on screen.
//
// The four globals below are published every frame by WindDirector
// (Scripts/Unity/Environment/WindDirector.cs) and nothing else may write them. They are declared
// outside any CBUFFER_START(UnityPerMaterial) block on purpose: a global inside the per-material
// buffer stops the SRP batcher from batching, since every batched material would then need to carry
// its own copy of the wind instead of sharing one set of globals across all of them.
float4 _BugWarWindDir;      // xyz = normalised world wind direction (y is 0), w = angle (radians) from +Z
float  _BugWarWindStrength; // 0..2, gust-modulated strength at the world origin
float4 _BugWarWindGust;     // x wavelength (m), y speed (m/s), z amplitude, w sharpness
float  _BugWarWindTime;     // wind clock in seconds, independent of _Time so it can be paused

// The two numbers that decide how hard grass moves, both as a fraction of a blade's own height at
// _MBAmplitude 1. They are here rather than in the material because they are the *shape* of the
// motion, shared by every wind material; _MBAmplitude is the per-material multiplier on top.
//
// LEAN is multiplied by the wind strength (0.35 at rest, up to 0.9 in a gust with the shipped
// WindDirector defaults) so at _MBAmplitude 1.5 a blade holds a lean of roughly 5% of its height
// in still air and 13% at the peak of a gust — a bend of a few degrees, which is what long grass
// actually does. Raising this past about 0.25 makes the field look like it is underwater.
#define BUGWAR_WIND_LEAN 0.10

// SWAY is signed and rides on top of the lean, so a blade breathes either side of where the wind
// is holding it. Keep it well under LEAN or the sway reads as a twitch rather than as slack.
#define BUGWAR_WIND_SWAY 0.03

/// Bends a vertex around its own root. `heightFraction` is 0 at the root and 1 at the tip; the
/// bend is quadratic in it, because a blade hinges at the base rather than leaning as a stick.
float3 BugWarWindBend(float3 positionWS, float3 rootWS, float heightFraction, float windTime)
{
    float2 dir = _BugWarWindDir.xz;

    // _BugWarWindStrength is WindDirector's WindField.Sample(clock, 0, 0).Strength: a base strength
    // plus the gust wave evaluated at the world origin (WindField.cs — Sample returns
    // baseStrength + gust, and WindDirector.Update always samples x = z = 0 before publishing).
    // Re-deriving the gust at this blade's own root and adding it straight on top of the published
    // strength would double-count: the origin's gust is already folded into _BugWarWindStrength, so
    // that would stack the origin's gust and the blade's gust instead of replacing one with the
    // other. The fix: back the origin's own gust out of the published strength first — recovering
    // the plain base strength WindField held before it added its gust term — then add the gust
    // freshly evaluated at the blade's own root on top of that base.
    float originPhase = (-windTime * _BugWarWindGust.y) / _BugWarWindGust.x;
    float originWave = 0.5 + 0.5 * sin(originPhase * 6.2831853);
    float originGust = pow(originWave, _BugWarWindGust.w) * _BugWarWindGust.z;
    float baseStrength = _BugWarWindStrength - originGust;

    float along = dot(rootWS.xz, dir);
    float phase = (along - windTime * _BugWarWindGust.y) / _BugWarWindGust.x;
    float wave = 0.5 + 0.5 * sin(phase * 6.2831853);
    float gust = pow(wave, _BugWarWindGust.w) * _BugWarWindGust.z;
    float strength = baseStrength + gust;

    // Per-blade jitter so a patch does not move as one welded sheet.
    float jitter = frac(sin(dot(rootWS.xz, float2(12.9898, 78.233))) * 43758.5453);

    // Signed, so a blade sways either side of the lean the wind is holding it at. An unsigned
    // term added to the lean makes the blade lurch out and snap back instead of breathing.
    float sway = sin((windTime + jitter * 6.2831853) * _MBFrequency) * BUGWAR_WIND_SWAY;

    // How far the tip travels, as a fraction of the blade's own height rather than in metres.
    // Metres cannot work here: one material drives hand-built blades authored at their full
    // sandbox height and another drives imported grass authored at 40 cm and scaled up ~17x, so
    // any absolute displacement is violent on one and invisible on the other.
    float lean = strength * BUGWAR_WIND_LEAN + sway;

    // The blade's height in world units: its authored height times whatever the instance is
    // scaled by. Taking the scale off the transform rather than trusting a material constant is
    // what makes this survive a prefab someone scaled by hand.
    float scaleY = length(GetObjectToWorldMatrix()._m01_m11_m21);
    float bladeHeight = max(0.001, _MBMaxHeight * scaleY);

    float bend = lean * _MBAmplitude * bladeHeight * heightFraction * heightFraction;
    positionWS.xz += dir * bend;
    // Keep the blade's length: a bend that only pushes sideways stretches it.
    positionWS.y -= bend * bend * 0.5 / bladeHeight;
    return positionWS;
}

#endif
