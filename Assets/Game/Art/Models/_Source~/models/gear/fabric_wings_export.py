"""Ship the fabric wings to Unity — WITH their rig.

Unlike the harness wingsuit, the membranes are skinned: the bones are what make
them wings, so `keep_armature=True` and Unity gets SkinnedMeshRenderers bound to
the Nomad's bone names. FabricWingsArtifact re-binds those to the wearer.

Exports are meant to be re-run; this only ever reads the .blend.

    blender --background --python models/gear/fabric_wings_export.py
"""

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(HERE)))

from _exportlib import export, unity_path  # noqa: E402

SRC = os.path.join(HERE, "fabric_wings.blend")
DST = unity_path("Items", "fabric_wings.fbx")


def main():
    export(SRC, DST, keep_armature=True)

    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            print("  %-24s verts=%d groups=%s" % (obj.name, len(obj.data.vertices),
                                                 [g.name for g in obj.vertex_groups]))


main()
