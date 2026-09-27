"""Composes a lineup of models for a preview render (not used by the game)."""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import generate
from meshkit import merge

TEAMS = [(0.92, 0.28, 0.3), (0.25, 0.5, 0.95), (0.95, 0.8, 0.15), (0.55, 0.95, 0.3)]


def place(model, pos, yaw, scale, team, out, tint=(0.55, 0.5, 0.72)):
    from meshkit import rot_matrix
    r = rot_matrix((0, yaw, 0))
    for p in model.parts:
        for mat, geos in p.groups.items():
            g = merge(geos)
            v = (g.v * scale) @ r.T + np.asarray(pos)
            n = g.n @ r.T
            if mat.startswith(("Color:", "Glow:")):
                h = mat.split(":")[1]
                col = tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
            elif mat in ("Team", "GlowTeam"):
                col = team
            elif mat == "TeamDark":
                col = tuple(c * 0.72 for c in team)
            elif mat == "TeamLight":
                col = tuple(c + (1 - c) * 0.4 for c in team)
            elif mat == "Glass":
                col = (0.75, 0.95, 1.0)
            else:
                col = tint
            out.append({"v": np.round(v, 4).reshape(-1).tolist(), "n": np.round(n, 3).reshape(-1).tolist(),
                        "f": g.f.reshape(-1).tolist(), "c": col,
                        "glow": mat.startswith("Glow") or mat == "GlowTeam", "glass": mat == "Glass"})


def main(out_dir):
    grunt, rifle, zapper, mush, rock = generate.grunt(), generate.pew_rifle(), generate.long_zapper(), generate.mushroom(), generate.rocks()[1]
    parts = []
    mount = grunt.markers["gunMount"]
    for k, (x, yaw, weapon) in enumerate([(-1.5, 20, rifle), (-0.5, 5, zapper), (0.5, -5, rifle), (1.5, -20, rifle)]):
        team = TEAMS[k]
        from meshkit import rot_matrix
        place(grunt, (x, 0, 0), yaw, 1.0, team, parts)
        gun_pos = rot_matrix((0, yaw, 0)) @ mount + np.array([x, 0, 0])
        place(weapon, gun_pos, yaw, 1.0, team, parts)
    place(mush, (-3.2, 0, -2.5), 30, 0.55, (1, 1, 1), parts)
    place(rock, (3.0, 0.45, -1.5), 40, 0.75, (1, 1, 1), parts, tint=(0.43, 0.4, 0.62))
    with open(os.path.join(out_dir, "scene.js"), "w") as fh:
        fh.write("window.MODELS = (window.MODELS||[]).concat([" + json.dumps({"name": "lineup", "parts": parts}) + "]);")


if __name__ == "__main__":
    main(sys.argv[1])
