"""Cut an entrance through the outer wall of castle.blend, then paint the castle.

castle.blend is hand-modelled, so this edits it in place and never regenerates it.
Everything it adds is new (a cutter object, Boolean modifiers, palette material
links); the only existing data it changes is the material slots of meshes that were
unpainted default grey.

    blender --background castle.blend --python castle_gate_and_colour.py -- --save

Without --save the edit is applied to the open scene and discarded, which is how the
look is iterated on.

The gate is a Boolean DIFFERENCE against one cutter box, added after each wall
course's Array modifier. Move or resize `Cutter_Gate_Entrance` in Blender and the
opening follows. The Boolean that was already on Cube.699 had no operand and is left
as it was.

Palette: Mat_Neutral_Black_Matte, Mat_Stone_Charcoal_Rough, Mat_Stone_Ash_Rough,
Mat_Paint_Warn_Red. Linked from palette.blend, never defined locally.
"""

import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

LIB_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
sys.path.insert(0, LIB_ROOT)
from _buildlib import link_materials  # noqa: E402

# --- Gate ------------------------------------------------------------------

GATE_NAME = "Cutter_Gate_Entrance"
GATE_COLLECTION = "Coll_Gate_Entrance"
GATE_MODIFIER = "Boolean_Gate"
GATE_WIDTH = 3.2  # metres along the wall
GATE_MARGIN = 1.0  # cutter overshoot past the wall on every side, so no sliver survives
OUTER_WALL_MIN_DIAMETER = 40.0  # only the ring wall is this wide; towers and keep are not

# --- Colour ----------------------------------------------------------------

BLACK = "Mat_Neutral_Black_Matte"
CHARCOAL = "Mat_Stone_Charcoal_Rough"
ASH = "Mat_Stone_Ash_Rough"
RED = "Mat_Paint_Warn_Red"

# Bottom to top. Dark plinth, alternating stone, a red cap.
WALL_COURSE_MATERIALS = (BLACK, BLACK, CHARCOAL, ASH, CHARCOAL, ASH, RED)

# Weighted pick for ordinary brick courses on towers and the keep.
BRICK_MATERIALS = (CHARCOAL, CHARCOAL, CHARCOAL, ASH, ASH, BLACK)

ROOF_TILT_MIN = 0.03  # radians; roof-tile arrays are the ones tilted off the vertical
BLOCK_MIN_HEIGHT = 0.7  # array objects taller than this are merlons and corbels, not bricks
RIM_MAX_HEIGHT = 0.7  # cylinders thinner than this are the gallery plates under battlements
MASSING_MIN_HEIGHT = 2.0  # plain boxes taller than this are buried keep massing, not steps
SPIRE_VERTEX_COUNT = 768  # the two tower meshes that carry a cone roof on top of the shaft
FOUNDATION_SLABS = ("Cube.523", "Cube.700")  # coincident slabs under the whole keep

# World Z where each spire mesh's cone roof begins, read off the mesh's radius profile.
SPIRE_ROOF_START_Z = {"Cylinder": 26.4, "Cylinder.018": 20.1}


def mesh_objects():
    return [o for o in bpy.data.objects if o.type == 'MESH']


def has_array(obj):
    return any(m.type == 'NODES' for m in obj.modifiers)


def outer_wall_courses():
    courses = [o for o in mesh_objects() if has_array(o) and o.dimensions.x > OUTER_WALL_MIN_DIAMETER]
    return sorted(courses, key=lambda o: o.location.z)


# --------------------------------------------------------------------------
# Gate
# --------------------------------------------------------------------------

