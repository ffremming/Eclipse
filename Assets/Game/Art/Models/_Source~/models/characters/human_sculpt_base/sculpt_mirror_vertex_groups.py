"""Give the left half of the body its own bones back.

    blender --background crumpy_rigged.blend --python mirror_groups.py -- out.blend report.json

A mirror or symmetrize in Blender copies geometry and its vertex groups, but only renames the
groups it recognises as a side pair — and it recognises Blender's own `.L`/`.R` SUFFIX, not the
`Left…`/`Right…` PREFIX this Unity-Humanoid skeleton uses. So the mirrored half arrived carrying
the right side's groups: 21 left-side groups completely empty, `RightLowerLeg`, `RightFoot`,
`RightHand` and `RightUpperArm` each holding exactly twice their vertices, and the centre bones
untouched. Unity then refused the avatar outright — "Required human bone 'LeftLowerLeg' not
found" — because a human bone with no skin cluster is not a bone it can map.

The fix is the rename the mirror did not do: for every vertex on the left of the midline, move
each `Right*` weight onto the matching `Left*` group. Weights are ADDED rather than assigned, so
the left-side vertices that kept a real `Left*` weight (`LeftUpperLeg` still had 1069) are not
overwritten by the mirrored copy.

Only vertex groups change. No vertex moves, no face is added or removed, no bone is touched --
asserted at the end rather than assumed.

Sides are read off the skeleton rather than assumed: the bones are built with `Right` on +X.
"""
import json
import sys

import bpy

BODY = "Mesh_HumanSculptBase"
RIG = "Human_Rig"
MIDLINE = 1e-4           # a vertex nearer the centre than this is shared and is left alone
MAX_INFLUENCES = 4       # what the rig was authored to, and what Unity skins with by default


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out_path, report_path = argv[0], argv[1]

    body = bpy.data.objects[BODY]
    rig = bpy.data.objects[RIG]

    # Which way is right? Read it off a bone rather than trusting a remembered convention.
    right_x = rig.data.bones["RightUpperLeg"].head_local.x
    left_x = rig.data.bones["LeftUpperLeg"].head_local.x
    assert right_x > 0.0 > left_x, f"unexpected side convention (right {right_x}, left {left_x})"

    groups = body.vertex_groups
    index_to_name = {g.index: g.name for g in groups}
    pairs = {}
    for group in groups:
        if group.name.startswith("Right"):
            partner = "Left" + group.name[len("Right"):]
            if partner in groups:
                pairs[group.index] = groups[partner].index
    assert pairs, "no Right*/Left* group pairs found"

    positions_before = [v.co.copy() for v in body.data.vertices]

    def weighted(counts_for):
        counts = {name: 0 for name in index_to_name.values()}
        for v in body.data.vertices:
            for g in v.groups:
                if g.weight > 0.0001:
                    counts[index_to_name[g.group]] += 1
        return counts

    before_counts = weighted(None)

    # Collect first, apply second: adding to a group while walking a vertex's own group list is
    # a good way to read back a weight this pass has already written.
    moves = []   # (vertex index, target group index, weight)
    strips = []  # (vertex index, source group index)
    for v in body.data.vertices:
        if v.co.x > -MIDLINE:
            continue
        for g in v.groups:
            if g.group in pairs and g.weight > 0.0:
                moves.append((v.index, pairs[g.group], g.weight))
                strips.append((v.index, g.group))

    # Add before removing, so a vertex is never momentarily unweighted.
    existing = {}
    for v in body.data.vertices:
        for g in v.groups:
            existing[(v.index, g.group)] = g.weight

    for vertex_index, target, weight in moves:
        total = existing.get((vertex_index, target), 0.0) + weight
        groups[target].add([vertex_index], total, 'REPLACE')
    for vertex_index, source in strips:
        groups[source].remove([vertex_index])

    # Match what the rig was authored to. Unity clamps to four influences anyway; doing it here
    # means Blender drops the smallest weights and renormalises, rather than Unity dropping
    # whichever four it happens to keep.
    # Both operators act on the SELECTED objects; making the body merely active is not enough and
    # fails silently, leaving vertices on five influences for Unity to clamp arbitrarily.
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=MAX_INFLUENCES)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

    after_counts = weighted(None)

    # --- verify -------------------------------------------------------------------------------
    assert all((a - b).length == 0 for a, b in zip(positions_before, (v.co for v in body.data.vertices))), \
        "the mesh moved"

    empty = sorted(n for n, c in after_counts.items() if c == 0)
    loose = sum(1 for v in body.data.vertices if sum(g.weight for g in v.groups) < 0.99)
    max_influences = max(len(v.groups) for v in body.data.vertices)

    # Every side pair should now hold a comparable number of vertices. The body is not perfectly
    # symmetric, so this is a sanity bound, not an equality.
    lopsided = []
    for right_index, left_index in pairs.items():
        r = after_counts[index_to_name[right_index]]
        l = after_counts[index_to_name[left_index]]
        if r + l > 0 and abs(r - l) / max(r, l) > 0.25:
            lopsided.append((index_to_name[right_index], r, index_to_name[left_index], l))

    report = {
        "vertices": len(body.data.vertices),
        "vertices_remapped": len({m[0] for m in moves}),
        "weights_moved": len(moves),
        "empty_groups_before": sorted(n for n, c in before_counts.items() if c == 0),
        "empty_groups_after": empty,
        "loose_verts": loose,
        "max_influences": max_influences,
        "lopsided_pairs": lopsided,
        "sample": {n: [before_counts[n], after_counts[n]] for n in
                   ("LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot",
                    "LeftHand", "RightHand", "LeftUpperArm", "RightUpperArm",
                    "LeftUpperLeg", "RightUpperLeg", "Hips", "Spine", "Head")},
    }
    with open(report_path, "w") as f:
        json.dump(report, f, indent=1)

    assert not empty, f"still empty after the remap: {empty}"
    assert loose == 0, f"{loose} vertices are not fully weighted"
    assert max_influences <= MAX_INFLUENCES

    bpy.ops.wm.save_as_mainfile(filepath=out_path)
    print(f"MIRRORED {report['weights_moved']} weights on {report['vertices_remapped']} vertices; "
          f"empty groups {len(report['empty_groups_before'])} -> {len(empty)}; "
          f"lopsided pairs {len(lopsided)}")


main()
