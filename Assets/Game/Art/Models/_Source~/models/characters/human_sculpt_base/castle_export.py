"""Export castle.blend to Unity as two playable castles, plus the anchors they hang gameplay on.

    blender --background castle.blend --python castle_export.py

Two castles come out of one model, because the game uses the castle twice:

  CastleKeep.fbx  the main building alone — the keep, its spire and the inner tower. The first
                  castle the player takes, six goblins, one locked door.
  CastleFull.fbx  the whole thing, ring wall and gatehouse included. Fifteen goblins, two locks.

The split is radial and falls out of the model itself rather than out of a hand-written object
list: the keep and its inner spire end at r=14, the courtyard watchtower stands at r=25 and the
ring wall at r=31, and NOTHING sits between r=15 and r=24 — that gap is the courtyard. So `KEEP_RADIUS` anywhere in the gap separates the two, and re-running this
after the castle is edited re-derives the split instead of going stale. A model change that fills
the courtyard would break that assumption, so it is asserted rather than assumed.

Both versions are joined down to one mesh per version, keeping their material slots as submeshes.
877 objects is right for a model being built and wrong for a scene: as separate objects it is 877
transforms and 877 draw calls, and Unity has to be handed the joined form because nothing
downstream can undo that.

The doors are added here rather than modelled, because the castle has none — no gate leaf in the
cut opening, and no doorway at all into the keep. They stay separate objects: the game moves them,
lights them and hangs an interaction on each, none of which survives being joined into the walls.

Ground is z=0 in this file, which is what lets the Unity side sit the castle on flat terrain and
have the outer wall meet it. The wall's lowest course starts at z=-0.17, so it buries its own foot.

Nothing here writes back to castle.blend. The .blend is the source of truth; this is a view of it.
"""

import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

LIB_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
sys.path.insert(0, LIB_ROOT)
from _buildlib import link_materials  # noqa: E402
from _exportlib import repo_root  # noqa: E402

# --- The split ------------------------------------------------------------

# Anywhere in the empty courtyard band. Checked against the model, not trusted.
#
# The band is r=15..24: the keep and its inner spire end at r=14, and the next thing outwards is
# the courtyard watchtower at r=25. The ring wall itself is out at r=31.
KEEP_RADIUS = 20.0
COURTYARD_GAP = (15.0, 24.0)  # must contain KEEP_RADIUS and hold no geometry

# An object whose footprint is wider than this wraps the whole castle, so it is a ring wall course
# however close to the middle its bounding box happens to be centred.
#
# This rule is what the split actually turns on, and leaving it out is what made the "keep alone"
# castle 86 m across. Every course of the ring wall is ONE brick with an Array modifier bending it
# round the perimeter, so the evaluated bounding box of a course is the whole 61 m ring and its
# CENTRE is the middle of the courtyard — radius nearly zero. Classified by centre alone, all ten
# courses of the outer wall came out as parts of the keep.
RING_MIN_SPAN = 40.0

# --- Doors ----------------------------------------------------------------

DOOR_THICKNESS = 0.28

# The gateway cut through the ring wall, and the leaf that fills it. A real opening: the player
# walks through here, so it has to clear a 1.24 m capsule with room to spare and read as a gate
# from across the courtyard.
GATEWAY_WIDTH = 3.6

# Kept under the height of the wall it goes through. The curtain is only ~2.5 m tall here and the
# gatehouse around the opening reaches ~3.5 m, so a taller cut stops being a gateway and becomes a
# notch out of the skyline with a slab standing in it — which is what 4.5 m looked like.
GATEWAY_HEIGHT = 3.0
GATEWAY_DEPTH = 9.0             # through BOTH wall rings, with overshoot at each end
GATE_LEAF_SIZE = (GATEWAY_WIDTH, GATEWAY_HEIGHT)
GATEWAY_SILL = 1.0              # how far below ground the cut reaches, so it leaves no threshold

GATEWAY_CUTTER = "Cutter_Gateway"
GATEWAY_MODIFIER = "Boolean_Gateway"
KEEP_DOOR_SIZE = (2.6, 3.4)     # width, height of the keep's door
KEEP_DOOR_FRAME = 0.35          # how far the surround stands proud of the leaf on each side

