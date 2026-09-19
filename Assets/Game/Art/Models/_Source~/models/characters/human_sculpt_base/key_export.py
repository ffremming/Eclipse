"""Export the castle's two keys to Unity, at a size that fits a hand.

    blender --background tower_key.blend --python key_export.py -- TowerKey
    blender --background wall_key.blend  --python key_export.py -- WallKey

Both keys are modelled large — the tower key is 15.7 units long and the wall key 24.3 — which is
the right way to model something small, because it is the only way to sculpt detail into it. What
Unity needs is a key the player can hold, so this normalises each one to `KEY_LENGTH` metres on its
longest axis rather than trusting whatever scale it was left at. Setting the scale on the Unity
importer instead would work exactly as well right up until the next key is modelled at a third
size, and then it is a number in the Inspector that nobody knows the provenance of.

The key is also re-origined to the middle of its own body. A key modelled around an origin that is
somewhere off in space arrives in Unity spinning around a point outside itself, and every pickup
and hand-grip offset after that is a correction for it.

Nothing here writes back to the .blend.
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector

LIB_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
sys.path.insert(0, LIB_ROOT)
from _exportlib import repo_root  # noqa: E402

OUT_FOLDER = "Assets/Game/Art/Models/Items/Keys"

# Long enough to read as a key in the hand and on the ground, short enough not to look like a
# weapon. The light artifacts are 0.7 to 1.1 m, so a key wants to be clearly smaller than those.
KEY_LENGTH = 0.30


def only_mesh():
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if len(meshes) != 1:
        raise SystemExit("Expected one mesh in %s, found %d: %s"
                         % (bpy.data.filepath, len(meshes), [o.name for o in meshes]))
    return meshes[0]


def normalise(obj, name):
    """Rename, centre and scale the key so it is `KEY_LENGTH` metres long about its own middle.

    Done on the mesh data rather than through `bpy.ops`, which needs a window's context and does
    not have one under `--background` for a file whose object is not in the active view layer.
    Baking the object's own matrix in as well means the exported key does not depend on whatever
    transform it happened to be left at in the .blend.
    """
    depsgraph = bpy.context.evaluated_depsgraph_get()

    # Modifiers baked first, so the bounds measured below are the bounds that get exported.
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    obj.modifiers.clear()
    obj.data = baked

    obj.name = name
    baked.name = name

    # Into world space, so the object's own transform stops mattering.
    baked.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)

    low = Vector((min(v.co[i] for v in baked.vertices) for i in range(3)))
    high = Vector((max(v.co[i] for v in baked.vertices) for i in range(3)))

    size = high - low
    longest = max(size)
    if longest <= 0.0:
        raise SystemExit("%s has no size to scale" % name)

    scale = KEY_LENGTH / longest
    centre = (low + high) * 0.5

    baked.transform(Matrix.Diagonal((scale, scale, scale, 1.0)) @ Matrix.Translation(-centre))

    print("  %s: %.3f units long -> %s m, centred on itself"
          % (name, longest, tuple(round(c * scale, 3) for c in size)))
    return obj


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) != 1:
        raise SystemExit("Usage: ... --python key_export.py -- <UnityName>")

    name = argv[0]
    normalise(only_mesh(), name)

    path = os.path.join(repo_root(), OUT_FOLDER, name + ".fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={'MESH'},
        apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        bake_space_transform=False,
        path_mode='COPY',
        embed_textures=False,
    )
    print("  wrote %s (%.0f KB)" % (path, os.path.getsize(path) / 1e3))


main()
