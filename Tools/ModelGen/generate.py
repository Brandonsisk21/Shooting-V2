"""
Generates the Space Grunts 3D models (GDD 5.1) into Assets/Resources/Models/*.bytes.

    python3 Tools/ModelGen/generate.py [--preview out_dir]

Materials are names the game resolves at runtime (ModelLibrary):
  Team / TeamDark / TeamLight  -> tinted with the combatant's or base's color
  GlowTeam                     -> unlit glow in the team color
  Glass                        -> see-through helmet glass
  Tint                         -> caller-provided color (rocks)
  Color:RRGGBB / Glow:RRGGBB   -> fixed lit / unlit colors
"""
import json
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
from meshkit import (Model, capsule_profile, ellipsoid, lathe, merge, rock, rounded_box, sphere,
                     superellipsoid, torus, tube)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")

SUIT = "Color:3f4458"
METAL = "Color:9ea6b8"
METAL_DARK = "Color:4a4f60"
SOLE = "Color:2a2c36"
SKIN = "Color:ffcfa1"
SKIN_DARK = "Color:f2b184"
EYE = "Color:ffffff"
PUPIL = "Color:1b1b26"
MOUTH = "Color:6b2231"
BLUSH = "Color:ff9aa6"


# ============================================================================ grunt

def grunt():
    """
    Space Grunt, 1.85 m, facing +z. Parts have pivots so the game can animate them:
    Torso (hips), LegL/LegR (hip joints), Arms (shoulder line, follows aim pitch),
    Head (neck, bobbles) and Helmet (pops off on death). Head/helmet line up with the
    gameplay head hitbox (r 0.25 at y 1.55).
    """
    m = Model("grunt")

    torso = m.part("Torso", (0, 0.55, 0))
    torso.add("Team",
              superellipsoid((0, 0.84, 0), (0.33, 0.3, 0.26), 0.55, 0.6),  # chunky chest/belly armor
              )
    torso.add("TeamLight",
              superellipsoid((0, 0.9, 0.13), (0.24, 0.2, 0.14), 0.45, 0.5),  # chest plate
              )
    torso.add(SUIT, ellipsoid((0, 0.56, 0), (0.26, 0.13, 0.21)))  # pelvis
    torso.add(METAL,
              torus((0, 0.6, 0), 0.27, 0.055, euler=(0, 0, 0)),  # belt
              rounded_box((0, 0.6, 0.29), (0.07, 0.05, 0.03), 0.3),  # buckle
              )
    torso.add("Glow:ffd54a", sphere((0, 0.6, 0.32), 0.022))
    for x in (-1, 1):
        torso.add("TeamDark", ellipsoid((0.36 * x, 1.02, 0), (0.145, 0.13, 0.15)))  # shoulder pads
        torso.add("Team", torus((0.36 * x, 0.97, 0), 0.13, 0.025))
    # Backpack with tanks, vents and a wobbly antenna.
    torso.add(METAL, rounded_box((0, 0.86, -0.3), (0.22, 0.26, 0.12), 0.3))
    torso.add(METAL_DARK, *[rounded_box((0, 0.76 + 0.08 * k, -0.43), (0.14, 0.018, 0.02), 0.3) for k in range(3)])
    for x in (-0.11, 0.11):
        torso.add("Team", lathe(capsule_profile(0.06, 0.2), pos=(x, 1.06, -0.3)))
    torso.add(METAL_DARK, tube([(0.15, 1.12, -0.36), (0.16, 1.35, -0.38), (0.19, 1.55, -0.42)], 0.014, seg=8))
    torso.add("GlowTeam", sphere((0.19, 1.57, -0.42), 0.05))
    torso.add(METAL_DARK, torus((0, 1.18, 0), 0.13, 0.04))  # neck ring

    for side, name in ((-1, "LegL"), (1, "LegR")):
        x = 0.13 * side
        leg = m.part(name, (x, 0.55, 0))
        leg.add(SUIT, lathe([(0, 0.13), (0.09, 0.14), (0.1, 0.3), (0.105, 0.5), (0.09, 0.58), (0, 0.6)], pos=(x, 0, 0)))
        leg.add("TeamDark", ellipsoid((x, 0.32, 0.08), (0.08, 0.07, 0.05)))  # knee pad
        leg.add(METAL, superellipsoid((x, 0.09, 0.04), (0.115, 0.08, 0.16), 0.45, 0.5))  # boot
        leg.add(SOLE, superellipsoid((x, 0.022, 0.04), (0.12, 0.025, 0.17), 0.3, 0.5))

    # Arms reach forward to hold the blaster (right hand on the grip, left under the barrel).
    arms = m.part("Arms", (0, 1.02, 0))
    for path, hand in (
        ([(0.38, 1.0, 0.0), (0.37, 0.88, 0.17), (0.26, 1.16, 0.3)], (0.26, 1.17, 0.31)),
        ([(-0.38, 1.0, 0.0), (-0.26, 0.9, 0.3), (0.02, 1.2, 0.55)], (0.02, 1.21, 0.56)),
    ):
        arms.add(SUIT, tube(path, 0.07, seg=12))
        arms.add(METAL_DARK, sphere(hand, 0.08, 16, 10))
        cuff = np.asarray(path[-1]) * 0.85 + np.asarray(path[-2]) * 0.15
        arms.add("TeamDark", sphere(cuff, 0.083, 14, 8))

    # Big goofy head: huge eyes, raised brows, button nose, cheeky grin.
    head = m.part("Head", (0, 1.28, 0))
    head.add(SKIN, ellipsoid((0, 1.53, 0), (0.205, 0.195, 0.19), 32, 20))
    head.add(SKIN_DARK, sphere((0, 1.51, 0.19), 0.036))
    for x in (-1, 1):
        head.add(EYE, ellipsoid((0.078 * x, 1.575, 0.15), (0.062, 0.074, 0.04)))
        head.add(PUPIL, ellipsoid((0.072 * x, 1.568, 0.184), (0.031, 0.037, 0.012)))
        head.add("Glow:ffffff", sphere((0.08 * x, 1.585, 0.194), 0.009, 8, 6))
        head.add(PUPIL, rounded_box((0.082 * x, 1.668, 0.168), (0.045, 0.011, 0.012), 0.4, euler=(0, 0, -14 * x)))
        head.add(BLUSH, ellipsoid((0.125 * x, 1.49, 0.13), (0.036, 0.024, 0.016), euler=(0, 40 * x, 0)))
    head.add(MOUTH, torus((0, 1.49, 0.165), 0.058, 0.012, arc_deg=150, arc_start_deg=105, euler=(90, 0, 0)))

    helmet = m.part("Helmet", (0, 1.28, 0))
    helmet.add("Team", torus((0, 1.3, 0), 0.215, 0.036, 36, 12))
    helmet.add("Glass", ellipsoid((0, 1.555, 0), (0.31, 0.3, 0.31), 36, 22))
    helmet.add(METAL_DARK, rounded_box((0, 1.86, 0), (0.03, 0.012, 0.03), 0.4))

    m.marker("gunMount", (0.24, 1.25, 0.2))  # where the held weapon's grip goes
    return m