# The keep has no doorway, so its door is a sealed portal: a frame and a leaf standing against the
# south face. That is not a compromise, it is the mechanic — the keep is entered by being carried to
# the top of the tower, never by walking through this door, so a door that opens would be a lie.

GATE_CUTTER = "Cutter_Gate_Entrance"

# --- Tower ----------------------------------------------------------------

TOWER_CENTRE = (-1.40, -0.84)   # the main spire's axis, from the model

# The lantern deck is BUILT here, not found. The tower has merlons round its head and a cone roof
# over it, but nothing between them to stand on — probing the model for a floor turns up the tops
# of the merlons and the cap of the shaft, with gaps between them and no headroom over either. A
# player carried to "the top of the tower" would arrive in the air and fall thirty metres.
#
# So the lighthouse gets the one thing a lighthouse must have and this castle was never modelled
# with: a gallery around the lamp. It is a disc rather than a ring because the core passes above
# it rather than through it, so there is nothing for a hole to clear.
# On TOP of the tower's head, not inside it. The shaft is a solid drum up to z=23.94 out to
# r=2.98 — a deck cut into that is a floor buried in stone, which probes as a floor and plays as
# a fall. The cone roof starts at 26.4, leaving 2.3 m of headroom over this, and the player's
# capsule is 1.24 m.
DECK_TOP_Z = 24.10
DECK_THICKNESS = 0.28
DECK_RADIUS = 3.0               # out to the inside of the merlons
DECK_SEGMENTS = 32

# The core the player strikes, standing proud of the new floor.
BEACON_HEIGHT = 1.8
BEACON_RADIUS = 0.62            # the shaft's own thickness at this height

# Where the player is put down: far enough from the core to see it, inside the rail.
DECK_STAND_RADIUS = 1.9

# The rail round the gallery. Posts rather than a solid wall, both because it matches the castle's
# blockwork and because a 24 m drop off an unguarded disc is the sort of thing a player finds once.
RAIL_POSTS = 20
RAIL_HEIGHT = 0.95
RAIL_POST = 0.34

# --- Output ---------------------------------------------------------------

OUT_FOLDER = "Assets/Game/Art/Models/World/Castle"
ANCHOR_FILE = "Assets/Game/Art/Models/World/Castle/CastleAnchors.json"

PALETTE_BLACK = "Mat_Neutral_Black_Matte"
PALETTE_RED = "Mat_Paint_Warn_Red"


# --------------------------------------------------------------------------
# Geometry helpers
# --------------------------------------------------------------------------

def mesh_objects():
    return [o for o in bpy.data.objects if o.type == 'MESH']


def evaluated_bounds(obj, depsgraph):
    """World-space min/max of an object AFTER its modifiers, or None if it has no geometry.

    After, not before: every wall course is a single brick with an Array modifier, so the
    pre-modifier bounds of the entire ring wall are one brick at the origin.
    """
    evaluated = obj.evaluated_get(depsgraph)
    try:
        mesh = evaluated.to_mesh()
    except RuntimeError:
        return None
    if not mesh.vertices:
        evaluated.to_mesh_clear()
        return None

    matrix = evaluated.matrix_world
    points = [matrix @ vertex.co for vertex in mesh.vertices]
    evaluated.to_mesh_clear()

    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def footprint_radius(low, high):
    """Distance from the castle's axis to the centre of a bounding box, on the ground plane."""
    return math.hypot((low.x + high.x) * 0.5, (low.y + high.y) * 0.5)


def footprint_span(low, high):
    """The wider of a bounding box's two ground dimensions."""
    return max(high.x - low.x, high.y - low.y)


def belongs_to_keep(low, high):
    """Whether this object is part of the base building rather than the ring wall around it.

    Two tests, and the span one has to come first — see RING_MIN_SPAN.
    """
    if footprint_span(low, high) > RING_MIN_SPAN:
        return False
    return footprint_radius(low, high) < KEEP_RADIUS


def box(name, centre, size, material):
    """An axis-aligned box as its own object, carrying one palette material."""
    mesh = bpy.data.meshes.new(name)
    builder = bmesh.new()
    bmesh.ops.create_cube(builder, size=1.0)
    bmesh.ops.scale(builder, vec=Vector(size), verts=builder.verts)
    bmesh.ops.recalc_face_normals(builder, faces=builder.faces)
    builder.to_mesh(mesh)
    builder.free()

    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(centre)
    bpy.context.scene.collection.objects.link(obj)
    return obj


