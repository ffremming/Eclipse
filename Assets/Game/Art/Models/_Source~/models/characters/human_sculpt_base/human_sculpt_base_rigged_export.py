"""Export the rigged human to Unity: Human.fbx and its base-colour texture.

Exports are the one kind of script here that is meant to be re-run: it only ever reads the .blend.

    blender --background --python models/characters/human_sculpt_base/human_sculpt_base_rigged_export.py

Flags follow `_exportlib` (see its docstring for why each is load-bearing). The axis flags only
declare the file's axes, so which way the character faces in Unity is decided in Blender: the rigged
file has it facing -Y, which arrives facing +Z with its right hand on +X.

One deliberate departure from `_exportlib`: the scale option is `FBX_SCALE_ALL`, not
`FBX_SCALE_NONE`. That is right for static props but gives every bone of a rig a scale of 100 in
Unity, so anything parented to a hand (a held item, a trail anchor) inherits it. Measured on this
model: `FBX_SCALE_NONE` -> bones at 100x, `FBX_SCALE_ALL` -> bones at 1x, same world size.

The FBX carries the skeleton, the skin weights and both eyes. Materials are named for what they are
here rather than what Blender called them, and only the texture is written out: the Unity materials
are built on the project's Lit shader, which the FBX cannot carry.
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(HERE))))

from _exportlib import unity_path  # noqa: E402

SOURCE = os.path.join(HERE, "human_sculpt_base_rigged.blend")
FBX = unity_path("Characters", "Human", "Human.fbx")
TEXTURE = unity_path("Characters", "Human", "Textures", "Human_BaseColor.png")
BODY_IMAGE = "Material Base Color"
MATERIAL_NAMES = {"Material": "Human_Body", "Material.001": "Human_Eyes"}


def export_texture():
    image = bpy.data.images[BODY_IMAGE]
    os.makedirs(os.path.dirname(TEXTURE), exist_ok=True)
    image.filepath_raw = TEXTURE
    image.file_format = 'PNG'
    image.save()


def main():
    if not os.path.exists(SOURCE):
        raise SystemExit(f"No rigged model at {SOURCE}")
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    for old, new in MATERIAL_NAMES.items():
        bpy.data.materials[old].name = new
    export_texture()
    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX,
        use_selection=False,
        object_types={'ARMATURE', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        armature_nodetype='NULL',
        bake_space_transform=False,
        path_mode='STRIP',
        embed_textures=False,
    )
    print(f"EXPORTED {FBX} ({os.path.getsize(FBX) / 1e6:.1f} MB) and {TEXTURE}")
    # Deliberately no save: the .blend is the source of truth and its materials were renamed in memory.


main()
