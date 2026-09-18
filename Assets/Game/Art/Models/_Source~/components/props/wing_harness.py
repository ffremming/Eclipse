"""Wing harness — the back frame a wingsuit's blades hinge from.

Worn between the shoulder blades over the suit. Its whole job is the two hinge
bosses at the top corners and one at the bottom: everything else exists so a
pair of two-metre sailcloth blades visibly have something to be bolted to.

Local frame, and it matters for the wingsuit model that hinges blades onto it:

    origin  = the mount point on the wearer's back, on the spine surface
              between the shoulder blades
    -Y      = forward (the wearer faces -Y); the frame stands off the back
              into +Y
    +Z      = up
    X       = across the shoulders; the wearer's RIGHT is -X (face -Y with +Z
              up and the right hand lands on -X — easy to get backwards)

The three pivots below are shared with `models/gear/wingsuit.py`, which imports
them rather than restating them. Each hinge pin runs along the axis the blade
folds about: the shoulder pins along Y (a wing swings out sideways about the
fore-aft axis) and the tail pin along X (the fan swings back about the
across-the-shoulders axis).

Three variations, different in structure rather than trim: a single dorsal
spar, a full tube cage, and a scrap-built spar lashed with rope.

    blender --background --python wing_harness.py -- --out <path>/wing_harness.blend

Generation script — historical record. The .blend is the source of truth; never
re-run this over the file it produced.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__)))))
from _buildlib import *  # noqa: E402,F403
from _tracked import TrackedPart  # noqa: E402

from mathutils import Matrix  # noqa: E402

STEEL, DARK, CANVAS, BLACK, BRASS, RUST, ROPE, WOOD, SAND = range(9)
MATS = ["Mat_Metal_Steel_Worn",       # spar, crossbar, braces, cage tubes
        "Mat_Metal_Steel_Dark",       # pins, buckles, fittings
        "Mat_Fabric_Canvas_Faded",    # back pad, shoulder straps, belt
        "Mat_Neutral_Black_Matte",    # shadow gaps under the pad
        "Mat_Metal_Brass_Tarnished",  # hinge bosses — the scavenged-bearing look
        "Mat_Metal_Rust_Heavy",       # scrap plates
        "Mat_Fabric_Rope_Hemp",       # lashings on the scrap build
        "Mat_Wood_Ply_Worn",          # scrap crossbar
        "Mat_Fabric_Canvas_Sand"]     # the deploy canister's sleeve

# Where the blades pin on. The wearer's right shoulder is on -X; the left is its mirror.
SHOULDER_PIVOT_RIGHT = (-0.21, 0.09, 0.26)
TAIL_PIVOT = (0.0, 0.07, -0.34)

# Overall envelope: 0.48 m across, 0.16 m deep, 0.66 m tall — high on the back
# of a three-metre wearer, clear of the expedition backpack slung lower down.
HALF_W = 0.24
Z_TOP, Z_BOT = 0.28, -0.36
BEVEL_W = 0.004


# ---------------------------------------------------------------------------
# Shared pieces
# ---------------------------------------------------------------------------

def back_pad(p, mat=CANVAS):
    """Padded canvas against the suit, with a shadow gap so it reads as a
    separate layer rather than as the frame's own colour."""
    p.slab((-0.17, 0.0, -0.31), (0.17, 0.006, 0.27), BLACK)
    p.slab((-0.16, 0.006, -0.30), (0.16, 0.024, 0.26), mat)


def straps(p, mat=CANVAS):
    """Shoulder loops and a waist belt — the only parts in front of the
    wearer's centreline, so they sit at the suit's rough depth."""
    hard = []
    for sx in (-1, 1):
        x = sx * 0.14
        # Over the shoulder and down the chest.
        hard += p.slab((x - 0.035, -0.30, 0.255), (x + 0.035, 0.024, 0.285), mat)
        hard += p.slab((x - 0.035, -0.31, -0.02), (x + 0.035, -0.29, 0.27), mat)
        # Buckle where the chest strap meets the belt.
        hard += p.box((x, -0.305, -0.04), (0.05, 0.014, 0.05), DARK)
    # Belt: the back run and the two flanks reaching forward.
    hard += p.slab((-0.25, 0.0, -0.33), (0.25, 0.024, -0.27), mat)
    for sx in (-1, 1):
        hard += p.slab((sx * 0.235, -0.30, -0.33), (sx * 0.26, 0.024, -0.27), mat)
    return hard


def hinge_boss(p, pivot, axis, radius=0.036, depth=0.10):
    """Brass bearing on a steel clevis, pin along `axis` through `pivot`.

    The pin is the fold axis and the pivot is on it — dropping a blade's root
    at this point needs no offset arithmetic.
    """
    hard = []
    p.cyl(pivot, radius, depth, axis, 12, BRASS)
    p.cyl(pivot, radius * 0.42, depth + 0.05, axis, 8, DARK)
    # Clevis cheeks either side of the boss, standing off the frame.
    for s in (-1, 1):
        off = [0.0, 0.0, 0.0]
        off['XYZ'.index(axis)] = s * (depth / 2 + 0.008)
        hard += p.box((pivot[0] + off[0], pivot[1] + off[1], pivot[2] + off[2]),
                      tuple(0.016 if 'XYZ'[i] == axis else radius * 2.1 for i in range(3)),
                      STEEL)
    return hard


def canister(p, sleeve=SAND):
    """The spring canister on the spar that throws the blades open — a cylinder
    down the spine with a canvas sleeve, so the deploy has a visible source."""
    p.cyl((0.0, 0.125, -0.06), 0.048, 0.24, 'Z', 14, DARK)
    p.cyl((0.0, 0.125, -0.06), 0.052, 0.14, 'Z', 14, sleeve)
    for z in (-0.17, 0.05):
        p.torus((0.0, 0.125, z), 0.050, 0.006, 'Z', 14, 6, BRASS)