# --------------------------------------------------------------------------
# Anchors
# --------------------------------------------------------------------------

def blender_to_unity(point):
    """Blender's Z-up right-handed metres as Unity's Y-up left-handed ones.

    The mapping is (x, y, z) -> (-x, z, -y). The X flip is the part that is easy to miss and it is
    not optional: going from a right-handed space to a left-handed one has to reverse a handedness
    somewhere, and with axis_forward='-Z', axis_up='Y' the FBX pipeline reverses X. Measured off
    the model rather than reasoned about — the lantern deck is built centred on Blender (-1.40,
    -0.84) and arrives in Unity centred on (+1.40, +0.84).

    It was wrong in exactly the way that hides: the keep's door sits on the castle's centreline at
    x=0, where the flip changes nothing, so the door lined up perfectly and everything looked
    correct. The outer gate at x=1.17 was 2.3 m sideways of its own gateway, and the tower's
    arrival spot was on the wrong side of the beacon — standing in it rather than beside it.
    """
    return {"x": round(-point[0], 4), "y": round(point[2], 4), "z": round(-point[1], 4)}


def keep_face_y(depsgraph):
    """World Y of the keep's south face, found by walking a ray at it from the courtyard.

    Cast rather than read off a named object: the keep is 521 objects and which of them the south
    wall happens to be is not something a builder should hard-code.
    """
    scene = bpy.context.scene
    for x in (0.0, -1.0, 1.0):
        hit = scene.ray_cast(depsgraph, Vector((x, -14.0, 1.4)), Vector((0.0, 1.0, 0.0)), distance=10.0)
        if hit[0]:
            return hit[1].y
    raise SystemExit("Found no south face on the keep to stand its door against.")


def cylinder(name, centre, radius, depth, material, segments=DECK_SEGMENTS):
    """A capped cylinder as its own object, carrying one palette material."""
    mesh = bpy.data.meshes.new(name)
    builder = bmesh.new()
    bmesh.ops.create_cone(builder, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=radius, radius2=radius, depth=depth)

    # create_cone leaves the caps facing inwards. Unity's raycasts ignore back faces by default
    # (Physics.queriesHitBackfaces is off), so an inside-out deck is a floor the player falls
    # straight through while the collider still reports the UNDERSIDE a few centimetres lower —
    # which looks, from every probe, exactly like a floor that is there.
    bmesh.ops.recalc_face_normals(builder, faces=builder.faces)

    builder.to_mesh(mesh)
    builder.free()

    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    obj.location = Vector(centre)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_lantern_deck(palette):
    """The gallery at the top of the tower, and the core standing in the middle of it.

    Returns the two anchors: where the player arrives, and where the beacon is.
    """
    x, y = TOWER_CENTRE

    cylinder("Tower_LanternDeck",
             (x, y, DECK_TOP_Z - DECK_THICKNESS * 0.5),
             DECK_RADIUS, DECK_THICKNESS, palette[PALETTE_BLACK])

    # The core is its own object so the game can light it as the beacon; joining it into the
    # castle would leave nothing to make glow.
    cylinder("Tower_BeaconCore",
             (x, y, DECK_TOP_Z + BEACON_HEIGHT * 0.5),
             BEACON_RADIUS, BEACON_HEIGHT, palette[PALETTE_RED], segments=16)

    for index in range(RAIL_POSTS):
        angle = index / RAIL_POSTS * math.tau
        box("Tower_LanternRail_%02d" % index,
            (x + math.cos(angle) * DECK_RADIUS,
             y + math.sin(angle) * DECK_RADIUS,
             DECK_TOP_Z + RAIL_HEIGHT * 0.5),
            (RAIL_POST, RAIL_POST, RAIL_HEIGHT),
            palette[PALETTE_BLACK])

    stand = (x + DECK_STAND_RADIUS, y, DECK_TOP_Z)
    core = (x, y, DECK_TOP_Z + BEACON_HEIGHT * 0.5)
    return blender_to_unity(stand), blender_to_unity(core)


