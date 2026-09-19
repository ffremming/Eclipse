# bear_sculpt_base — build record

A rigged, animated quadruped imported to be reshaped into something that is not a bear. It is a
starting point, not a game-ready asset: it is still recognisably the stock bear, and nothing in the
game references it.

Built 2026-09-19 by `bear_sculpt_base.py`, which is historical record. **The `.blend` is the source
of truth — never re-run the script over it.** It refuses to overwrite for that reason.

## Source

`bear_sculpt_base/bear_source.fbx` is `source/Bear Animated.fbx` from
`bear-animated-cel-shading-3d-model.zip`, a bear from a "Realistic Animals BIG PACK" (the FBX's
texture paths still point at `D:/William/UNITY/Realistic Animals PACK/...`, the machine that
authored it). Exported by Unity's FBX Exporter, FBX 7700.

**No licence text came with the download.** Check where it was downloaded from before shipping
anything derived from it — the same caveat as `human_sculpt_base`.

The FBX: one triangulated mesh, 3,769 verts / 7,508 tris, 3 material slots, 2 UV sets, a 35-bone
quadruped rig, and 81 animations.

### What the download did not ship

The FBX asks for five texture maps. The zip contains three, and only two of them are wanted:

| Map the FBX asks for | Shipped? |
| --- | --- |
| `Bear Body.png` | yes, as `Bear_Body_A.png` |
| `Bear Head.png` | yes, as `Bear_Head_A.png` |
| `Bear Teef.png` | **no** — the teeth slot takes a flat palette colour instead |
| `Bear Body N.png`, `Bear Head N.png` (normals) | **no** — the two body/head materials have no normal map |

`bear_sculpt_base/textures/Bear_Eye_A.png` also shipped and is **not wired to anything**: the mesh
has no eye slot, its eyes are painted into `Bear Head`. It is kept because it came with the set and
would be wanted if the eyes are ever split onto their own material.

## What the import changed

| | Source FBX | `.blend` |
| --- | --- | --- |
| Root empty | `Bear_Animated` | `Root_BearSculptBase`, scale 0.01 — kept, see below |
| Armature | `RigRoot`, 35 bones | `Arm_BearSculptBase`, same 35 bones, same names |
| Mesh | `sm_3_0_0`, object scale (84.75, 92.15, 84.75) | `Mesh_BearSculptBase`, uniform scale, world scale 1.0 |
| Leaf-bone tip markers | 10 stray `*_end` empties | gone (`ignore_leaf_bones`) |
| Materials | `Material_001` … `_003`, all maps broken | `Bear_Body`, `Bear_Head`, `Bear_Teeth` |
| Actions | 81, one assigned | 81, every one given a fake user |
| Collection | none | `Coll_BearSculptBase` |

Undeformed size 0.756 × 2.372 × 1.438 m — a bear on all fours, already at real-world scale. It
faces **−Y**, which is the library convention, so nothing was reoriented.

## Decomposition

None — one mesh, `Mesh_BearSculptBase` in `Coll_BearSculptBase`. Same reasoning as
`human_sculpt_base`: the point of the file is to be reshaped as a single skinned surface, and there
is nothing here that could plausibly be reused on another model. A leg or a jaw cut out of it would
be a bear's leg or a bear's jaw, and it would lose its skin weights on the way out.

- **Components reused:** none.
- **New components:** none.
- **Palette materials:** one value, not a link. `Bear_Teeth` takes the base colour and roughness of
  `Mat_Hide_Ivory_Spine` (`#E2D8C0`, roughness 0.38) — the palette's pale keratin, written for the
  Vrescal's teeth — read out of `palette.blend` at build time rather than hardcoded. It is copied
  rather than linked because `goblin.blend` and `mountain_dragon.blend` own their materials too: a
  link into `_Source~/palette.blend` does not resolve once a model is exported into Unity. The two
  textured slots are not palette materials at all, which is the same deviation those two creature
  imports make — the palette is flat hand-authored surfaces, not a place for a pack's albedo maps.
- **Armature:** kept exactly as shipped, including every bone name.

## The rig hierarchy is load-bearing

`Root_BearSculptBase` looks redundant — an empty at the origin whose 0.01 scale seems to duplicate a
rotation the armature already undoes. It is not.

