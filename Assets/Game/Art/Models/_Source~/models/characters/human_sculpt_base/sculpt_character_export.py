"""Export a rigged sculpt-base character to Unity: <Name>.fbx and its base-colour texture.

Exports are the one kind of script here that is meant to be re-run: it only ever reads the .blend.

    blender --background --python models/characters/human_sculpt_base/sculpt_character_export.py -- human
    blender --background --python models/characters/human_sculpt_base/sculpt_character_export.py -- alien crumpy

With no names it exports every character in CHARACTERS.

One script for all of them because they are one model: the alien and the crumpy are the human sculpt
pushed around, rigged by the same `human_sculpt_base_rigged.py` skeleton, with the same object and
material names inside. Three copies of these export flags is three chances for one character to
arrive in Unity at a different scale or facing a different way from its siblings.

Flags follow `_exportlib` (see its docstring for why each is load-bearing). The axis flags only
declare the file's axes, so which way the character faces in Unity is decided in Blender: the rigged
files have them facing -Y, which arrives facing +Z with the right hand on +X.

One deliberate departure from `_exportlib`: the scale option is `FBX_SCALE_ALL`, not
`FBX_SCALE_NONE`. That is right for static props but gives every bone of a rig a scale of 100 in
Unity, so anything parented to a hand (a held weapon, a trail anchor) inherits it. Measured on this
model: `FBX_SCALE_NONE` -> bones at 100x, `FBX_SCALE_ALL` -> bones at 1x, same world size.

The FBX carries the skeleton, the skin weights and both eyes. Materials are named for what they are
here rather than what Blender called them, and only the texture is written out: the Unity materials
are built on the project's Lit shader, which the FBX cannot carry.

The rig arrives as Humanoid, and for the crumpy that takes help. Unity's automatic humanoid mapper
guesses the bone mapping from the shape of the skeleton, and on the crumpy's refitted legs it gives
up with "Required human bone 'LeftLowerLeg' not found" — the character then imports as Generic,
which costs it the shared Creature clips and, silently, the hand `EnemyGear` seats its blade into.
So `Crumpy.fbx.meta` carries an EXPLICIT bone mapping (`humanDescription.human` and `.skeleton`)
instead of an empty one, which is Unity's own answer for a body the mapper cannot read. That mapping
is by bone NAME, so it survives re-running this script; what would break it is renaming a bone in
`human_sculpt_base_rigged.py`, or deleting the .meta and letting Unity write a fresh one. The
`EnemyWeaponSeatingTests` are what catch it either way.
"""
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(HERE))))

from _exportlib import unity_path  # noqa: E402

# name -> (source .blend, folder and file stem under Assets/Game/Art/Models/Characters/)
CHARACTERS = {
    "human": ("human_sculpt_base_rigged.blend", "Human"),
    "alien": ("alien_rigged.blend", "Alien"),
    "crumpy": ("crumpy_rigged.blend", "Crumpy"),
}

BODY_IMAGE = "Material Base Color"


def export_texture(asset_name):
    image = bpy.data.images[BODY_IMAGE]
    path = unity_path("Characters", asset_name, "Textures", f"{asset_name}_BaseColor.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = 'PNG'
    image.save()
    return path


def export(name):
    source_file, asset_name = CHARACTERS[name]
    source = os.path.join(HERE, source_file)
    if not os.path.exists(source):
        raise SystemExit(f"No rigged model at {source}")

    bpy.ops.wm.open_mainfile(filepath=source)

    # Blender's own names ("Material", "Material.001") say nothing about which slot is skin and
    # which is eye, and the Unity material assignment is made by name.
    for old, new in (("Material", f"{asset_name}_Body"), ("Material.001", f"{asset_name}_Eyes")):
        bpy.data.materials[old].name = new

    texture = export_texture(asset_name)
    fbx = unity_path("Characters", asset_name, f"{asset_name}.fbx")
    os.makedirs(os.path.dirname(fbx), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=fbx,
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
    print(f"EXPORTED {fbx} ({os.path.getsize(fbx) / 1e6:.1f} MB) and {texture}")
    # Deliberately no save: the .blend is the source of truth and its materials were renamed in memory.


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(CHARACTERS)

    unknown = [name for name in names if name not in CHARACTERS]
    if unknown:
        raise SystemExit(f"Not a sculpt-base character: {', '.join(unknown)}. "
                         f"Known: {', '.join(CHARACTERS)}")

    for name in names:
        export(name)


main()