# --------------------------------------------------------------------------
# The gateway
# --------------------------------------------------------------------------

def cut_gateway(wall_objects, gate_centre):
    """Cut a way in and out through the ring wall, and return the middle of the gateway.

    The castle is modelled closed. `castle_gate_and_colour.py` already subtracts an opening, but
    only from the ten array courses and only across the span between them — and the wall on this
    axis is TWO rings, at y=-37 and y=-34.4, with facing stones outside both. The result was an
    opening with a wall at each end of it: a hole nobody can walk through, which from outside
    looks exactly like a gate.

    So the cut is redone here over the whole wall group, with a cutter deep enough to overshoot
    both rings. Every wall object whose bounding box meets the cutter is subtracted from, not just
    the courses, because the merlons and facing stones standing in the opening are separate
    objects and each one left uncut is a doorstep at head height.

    Additive, like everything else this script does: the Boolean modifiers are added in memory and
    baked by `apply_modifiers`. castle.blend is never written to.
    """
    half = Vector((GATEWAY_WIDTH, GATEWAY_DEPTH, GATEWAY_HEIGHT + GATEWAY_SILL)) * 0.5
    centre = Vector((gate_centre.x, gate_centre.y,
                     (GATEWAY_HEIGHT - GATEWAY_SILL) * 0.5))

    mesh = bpy.data.meshes.new(GATEWAY_CUTTER)
    builder = bmesh.new()
    bmesh.ops.create_cube(builder, size=1.0)
    bmesh.ops.scale(builder, vec=half * 2.0, verts=builder.verts)
    bmesh.ops.recalc_face_normals(builder, faces=builder.faces)
    builder.to_mesh(mesh)
    builder.free()

    cutter = bpy.data.objects.new(GATEWAY_CUTTER, mesh)
    cutter.location = centre
    cutter.display_type = 'WIRE'
    bpy.context.scene.collection.objects.link(cutter)

    low = centre - half
    high = centre + half

    cut = 0
    for obj, (obj_low, obj_high) in wall_objects:
        # Only what actually stands in the opening. A Boolean against all 270 wall objects is
        # slow and rewrites topology that nothing asked to change.
        if (obj_high.x < low.x or obj_low.x > high.x or
                obj_high.y < low.y or obj_low.y > high.y or
                obj_high.z < low.z or obj_low.z > high.z):
            continue

        modifier = obj.modifiers.new(GATEWAY_MODIFIER, 'BOOLEAN')
        modifier.operation = 'DIFFERENCE'
        modifier.solver = 'EXACT'
        modifier.operand_type = 'OBJECT'
        modifier.object = cutter
        cut += 1

    print("  gateway: %.1f x %.1f m cut through %d wall object(s) at (%.2f, %.2f)"
          % (GATEWAY_WIDTH, GATEWAY_HEIGHT, cut, centre.x, centre.y))

    return cutter, Vector((centre.x, centre.y, 0.0))


# --------------------------------------------------------------------------
# Doors
# --------------------------------------------------------------------------

def build_doors(depsgraph, palette, gate_centre):
    """The gate leaf, its frame, and the keep's sealed portal. Returns the two anchors."""
    # The leaf fills the gateway the Boolean cut, hung in the middle of it. Swinging it aside is
    # what opens the castle, so it has to be the same size as the hole — a leaf smaller than its
    # opening leaves a gap to walk through with the gate still shut.
    width, height = GATE_LEAF_SIZE
    box("Door_OuterGate",
               (gate_centre.x, gate_centre.y, height * 0.5),
               (width, DOOR_THICKNESS, height),
               palette[PALETTE_BLACK])

    # The same red surround the keep's door gets, so the two locked doors of the castle read as
    # the same kind of thing. Against black stone a black slab is just a darker patch of wall.
    box("Door_OuterGateFrame",
        (gate_centre.x, gate_centre.y + DOOR_THICKNESS * 0.75, (height + KEEP_DOOR_FRAME) * 0.5),
        (width + KEEP_DOOR_FRAME * 2, DOOR_THICKNESS * 0.5, height + KEEP_DOOR_FRAME),
        palette[PALETTE_RED])

    face_y = keep_face_y(depsgraph)
    leaf_width, leaf_height = KEEP_DOOR_SIZE

    # Stood against the face, not in it: the keep is solid and stays solid.
    box("Door_KeepEntrance",
                    (0.0, face_y - DOOR_THICKNESS * 0.5, leaf_height * 0.5),
                    (leaf_width, DOOR_THICKNESS, leaf_height),
                    palette[PALETTE_BLACK])

    # A surround, so the door reads as an entrance from across the courtyard rather than as a
    # panel someone leaned on the wall. This is the only thing telling the player where to go.
    box("Door_KeepFrame",
        (0.0, face_y - DOOR_THICKNESS * 0.75, (leaf_height + KEEP_DOOR_FRAME) * 0.5),
        (leaf_width + KEEP_DOOR_FRAME * 2, DOOR_THICKNESS * 0.5, leaf_height + KEEP_DOOR_FRAME),
        palette[PALETTE_RED])

    return {
        "outerGate": blender_to_unity((gate_centre.x, gate_centre.y, height * 0.5)),
        "keepEntrance": blender_to_unity((0.0, face_y - DOOR_THICKNESS * 0.5, leaf_height * 0.5)),
    }


