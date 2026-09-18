# Vegetation

Scatters plants and props over ground in the editor, and bakes the result into the scene.

## The pieces

| Piece | Where | What it is |
| --- | --- | --- |
| Layout (pure) | `Assets/Game/Scripts/World/Vegetation/Layout/` | Engine-free placement maths: `ScatterLayout`, `PatchLayout`, `VegetationLayout`, `SpatialHash`, `ValueNoise`, `FloatRange` |
| Authored data | `VegetationLayerAsset`, `VegetationItem` | One layer of plants — prefabs, weights and ranges — as a ScriptableObject |
| Field | `VegetationField` | A box of ground with a seed and a list of layers. It *plans* only: it raycasts the ground and says what stands where |
| Bake | `Assets/Game/Editor/World/VegetationBaker.cs` | Puts the plan in the scene under a `Vegetation` holder the bake owns and clears |

Assembly: `SpaceGame.Vegetation`. The layout half has no Unity types in it, so it is tested in
EditMode with no scene open (`Assets/Game/Tests/EditMode/VegetationLayoutTests.cs`).

## The two distributions

- **Scatter** — darts thrown across the box, each rejected if it lands within `minDistance` of one
  already placed. For landmarks: trees, rock formations, stumps, boulders. The count is a ceiling;
  the gap is what shapes the result.
- **Patch** — a jittered lattice at `spacing`, kept wherever a fractal noise field is above a
  cutoff. Blobs of undergrowth with bare lanes between them. `coverage` is honoured as a quantile
  of the noise actually sampled, so it means what it says whatever the noise does.

Both are deterministic: a seed grows the same field every time, and each layer takes its seed from
the field's plus its index, so adding a layer does not reshuffle the ones already tuned.

## Using it

1. `Tools ▸ Eclipse ▸ World ▸ Create Starter Vegetation Layers` writes one layer per imported prop
   folder into `Assets/Game/ScriptableObjects/Vegetation/`.
2. Add a `VegetationField` to the scene, set its size, seed, ground mask and layers.
3. **Bake** on the field's inspector, or `Tools ▸ Eclipse ▸ World ▸ Bake Vegetation` for every field
   in the open scene. Baking again replaces the holder rather than doubling it; **Clear** empties it.

Ground fit: a ray from `rayHeight` above each spot, onto `groundMask`. Ground steeper than
`maxSlope` is left bare, and `alignToGround` blends between standing upright and following the
slope normal.

## Why it bakes instead of spawning

Baked plants are ordinary scene geometry: nothing is spawned at runtime and there is no runtime
state for a save file to remember. The field component itself holds only authoring numbers.

Layers are assets so the numbers are tuned in the inspector rather than in code
(`GDC-L1-CONTENT-0002`), and `VegetationLayerAsset.Problem()` refuses a layer with a missing prefab
before a bake rather than throwing halfway through one (`GDC-L1-CONTENT-0004`).

## The imported props

`Assets/ThirdParty/BugWarNature/` — trees, stumps, rock formations, ferns, mushrooms, ground cover,
and the Nicrom Low Poly Wind grass, flowers and rocks. Sizes were set from what each prefab actually
measures in Unity: a tree is 12 m, a rock formation 2.5 m, a stump 0.9 m, a fern 0.75 m, ground
cover 0.3 m and a mushroom 0.16 m, with every variant in a family scaled by the same factor so they
keep their sizes relative to one another. Colliders are size-tiered: a tree carries a trunk capsule
(the canopy is walk-through), rocks and stumps a mesh collider, small foliage none. The
`DistantTrees` prefabs are LOD backdrop trees and are not part of a starter layer.

The plant shaders (`BugWar/GrassWind`, `BugWar/TreeBillboard`) bend to **global** shader uniforms, so
a scene showing these plants needs one `VegetationWind` in it. Without one the uniforms are zero and
the gust wavelength among them is a divisor.

Every plant is a baked GameObject, so undergrowth spacings are metres apart, not centimetres: a
carpet at half-metre spacing is half a million objects over a 500 m island and a 1.6 GB scene. A
real carpet of grass belongs in the terrain's own detail layers, which draw instanced and cost
nothing in the scene file.

## The nature world

`Tools ▸ Eclipse ▸ World ▸ Build Nature World` writes
`Assets/Game/Scenes/World/NatureWorld.unity` from nothing: a 500 × 500 m island of generated terrain
(`TerrainShape`, same value noise as the layouts), three generated ground layers painted by slope and
height, a sea plane at 7 m, a sun, a wind driver, a camera on the shore, and every vegetation layer
planted above the beaches. It refuses to run while the open scene has unsaved changes, because it
replaces that scene.

The scene is an output, not a source. Hand edits to it are lost on the next build — tune the seed and
the constants in `NatureWorldBuilder`/`TerrainShape`, or the layer assets, instead.
