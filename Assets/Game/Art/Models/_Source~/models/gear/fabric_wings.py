"""Fabric wings — the wingsuit membranes, sewn onto the Nomad's own skeleton.

Two triangles of sailcloth, one per side, each running from the wrist along the
underside of the arm to the armpit and down the flank to the hip: the classic
wingsuit arm wing. Nothing rigid. The cloth is weighted to the character's
bones so it follows the arms wherever the animation puts them — folded flat
along the body when the arms hang, filled when they spread.

Built on the rig, not near it: this script opens `nomad.blend` (READ-ONLY, the
way `wing_pack_folded.py` opens `dune_ornithopter.blend`), reads the live
skeleton's bone positions, sews the membranes between them with per-vertex
weights, and then throws away everything that is not the rig or the wings
before saving to a NEW file. The rig is recentred to the origin exactly as
`nomad_export.py` recentres it, so the exported membranes bind to the same rest
pose Unity already has for the Nomad and can be re-bound to a worn Nomad's
bones by name.

    blender --background --python fabric_wings.py -- --out <path>/fabric_wings.blend

Generation script — historical record. The .blend is the source of truth; never
re-run this over the file it produced.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_LIB = os.path.dirname(os.path.dirname(_HERE))
sys.path.insert(0, _LIB)

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from _buildlib import SkinPart, link_materials, parse_out, collection  # noqa: E402

NOMAD = os.path.join(_LIB, "models", "characters", "nomad", "nomad.blend")

# The armature copy the live Nomad is bound to. nomad.blend accumulated thirty
# skeleton copies; this is the one nomad_export.py ships.
TARGET_ARMATURE = "Armature.001"

MATS = ["Mat_Fabric_Wing_Ochre",   # 0  the sailcloth
        "Mat_Fabric_Canvas_Faded"]  # 1  the hem, stitched along the free edge

STATIONS = 7          # rows from armpit to wrist
HEM_WIDTH = 0.045     # metres of darker canvas along the trailing edge
ARM_STANDOFF = 0.05   # cloth hangs this far under the arm bone, not through it
TORSO_OUT = 0.10      # how far outside the spine line the flank seam runs
SAG = 0.06            # mid-row droop at the widest point, so the panel is not a plank


def flush_edit_mode():
    """The source file was once saved mid-edit; flush so meshes are current."""
    for obj in bpy.data.objects:
        if obj.mode != 'OBJECT':
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.mode_set(mode='OBJECT')


def bone_points(arm, name):
    b = arm.data.bones["mixamorig:" + name]
    return arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local


def lerp(a, b, t):
    return a + (b - a) * t


def wing(arm, side, mats, coll):
    """One arm wing. `side` is 'Left' or 'Right'.

    Rows, root to tip: the flank seam (on the body), a mid row, and the arm
    seam. Columns run from the armpit out to the wrist. Weights follow what
    each vertex is sewn to: the arm seam to the arm bone under it, the flank
    seam graded down the spine to the hips, the mid row half and half.
    """
    shoulder_h, shoulder_t = bone_points(arm, side + "Arm")
    elbow_h, wrist = bone_points(arm, side + "ForeArm")
    hips_h, _ = bone_points(arm, "Hips")
    spine_h, _ = bone_points(arm, "Spine")
    chest_h, chest_t = bone_points(arm, "Spine2")
    upleg_h, _ = bone_points(arm, side + "UpLeg")

    outward = 1.0 if shoulder_t.x > shoulder_h.x else -1.0
    down = Vector((0.0, 0.0, -ARM_STANDOFF))

    # The arm seam: shoulder -> elbow -> wrist, hung a little under the bone.
    def arm_point(t):
        if t < 0.5:
            return lerp(shoulder_h, elbow_h, t * 2.0) + down
        return lerp(elbow_h, wrist, (t - 0.5) * 2.0) + down

    # The flank seam: armpit (just under the shoulder joint, in against the
    # ribs) straight down the side of the torso to the hip joint.
    armpit = Vector((chest_h.x + outward * TORSO_OUT, chest_h.y, shoulder_h.z - 0.12))
    hip = Vector((upleg_h.x + outward * 0.06, upleg_h.y, upleg_h.z + 0.04))

    def flank_point(t):
        return lerp(armpit, hip, t)

    # Weights along each seam (normalised by SkinPart on output).
    def arm_weights(t):
        if t < 0.5:
            k = t * 2.0
            return {"mixamorig:" + side + "Shoulder": max(0.0, 0.3 - k),
                    "mixamorig:" + side + "Arm": 0.7 + 0.3 * k}
        k = (t - 0.5) * 2.0
        return {"mixamorig:" + side + "Arm": 0.5 * (1.0 - k),
                "mixamorig:" + side + "ForeArm": 0.5 + 0.1 * k,
                "mixamorig:" + side + "Hand": 0.4 * k}

    def flank_weights(t):
        # Chest at the armpit, hips at the hip, the two middle spine bones between.
        chain = ["mixamorig:Spine2", "mixamorig:Spine1", "mixamorig:Spine", "mixamorig:Hips"]
        slot = t * (len(chain) - 1)
        lower = min(int(slot), len(chain) - 1)
        upper = min(lower + 1, len(chain) - 1)
        blend = slot - lower
        # At the hip end lower and upper are the same bone; writing both keys into one
        # dict would leave that bone with the SECOND value — zero — and the vertex unweighted.
        if lower == upper:
            return {chain[lower]: 1.0}
        return {chain[lower]: 1.0 - blend, chain[upper]: blend}

    p = SkinPart(mats)
    rows = [[], [], []]
    for i in range(STATIONS):
        t = i / (STATIONS - 1)
        a = arm_point(t)
        f = flank_point(t)
        mid = lerp(f, a, 0.5) + Vector((0.0, 0.0, -SAG * math.sin(t * math.pi)))

        wa, wf = arm_weights(t), flank_weights(t)
        wm = {k: v * 0.5 for k, v in wa.items()}
        for k, v in wf.items():
            wm[k] = wm.get(k, 0.0) + v * 0.5

        rows[0].append(p.vert(f, wf))
        rows[1].append(p.vert(mid, wm))
        rows[2].append(p.vert(a, wa))

    p.bridge(rows[:2], mat=0)
    p.bridge(rows[1:], mat=0)

    # Hem: a narrow strip of canvas hung off the trailing edge (wrist -> hip),
    # weighted like the edge it hangs from.
    hem_rows = [[], []]
    hem_a, hem_f = arm_point(1.0), flank_point(1.0)
    drop = Vector((0.0, 0.0, -HEM_WIDTH))
    for k in range(4):
        t = k / 3.0
        pt = lerp(hem_f, hem_a, t)
        w_edge = {**{n: v * (1.0 - t) for n, v in flank_weights(1.0).items()}}
        for n, v in arm_weights(1.0).items():
            w_edge[n] = w_edge.get(n, 0.0) + v * t
        hem_rows[0].append(p.vert(pt, w_edge))
        hem_rows[1].append(p.vert(pt + drop, w_edge))
    p.bridge(hem_rows, mat=1)

    groups = ["mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
              "mixamorig:" + side + "Shoulder", "mixamorig:" + side + "Arm",
              "mixamorig:" + side + "ForeArm", "mixamorig:" + side + "Hand"]
    obj = p.finish("Mesh_FabricWing_" + side, coll, groups)

    # Rig it: parent to the armature keeping world placement, and let the
    # armature deform it. The parent-inverse dance is the one nomad_export.py
    # documents — skipping it folds the rig's 0.01 scale into the mesh.
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = arm
    return obj


def keep_only(arm, wings):
    """Everything in nomad.blend that is not the live rig or the wings goes."""
    keep = {arm.name} | {w.name for w in wings}
    for obj in list(bpy.data.objects):
        if obj.name not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.curves, bpy.data.armatures, bpy.data.materials):
        for datablock in list(block):
            if datablock.users == 0:
                block.remove(datablock)


def recentre(arm):
    """Slide the rig to the origin the way nomad_export.py does, so the bind
    pose Unity sees for the wings is the one it already has for the Nomad."""
    delta = arm.matrix_world.translation.copy()
    arm.matrix_world = Matrix.Translation(-delta) @ arm.matrix_world
    bpy.context.view_layer.update()
    return delta


def main():
    out = parse_out()
    if os.path.exists(out):
        raise SystemExit("Refusing to overwrite existing file: %s\nThe .blend is the "
                         "source of truth. Edit it via MCP instead of regenerating." % out)
    if not os.path.exists(NOMAD):
        raise SystemExit("No Nomad at %s" % NOMAD)

    bpy.ops.wm.open_mainfile(filepath=NOMAD)
    flush_edit_mode()

    arm = bpy.data.objects.get(TARGET_ARMATURE)
    if arm is None or arm.type != 'ARMATURE':
        raise SystemExit("No armature named %s in %s" % (TARGET_ARMATURE, NOMAD))

    mats = link_materials(MATS)
    coll = collection("Coll_FabricWings")
    wings = [wing(arm, "Left", mats, coll), wing(arm, "Right", mats, coll)]

    keep_only(arm, wings)
    for c in list(bpy.data.collections):
        if c.name != coll.name and not c.objects and not c.children:
            bpy.data.collections.remove(c)
    if arm.name not in coll.objects:
        coll.objects.link(arm)

    moved = recentre(arm)
    print("  recentred the rig from (%.3f, %.3f, %.3f)" % (moved.x, moved.y, moved.z))
    for w in wings:
        print("  %-24s verts=%d groups=%s" % (w.name, len(w.data.vertices),
                                             [g.name for g in w.vertex_groups]))

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out))
    print("Wrote %s" % out)


if __name__ == "__main__":
    main()