# --------------------------------------------------------------------------
# Export
# --------------------------------------------------------------------------

def apply_modifiers():
    """Bake every modifier into its mesh, before anything is joined.

    Joining does NOT do this. A join keeps only the ACTIVE object's modifier stack and throws the
    rest away, so joining first and exporting with use_mesh_modifiers would ship the castle as its
    base meshes — one brick per wall course instead of the whole course, because every course is a
    single brick with a geometry-nodes Array over it. The castle came out as a scatter of loose
    bricks and an intact keep, which looks enough like a castle in a thumbnail to be missed.
    """
    targets = [o for o in mesh_objects() if o.modifiers]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in targets:
        obj.select_set(True)
    if not targets:
        return 0

    bpy.context.view_layer.objects.active = targets[0]
    bpy.ops.object.convert(target='MESH')
    return len(targets)


def join_into(name, objects):
    """Join `objects` into one mesh object called `name`, keeping their material slots.

    Joining is what makes the castle shippable: one object, one renderer, one submesh per palette
    material. It is destructive to the open file, which is why nothing here ever saves it.
    """
    if not objects:
        raise SystemExit("Nothing to join into %s" % name)

    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    if len(objects) > 1:
        bpy.ops.object.join()

    joined = bpy.context.view_layer.objects.active
    joined.name = name

    # Bake the object's transform into its vertices and leave it at identity.
    #
    # A join keeps the ACTIVE object's transform, and the active object here is whichever of 600
    # hand-modelled bricks sorted first — one of which turned out to carry a scale of
    # (-6.75, -6.75, -0.94). Negative and non-uniform. Renderers cope with that by flipping the
    # winding, so the castle LOOKED correct; PhysX does not, and a MeshCollider under a negative
    # scale is mirrored. The castle was solid where it looked open and open where it looked solid,
    # and the lantern deck could not be stood on at all.
    #
    # Baking removes the whole class of problem rather than the instance: whatever transform the
    # join happens to inherit, what Unity receives is an identity-transformed mesh in world space.
    joined.data.transform(joined.matrix_world)
    joined.matrix_world = Matrix.Identity(4)

    return joined


def write_fbx(path):
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
    print("  wrote %s (%.1f MB)" % (path, os.path.getsize(path) / 1e6))


def check_courtyard_is_empty(bounds_by_name):
    """The split only works because nothing stands in the courtyard. Prove it every run.

    Ring-spanning objects are exempt: a wall course's bounding box is centred on the courtyard
    without a single vertex being in it.
    """
    low, high = COURTYARD_GAP
    intruders = [name for name, (obj_low, obj_high) in bounds_by_name.items()
                 if footprint_span(obj_low, obj_high) <= RING_MIN_SPAN
                 and low < footprint_radius(obj_low, obj_high) < high]

    if intruders:
        raise SystemExit(
            "The courtyard band r=%.0f..%.0f is no longer empty (%d object(s), e.g. %s), so a "
            "radius cannot separate the keep from the ring wall any more. Split by collection "
            "instead." % (low, high, len(intruders), ", ".join(sorted(intruders)[:5])))


