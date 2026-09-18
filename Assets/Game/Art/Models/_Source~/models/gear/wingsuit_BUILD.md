# Wingsuit — build record

The Wingsuit artifact's model: a back-worn harness with two sailcloth blades
that swing out from the shoulders and a tail fan that swings back from the
hips. Worn stowed; deployed mid-air by `WingsuitArtifact`.

## Reused, by path

| Object | From | Change |
|---|---|---|
| `Mesh_WingHarness_Spar` | `components/props/wing_harness.blend` | none — the model is built in the harness's own frame |
| `Mesh_WingBlade_Secondary` (×2, `_R` / `_L`) | `components/mechanical/wing_blade.blend` | re-pinned onto the shoulder bosses; the left is the right mirrored across the spine |
| `Mesh_WingBlade_TailFan` | `components/mechanical/wing_blade.blend` | re-pinned onto the tail boss |

No geometry is unique to this model. The ornithopter's blades are the whole
reason this reads as the same world's technology: sailcloth over a steel spine
with plywood battens and a brass root collar, exactly what the craft flies on.

## New component: `components/props/wing_harness.blend`

Created for this build and separate from the model because a back frame with
hinge bosses is reusable — a glider pack, a jump pack, a parachute rig all hang
off the same thing. Three variations, differing in structure:

- `Coll_WingHarness_Spar` — one dorsal spar, crossbar, two braces. **Used.**
- `Coll_WingHarness_Cage` — a welded tube cage with corner gussets. Built ahead.
- `Coll_WingHarness_Scrap` — rusty spar, ply crossbar, rope lashings and rope
  straps. Built ahead.

All three share the same three pivots (`SHOULDER_PIVOT`, `TAIL_PIVOT` in
`wing_harness.py`), so any of them takes the same blades unchanged.

## Frame and pivots

Harness frame: origin on the wearer's back between the shoulder blades, -Y
forward, +Z up, the wearer's right is **-X** (face -Y with +Z up and the right
hand is on -X). The FBX axis conversion mirrors X, so in Unity the `_R` blade
sits on +X — the wearer's right there as well. Every hinged part's origin is
its pin:

| Part | Pin (Blender) | Pin (Unity) | Folds about |
|---|---|---|---|
| right blade | (-0.21, 0.09, 0.26) | (0.21, 0.26, -0.09) | fore-aft axis — swings out sideways |
| left blade | (0.21, 0.09, 0.26) | (-0.21, 0.26, -0.09) | fore-aft axis |
| tail fan | (0.00, 0.07, -0.34) | (0.00, -0.34, -0.07) | across axis — swings back |

**Folded is the rest pose.** The blades hang down the back (tip direction
(-0.087, 0, -0.996) for the right: 85° from spread, the last 5° keeping the
tips outboard so the two never cross behind the spine) and the fan lies down
the legs. Spread is 85° about the fore-aft axis (opposite signs per side) and
45° about the across axis for the fan — the angles `WingsuitArtifact` rotates
through, applied to each part's own transform about its origin.

The blades are placed spread-first — tip out the wearer's side, chord fore-aft
with the trailing edge aft so the blade's built-in sweep bends the tip back,
cambered face up so the authored tip droop reads as a slight anhedral — and
then folded with the same rotation the game will undo. The left blade is a
mirrored copy of the placed right one, so camber and droop match side to side.

The ornithopter's `wing_blade.blend` predates the library's move and links its
palette from the old location; `rebind_palette_materials` in `wingsuit.py`
re-points the appended blades at the real palette and drops the dead library,
or the blades export black.

## Rig

`Arm_Wingsuit` with three root-level bones (`Bone_WingR`, `Bone_WingL`,
`Bone_TailFan`), head on each pin, tail along the folded part. Root-level so a
bone's rest frame is the model's own axes. Kept in the `.blend` for posing; the
export drops it (`keep_armature=False`) and ships the parts as plain objects on
their pins — the same shape `ArticulatedPart`-driven panels take, and what the
artifact rotates directly.

## Materials

All from the palette via the components; nothing added. The blades bring
`Mat_Fabric_Wing_Ochre/Beige`, steel, brass, rust and ply; the harness adds
`Mat_Fabric_Canvas_Faded/Sand`, `Mat_Neutral_Black_Matte`, `Mat_Fabric_Rope_Hemp`
and `Mat_Wood_Ply_Worn`.

## Scale

Real scale for the three-metre wearer: harness 0.52 × 0.49 × 0.67 m, blades
2.36 m, fan 1.43 m, ~3.8k tris in total. Worn at the prefab's own scale
(`holdSize 0`).

## Unity

`wingsuit_export.py` → `Assets/Game/Art/Models/Items/wingsuit.fbx` (static,
four objects, -Y forward onto Unity's +Z). Nested by
`Assets/Game/Editor/Items/WingsuitBuilder.cs`, which binds the three hinged
parts by name.

## Decided without asking

- Blade size: the secondary blade unscaled (2.36 m) on a 3 m body — a 4.9 m
  span. Deliberately generous; a wingsuit that reads from the third-person
  camera needs wings, not sleeves. Swap for `Mesh_WingBlade_Covert` (1.48 m)
  in `wingsuit.py` for something more suit-like.
- Harness variation: `Spar` ships. The other two are a one-line change.
- The tail fan folds down the legs rather than up the back because up the back
  is where the expedition backpack is.
