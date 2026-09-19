"""Rig the finished human sculpt with a Unity-Humanoid skeleton, into a new file.

Historical record, not source of truth: once human_sculpt_base_rigged.blend exists and is being
worked on, never re-run this over it. It only ever reads human_sculpt_base_clean.blend.

    blender --background --python human_sculpt_base_rigged.py

The mesh is not touched: no vertex moves, no face is added or removed. What is added is an armature,
vertex groups (the skin weights) on the body, and a bone parent on each eye.

Bone names are Unity's HumanBodyBones names without spaces, so the Humanoid mapping is unambiguous.

The sculpt faces +Y; the library convention is -Y, and an FBX carries no choice in the matter (its
axis flags only declare the file's axes, so a +Y character arrives in Unity facing -Z). The bones
are therefore built where the sculpt is, and the rig object is then turned 180 degrees about Z. The
mesh and eyes follow it as children; their vertex data is not touched.
"""
import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "human_sculpt_base_clean.blend")
OUT = os.path.join(HERE, "human_sculpt_base_rigged.blend")
BODY = "Mesh_HumanSculptBase"
EYES = ("Sphere", "Sphere.001")
RIG_NAME = "Human_Rig"
MAX_INFLUENCES = 4       # Unity skins with four bones a vertex by default
MIN_WEIGHT_SUM = 0.99    # every vertex must be fully claimed by some bone

# --- centre-line bones: (name, parent, head, tail) ---------------------------------------------
CENTRE = [
    # Not a Humanoid bone: a ground-level pivot above the hips, like the goblin's Root_Jnt. The game
    # measures "the body stands on its own pivot" from the skinned mesh's root bone.
    ("Root", None, (0.0, 0.0, 0.0), (0.0, 0.0, 0.150)),
    ("Hips", "Root", (0.0, -0.010, 0.870), (0.0, -0.016, 0.975)),
    ("Spine", "Hips", (0.0, -0.016, 0.975), (0.0, -0.030, 1.190)),
    ("Chest", "Spine", (0.0, -0.030, 1.190), (0.0, -0.048, 1.450)),
    ("Neck", "Chest", (0.0, -0.048, 1.450), (0.0, -0.040, 1.630)),
    ("Head", "Neck", (0.0, -0.040, 1.630), (0.0, 0.005, 1.895)),
]

# --- right side (+X); the left is the mirror image ---------------------------------------------
ARM = [
    ("Shoulder", "Chest", (0.030, -0.035, 1.470), (0.210, -0.085, 1.430)),
    ("UpperArm", "Shoulder", (0.210, -0.085, 1.430), (0.368, -0.078, 1.165)),
    ("LowerArm", "UpperArm", (0.368, -0.078, 1.165), (0.477, -0.044, 0.955)),
    ("Hand", "LowerArm", (0.477, -0.044, 0.955), (0.505, -0.030, 0.800)),
]
# Each finger is four joint positions: base, then the ends of the proximal, intermediate, distal bones.
FINGERS = {
    "Thumb": [(0.474, 0.020, 0.870), (0.472, 0.030, 0.800), (0.470, 0.045, 0.760), (0.470, 0.055, 0.730)],
    "Index": [(0.507, 0.010, 0.800), (0.507, 0.012, 0.760), (0.505, 0.014, 0.715), (0.503, 0.015, 0.675)],
    "Middle": [(0.512, -0.030, 0.800), (0.512, -0.030, 0.755), (0.510, -0.032, 0.710), (0.509, -0.033, 0.665)],
    "Ring": [(0.505, -0.063, 0.800), (0.503, -0.064, 0.755), (0.501, -0.065, 0.715), (0.501, -0.065, 0.682)],
    "Little": [(0.484, -0.099, 0.800), (0.480, -0.100, 0.765), (0.476, -0.101, 0.742), (0.474, -0.102, 0.723)],
}
FINGER_JOINTS = ("Proximal", "Intermediate", "Distal")
LEG = [
    ("UpperLeg", "Hips", (0.095, -0.005, 0.840), (0.150, -0.030, 0.500)),
    ("LowerLeg", "UpperLeg", (0.150, -0.030, 0.500), (0.165, -0.055, 0.190)),
    ("Foot", "LowerLeg", (0.165, -0.055, 0.190), (0.170, 0.120, 0.035)),
    ("Toes", "Foot", (0.170, 0.120, 0.035), (0.160, 0.215, 0.015)),
]


def sided_bones():
    """Every bone, as (name, parent, head, tail), for both sides."""
    bones = list(CENTRE)
    for side, sign in (("Right", 1), ("Left", -1)):
        def flip(p):
            return (p[0] * sign, p[1], p[2])
        for name, parent, head, tail in ARM + LEG:
            bones.append((side + name, side + parent if parent not in ("Chest", "Hips") else parent, flip(head), flip(tail)))
        for finger, joints in FINGERS.items():
            for i, joint in enumerate(FINGER_JOINTS):
                parent = side + "Hand" if i == 0 else f"{side}{finger}{FINGER_JOINTS[i - 1]}"
                bones.append((f"{side}{finger}{joint}", parent, flip(joints[i]), flip(joints[i + 1])))
    return bones


def build_armature():
    arm_data = bpy.data.armatures.new(RIG_NAME)
    arm = bpy.data.objects.new(RIG_NAME, arm_data)
    bpy.context.scene.collection.children[0].objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    edit = arm_data.edit_bones
    for name, parent, head, tail in sided_bones():
        bone = edit.new(name)
        bone.head, bone.tail = Vector(head), Vector(tail)
        if parent:
            bone.parent = edit[parent]
            bone.use_connect = (Vector(head) - edit[parent].tail).length < 1e-6
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def skin_body(arm, body):
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    body.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=MAX_INFLUENCES)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)


def attach_eyes(arm):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    arm.data.bones.active = arm.data.bones["Head"]
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    arm.select_set(True)
    for name in EYES:
        bpy.data.objects[name].select_set(True)
    bpy.ops.object.parent_set(type='BONE')
    bpy.ops.object.mode_set(mode='OBJECT')


def verify(arm, body):
    bone_names = {b.name for b in arm.data.bones}
    weighted = {g.name for g in body.vertex_groups}
    assert weighted <= bone_names, f"vertex groups without a bone: {weighted - bone_names}"
    loose = 0
    for v in body.data.vertices:
        total = sum(g.weight for g in v.groups)
        if total < MIN_WEIGHT_SUM:
            loose += 1
    assert loose == 0, f"{loose} vertices are not fully weighted"
    assert max(len(v.groups) for v in body.data.vertices) <= MAX_INFLUENCES
    for name in EYES:
        eye = bpy.data.objects[name]
        assert eye.parent == arm and eye.parent_bone == "Head", f"{name} is not on the Head bone"
    print("bones", len(bone_names), "weighted groups", len(weighted), "vertices", len(body.data.vertices))


def main():
    if os.path.exists(OUT):
        raise SystemExit(f"Refusing to overwrite existing file: {OUT}\nThe .blend is the source of truth.")
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    body = bpy.data.objects[BODY]
    positions_before = [v.co.copy() for v in body.data.vertices]
    arm = build_armature()
    skin_body(arm, body)
    attach_eyes(arm)
    arm.rotation_euler.z = math.pi
    bpy.context.view_layer.update()
    assert all((a - b).length == 0 for a, b in zip(positions_before, (v.co for v in body.data.vertices))), "the mesh moved"
    verify(arm, body)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)


main()
