"""Wingsuit — the Wingsuit artifact's model: a wing harness with two hinged
sailcloth blades and a tail fan.

Assembly only, in the manner of `grapple_bracer.py`: nothing here is modelled.
The harness is a component, the blades are the ornithopter's own secondary
wing blades, the tail is its tail fan. What this file decides is where the
pins are and which way everything folds.

| Object                          | Where it comes from |
|---|---|
| `Mesh_WingHarness_Spar`         | `components/props/wing_harness.blend`, unchanged |
| `Mesh_WingBlade_Secondary_R`    | `components/mechanical/wing_blade.blend`, re-pinned |
| `Mesh_WingBlade_Secondary_L`    | the right blade mirrored across the wearer's centreline |
| `Mesh_WingBlade_TailFan`        | `components/mechanical/wing_blade.blend`, re-pinned |

Frame: the harness's — origin on the wearer's back between the shoulder
blades, -Y forward, +Z up, the wearer's right is -X (Unity mirrors that axis on
import, so the `_R` blade lands on Unity +X: the wearer's right there too).

## Folded is the rest pose

The file holds the wings STOWED: hanging down the back with the tail fan
against the legs. That is what the item looks like lying in the sand and what
it looks like worn until the pilot deploys it, so it is the pose the geometry
ships in. Each hinged part's origin is its pin — `place()` bakes the fold into
the mesh and leaves the object's location on the pivot — so deploying in Unity
is a rotation of the part about its own origin. The blades open about the
fore-aft axis (the shoulder pins run along Y), the fan about the across axis
(its pin runs along X): `WingsuitArtifact` rotates the three transforms, the
same way `ArticulatedPart` swings the ship's panels about hinge origins.

A rig is kept in the .blend for anyone posing it in Blender, but the export
drops it (`keep_armature=False`) — Unity drives the objects, not bones.

    blender --background --python wingsuit.py -- --out <path>/wingsuit.blend

Generation script — historical record. The .blend is the source of truth; never
re-run this over the file it produced.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_LIB = os.path.dirname(os.path.dirname(_HERE))
sys.path.insert(0, _LIB)
sys.path.insert(0, os.path.join(_LIB, "components", "props"))
sys.path.insert(0, _HERE)

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from _buildlib import *  # noqa: E402,F403
from item_scanner import append_objects, place  # noqa: E402
from wing_harness import SHOULDER_PIVOT_RIGHT, TAIL_PIVOT  # noqa: E402

HARNESS = os.path.join(_LIB, "components", "props", "wing_harness.blend")
BLADES = os.path.join(_LIB, "components", "mechanical", "wing_blade.blend")

# How far the wings swing when folded, from straight-out (spread) toward
# straight-down. 85 rather than 90 leaves the tips a little outboard so the
# two blades never cross behind the spine.
WING_FOLD_DEG = 85.0
# The fan sits back-and-down at 45 when spread; folding it 45 more lays it
# straight down the legs.
TAIL_FOLD_DEG = 45.0


def spread_wing_matrix():
    """Blade local frame onto the spread right wing.

    The blade is authored root pin at origin, +Y out to the tip, X across the
    chord, +Z the cambered face. Spread, the tip points out the wearer's right
    (-X), the trailing edge lies toward the wearer's back (+Y — the blade's
    built-in sweep then bends the tip aft, which is the right way round), and
    the cambered face stays up, so the authored tip droop reads as a slight
    anhedral. Columns are the images of local X, Y, Z; the determinant is +1,
    so this is a rotation and not a hidden mirror.
    """
    return Matrix(((0.0, -1.0, 0.0),
                   (1.0, 0.0, 0.0),
                   (0.0, 0.0, 1.0))).to_4x4()


def spread_tail_matrix():
    """Blade local frame onto the spread tail fan: tip back and down at 45,
    chord across the wearer, cambered face toward the back."""
    c = math.sqrt(0.5)
    return Matrix(((1.0, 0.0, 0.0),
                   (0.0, c, c),
                   (0.0, -c, c))).to_4x4()


def mirror_x(obj, coll, name):
    """The wearer's left half of a right-side part: mirrored across the spine
    with normals corrected, origin mirrored with it."""
    mesh = obj.data.copy()
    mesh.name = name
    mesh.transform(Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0)))
    mesh.flip_normals()
    new = bpy.data.objects.new(name, mesh)
    new.location = (-obj.location.x, obj.location.y, obj.location.z)
    coll.objects.link(new)
    return new


def rebind_palette_materials(objects):
    """Point every material slot at the palette this library actually lives in.

    `wing_blade.blend` predates the library's move under `Assets/Game/Art` and
    still links its materials from the old location, so an appended blade
    arrives wearing dead links — black in a render, empty in an export. The
    names are right; only the library behind them is stale. Load the same
    names from the real palette, reassign the slots, and drop the dead library
    so the saved file carries one palette reference and not two.
    """
    names = sorted({slot.material.name for obj in objects
                    for slot in obj.material_slots if slot.material is not None})
    palette = os.path.abspath(PALETTE)

    def from_palette(material):
        return (material.library is not None and
                os.path.abspath(bpy.path.abspath(material.library.filepath)) == palette)

    with bpy.data.libraries.load(PALETTE, link=True) as (src, dst):
        unknown = [n for n in names if n not in set(src.materials)]
        if unknown:
            raise SystemExit("Not in the palette: %s" % ", ".join(unknown))
        dst.materials = names

    good = {m.name: m for m in bpy.data.materials if from_palette(m)}
    for obj in objects:
        for slot in obj.material_slots:
            if slot.material is not None:
                slot.material = good[slot.material.name]

    for lib in list(bpy.data.libraries):
        if os.path.abspath(bpy.path.abspath(lib.filepath)) != palette:
            bpy.data.libraries.remove(lib)


def rig(coll, parts):
    """One bone per hinged part, head on its pin, tail along the folded part.

    Root-level bones, deliberately: a bone with no parent has the model's own
    axes as its rest frame, so a pose rotation about a bone's parent axis is a
    rotation about a world axis — the same axes the Unity code uses.
    """
    arm_data = bpy.data.armatures.new("Arm_Wingsuit")
    arm = bpy.data.objects.new("Arm_Wingsuit", arm_data)
    coll.objects.link(arm)
    bpy.context.view_layer.objects.active = arm

    bpy.ops.object.mode_set(mode='EDIT')
    for obj, bone_name, direction in parts:
        bone = arm_data.edit_bones.new(bone_name)
        bone.head = obj.location
        bone.tail = obj.location + Vector(direction) * 0.4
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()

    for obj, bone_name, _ in parts:
        before = obj.matrix_world.copy()
        pose_bone = arm.pose.bones[bone_name]
        obj.parent = arm
        obj.parent_type = 'BONE'
        obj.parent_bone = bone_name
        # Bone parenting attaches at the bone's TAIL; cancel that so the part
        # stays exactly where it was placed.
        tail_space = arm.matrix_world @ pose_bone.matrix @ Matrix.Translation(
            (0.0, pose_bone.length, 0.0))
        obj.matrix_parent_inverse = tail_space.inverted()
        bpy.context.view_layer.update()
        drift = (obj.matrix_world.translation - before.translation).length
        if drift > 1e-4:
            raise SystemExit("%s moved %.4f m when parented to %s"
                             % (obj.name, drift, bone_name))
    return arm


def main():
    out = parse_out()
    start(out)

    root = collection("Coll_Wingsuit")
    components = collection("Coll_Components", root)
    rig_coll = collection("Coll_Rig", root)

    append_objects(HARNESS, ["Mesh_WingHarness_Spar"], components)

    pivot_r = Vector(SHOULDER_PIVOT_RIGHT)
    # Negative: the right tip starts on -X and swings down through -Z, and the
    # last five degrees keep it outboard rather than crossing the spine.
    fold_wing = Matrix.Rotation(math.radians(-WING_FOLD_DEG), 4, 'Y')
    (wing_r,) = append_objects(BLADES, ["Mesh_WingBlade_Secondary"], components)
    wing_r.name = "Mesh_WingBlade_Secondary_R"
    place(wing_r, Matrix.Translation(pivot_r) @ fold_wing @ spread_wing_matrix())
    wing_l = mirror_x(wing_r, components, "Mesh_WingBlade_Secondary_L")

    fold_tail = Matrix.Rotation(math.radians(-TAIL_FOLD_DEG), 4, 'X')
    (tail,) = append_objects(BLADES, ["Mesh_WingBlade_TailFan"], components)
    place(tail, Matrix.Translation(Vector(TAIL_PIVOT)) @ fold_tail @ spread_tail_matrix())

    rebind_palette_materials([wing_r, wing_l, tail])

    # Folded directions, for the bones: where each part's tip points at rest.
    tip_r = (fold_wing @ spread_wing_matrix()).to_3x3() @ Vector((0.0, 1.0, 0.0))
    tip_tail = (fold_tail @ spread_tail_matrix()).to_3x3() @ Vector((0.0, 1.0, 0.0))
    rig(rig_coll, [
        (wing_r, "Bone_WingR", tip_r),
        (wing_l, "Bone_WingL", Vector((-tip_r.x, tip_r.y, tip_r.z))),
        (tail, "Bone_TailFan", tip_tail),
    ])

    print("  folded tip directions: wing %s, tail %s"
          % (tuple(round(v, 3) for v in tip_r), tuple(round(v, 3) for v in tip_tail)))
    report()
    save(out)


if __name__ == "__main__":
    main()
