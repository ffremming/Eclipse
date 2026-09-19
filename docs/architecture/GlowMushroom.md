# Glow mushrooms

The BugWar mushrooms, grown to 3–8 times their pack size, glowing in one of three colours and lighting
the ground and whatever stands near them, and breakable: one hit destroys one and it bursts into 5 light
orbs. Light is health — the player's
health is counted in orbs and the lantern is the health bar — so the mushrooms are a finite source
of healing scattered over the island.

## The pieces

| Piece | Where | What it is |
| --- | --- | --- |
| `GlowMushroomBuilder` | `Editor/AssetPipeline/` | `Tools ▸ Eclipse ▸ World ▸ Build Glow Mushrooms` |
| The variants | `Prefabs/Environment/GlowMushroom/` | One prefab variant of each of the pack's 18 mushroom prefabs |
| The glow materials | `Art/Materials/Environment/GlowMushroom_*.mat` | The pack's material with emission on, one per colour |
| The layer | `ScriptableObjects/Vegetation/Mushrooms.asset` | Plants the variants, in groves, at 3–8× |
| `OrbBurstOnDeath`, `OrbBurst` | `Scripts/Gameplay/Light/` | Throws the orbs when a mushroom's health runs out |
| `ScaledLight` | same | Sets the mushroom's light range from the scale it is planted at |
| `LightOrb` | same | **Not part of this feature.** The pickup itself: falls, rests, heals the player on touch |

A hit needs nothing of its own: a swing ends in `Damage.Apply`, which finds the mushroom's
`HealthComponent`. It has 10 health and a bare-handed hit does 12, so one hit kills it.

The variants leave the pack alone: each stores only its collider, health, burst and material override.
The pack's prefabs carried no collider, which is why the plain ones could not be hit at all.

## Colours

Each variant's glow follows its pack colour — Brown glows amber, Purple violet, Red crimson. The
emission is the cap's own texture times that colour, so the markings stay. The colour is cosmetic:
every mushroom gives the same 5 light, so the player cannot read a difference in value from it.

## Light

Each variant carries a shadowless point light in its glow colour, at the middle of its body. The world
has no sun, so these are a good part of what there is to see by: each mushroom throws a coloured pool
on the ground and the pools blend where groves overlap. The reach follows the size the mushroom is
planted at — `ScaledLight` sets the range to `rangePerScale` times the scale, so a 3× mushroom lights
6.6 m and an 8× one 17.6 m from the one prefab. A light's own range ignores the transform's scale.

Shadowless because a shadow map per mushroom is not affordable. The PC renderer is Forward+, which
handles this many lights per pixel; at 150 mushrooms only the few near the camera cost anything. If a
place proves too heavy, thin the groves before touching the lights.

## Planting

The layer is `Cluster` mode: 30 groves of 5, a grove about 9 m across. That is 150 mushrooms and 750
light, some twenty-five full lanterns. The pack's own layer planted 3,167 in patches, which at 5 light
each would have been about 16,000. Mushrooms do not grow back, so the count is the total supply and
nothing refills it (`GDC-L1-SYS-0008`, `GDC-L1-ECON-0002`, `GDC-L1-PROG-0003`).

To plant them, **Bake Vegetation** on the scene's field: the layer keeps its asset, so a field that
already lists `Mushrooms` needs no rewiring.

## What to tune, and where

- **How much light the island holds**: `GroveCount`, `GroveRadius`, `MushroomsPerGrove` in the builder,
  then run it again. The layer is rewritten from those constants.
- **Light per mushroom**: `OrbsPerMushroom` in the builder; **what each orb is worth** is `lightValue`
  on the orb prefab.
- **How hard the caps glow**: `EmissionIntensity` in the builder, or the emission colour on the
  material. The materials are made once and left alone, so a glow tuned by eye survives a rebuild.
- **How much they light their surroundings**: `LightIntensity` and `LightRangePerScale` in the builder,
  then run it again; or the `Glow` light and its `ScaledLight` on a variant, to tune by eye.

`VegetationStarterLayers` no longer writes the mushroom layer; it would put the plain prefabs back.
