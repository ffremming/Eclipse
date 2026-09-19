"""Refit the human skeleton onto a sculpted variant of it, then re-skin.

    blender --background variant_rigged.blend --python refit.py -- human.bin out.blend report.json

The alien and the crumpy are the human sculpt with its vertices pushed around: same vertex
count, same vertex order, byte-identical face index stream. What was never done is move the
SKELETON with them, so all 52 bones sit at the human's joint positions inside a body that is a
different shape, and the skin they were bound to deforms around pivots that are not in its limbs.

Because the topology is shared, each joint can be carried through the same deformation the
sculptor applied, instead of being hand-placed per character: take the joint's neighbourhood on
the reference body, and move the joint by that neighbourhood's own average displacement.

    p' = p + sum_i w_i * (v_target_i - v_reference_i)

with w_i a Gaussian over the K nearest reference vertices. Displacement rather than absolute
position, so a joint keeps its offset from the surface — a hip stays as deep inside the pelvis
as it was, rather than being dragged onto the skin.

Two things are deliberately NOT transferred:

  * `Root` is the ground-level pivot the game measures "the body stands on its own pivot" from.
    It belongs at the origin, not wherever the ankles happened to move, so it is pinned.
  * The eyes are hand-placed on each sculpted face and are bone-parented, not skinned, so moving
    the Head bone would drag them off the face. Their world matrices are captured before the
    edit and restored after it.

The mesh is not touched: no vertex moves, no face is added or removed. Verified, not assumed.
"""
import json
import struct
import sys

import bpy
from mathutils import Vector, kdtree

BODY = "Mesh_HumanSculptBase"
RIG = "Human_Rig"
EYES = ("Sphere", "Sphere.001")
PINNED = {"Root"}          # the ground pivot; see the docstring
NEIGHBOURS = 128           # reference vertices averaged per joint
MAX_INFLUENCES = 4         # Unity skins with four bones a vertex by default
MIN_WEIGHT_SUM = 0.99      # every vertex must be fully claimed by some bone


def read_reference(path):
    data = open(path, "rb").read()
    count = struct.unpack_from("<i", data)[0]
    return [Vector(struct.unpack_from("<3f", data, 4 + 12 * i)) for i in range(count)]


def surface_depth(body, point):
    """Signed distance from `point` to the body surface: positive inside, negative outside."""
    ok, location, normal, _ = body.closest_point_on_mesh(point)
    if not ok:
        return None
    offset = point - location
    inside = offset.dot(normal) <= 0.0
    return offset.length * (1.0 if inside else -1.0)


