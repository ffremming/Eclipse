"""Copy human_sculpt_base.blend to human_sculpt_base_clean.blend, repair its centre line, UV unwrap it.

Historical record, not source of truth: once the clean copy is being sculpted or textured, never re-run
this over it. It only ever reads human_sculpt_base.blend, which stays untouched.

    blender --background --python human_sculpt_base_clean.py

What it does to the copy:
  1. Deletes the zero-thickness walls and doubled vertices left along the mirror plane, welds the
     halves back together, fills the diamond hole that leaves at the top of the throat and relaxes
     the throat vertices that were dragged proud of the skin.
  2. Joins triangle pairs into quads, recomputes normals and makes every face smooth.
  3. Splits the body into parts, seams their borders plus a hidden slit per part, unwraps, relaxes
     stretch, equalises texel density and packs the islands into 0-1.
"""
import collections
import heapq
import math
import os

import bmesh
import bpy
from mathutils import Vector
from mathutils.geometry import intersect_point_line

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "human_sculpt_base.blend")
OUT = os.path.join(HERE, "human_sculpt_base_clean.blend")
BODY = "Mesh_HumanSculptBase"

# --- centre-line repair ------------------------------------------------------------------------
WALL_MAX_X = 1e-2            # a wall face lies within this distance of the mirror plane...
WALL_MIN_NORMAL_X = 0.7      # ...and faces sideways, along the mirror plane's normal
BOUNDARY_WELD_DIST = 1.5e-3  # gaps between the mirrored halves are closed up to this wide
STUB_WELD_DIST = 1e-3
MIRROR_PLANE_X = 1e-3
THROAT_Z = (1.47, 1.56)      # vertices on the throat centre line that were dragged forward
THROAT_RELAX_STEPS = 6
THROAT_RELAX_BLEND = 0.5
JOIN_MAX_FACE_ANGLE = math.radians(60)
JOIN_MAX_SHAPE_ANGLE = math.radians(90)

# --- UV unwrap ---------------------------------------------------------------------------------
# (part, segment start, segment end, weight) on the right side; sided parts are mirrored. Every
# vertex belongs to the part whose skeleton segment, less its weight, is nearest.
SKELETON = [
    ("head", (0.0, 0.0, 1.66), (0.0, 0.02, 1.84), 0.06),
    ("pelvis", (0.0, -0.02, 0.92), (0.0, -0.024, 1.0), 0.115),
    ("torso", (0.0, -0.024, 1.0), (0.0, -0.05, 1.56), 0.115),
    ("arm", (0.215, -0.06, 1.52), (0.36, -0.08, 1.17), 0.075),
    ("arm", (0.36, -0.08, 1.17), (0.48, -0.04, 0.99), 0.05),
    ("hand", (0.485, -0.035, 0.97), (0.50, -0.03, 0.68), 0.05),
    ("leg", (0.10, -0.005, 0.80), (0.166, -0.05, 0.20), 0.09),
    ("foot", (0.167, -0.04, 0.16), (0.17, 0.13, 0.03), 0.06),
]
SIDED_PARTS = {"arm", "hand", "leg", "foot"}
SKULL_BACK_Y = -0.08           # the head behind this plane is its own island, so the face stays even
LABEL_SMOOTHING_PASSES = 4
DIGIT_MIN_LENGTH = 0.02        # a hand/foot extremity shorter than this from the ring is not a digit
DIGIT_MIN_SPACING = 0.03
CROTCH_MAX_X = 3e-3            # the pelvis slit ends at the lowest vertex this close to the mirror plane
MIDLINE_PENALTY = 40.0        # slits along the back keep to the mirror plane
INNER_SIDE_PENALTY = 8.0       # limb slits keep to the side facing the body
STRETCH_ITERATIONS = 400
ISLAND_MARGIN = 0.004
MIN_FACE_AREA = 1e-9


def load_copy():
    if os.path.exists(OUT):
        raise SystemExit(f"Refusing to overwrite existing file: {OUT}\nThe .blend is the source of truth.")
    bpy.ops.wm.open_mainfile(filepath=SOURCE)
    return bpy.data.objects[BODY]


def grow_wall_faces(bm):
    def is_wall(f):
        return all(abs(v.co.x) < WALL_MAX_X for v in f.verts) and abs(f.normal.x) > WALL_MIN_NORMAL_X

    walls = {f for f in bm.faces if is_wall(f) and any(not e.is_manifold for e in f.edges)}
    frontier = list(walls)
    while frontier:
        for e in frontier.pop().edges:
            for g in e.link_faces:
                if g not in walls and is_wall(g):
                    walls.add(g)
                    frontier.append(g)
    return walls


def average(points):
    return sum(points, Vector()) / len(points)


