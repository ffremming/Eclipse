# human_sculpt_base — build record

A male human base mesh imported so it can be sculpted by hand. It is a starting point, not a game-ready
asset: no rig, no UVs, no materials.

## Source

The sculpt was imported from a downloaded base mesh, `FinalBaseMesh.obj` (dated 2014-08-21). No
licence text came with it, so on 2026-09-20 both the imported `human_sculpt_base_source.obj` and the
archive it came out of were **removed from the repository** and kept outside it, in
`../Eclipse-asset-downloads/`. Settle the licence before shipping anything derived from the sculpt.

`human_sculpt_base.blend` is the source of truth now; `human_sculpt_base.py` was the one-time import
and cannot re-run without the OBJ, which is deliberate.

The OBJ: 24,461 verts, 24,459 quads, no triangles or n-gons, closed, symmetric in X, no UVs, no `.mtl`.

## What the import changed

| | Source OBJ | `.blend` |
| --- | --- | --- |
| Up axis | Y | Z (baked into the mesh, object rotation is 0) |
| Height | 20.68 units | 1.8 m |
| Feet | Y = -0.057 | Z = 0 |
| Origin | world origin | world origin, between the feet |
| Materials | `default` (importer invented it) | none |

The scale is uniform. The figure faces -Y, matching the library convention.

## Decomposition

None — one mesh, `Mesh_HumanSculptBase` in `Coll_HumanSculptBase`. There is nothing here that could
plausibly be reused on another model, and the point of the file is to be sculpted as a single surface.

- **Components reused:** none.
- **New components:** none.
- **Palette materials:** none. A sculpt base carries no material of its own; add palette materials when
  it becomes a model.
- **Armature:** none, because nothing moves yet. Rig it once the sculpt is settled — rigging a base
  that is about to change shape means redoing the weights.

## human_sculpt_base_clean.blend — repaired and unwrapped copy

`human_sculpt_base_clean.blend` is a copy of the sculpt as it stood on 2026-09-19, made by
`human_sculpt_base_clean.py`. `human_sculpt_base.blend` was not touched. The copy faces +Y, as the sculpt
does (the library convention is -Y).

**Repair.** The centre line of the sculpt had zero-thickness walls and doubled vertices lying on the
mirror plane: a razor-thin fold at the throat and a hairline slit down the spine, 47 non-manifold edges,
a strip of faces flagged sharp along the throat and custom normals that made the seam show as a crease. The script deletes the walls, welds the mirrored halves,
fills the one hole that leaves at the top of the throat, relaxes three throat vertices that stood 2-4 mm
proud, joins triangle pairs into quads, recomputes normals and smooths every face. Result: closed and
manifold, 24,830 verts, volume unchanged to 0.02 %, triangles 20 → 10. Sculpt mask and face sets kept.

Ten triangles remain: two at the mouth corners and eight in a cluster over the sternum. Removing them
means retopologising the patch, which would move sculpted geometry, so they were left.

**UVs.** One `UVMap`, 12 islands, no overlap, no flipped faces, all inside 0-1:
head (face), back of skull, torso, pelvis, and left/right arm, hand, leg, foot. Seams run along the part
borders, plus one hidden slit per part: down the back midline for torso and pelvis, along the side facing
the body for arms and legs, and along each finger and toe. Texel density is even to within 0.5-2x on 93 %
of faces; toe tips and the hand-back are the loosest. The seams are stored on the mesh, so Blender's
Unwrap (Angle Based) reproduces the layout. `human_sculpt_base_clean_uv_layout.png` is the layout as a
starting template.

The eyes (`Sphere`, `Sphere.001`) are unchanged. The mesh still uses the sculpt's own `Material` slot
rather than a palette material.

## human_sculpt_base_rigged.blend — the player body

`human_sculpt_base_rigged.blend` is `human_sculpt_base_clean.blend` (as saved with its painted texture)
plus a skeleton, made by `human_sculpt_base_rigged.py`. It exists so the player can wear this model with
the Goblin's Humanoid animations. The mesh is not touched (the script asserts no vertex moves); what is
added is the armature `Human_Rig`, skin weights, and a bone parent on each eye.

- **Skeleton.** 52 bones: a ground-level `Root` (not a Humanoid bone; the game measures "the body stands
  on its own pivot" from the skinned mesh's root bone, as it did from the goblin's `Root_Jnt`), then
  `Hips`, `Spine`, `Chest`, `Neck`, `Head`, both arms with all 15 finger bones, both legs with `Toes`.
  Names are Unity's `HumanBodyBones` names, so the Humanoid mapping needs no guessing. Joint positions
  were read off the mesh (arm, leg, hand and foot cross-sections) rather than guessed.
- **Weights.** Blender's automatic weights, limited to four influences a vertex, all 24,830 vertices fully
  weighted. Judged by posing: fists, bent knees, a turned head and a squat hold up; arms straight overhead
  pinch the shoulder slightly.
- **Eyes.** `Sphere` and `Sphere.001` are parented to the `Head` bone, unskinned.
- **Facing.** The sculpt faces +Y and the library convention is -Y, so the rig object is turned 180 degrees
  about Z (mesh and eyes follow as children; their data is untouched). An FBX cannot choose: its axis flags
  only declare the file's axes, measured by exporting all six settings, so the facing has to be right in
  Blender for the character to arrive facing +Z with its right hand on +X.
- **Export.** `human_sculpt_base_rigged_export.py` writes `Assets/Game/Art/Models/Characters/Human/Human.fbx`
  and `Textures/Human_BaseColor.png`. It uses `FBX_SCALE_ALL`, not `_exportlib`'s `FBX_SCALE_NONE`: that gives
  every bone a scale of 100 in Unity, so anything parented to a hand inherits it (measured on this model).
- **In Unity.** Human avatar, generic import settings live in `Human.fbx.meta`. Unity's automatic mapping
  picked `Root` as the Hips and skipped `Chest`, so both are pinned by hand there. The mapping is
  incomplete only for `Jaw` and `UpperChest`, which the model has no bones for.

## Sculpting notes

24k verts is enough for broad forms and proportion changes. For fine detail add a Multires modifier
(Subdivide, not Simple) rather than sculpting the base directly, so the base stays clean for rigging.
The mesh is all quads with edge loops that follow the muscles, so keep the base topology intact where
you can.
