"""Import the human sculpting base mesh into a clean .blend.

Historical record, not source of truth: once the .blend is being sculpted, never re-run this over it.

    blender --background --python human_sculpt_base.py

Reads human_sculpt_base_source.obj beside this script and writes human_sculpt_base.blend beside it.
"""
import os

import bmesh
import bpy
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE_OBJ = os.path.join(HERE, "human_sculpt_base_source.obj")
OUT = os.path.join(HERE, "human_sculpt_base.blend")

HEIGHT_M = 1.8
NAME = "HumanSculptBase"

if os.path.exists(OUT):
    raise SystemExit(
        f"Refusing to overwrite existing file: {OUT}\n"
        "The .blend is the source of truth. Edit it via MCP instead of regenerating."
    )

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

bpy.ops.wm.obj_import(filepath=SOURCE_OBJ)
imported = bpy.context.selected_objects
assert len(imported) == 1, f"expected one object in {SOURCE_OBJ}, got {len(imported)}"
obj = imported[0]
mesh = obj.data

# The importer leaves the Y-up -> Z-up rotation on the object; bake it into the mesh so the
# object transform is identity, then scale to a real-world height and stand the feet on Z=0.
mesh.transform(obj.matrix_world)
obj.matrix_world.identity()
zs = [v.co.z for v in mesh.vertices]
mesh.transform(Matrix.Scale(HEIGHT_M / (max(zs) - min(zs)), 4))
mesh.transform(Matrix.Translation((0.0, 0.0, -min(v.co.z for v in mesh.vertices))))
mesh.update()

# The OBJ has no .mtl, so the importer invents a 'default' material. Materials belong to the
# palette, and a sculpt base has none of its own.
mesh.materials.clear()
for material in [m for m in bpy.data.materials if m.users == 0]:
    bpy.data.materials.remove(material)

obj.name = f"Mesh_{NAME}"
mesh.name = f"Mesh_{NAME}"

for coll in list(obj.users_collection):
    coll.objects.unlink(obj)
collection = bpy.data.collections.new(f"Coll_{NAME}")
scene.collection.children.link(collection)
collection.objects.link(obj)

bm = bmesh.new()
bm.from_mesh(mesh)
signed_volume = bm.calc_volume(signed=True)
bm.free()
assert signed_volume > 0, "normals point inward"

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