def repair_centre_line(bm):
    bm.faces.index_update()
    bmesh.ops.delete(bm, geom=sorted(grow_wall_faces(bm), key=lambda f: f.index), context='FACES_ONLY')
    bmesh.ops.delete(bm, geom=[e for e in bm.edges if not e.link_faces], context='EDGES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_edges], context='VERTS')

    # Merge order decides which vertex survives, so keep it fixed: a set would vary from run to run.
    bm.verts.index_update()
    open_verts = sorted({v for e in bm.edges if e.is_boundary for v in e.verts}, key=lambda v: v.index)
    bmesh.ops.remove_doubles(bm, verts=open_verts, dist=BOUNDARY_WELD_DIST)
    bm.verts.index_update()
    bm.edges.ensure_lookup_table()
    stub_verts = sorted({v for e in bm.edges if not e.is_manifold for v in e.verts}, key=lambda v: v.index)
    bmesh.ops.remove_doubles(bm, verts=stub_verts, dist=STUB_WELD_DIST)

    open_edges = [e for e in bm.edges if e.is_boundary]
    if open_edges:
        filled = bmesh.ops.holes_fill(bm, edges=open_edges, sides=4)["faces"]
        bm.verts.index_update()
        for _ in range(3):
            fill_verts = {v for f in filled for v in f.verts if abs(v.co.x) < MIRROR_PLANE_X and len(v.link_edges) == 3}
            for v in sorted(fill_verts, key=lambda v: v.index):
                v.co = average([e.other_vert(v).co for e in v.link_edges])
                v.co.x = 0.0

    throat = [v for v in bm.verts if abs(v.co.x) < MIRROR_PLANE_X and v.co.y > 0 and THROAT_Z[0] < v.co.z < THROAT_Z[1]]
    for _ in range(THROAT_RELAX_STEPS):
        for v in throat:
            v.co = v.co.lerp(average([e.other_vert(v).co for e in v.link_edges]), THROAT_RELAX_BLEND)
            v.co.x = 0.0

    triangles = [f for f in bm.faces if len(f.verts) == 3]
    bmesh.ops.join_triangles(bm, faces=triangles, cmp_seam=False, cmp_sharp=False, cmp_uvs=False, cmp_vcols=False,
                             cmp_materials=False, angle_face_threshold=JOIN_MAX_FACE_ANGLE,
                             angle_shape_threshold=JOIN_MAX_SHAPE_ANGLE)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        f.smooth = True


def part_of_point(p):
    best = None
    for name, start, end, weight in SKELETON:
        for sign in (1, -1) if name in SIDED_PARTS else (1,):
            a = Vector((start[0] * sign, start[1], start[2]))
            b = Vector((end[0] * sign, end[1], end[2]))
            _, t = intersect_point_line(p, a, b)
            distance = (p - a.lerp(b, max(0.0, min(1.0, t)))).length - weight
            if best is None or distance < best[0]:
                best = (distance, name, sign)
    _, name, sign = best
    return f"{name}_{'R' if sign > 0 else 'L'}" if name in SIDED_PARTS else name


def label_faces(bm):
    labels = []
    for v in bm.verts:
        part = part_of_point(v.co)
        labels.append("head_back" if part == "head" and v.co.y < SKULL_BACK_Y else part)
    face_label = {f: collections.Counter(labels[v.index] for v in f.verts).most_common(1)[0][0] for f in bm.faces}
    for _ in range(LABEL_SMOOTHING_PASSES):
        face_label = {f: collections.Counter(
            [face_label[f]] * 2 + [face_label[g] for e in f.edges for g in e.link_faces if g is not f]
        ).most_common(1)[0][0] for f in bm.faces}
    return face_label


def absorb_stray_patches(bm, face_label):
    def patches(label):
        seen, found = set(), []
        for f in bm.faces:
            if face_label[f] != label or f in seen:
                continue
            stack, patch = [f], []
            seen.add(f)
            while stack:
                cur = stack.pop()
                patch.append(cur)
                for e in cur.edges:
                    for g in e.link_faces:
                        if g not in seen and face_label[g] == label:
                            seen.add(g)
                            stack.append(g)
            found.append(patch)
        return sorted(found, key=len, reverse=True)

    for label in sorted(set(face_label.values())):
        for stray in patches(label)[1:]:
            neighbours = collections.Counter(face_label[g] for f in stray for e in f.edges for g in e.link_faces
                                             if face_label[g] != label)
            for f in stray:
                face_label[f] = neighbours.most_common(1)[0][0]


class SeamCutter:
    def __init__(self, bm, face_label):
        self.bm = bm
        self.face_label = face_label

    def border(self, a, b):
        return {v for e in self.bm.edges if len(e.link_faces) == 2
                and {self.face_label[e.link_faces[0]], self.face_label[e.link_faces[1]]} == {a, b} for v in e.verts}

    def part_verts(self, label):
        return {v for f in self.bm.faces if self.face_label[f] == label for v in f.verts}

    def mark_borders(self):
        for e in self.bm.edges:
            e.seam = len(e.link_faces) == 2 and self.face_label[e.link_faces[0]] != self.face_label[e.link_faces[1]]

    def in_part(self, label):
        return lambda e: any(self.face_label[f] == label for f in e.link_faces)

    @staticmethod
    def shortest_paths(sources, allowed, cost):
        dist = {v: 0.0 for v in sources}
        prev = {}
        heap = [(0.0, v.index, v) for v in sources]
        heapq.heapify(heap)
        while heap:
            d, _, v = heapq.heappop(heap)
            if d > dist.get(v, math.inf):
                continue
            for e in v.link_edges:
                if not allowed(e):
                    continue
                w = e.other_vert(v)
                if d + cost(e) < dist.get(w, math.inf):
                    dist[w] = d + cost(e)
                    prev[w] = (v, e)
                    heapq.heappush(heap, (dist[w], w.index, w))
        return dist, prev

    @staticmethod
    def path_to(prev, v):
        edges = []
        while v in prev:
            v, e = prev[v]
            edges.append(e)
        return edges

    def slit(self, label, sources, targets, cost):
        dist, prev = self.shortest_paths(sources, self.in_part(label), cost)
        goal = min(targets, key=lambda v: dist.get(v, math.inf))
        assert goal in dist, f"no slit path inside {label}"
        for e in self.path_to(prev, goal):
            e.seam = True

    def digit_slits(self, label, ring, cost):
        allowed = self.in_part(label)
        reach, _ = self.shortest_paths(list(ring), allowed, lambda e: e.calc_length())
        _, prev = self.shortest_paths(list(ring), allowed, cost)
        tips = []
        for v, d in sorted(reach.items(), key=lambda kv: -kv[1]):
            if d < DIGIT_MIN_LENGTH:
                break
            is_peak = all(reach.get(e.other_vert(v), 0) <= d for e in v.link_edges if allowed(e))
            if is_peak and all((v.co - t.co).length > DIGIT_MIN_SPACING for t in tips):
                tips.append(v)
        for tip in tips:
            for e in self.path_to(prev, tip):
                e.seam = True
        return len(tips)


def midline_cost(e):
    return e.calc_length() * (1 + MIDLINE_PENALTY * abs(e.verts[0].co.x + e.verts[1].co.x) / 2)


def inner_side_cost(sign):
    inward = Vector((-sign, 0, 0))

    def cost(e):
        normal = (e.verts[0].normal + e.verts[1].normal).normalized()
        return e.calc_length() * (1 + INNER_SIDE_PENALTY * (1 - max(0.0, normal.dot(inward))))
    return cost


def back_midline_vertex(ring):
    return min((v for v in ring if v.co.y < 0), key=lambda v: (abs(v.co.x), v.co.z))


def mark_seams(bm, face_label):
    cutter = SeamCutter(bm, face_label)
    cutter.mark_borders()

    nape = back_midline_vertex(cutter.border("head_back", "torso"))
    waist = back_midline_vertex(cutter.border("torso", "pelvis"))
    cutter.slit("torso", [nape], [waist], midline_cost)
    crotch = min((v for v in cutter.part_verts("pelvis") if abs(v.co.x) < CROTCH_MAX_X), key=lambda v: v.co.z)
    cutter.slit("pelvis", [waist], [crotch], midline_cost)

    for sign, side in ((1, "R"), (-1, "L")):
        cost = inner_side_cost(sign)
        armpit = min(cutter.border(f"arm_{side}", "torso"), key=lambda v: abs(v.co.x) - 0.3 * v.co.z)
        cutter.slit(f"arm_{side}", [armpit], cutter.border(f"arm_{side}", f"hand_{side}"), cost)
        groin = min(cutter.border(f"leg_{side}", "pelvis"), key=lambda v: abs(v.co.x))
        cutter.slit(f"leg_{side}", [groin], cutter.border(f"leg_{side}", f"foot_{side}"), cost)
        for part, joint in (("hand", "arm"), ("foot", "leg")):
            digits = cutter.digit_slits(f"{part}_{side}", cutter.border(f"{part}_{side}", f"{joint}_{side}"), cost)
            assert digits >= 5, f"{part}_{side}: only {digits} digits found"


def unwrap(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    bpy.ops.uv.unwrap(method='ANGLE_BASED', fill_holes=True, margin=0.0)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.minimize_stretch(iterations=STRETCH_ITERATIONS)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=ISLAND_MARGIN, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')


def verify(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    assert not [e for e in bm.edges if not e.is_manifold], "mesh is not closed and manifold"
    assert bm.calc_volume(signed=True) > 0, "normals point inward"
    layer = bm.loops.layers.uv.active
    for f in bm.faces:
        uvs = [l[layer].uv for l in f.loops]
        area = sum(p.x * q.y - q.x * p.y for p, q in zip(uvs, uvs[1:] + uvs[:1])) / 2
        assert area >= 0, "a face is flipped in UV space"
        assert all(0.0 <= uv.x <= 1.0 and 0.0 <= uv.y <= 1.0 for uv in uvs), "a UV lies outside 0-1"
    bm.free()


def main():
    body = load_copy()
    mesh = body.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    repair_centre_line(bm)
    bm.normal_update()
    bm.verts.ensure_lookup_table()
    face_label = label_faces(bm)
    absorb_stray_patches(bm, face_label)
    mark_seams(bm, face_label)
    bm.to_mesh(mesh)
    bm.free()
    if "custom_normal" in mesh.attributes:
        mesh.attributes.remove(mesh.attributes["custom_normal"])
    mesh.update()
    unwrap(body)
    verify(mesh)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)


main()
