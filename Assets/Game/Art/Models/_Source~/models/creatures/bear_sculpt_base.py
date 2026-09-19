"""Import the shipped bear FBX as a sculpt base: a rigged, animated quadruped to reshape.

Historical record, not source of truth: once the .blend is being sculpted, never re-run this over it.

    blender --background --python bear_sculpt_base.py

Reads bear_sculpt_base/bear_source.fbx and writes bear_sculpt_base.blend, both beside this script.

Not an entry in `creature_import.py`, though it borrows that script's `rebuild_material`. That one is
for creatures already living in the Unity tree under `Creatures/<Name>/`, imported as finished
assets. This is a base to be reshaped into something that is not a bear, so its source stays in
`_Source~` where Unity will not import 40 MB and 81 clips of an animal the game does not yet use.

What it does beyond a plain import, and why:

  * Keeps all 81 actions, each with a fake user so nothing purges them on save. They are the reason
    to build on this rig rather than sculpt freely: idles, four gaits with turn variants, rears,
    attacks, hit reactions, deaths and a full transition graph, inherited free by anything that
    keeps the skeleton roughly quadruped.
  * Leaves the rig hierarchy alone, which is less obvious than it looks. Every action animates the
    armature *object* -- `location`, `rotation_euler` (-90 deg about X) and `scale` (1.0) -- and not
    just its pose bones, so the armature's own transform belongs to the animation and cannot hold
    anything else. The metre conversion therefore has to sit above it, which is exactly what the
    FBX's root empty is for. Deleting that empty as redundant, or writing a scale onto the armature,
    silently loses to the next frame evaluation and lands the creature 100x too large and on its
    side.
  * Normalises the *mesh object's* scale, which is the one transform here that is safe to touch:
    nothing animates the mesh. The pack ships it squashed -- object scale (84.75, 92.15, 84.75)
    under a root at 0.01 -- which renders correctly but means a hand edit of one unit moves 0.85 m
    across the bear and 0.92 m along it. Making that scale uniform and baking the difference into
    the vertices leaves the space you sculpt in square and metric. It is an identity by
    construction (`new_basis @ delta == old_basis`), so it holds on every animated frame, not just
    at rest.
  * Rebuilds the three materials from the two maps the download actually shipped. The FBX asks for
    five, by absolute path on the machine that authored it (`D:/William/UNITY/...`), so every one
    arrives broken.

Bone names are left exactly as the pack wrote them: every action addresses its channels by bone
name, so renaming one silently breaks 81 animations.

The teeth slot shipped no map at all, so it takes a flat colour read out of `palette.blend`'s
`Mat_Hide_Ivory_Spine` -- the palette's pale keratin, written for the Vrescal's teeth. The value is
copied rather than linked: `goblin.blend` and `mountain_dragon.blend` own their materials too,
because a link into `_Source~/palette.blend` does not resolve once the model is exported into Unity.
"""
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from creature_import import rebuild_material  # noqa: E402

LIB_ROOT = HERE.parents[1]
PALETTE = LIB_ROOT / "palette.blend"
SOURCE = HERE / "bear_sculpt_base" / "bear_source.fbx"
TEXTURES = HERE / "bear_sculpt_base" / "textures"
TARGET = HERE / "bear_sculpt_base.blend"

NAME = "BearSculptBase"
ROOT_EMPTY = "Bear_Animated"          # the FBX scene root, and the only place the 0.01 can live
MESH_OBJECT = "sm_3_0_0"
ARMATURE_OBJECT = "RigRoot"
TEETH_PALETTE_MATERIAL = "Mat_Hide_Ivory_Spine"

# Slot order in the shipped mesh, with the map each one gets. Named by what they are: the FBX calls
# them Material_001..003, which says nothing about which is hide and which is enamel.
SLOTS = {
    "Material_003": ("Bear_Body", {"base": "Bear_Body_A.png"}),
    "Material_001": ("Bear_Head", {"base": "Bear_Head_A.png"}),
    "Material_002": ("Bear_Teeth", {}),        # filled from the palette below
}

EXPECTED_BONES = 35
EXPECTED_ACTIONS = 81
LANDMARK_BONES = ("RigPelvis", "RigSpine1", "RigChest", "RigHead", "RigJaw", "RigTail1")