def bosses(p):
    hard = []
    for sx in (1, -1):
        hard += hinge_boss(p, (sx * SHOULDER_PIVOT_RIGHT[0], SHOULDER_PIVOT_RIGHT[1],
                               SHOULDER_PIVOT_RIGHT[2]), 'Y')
    hard += hinge_boss(p, TAIL_PIVOT, 'X', radius=0.030, depth=0.12)
    return hard


# ---------------------------------------------------------------------------
# Variations
# ---------------------------------------------------------------------------

def spar(coll, mats):
    """One dorsal spar, a crossbar, two braces. The standard issue."""
    p = TrackedPart(mats)
    hard = []
    back_pad(p)
    hard += straps(p)

    hard += p.slab((-0.035, 0.024, Z_BOT), (0.035, 0.085, Z_TOP), STEEL)
    hard += p.slab((-HALF_W, 0.03, 0.22), (HALF_W, 0.085, Z_TOP), STEEL)
    for sx in (-1, 1):
        hard += p.seam((sx * 0.21, 0.058, 0.22), (sx * 0.04, 0.058, -0.06),
                       width=0.028, depth=0.03, axis='Y', mat=STEEL)
    p.rivets((0.0, 0.088, -0.30), (0.0, 0.088, 0.18), 6, radius=0.008,
             height=0.006, axis='Y', mat=DARK)
    p.rivets((-0.20, 0.088, 0.25), (0.20, 0.088, 0.25), 5, radius=0.008,
             height=0.006, axis='Y', mat=DARK)

    hard += bosses(p)
    canister(p)

    p.restamp()
    p.bevel(hard, width=BEVEL_W, segments=1)
    return p.finish("Mesh_WingHarness_Spar", coll)


def cage(coll, mats):
    """A welded tube cage: two uprights, three rungs, corner gussets. Heavier,
    boxier silhouette for a wearer who trusts welds over a single spar."""
    p = TrackedPart(mats)
    hard = []
    back_pad(p)
    hard += straps(p)

    for sx in (-1, 1):
        p.cyl((sx * 0.19, 0.06, (Z_TOP + Z_BOT) / 2), 0.022,
              Z_TOP - Z_BOT, 'Z', 10, STEEL)
    for z in (Z_TOP - 0.03, -0.02, Z_BOT + 0.03):
        p.cyl((0.0, 0.06, z), 0.020, 0.40, 'X', 10, STEEL)
    for sx in (-1, 1):
        for z in (Z_TOP - 0.03, Z_BOT + 0.03):
            hard += p.box((sx * 0.19, 0.06, z), (0.07, 0.07, 0.07), DARK)
    # Cross-brace so the cage does not read as a ladder.
    hard += p.seam((-0.17, 0.06, 0.22), (0.17, 0.06, -0.30), width=0.02,
                   depth=0.02, axis='Y', mat=STEEL)
    hard += p.seam((0.17, 0.06, 0.22), (-0.17, 0.06, -0.30), width=0.02,
                   depth=0.02, axis='Y', mat=STEEL)

    hard += bosses(p)
    canister(p)

    p.restamp()
    p.bevel(hard, width=BEVEL_W, segments=1)
    return p.finish("Mesh_WingHarness_Cage", coll)


def scrap(coll, mats):
    """The improvised one: a ply crossbar lashed to a rusty spar with rope, the
    straps rope too. Same pivots, so the same blades bolt on."""
    p = TrackedPart(mats)
    hard = []
    back_pad(p, mat=SAND)

    hard += p.slab((-0.035, 0.024, Z_BOT), (0.035, 0.08, Z_TOP), RUST)
    hard += p.slab((-HALF_W, 0.035, 0.21), (HALF_W, 0.09, Z_TOP - 0.01), WOOD)
    # Rope lashings where the crossbar meets the spar and at each boss.
    for z in (0.23, 0.27):
        p.torus((0.0, 0.058, z), 0.055, 0.010, 'Z', 10, 5, ROPE)
    for sx in (-1, 1):
        p.torus((sx * 0.17, 0.062, 0.25), 0.045, 0.010, 'X', 10, 5, ROPE)
    # A repair plate over a crack, bolted on crooked.
    hard += p.box((0.0, 0.086, -0.12), (0.11, 0.008, 0.16), RUST,
                  rot=Matrix.Rotation(math.radians(7), 4, 'Y'))
    p.rivets((-0.04, 0.092, -0.18), (0.04, 0.092, -0.06), 3, radius=0.009,
             height=0.007, axis='Y', mat=DARK)
    # Rope straps over the shoulders and round the waist.
    for sx in (-1, 1):
        x = sx * 0.14
        p.cyl((x, -0.14, 0.27), 0.014, 0.32, 'Y', 8, ROPE)
        p.cyl((x, -0.30, 0.125), 0.014, 0.29, 'Z', 8, ROPE)
    p.cyl((0.0, 0.01, -0.30), 0.014, 0.50, 'X', 8, ROPE)

    hard += bosses(p)

    p.restamp()
    p.bevel(hard, width=BEVEL_W, segments=1)
    return p.finish("Mesh_WingHarness_Scrap", coll)


def main():
    out = parse_out()
    start(out)
    mats = link_materials(MATS)

    spar(collection("Coll_WingHarness_Spar"), mats)
    cage(collection("Coll_WingHarness_Cage"), mats)
    scrap(collection("Coll_WingHarness_Scrap"), mats)

    report()
    save(out)


if __name__ == "__main__":
    main()