# ============================================================================ weapons

def along_z(profile, pos, seg=24):
    """Lathe around +z instead of +y (for barrels), profile y -> z."""
    return lathe(profile, seg=seg, pos=pos, euler=(90, 0, 0))


def pew_rifle():
    """Chunky toy blaster, grip at origin, barrel along +z (0.7 m)."""
    m = Model("pew_rifle")
    p = m.part("Body")
    toy, orange, dark = "Color:f4f1e8", "Color:ff8f33", "Color:3d4150"
    p.add(toy, along_z([(0, -0.08), (0.045, -0.075), (0.07, -0.03), (0.078, 0.1), (0.074, 0.3), (0.06, 0.44), (0, 0.46)], (0, 0, 0)))
    p.add(orange, rounded_box((0, 0.075, 0.18), (0.052, 0.03, 0.2), 0.35))
    p.add(orange, *[rounded_box((0.05 * s, 0.035, -0.02), (0.012, 0.045, 0.06), 0.4, euler=(15, 0, 0)) for s in (-1, 1)])  # fins
    p.add(dark, rounded_box((0, -0.1, 0.02), (0.03, 0.075, 0.036), 0.45, euler=(-15, 0, 0)))  # grip
    p.add(dark, torus((0, -0.065, 0.1), 0.042, 0.008, arc_deg=180, arc_start_deg=0, euler=(0, 90, 90)))  # trigger guard
    p.add(dark, along_z([(0, 0.44), (0.032, 0.44), (0.032, 0.62), (0, 0.62)], (0, 0, 0), seg=18))  # barrel
    p.add("Glow:ff9a40", torus((0, 0, 0.62), 0.036, 0.012, 24, 8, euler=(90, 0, 0)))
    for s in (-1, 1):  # energy cells
        p.add("Glow:ff9a40", along_z(capsule_profile(0.024, 0.13), (0.072 * s, -0.005, 0.1), seg=14))
        p.add(dark, along_z([(0, 0.0), (0.028, 0.0), (0.028, 0.02), (0, 0.02)], (0.072 * s, -0.005, 0.095), seg=14))
    p.add(dark, rounded_box((0, 0.118, 0.34), (0.012, 0.02, 0.015), 0.4))  # sight
    p.add(dark, tube([(-0.03, 0.09, 0.02), (-0.034, 0.2, 0.0), (-0.03, 0.3, -0.02)], 0.006, seg=6))
    p.add("Glow:ff4a5a", sphere((-0.03, 0.31, -0.02), 0.026, 14, 8))
    m.marker("muzzle", (0, 0, 0.64))
    return m