def joint_report(body, joints):
    """How deep inside the body each joint sits, worst (most outside) first."""
    rows = [(name, round(surface_depth(body, p), 4)) for name, p in joints.items()]
    rows.sort(key=lambda r: r[1])
    return {
        "outside": [r for r in rows if r[1] < 0.0],
        "worst": rows[:8],
        "min": rows[0][1],
        "median": sorted(r[1] for r in rows)[len(rows) // 2],
    }


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    reference_path, out_path, report_path = argv[0], argv[1], argv[2]

    body = bpy.data.objects[BODY]
    rig = bpy.data.objects[RIG]

    # Bone rest positions and mesh vertices are only comparable if they share a space. The mesh
    # is parented to the rig at identity, so armature-local IS mesh-local — but assert it rather
    # than trust it, because a hand edit to either object's transform would silently skew every
    # joint this script places.
    skew = max(abs(a - b) for row_a, row_b in zip(body.matrix_world, rig.matrix_world)
               for a, b in zip(row_a, row_b))
    assert skew < 1e-5, f"mesh and rig do not share a space (max element delta {skew})"

    reference = read_reference(reference_path)
    target = [v.co.copy() for v in body.data.vertices]
    assert len(reference) == len(target), \
        f"reference has {len(reference)} vertices, this body has {len(target)}"

    tree = kdtree.KDTree(len(reference))
    for i, co in enumerate(reference):
        tree.insert(co, i)
    tree.balance()

    def transfer(point):
        """Carry `point` through the sculpt deformation of the body around it."""
        found = tree.find_n(point, NEIGHBOURS)
        sigma = max(found[-1][2], 1e-4) * 0.5
        total = 0.0
        shift = Vector((0.0, 0.0, 0.0))
        for co, index, distance in found:
            weight = pow(2.718281828, -(distance / sigma) ** 2)
            shift += (target[index] - reference[index]) * weight
            total += weight
        return point + shift / total if total > 0.0 else point.copy()

    # --- where the joints are now, and how badly that reads on this body -----------------------
    before = {}
    for bone in rig.data.bones:
        before[bone.name + ".head"] = bone.head_local.copy()
        before[bone.name + ".tail"] = bone.tail_local.copy()
    report = {"before": joint_report(body, before)}

    # --- carry every joint through the sculpt --------------------------------------------------
    # One cache keyed on the position, so a connected child's head and its parent's tail — which
    # are the same point — are transferred once and stay welded together.
    moved = {}

    def transferred(point):
        key = (round(point.x, 6), round(point.y, 6), round(point.z, 6))
        if key not in moved:
            moved[key] = transfer(point)
        return moved[key]

    eye_matrices = {name: bpy.data.objects[name].matrix_world.copy() for name in EYES}

    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    edit = rig.data.edit_bones

    # Connection is restored after the edit. While it is on, assigning a parent's tail drags its
    # connected children's heads with it, so a loop over bones would read positions that an
    # earlier iteration had already moved.
    connected = {bone.name: bone.use_connect for bone in edit}
    for bone in edit:
        bone.use_connect = False

    placed = {}
    for bone in edit:
        if bone.name in PINNED:
            placed[bone.name] = (bone.head.copy(), bone.tail.copy())
        else:
            placed[bone.name] = (transferred(bone.head), transferred(bone.tail))
    for bone in edit:
        bone.head, bone.tail = placed[bone.name]

    for bone in edit:
        # A zero-length bone is deleted by Blender on leaving edit mode, taking its vertex group
        # and, for the fingers, its whole chain with it.
        if (bone.tail - bone.head).length < 1e-5:
            bone.tail = bone.head + Vector((0.0, 0.0, 1e-3))
        bone.use_connect = connected[bone.name]

    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()

    # --- put the eyes back on the face ---------------------------------------------------------
    for name, matrix in eye_matrices.items():
        bpy.data.objects[name].matrix_world = matrix
    bpy.context.view_layer.update()

    # --- re-skin against the corrected skeleton -------------------------------------------------
    # The old weights were solved against joints that were in the wrong place; keeping them would
    # leave the character deforming around the human's pivots with a new skeleton drawn over them.
    positions_before = [v.co.copy() for v in body.data.vertices]
    body.vertex_groups.clear()

    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=MAX_INFLUENCES)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

    # --- verify ---------------------------------------------------------------------------------
    assert all((a - b).length == 0 for a, b in zip(positions_before, (v.co for v in body.data.vertices))), \
        "the mesh moved"

    after = {}
    for bone in rig.data.bones:
        after[bone.name + ".head"] = bone.head_local.copy()
        after[bone.name + ".tail"] = bone.tail_local.copy()
    report["after"] = joint_report(body, after)

    bone_names = {b.name for b in rig.data.bones}
    weighted = {g.name for g in body.vertex_groups}
    assert weighted <= bone_names, f"vertex groups without a bone: {weighted - bone_names}"
    loose = sum(1 for v in body.data.vertices if sum(g.weight for g in v.groups) < MIN_WEIGHT_SUM)
    assert loose == 0, f"{loose} vertices are not fully weighted"
    assert max(len(v.groups) for v in body.data.vertices) <= MAX_INFLUENCES

    for name in EYES:
        eye = bpy.data.objects[name]
        assert eye.parent == rig and eye.parent_bone == "Head", f"{name} is not on the Head bone"
        drift = (eye.matrix_world.translation - eye_matrices[name].translation).length
        assert drift < 1e-4, f"{name} drifted {drift:.5f} from the face"

    report.update({
        "bones": len(bone_names),
        "weighted_groups": len(weighted),
        "vertices": len(body.data.vertices),
        "joints_moved": sum(1 for k in before if (before[k] - after[k]).length > 1e-5),
        "largest_joint_move": round(max((before[k] - after[k]).length for k in before), 4),
    })
    with open(report_path, "w") as f:
        json.dump(report, f, indent=1)

    bpy.ops.wm.save_as_mainfile(filepath=out_path)
    print(f"REFIT {out_path}: {report['joints_moved']} joints moved, "
          f"deepest-outside before {report['before']['min']} after {report['after']['min']}")


main()
