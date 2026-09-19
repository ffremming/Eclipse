"""Export boomerang.blend to the FBX Unity consumes.

Meant to be re-run -- it is an export, not a generator, and it never writes to
the .blend it opens.

The model is one crescent mesh, lying flat: its thin axis is Blender Z, so it
arrives in Unity lying in the XZ plane with its face up. The source is 3 m
across and the boomerang ships at about half a metre; that scaling is the
builder's job (`BoomerangBuilder`), because it measures the imported bounds
and a hard-coded factor here would be a second place to change it.

The one thing done here is naming. The artist's `Sphere` and `Material.001`
say nothing about what they are, and the builder picks the glowing material for
the mesh by name.

    blender --background --python boomerang_export.py
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "..")))

from _exportlib import export, rename, unity_path

SOURCE = os.path.join(HERE, "boomerang.blend")
DESTINATION = unity_path("Weapons", "Boomerang", "boomerang.fbx")


def prepare():
    rename(objects={"Sphere": "Boomerang"}, materials={"Material.001": "Boomerang_Glow"})


def run(dst=DESTINATION):
    export(SOURCE, dst, prepare=prepare)


if __name__ == "__main__":
    run()