def long_zapper():
    """Absurdly long sniper with glowing coils and a satellite-dish scope, 1.3 m."""
    m = Model("long_zapper")
    p = m.part("Body")
    purple, dark, toy, silver = "Color:7f6bc7", "Color:3d4150", "Color:f4f1e8", "Color:d8dce6"
    p.add(purple, rounded_box((0, 0, 0.14), (0.055, 0.07, 0.24), 0.3))
    p.add(dark, rounded_box((0, -0.02, -0.17), (0.045, 0.065, 0.11), 0.35))
    p.add(dark, rounded_box((0, -0.11, 0.02), (0.03, 0.075, 0.036), 0.45, euler=(-15, 0, 0)))
    p.add(dark, torus((0, -0.07, 0.1), 0.042, 0.008, arc_deg=180, euler=(0, 90, 90)))
    p.add(toy, along_z([(0, 0.36), (0.03, 0.36), (0.028, 1.18), (0.05, 1.22), (0.052, 1.28), (0.032, 1.29), (0, 1.29)], (0, 0.01, 0), seg=20))
    for z in (0.55, 0.78, 1.01):
        p.add("Glow:73f2ff", torus((0, 0.01, z), 0.042, 0.013, 24, 8, euler=(90, 0, 0)))
    # Dish scope on a little stem, tilted forward.
    p.add(dark, along_z([(0, 0), (0.012, 0), (0.012, 0.08), (0, 0.08)], (0, 0.07, 0.1), seg=10).transformed(euler=(0, 0, 0)))
    dish = lathe([(0, -0.012), (0.11, 0.02), (0.115, 0.032), (0.1, 0.03), (0, 0.004)], seg=28)
    p.add(silver, dish.transformed(pos=(0, 0.16, 0.12), euler=(-70, 0, 0)))
    p.add(dark, tube([(0, 0.16, 0.12), (0, 0.21, 0.17)], 0.006, seg=6))
    p.add("Glow:73f2ff", sphere((0, 0.215, 0.175), 0.02, 12, 8))
    m.marker("muzzle", (0, 0.01, 1.3))
    return m


# ============================================================================ world props

