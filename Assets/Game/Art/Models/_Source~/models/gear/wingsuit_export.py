"""Ship the wingsuit to Unity.

Exports the whole model file like `grapple_bracer_export.py`. No rig
(`keep_armature=False`): the .blend's bones exist for posing in Blender, but in
the game `WingsuitArtifact` swings the three hinged parts about their own
origins, so the FBX ships them as plain objects sitting on their pins.

Exports are meant to be re-run; this only ever reads the .blend.

    blender --background --python models/gear/wingsuit_export.py
"""

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(HERE)))

from _exportlib import export, unity_path  # noqa: E402

SRC = os.path.join(HERE, "wingsuit.blend")
DST = unity_path("Items", "wingsuit.fbx")


def main():
    export(SRC, DST, keep_armature=False)

    # The builder binds the three hinged parts by name and rotates them about
    # their origins, so the origins have to be the pins. Printed in both frames:
    # Blender (x, y, z) arrives in Unity as (-x, z, -y) — measured off the
    # imported file, X included, which the grapple bracer's note leaves out.
    for obj in sorted((o for o in bpy.data.objects if o.type == 'MESH'),
                      key=lambda o: o.name):
        b = obj.location
        print("  PIVOT %-30s blender (%.4f, %.4f, %.4f)  unity (%.4f, %.4f, %.4f)"
              % (obj.name, b.x, b.y, b.z, -b.x, b.z, -b.y))


main()