def main():
    # The old cutter is a tool, not a part of the castle: it is the box the first gate attempt was
    # subtracted with, and it is still sitting in the gateway. Exported, it fills the opening.
    old_cutter = bpy.data.objects.get(GATE_CUTTER)
    gate_centre = (old_cutter.matrix_world.translation.copy() if old_cutter is not None
                   else Vector((0.0, 0.0, 0.0)))
    if old_cutter is None:
        raise SystemExit("No %s in the file — run castle_gate_and_colour.py first." % GATE_CUTTER)

    depsgraph = bpy.context.evaluated_depsgraph_get()
    palette_names = (PALETTE_BLACK, PALETTE_RED)
    palette = dict(zip(palette_names, link_materials(list(palette_names))))

    anchors = build_doors(depsgraph, palette, gate_centre)
    anchors["towerDeck"], anchors["towerBeacon"] = build_lantern_deck(palette)

    # Added rather than classified: they are new objects at the tower's axis, which puts them in
    # the keep either way, but naming them here means the keep version cannot lose its lighthouse
    # to a change in the split.
    keep_extra = {"Tower_LanternDeck", "Tower_BeaconCore"}
    keep_extra |= {"Tower_LanternRail_%02d" % index for index in range(RAIL_POSTS)}
    door_names = {"Door_OuterGate", "Door_OuterGateFrame",
                  "Door_KeepEntrance", "Door_KeepFrame"} | keep_extra

    bpy.data.objects.remove(old_cutter, do_unlink=True)

    # Classified BEFORE the modifiers are baked, off evaluated bounds: an Array has to be
    # evaluated to know how far a wall course reaches, but it must not be applied yet, because the
    # gateway Boolean below has to go on top of it.
    depsgraph = bpy.context.evaluated_depsgraph_get()
    bounds_by_name = {}
    for obj in mesh_objects():
        if obj.name in door_names and obj.name not in keep_extra:
            continue
        bounds = evaluated_bounds(obj, depsgraph)
        if bounds is None:
            continue
        bounds_by_name[obj.name] = bounds

    check_courtyard_is_empty(bounds_by_name)

    keep_names = {name for name, bounds in bounds_by_name.items()
                  if belongs_to_keep(*bounds)} | keep_extra
    wall_names = set(bounds_by_name) - keep_names
    print("  keep: %d object(s), ring wall and gatehouse: %d object(s)"
          % (len(keep_names), len(wall_names)))

    gateway_cutter, gate_middle = cut_gateway(
        [(bpy.data.objects[n], bounds_by_name[n]) for n in sorted(wall_names)], gate_centre)

    print("  applied modifiers on %d object(s)" % apply_modifiers())
    bpy.data.objects.remove(gateway_cutter, do_unlink=True)

    root = repo_root()
    anchors_path = os.path.join(root, ANCHOR_FILE)
    os.makedirs(os.path.dirname(anchors_path), exist_ok=True)
    with open(anchors_path, "w") as handle:
        json.dump(anchors, handle, indent=2, sort_keys=True)
        handle.write("\n")
    print("  wrote %s" % anchors_path)

    # Two joins, not one. Joining the whole castle into a single object would be enough for the
    # full export and would take the keep down with it — after a join the parts are faces in one
    # mesh, and no amount of deleting gets the ring wall back out of them. Keeping the two halves
    # as separate objects lets the full castle ship as both of them and the keep ship as one.
    join_into("CastleKeep", [bpy.data.objects[n] for n in sorted(keep_names)])
    wall = join_into("CastleWall", [bpy.data.objects[n] for n in sorted(wall_names)])

    write_fbx(os.path.join(root, OUT_FOLDER, "CastleFull.fbx"))

    # The base building alone: the ring wall goes, and the gate leaf goes with it — there is no
    # longer a gateway for it to hang in.
    bpy.data.objects.remove(wall, do_unlink=True)
    for name in ("Door_OuterGate", "Door_OuterGateFrame"):
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    print("  base-building version: dropped the ring wall, the gate leaf and its frame")

    write_fbx(os.path.join(root, OUT_FOLDER, "CastleKeep.fbx"))

    print("  anchors: %s" % json.dumps(anchors, sort_keys=True))


main()