def mushroom():
    """Giant alien mushroom, 4 m to the cap underside, cap radius ~1.65 m. Scaled uniformly in game."""
    m = Model("mushroom")
    stalk = m.part("Stalk")
    stalk.add("Color:f5e8c8", lathe([(0, 0), (0.55, 0), (0.5, 0.15), (0.4, 0.6), (0.35, 1.6), (0.34, 2.6), (0.38, 3.4), (0.46, 3.85), (0, 3.9)], seg=28))
    stalk.add("Color:ffd9e0", torus((0, 2.55, 0), 0.4, 0.08, 28, 10))  # skirt ring
    cap = m.part("Cap")
    cap.add("Color:ffc2d1", lathe([(0, 3.72), (0.9, 3.74), (1.62, 3.8)], seg=40))  # gills side (underside)
    cap.add("Color:fb6f9b", lathe([(1.62, 3.8), (1.72, 3.9), (1.7, 4.1), (1.5, 4.45), (1.1, 4.8), (0.55, 5.02), (0, 5.08)], seg=40))
    # Spots sit on the dome surface, flattened along its normal (from the cap profile).
    dome = [(1.7, 4.1), (1.5, 4.45), (1.1, 4.8), (0.55, 5.02), (0, 5.08)]
    rng = np.random.default_rng(4)
    for k in range(10):
        a = k * 2.3998 + 0.4
        t = 0.08 + 0.8 * ((k * 0.37) % 1.0) ** 0.8
        seg = min(int(t * (len(dome) - 1)), len(dome) - 2)
        f = t * (len(dome) - 1) - seg
        (r0, y0), (r1, y1) = dome[seg], dome[seg + 1]
        r, y = r0 + (r1 - r0) * f, y0 + (y1 - y0) * f
        nr, ny = (y1 - y0), -(r1 - r0)  # outward normal in the (r, y) plane
        ln = math.hypot(nr, ny)
        nr, ny = nr / ln, ny / ln
        size = rng.uniform(0.2, 0.34) * (1 - 0.4 * t)
        pos = (math.sin(a) * (r + nr * 0.01), y + ny * 0.01, math.cos(a) * (r + nr * 0.01))
        tilt = math.degrees(math.atan2(nr, ny))
        cap.add("Color:fffaf0", ellipsoid(pos, (size, size * 0.28, size), 18, 8, euler=(tilt, math.degrees(a), 0)))
    for k in range(10):
        a = k * math.tau / 10
        cap.add("Glow:73fbe6", sphere((math.sin(a) * 1.25, 3.68, math.cos(a) * 1.25), 0.07, 10, 6))
    return m


def dropship():
    """Crashed dropship, ~22 m long, nose toward +z. Team-colored wings, fin and stripe."""
    m = Model("dropship")
    hull = m.part("Hull")
    white, panel, dark = "Color:e9e5db", "Color:a3abbd", "Color:4a4f60"
    hull.add(white, along_z([(0, -10.5), (2.3, -10.2), (2.9, -7), (3.2, -2), (3.1, 3), (2.6, 7), (1.6, 10), (0.6, 11.2), (0, 11.4)], (0, 0, 0), seg=40))
    hull.add("Team", along_z([(3.18, -1.2), (3.28, -1.0), (3.26, 1.6), (3.16, 1.8)], (0, 0, 0), seg=40).transformed())
    hull.add("Glow:7fe6ff", ellipsoid((0, 1.7, 7.2), (1.5, 1.1, 2.5), 32, 16))  # cockpit canopy
    hull.add(panel, *[rounded_box((0, 3.05, z), (0.6, 0.08, 0.9), 0.35) for z in (-5, -2.5, 0)])
    for s in (-1, 1):
        wing_euler = (0, 0, -7 * s) if s > 0 else (6, 0, 14)  # left wing bent from the crash
        hull.add("Team", rounded_box((4.6 * s, -0.6, -1.5), (2.8, 0.22, 2.1), 0.3, euler=wing_euler))
        hull.add(dark, lathe([(0, 0), (1.0, 0), (1.15, 0.6), (1.1, 3.0), (0.8, 3.4), (0, 3.4)], seg=24).transformed(pos=(1.7 * s, -0.6, -8.4), euler=(-90, 0, 0)))
        hull.add("Glow:ff9a40", along_z([(0, -12.0), (0.85, -11.98), (0.85, -11.96), (0, -11.95)], (1.7 * s, -0.6, 0), seg=24))
    hull.add("Team", rounded_box((0, 3.6, -7.6), (0.25, 2.0, 1.6), 0.3, euler=(22, 0, 0)))  # tail fin
    hull.add(dark, *[rounded_box((0.9 * s, -2.9, 3), (0.12, 0.6, 0.12), 0.4, euler=(0, 0, 25 * s)) for s in (-1, 1)])  # landing struts
    return m


