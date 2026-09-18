# Fabric Wings — build record

The Fabric Wings artifact's model: two sailcloth membranes sewn from wrist to
hip on the Nomad's own skeleton — a traditional wingsuit's arm wings, worn on
the body, with nothing rigid about them.

## Derivation, not modelling

No geometry is authored against a blank scene. `fabric_wings.py` opens
`models/characters/nomad/nomad.blend` — which carries hand edits and is
**never written** — takes the live rig (`Armature.001`, the copy
`nomad_export.py` ships), reads the world positions of the arm, spine and hip
bones, and sews each membrane between them:

| Row | Runs | Weighted to |
|---|---|---|
| arm seam | shoulder joint → elbow → wrist, hung 5 cm under the bone | `Shoulder`→`Arm` near the shoulder, `Arm`→`ForeArm` past the elbow, `ForeArm`+`Hand` at the wrist |
| mid row | halfway between, drooping 6 cm at the widest | half arm-seam, half flank-seam weights |
| flank seam | armpit (10 cm outside the spine line) straight down the side of the torso to the hip joint | graded `Spine2` → `Spine1` → `Spine` → `Hips` by height, like the cape |

Seven stations from armpit to wrist; a 4.5 cm hem of faded canvas hangs off the
trailing (wrist-to-hip) edge with the edge's own weights. Two objects,
`Mesh_FabricWing_Left` and `Mesh_FabricWing_Right`, ~50 verts each, parented to
the armature with an Armature modifier.

Everything else in the source file — the Nomad's seventy meshes and its
twenty-nine spare armature copies — is deleted from the in-memory copy before
saving, and the rig is recentred to the origin exactly as `nomad_export.py`
does, so the bind pose the wings ship with is the bind pose Unity already has
for the Nomad.

## Why on the rig

A wing that is part of the body has to deform with the body: folded flat along
the flank when the arms hang, filled when they spread, following whatever the
animator does in between. Skinning is the only honest way to get that, and
skinning to the character's own bones is what lets `FabricWingsArtifact`
re-bind the membranes to a worn Nomad by bone name at equip — no pose data of
its own, no second rig to keep in step.

## Materials

`Mat_Fabric_Wing_Ochre` for the cloth, `Mat_Fabric_Canvas_Faded` for the hem,
both linked from the palette. Nothing added. In Unity the builder replaces the
imported material with a double-sided URP Lit ochre (`Art/Materials/Items/
FabricWings.mat`) — a single layer of cloth is seen from both sides.

## Variations

None. The wing is defined by the body it is sewn to; a second cut would be a
second suit, not a variation.

## Unity

`fabric_wings_export.py` → `Assets/Game/Art/Models/Items/fabric_wings.fbx`
**with the armature kept** (`keep_armature=True`): the bones are what make the
membranes wings. `Assets/Game/Editor/Items/FabricWingsBuilder.cs` nests it and
binds every SkinnedMeshRenderer to `FabricWingsArtifact.membranes`.

## Decided without asking

- Arm wings only, no leg wing, per the brief ("from the arms to the hips").
- The cloth hangs from the underside of the arm rather than the trailing edge;
  at rest in the Nomad's A-pose that reads as a wingsuit with the arms down.
- The wearer's arms are posed out by code (`FabricWingsArtifact.ShowWings`),
  not by a clip: the project has no glide animation, and posing the two arm
  bones with a rig-agnostic `FromToRotation` after the Animator keeps the suit
  working on any Humanoid the player might wear next.