**Every action animates the armature *object*, not only its pose bones.** Each of the 81 actions
drives `location`, `rotation_euler` (−90° about X) and `scale` (1.0) on `Arm_BearSculptBase`. The
armature's own transform therefore belongs to the animation and cannot carry anything else, so the
metre conversion has to sit above it — which is what the root empty is for.

Deleting that empty and writing `scale = 0.01` onto the armature instead looks correct, saves
without complaint, and then loses to the first frame evaluation: the creature comes back 100× too
large and lying on its side. That was built and caught here by comparing deformed geometry against
the source FBX; `verify()` in the build script now asserts the hierarchy so it cannot recur.

## Why the mesh object was rescaled and the rig was not

The pack ships the mesh squashed: object scale (84.75, 92.15, 84.75) under a root at 0.01. It
renders at the right size, but it means a hand edit of one unit moves 0.85 m across the bear and
0.92 m along it — a distorted space to model in, and this file exists to be modelled in.

Nothing animates the mesh object, so its scale is free. It is now uniform, with the difference baked
into the vertices, which makes `Mesh_BearSculptBase`'s world scale exactly 1.0 — **its vertex
coordinates read in metres and edits are 1:1**. The compensation is an identity by construction
(`new_basis @ delta == old_basis`), so it holds on every animated frame rather than only at rest.

The armature keeps its 0.01-via-the-root: rescaling a rig rescales the location channel of all 81
actions. Every other rigged file in this library ships at 0.01 for the same reason, and
`_exportlib`'s `FBX_SCALE_ALL` is where that is dealt with on the way to Unity — see
`sculpt_character_export.py`, which documents the measurement (`FBX_SCALE_NONE` → bones at 100× in
Unity, `FBX_SCALE_ALL` → bones at 1×).

So the library's "all transforms applied, uniform scale 1.0" rule is met where it can be and
knowingly broken where it cannot: the mesh is 1.0 in world terms, the rig is not, and it is the
animation that forbids it.

## Verification

`bear_sculpt_base.py` asserts, on every build: the hierarchy and the root's 0.01; 35 bones with the
landmark names intact; 81 actions, all fake-user; 35 vertex groups and a live armature modifier; the
mesh's scale uniform and its world scale 1.0; no texture path that fails to resolve; and that the
rescale moved no vertex (measured drift 2.7 × 10⁻⁷ m).

Separately, the deformed mesh was sampled at 5 frames of each of 6 actions — `Walk`, `Sprint`,
`Attack_StandAngry_01_High`, `Death_Stand_R01`, `StandHind_Idle_01`, `Trans_Stand_to_Sneak` — and
compared against the same actions evaluated from a fresh import of the untouched FBX. **Worst
deviation 1.1 × 10⁻⁶ m across 30 samples, against poses travelling up to 7.1 m.** The animations
deform identically to the source.

## Known state, deliberately left alone

- **24 non-manifold edges** of 11,274, almost certainly the mouth, tongue and eyelid openings. Left
  as the pack authored them: sealing them would move UVs and skin weights for no gain on a mesh
  about to be reshaped.
- **Triangles, not quads.** 7,508 tris is a low-poly game cage, so it takes proportion changes but
  not surface detail. Deliberate: the decision on record was to push this cage directly and keep the
  rig and animations, rather than remesh to a sculptable quad surface and reweight.
- **`UVSet1`** is unused by any material and not the render UV. Kept in case the pack's lightmap or
  detail pass is wanted.
- **Frame 1 is a pose, not a bind pose.** `Stand_Eating_01` is the action left assigned, so the file
  opens mid-animation. Clear the action in the dope sheet to model on the bind pose.

## Working in this file

Reshape by moving vertices on `Mesh_BearSculptBase` and by editing bone *rest* positions on
`Arm_BearSculptBase` — scaling a limb's rest bone rescales its animation with it, which is the cheap
way to change proportion without breaking a gait.

Two things will break the 81 animations if changed: **bone names** (every action addresses its
channels by name) and **the armature object's own transform** (owned by the animation, as above).
Everything else — silhouette, limb proportion, head shape, materials — is yours.

There is no export script yet. Writing one means following `_exportlib` and, critically,
`apply_scale_options='FBX_SCALE_ALL'`, or every bone arrives in Unity at 100×.
