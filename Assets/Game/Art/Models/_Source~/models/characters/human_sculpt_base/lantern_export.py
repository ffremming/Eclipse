"""Export lantern.blend to the FBX Unity consumes.

Meant to be re-run -- it is an export, not a generator, and it never writes to
the .blend it opens.

Three meshes: the glass, the frame round the top of it with the carrying loop,
and the base with its spike. The glass is the part that lights up, so it is
named for what it is and the two frame pieces are told apart from it only by
name -- `LanternBuilder` gives the glass the warm emissive material and
everything else the dark one.

The model stands 9 units tall with its origin below the base. Scaling it to a
carried size and hanging it from the loop is the builder's job, which measures
the imported bounds rather than trusting a factor written down here.

    blender --background --python lantern_export.py
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "..")))

from _exportlib import export, rename, unity_path

SOURCE = os.path.join(HERE, "lantern.blend")
DESTINATION = unity_path("Items", "Lantern", "lantern.fbx")


def prepare():
    rename(
        objects={
            "Cylinder": "Glass",
            "Cylinder.001": "Frame_Top",
            "Cylinder.002": "Frame_Base",
        },
        materials={
            "Material.001": "Lantern_Glass",
            "Material.002": "Lantern_Frame",
        },
    )


def run(dst=DESTINATION):
    export(SOURCE, dst, prepare=prepare)


if __name__ == "__main__":
    run()