def rocks():
    out = []
    for i, seed in enumerate((11, 23, 37)):
        m = Model("rock_" + "abc"[i])
        m.part("Rock").add("Tint", rock(seed))
        out.append(m)
    return out


def crate():
    """1 m supply crate: rounded body, metal edge frame, glowing label panel."""
    m = Model("crate")
    p = m.part("Crate")
    p.add("Color:f29a33", rounded_box((0, 0.5, 0), (0.46, 0.46, 0.46), 0.18, seg_u=24, seg_v=12))
    frame = "Color:4a4f60"
    lo = dict(seg_u=12, seg_v=6)
    for a in (-0.44, 0.44):
        for b in (-0.44, 0.44):
            p.add(frame, rounded_box((a, 0.5, b), (0.06, 0.5, 0.06), 0.35, **lo))
            p.add(frame, rounded_box((a, 0.06 if b < 0 else 0.94, 0), (0.06, 0.06, 0.5), 0.35, **lo))
            p.add(frame, rounded_box((0, 0.06 if a < 0 else 0.94, b), (0.5, 0.06, 0.06), 0.35, **lo))
    for z in (-0.47, 0.47):
        p.add("Glow:73f2ff", rounded_box((0, 0.55, z), (0.18, 0.1, 0.012), 0.3))
    return m


MODELS = [grunt, pew_rifle, long_zapper, mushroom, dropship, crate]


def all_models():
    out = []
    for fn in MODELS:
        out.append(fn())
    out.extend(rocks())
    return out


def preview_json(model):
    """Flattened triangles + per-vertex color for the WebGL preview."""
    team = (0.25, 0.5, 0.95)
    parts = []
    for p in model.parts:
        for mat, geos in p.groups.items():
            g = merge(geos)
            if mat.startswith(("Color:", "Glow:")):
                hexv = mat.split(":")[1]
                col = tuple(int(hexv[i:i + 2], 16) / 255 for i in (0, 2, 4))
            elif mat in ("Team", "GlowTeam"):
                col = team
            elif mat == "TeamDark":
                col = tuple(c * 0.75 for c in team)
            elif mat == "TeamLight":
                col = tuple(c + (1 - c) * 0.4 for c in team)
            elif mat == "Glass":
                col = (0.75, 0.95, 1.0)
            else:
                col = (0.55, 0.5, 0.72)
            parts.append({
                "v": np.round(g.v, 4).reshape(-1).tolist(),
                "n": np.round(g.n, 3).reshape(-1).tolist(),
                "f": g.f.reshape(-1).tolist(),
                "c": col,
                "glow": mat.startswith("Glow") or mat == "GlowTeam",
                "glass": mat == "Glass",
            })
    return {"name": model.name, "parts": parts}


def main():
    os.makedirs(OUT, exist_ok=True)
    models = all_models()
    for m in models:
        data = m.to_bytes()
        with open(os.path.join(OUT, m.name + ".bytes"), "wb") as fh:
            fh.write(data)
        verts, tris = m.stats()
        lo, hi = m.bounds()
        print(f"{m.name:12s} {verts:6d} verts {tris:6d} tris  {len(data) / 1024:7.1f} KB  "
              f"size {np.round(hi - lo, 2).tolist()}")
    if "--preview" in sys.argv:
        out_dir = sys.argv[sys.argv.index("--preview") + 1]
        os.makedirs(out_dir, exist_ok=True)
        with open(os.path.join(out_dir, "models.js"), "w") as fh:
            fh.write("window.MODELS = " + json.dumps([preview_json(m) for m in models]) + ";")


if __name__ == "__main__":
    main()