def palette_surface(material_name):
    """The base colour and roughness of a palette material, without linking the datablock."""
    with bpy.data.libraries.load(str(PALETTE), link=False) as (source, target):
        if material_name not in source.materials:
            raise SystemExit(f"{material_name} is not in {PALETTE}")
        target.materials = [material_name]
    material = target.materials[0]
    bsdf = next(n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    colour = tuple(bsdf.inputs["Base Color"].default_value)[:3]
    roughness = bsdf.inputs["Roughness"].default_value
    bpy.data.materials.remove(material)     # the numbers are wanted, not the material
    return colour, roughness


def make_metric(obj):
    """Make `obj`'s own scale uniform, baking the difference into its mesh.

    The uniform value is the reciprocal of whatever its parents scale by, so the vertices end up
    reading in metres. Only the basis is touched and the mesh is compensated exactly, so
    `new_basis @ (delta @ v) == old_basis @ v` holds whatever the animated parents are doing.
    """
    bpy.context.view_layer.update()
    # basis / world, per axis, is the parent chain's scale; invert it to land the vertices in metres.
    factors = [b / w for b, w in zip(obj.matrix_basis.to_scale(), obj.matrix_world.to_scale())]
    assert max(factors) - min(factors) < 1e-3, f"parents scale non-uniformly: {factors}"
    target = sum(factors) / len(factors)

    old_basis = obj.matrix_basis.copy()
    location, rotation, _ = old_basis.decompose()
    new_basis = Matrix.LocRotScale(location, rotation, Vector((target, target, target)))
    obj.data.transform(new_basis.inverted() @ old_basis)
    obj.matrix_basis = new_basis
    obj.data.update()
    bpy.context.view_layer.update()


def main():
    if TARGET.exists():
        raise SystemExit(f"{TARGET} already exists; edit it in place, never regenerate")
    if not SOURCE.is_file():
        raise SystemExit(f"No source FBX at {SOURCE}")

    bpy.ops.wm.read_homefile(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0

    # ignore_leaf_bones drops the tip markers FBX adds past each chain's last real bone; without it
    # they arrive as ten stray *_end empties parented to the rig.
    bpy.ops.import_scene.fbx(filepath=str(SOURCE), global_scale=1.0, ignore_leaf_bones=True)

    root = bpy.data.objects[ROOT_EMPTY]
    armature = bpy.data.objects[ARMATURE_OBJECT]
    mesh_object = bpy.data.objects[MESH_OBJECT]

    bpy.context.view_layer.update()
    world_before = [mesh_object.matrix_world @ v.co.copy() for v in mesh_object.data.vertices]

    for leftover in [o for o in bpy.data.objects if o.name.endswith("_end")]:
        bpy.data.objects.remove(leftover, do_unlink=True)

    make_metric(mesh_object)

    colour, roughness = palette_surface(TEETH_PALETTE_MATERIAL)
    SLOTS["Material_002"][1].update(colour=colour, roughness=roughness)
    for old_name, (new_name, maps) in SLOTS.items():
        material = bpy.data.materials[old_name]
        material.name = new_name
        rebuild_material(material, maps, TEXTURES)

    root.name = f"Root_{NAME}"
    armature.name = armature.data.name = f"Arm_{NAME}"
    mesh_object.name = mesh_object.data.name = f"Mesh_{NAME}"

    # Every action, not just the one left assigned, or saving drops the other eighty.
    for action in bpy.data.actions:
        action.use_fake_user = True

    collection = bpy.data.collections.new(f"Coll_{NAME}")
    scene.collection.children.link(collection)
    for obj in (root, armature, mesh_object):
        for existing in list(obj.users_collection):
            existing.objects.unlink(obj)
        collection.objects.link(obj)

    verify(root, armature, mesh_object, world_before)
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET), relative_remap=True)
    print(f"wrote {TARGET}")


def verify(root, armature, mesh_object, world_before):
    # Read every matrix after this, or a stale one makes the comparisons agree with themselves and
    # prove nothing -- which is how a 100x rig once passed this function.
    bpy.context.view_layer.update()

    assert armature.parent is root, "the rig must stay under the root empty that carries the 0.01"
    assert mesh_object.parent is armature, "the mesh must stay parented to the rig"
    root_scale = root.matrix_basis.to_scale()
    assert all(abs(s - 0.01) < 1e-9 for s in root_scale), f"root scale {root_scale[:]} is not 0.01"

    bones = armature.data.bones
    assert len(bones) == EXPECTED_BONES, f"{len(bones)} bones, expected {EXPECTED_BONES}"
    missing_bones = [b for b in LANDMARK_BONES if b not in bones]
    assert not missing_bones, f"renamed or lost bones, which breaks every action: {missing_bones}"
    assert len(bpy.data.actions) == EXPECTED_ACTIONS, f"{len(bpy.data.actions)} actions"
    assert all(a.use_fake_user for a in bpy.data.actions), "an action would be purged on save"
    assert armature.animation_data and armature.animation_data.action, "rig lost its animation data"
    assert len(mesh_object.vertex_groups) == EXPECTED_BONES, "skin weights lost a group"
    assert any(m.type == 'ARMATURE' for m in mesh_object.modifiers), "armature modifier gone"

    basis_scale = mesh_object.matrix_basis.to_scale()
    assert max(basis_scale) - min(basis_scale) < 1e-4, f"mesh scale {basis_scale[:]} is not uniform"
    world_scale = mesh_object.matrix_world.to_scale()
    assert all(abs(s - 1.0) < 1e-4 for s in world_scale), \
        f"mesh world scale {world_scale[:]} is not 1.0, so its vertices do not read in metres"

    drift = max((mesh_object.matrix_world @ v.co - before).length
                for v, before in zip(mesh_object.data.vertices, world_before))
    assert drift < 1e-5, f"rescale moved geometry by {drift:.6f} m"

    # Resolved paths, not `has_data`: background Blender loads no pixels, but a leftover
    # `D:/William/UNITY/...` from the pack still has to fail here rather than in the viewport.
    unresolved = [n.image.filepath for m in bpy.data.materials if m.node_tree
                  for n in m.node_tree.nodes
                  if n.type == 'TEX_IMAGE' and n.image
                  and not Path(bpy.path.abspath(n.image.filepath)).is_file()]
    assert not unresolved, f"materials point at images that do not exist: {unresolved}"

    size = [max(c) - min(c) for c in zip(*(mesh_object.matrix_world @ v.co
                                           for v in mesh_object.data.vertices))]
    print(f"verified: {len(bones)} bones, {len(bpy.data.actions)} actions, "
          f"{len(mesh_object.data.polygons)} tris, drift {drift:.2e} m, "
          f"rest size {size[0]:.3f} x {size[1]:.3f} x {size[2]:.3f} m")


main()