def gate_extents(courses):
    """Where the ring wall crosses the -Y axis through its centre, from the evaluated bricks."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    centre_x = courses[0].location.x
    front_ys, all_zs = [], []
    for course in courses:
        evaluated = course.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        for vertex in mesh.vertices:
            world = evaluated.matrix_world @ vertex.co
            all_zs.append(world.z)
            if abs(world.x - centre_x) <= GATE_WIDTH / 2 and world.y < course.location.y:
                front_ys.append(world.y)
        evaluated.to_mesh_clear()
    return centre_x, min(front_ys), max(front_ys), min(all_zs), max(all_zs)


def make_cutter(centre, size):
    mesh = bpy.data.meshes.new(GATE_NAME)
    builder = bmesh.new()
    bmesh.ops.create_cube(builder, size=1.0)
    bmesh.ops.scale(builder, vec=size, verts=builder.verts)
    builder.to_mesh(mesh)
    builder.free()
    cutter = bpy.data.objects.new(GATE_NAME, mesh)
    cutter.location = centre
    cutter.display_type = 'WIRE'
    cutter.hide_render = True
    collection = bpy.data.collections.new(GATE_COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    collection.objects.link(cutter)
    return cutter


def cut_gate():
    if GATE_NAME in bpy.data.objects:
        print("Gate cutter already present; leaving the gate as it is")
        return
    courses = outer_wall_courses()
    if not courses:
        raise SystemExit("No outer wall courses found (array objects wider than %s m)" % OUTER_WALL_MIN_DIAMETER)
    centre_x, front_y, back_y, low_z, high_z = gate_extents(courses)
    size = Vector((
        GATE_WIDTH,
        (back_y - front_y) + 2 * GATE_MARGIN,
        (high_z - low_z) + 2 * GATE_MARGIN,
    ))
    centre = Vector((centre_x, (front_y + back_y) / 2, (low_z + high_z) / 2))
    cutter = make_cutter(centre, size)
    for course in courses:
        gate = course.modifiers.new(GATE_MODIFIER, 'BOOLEAN')
        gate.operation = 'DIFFERENCE'
        gate.solver = 'EXACT'
        gate.operand_type = 'OBJECT'
        gate.object = cutter
    print("Gate: %d courses cut, cutter at %s, size %s" % (len(courses), tuple(round(c, 2) for c in centre), tuple(round(s, 2) for s in size)))


# --------------------------------------------------------------------------
# Colour
# --------------------------------------------------------------------------

def spire_face_materials(obj):
    roof_start = SPIRE_ROOF_START_Z[obj.name]
    faces = []
    for poly in obj.data.polygons:
        lowest = min((obj.matrix_world @ obj.data.vertices[i].co).z for i in poly.vertices)
        faces.append(RED if lowest >= roof_start else CHARCOAL)
    return faces


def plain_material(obj, wall_courses):
    """One palette material name for an object that is painted a single colour."""
    if obj in wall_courses:
        return WALL_COURSE_MATERIALS[wall_courses.index(obj)]
    if has_array(obj):
        tilt = max(abs(obj.rotation_euler.x), abs(obj.rotation_euler.y))
        if tilt > ROOF_TILT_MIN:
            return RED
        if obj.dimensions.z > BLOCK_MIN_HEIGHT:
            return BLACK
        return random.Random(obj.name).choice(BRICK_MATERIALS)
    if obj.name.startswith("Plane"):
        return RED
    if obj.name.startswith("Cylinder"):
        return RED if obj.dimensions.z < RIM_MAX_HEIGHT else BLACK
    if obj.name in FOUNDATION_SLABS or obj.dimensions.z >= MASSING_MIN_HEIGHT:
        return BLACK
    return CHARCOAL


def paint(obj, materials, per_face=None):
    mesh = obj.data
    mesh.materials.clear()
    for material in materials:
        mesh.materials.append(material)
    if per_face is not None:
        for poly, material in zip(mesh.polygons, per_face):
            poly.material_index = materials.index(material)


def colour_castle():
    names = (BLACK, CHARCOAL, ASH, RED)
    palette = dict(zip(names, link_materials(list(names))))
    wall_courses = outer_wall_courses()
    for obj in mesh_objects():
        if obj.name in SPIRE_ROOF_START_Z:
            per_face = [palette[n] for n in spire_face_materials(obj)]
            paint(obj, [palette[CHARCOAL], palette[RED]], per_face)
        else:
            paint(obj, [palette[plain_material(obj, wall_courses)]])


# --------------------------------------------------------------------------
# Verify
# --------------------------------------------------------------------------

def verify_gate():
    courses = outer_wall_courses()
    cutter = bpy.data.objects[GATE_NAME]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    half = GATE_WIDTH / 2
    inside, before_after = 0, []
    for course in courses:
        evaluated = course.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        for vertex in mesh.vertices:
            world = evaluated.matrix_world @ vertex.co
            if abs(world.x - cutter.location.x) < half - 1e-3 and abs(world.y - cutter.location.y) < cutter.dimensions.y / 2:
                inside += 1
        before_after.append(len(mesh.vertices))
        evaluated.to_mesh_clear()
    print("Verify: %d vertices remain inside the gate opening; evaluated verts per course %s" % (inside, before_after))
    if inside:
        raise SystemExit("Gate is not clear")


def main():
    cut_gate()
    colour_castle()
    verify_gate()
    if "--save" in sys.argv:
        bpy.ops.file.make_paths_relative()  # link the palette by relative path so the repo stays portable
        bpy.ops.wm.save_mainfile()
        print("Saved %s" % bpy.data.filepath)


main()
