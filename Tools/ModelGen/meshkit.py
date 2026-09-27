"""
Tiny procedural modeling kit for Space Grunts.

Coordinates are Unity's: x right, y up, z forward, meters. A triangle (a, b, c) faces the
direction of cross(b - a, c - a), which is Unity's front-face convention; every generator
below produces outward-facing triangles. Smooth normals are accumulated from face normals.
"""
import math
import struct
import numpy as np


# ----------------------------------------------------------------------------- transforms

def rot_matrix(euler_deg):
    """Unity Euler (degrees): applied Z, then X, then Y."""
    x, y, z = (math.radians(a) for a in euler_deg)
    rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return ry @ rx @ rz


class Geo:
    """Triangle soup with per-vertex normals."""

    def __init__(self, v, n, f):
        self.v = np.asarray(v, dtype=np.float64).reshape(-1, 3)
        self.n = np.asarray(n, dtype=np.float64).reshape(-1, 3)
        self.f = np.asarray(f, dtype=np.int64).reshape(-1, 3)

    def transformed(self, pos=(0, 0, 0), euler=(0, 0, 0), scale=(1, 1, 1)):
        s = np.asarray(scale, dtype=np.float64)
        r = rot_matrix(euler)
        v = (self.v * s) @ r.T + np.asarray(pos)
        n = (self.n / s) @ r.T
        n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        f = self.f
        if np.prod(np.sign(s)) < 0:  # mirrored: keep faces pointing outward
            f = f[:, [0, 2, 1]]
        return Geo(v, n, f)

    def mirrored_x(self):
        return self.transformed(scale=(-1, 1, 1))


def merge(geos):
    vs, ns, fs, off = [], [], [], 0
    for g in geos:
        vs.append(g.v)
        ns.append(g.n)
        fs.append(g.f + off)
        off += len(g.v)
    if not vs:
        return Geo(np.zeros((0, 3)), np.zeros((0, 3)), np.zeros((0, 3), dtype=np.int64))
    return Geo(np.vstack(vs), np.vstack(ns), np.vstack(fs))


# ----------------------------------------------------------------------------- grid surfaces

def grid(points, wrap_u=True, smooth=True):
    """
    points[i, j]: i runs around (u), j runs along (v). Face normal = cross(dP/du, dP/dv).
    wrap_u joins the last column back to the first (closed around).
    """
    nu, nv, _ = points.shape
    v = points.reshape(-1, 3)
    idx = np.arange(nu * nv).reshape(nu, nv)
    faces = []
    for i in range(nu if wrap_u else nu - 1):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            a, b, c, d = idx[i, j], idx[i2, j], idx[i2, j + 1], idx[i, j + 1]
            faces.append((a, b, c))
            faces.append((a, c, d))
    f = np.array(faces, dtype=np.int64)
    n = smooth_normals(v, f)
    # Rows that collapse to a point (poles): give every copy the same averaged normal.
    for j in range(nv):
        row = points[:, j, :]
        if np.max(np.linalg.norm(row - row.mean(axis=0), axis=1)) < 1e-9:
            avg = n[idx[:, j]].mean(axis=0)
            nrm = np.linalg.norm(avg)
            if nrm > 1e-9:
                n[idx[:, j]] = avg / nrm
    return Geo(v, n, f)


def smooth_normals(v, f):
    fn = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    n = np.zeros_like(v)
    for k in range(3):
        np.add.at(n, f[:, k], fn)
    nrm = np.linalg.norm(n, axis=1, keepdims=True)
    return n / np.maximum(nrm, 1e-12)


def flat(g):
    """Faceted look: every triangle gets its own vertices and face normal."""
    v = g.v[g.f].reshape(-1, 3)
    fn = np.cross(g.v[g.f[:, 1]] - g.v[g.f[:, 0]], g.v[g.f[:, 2]] - g.v[g.f[:, 0]])
    fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)
    n = np.repeat(fn, 3, axis=0)
    f = np.arange(len(v)).reshape(-1, 3)
    return Geo(v, n, f)


# ----------------------------------------------------------------------------- primitives

def _spow(x, e):
    return np.sign(x) * np.abs(x) ** e


def superellipsoid(center, radii, e_lat=1.0, e_lon=1.0, seg_u=28, seg_v=16, euler=(0, 0, 0)):
    """e = 1: ellipsoid. e -> 0.2: rounded box. Oriented so normals face outward."""
    phi = np.linspace(0, 2 * math.pi, seg_u, endpoint=False)
    theta = np.linspace(-math.pi / 2, math.pi / 2, seg_v + 1)
    P, T = np.meshgrid(phi, theta, indexing="ij")
    ct, st = _spow(np.cos(T), e_lat), _spow(np.sin(T), e_lat)
    x = radii[0] * ct * _spow(np.sin(P), e_lon)
    y = radii[1] * st
    z = radii[2] * ct * _spow(np.cos(P), e_lon)
    g = grid(np.stack([x, y, z], axis=-1))
    return g.transformed(pos=center, euler=euler)


def ellipsoid(center, radii, seg_u=28, seg_v=16, euler=(0, 0, 0)):
    return superellipsoid(center, radii, 1.0, 1.0, seg_u, seg_v, euler)


def sphere(center, r, seg_u=24, seg_v=14):
    return ellipsoid(center, (r, r, r), seg_u, seg_v)


def rounded_box(center, half, roundness=0.25, euler=(0, 0, 0), seg_u=28, seg_v=16):
    return superellipsoid(center, half, roundness, roundness, seg_u, seg_v, euler)


def lathe(profile, seg=28, pos=(0, 0, 0), euler=(0, 0, 0), scale=(1, 1, 1)):
    """
    Surface of revolution around +y. profile: [(radius, y), ...]. Walk it so the solid is on
    your right-hand side when moving: start at the bottom center, go outward, up the outside,
    and back in along the top. That keeps normals facing outward.
    """
    phi = np.linspace(0, 2 * math.pi, seg, endpoint=False)
    prof = np.asarray(profile, dtype=np.float64)
    r = prof[:, 0][None, :]
    y = np.broadcast_to(prof[:, 1][None, :], (seg, len(prof)))
    x = r * np.sin(phi)[:, None]
    z = r * np.cos(phi)[:, None]
    g = grid(np.stack([x, y, z], axis=-1))
    return g.transformed(pos=pos, euler=euler, scale=scale)


def capsule_profile(radius, height, rings=6):
    """Profile for a capsule standing on y=0 with total height `height`."""
    pts = [(0.0, 0.0)]
    for k in range(1, rings + 1):
        a = -math.pi / 2 + (math.pi / 2) * k / rings
        pts.append((radius * math.cos(a), radius + radius * math.sin(a)))
    for k in range(0, rings + 1):
        a = (math.pi / 2) * k / rings
        pts.append((radius * math.cos(a), height - radius + radius * math.sin(a)))
    return pts


def torus(center, big_r, small_r, seg_u=32, seg_v=12, arc_deg=360.0, arc_start_deg=0.0, euler=(0, 0, 0)):
    closed = abs(arc_deg - 360.0) < 1e-6
    u = np.linspace(math.radians(arc_start_deg), math.radians(arc_start_deg + arc_deg), seg_u, endpoint=not closed)
    v = np.linspace(0, 2 * math.pi, seg_v + 1)
    U, V = np.meshgrid(u, v, indexing="ij")
    rr = big_r + small_r * np.cos(V)
    pts = np.stack([rr * np.sin(U), small_r * np.sin(V), rr * np.cos(U)], axis=-1)
    g = grid(pts, wrap_u=closed)
    return g.transformed(pos=center, euler=euler)


def tube(path, radius, seg=10, samples=24, cap=True):
    """Smooth tube through control points (Catmull-Rom), with rounded caps."""
    path = [np.asarray(p, dtype=np.float64) for p in path]
    if len(path) == 2:
        pts = [path[0] + (path[1] - path[0]) * t for t in np.linspace(0, 1, samples)]
    else:
        ext = [path[0] * 2 - path[1]] + path + [path[-1] * 2 - path[-2]]
        pts = []
        per = max(2, samples // (len(path) - 1))
        for s in range(1, len(ext) - 2):
            p0, p1, p2, p3 = ext[s - 1], ext[s], ext[s + 1], ext[s + 2]
            for t in np.linspace(0, 1, per, endpoint=(s == len(ext) - 3)):
                pts.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                  + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    pts = np.array(pts)
    tangents = np.gradient(pts, axis=0)
    tangents /= np.linalg.norm(tangents, axis=1, keepdims=True)
    ref = np.array([0.0, 1.0, 0.0]) if abs(tangents[0][1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    rings = []
    normal = np.cross(np.cross(tangents[0], ref), tangents[0])
    for i, t in enumerate(tangents):
        normal = normal - t * np.dot(normal, t)
        normal /= np.linalg.norm(normal)
        # u runs around the ring and v along the path, so the face normal is cross(dP/du, dP/dv)
        # = cross(B, T); with B = cross(T, N) that equals +N (outward).
        binormal = np.cross(t, normal)
        ang = np.linspace(0, 2 * math.pi, seg, endpoint=False)
        rings.append(pts[i] + radius * (np.cos(ang)[:, None] * normal + np.sin(ang)[:, None] * binormal))
    grid_pts = np.stack(rings, axis=1)  # (seg, len, 3): u around, v along
    body = grid(grid_pts)
    parts = [body]
    if cap:
        parts.append(sphere(pts[0], radius, 10, 6))
        parts.append(sphere(pts[-1], radius, 10, 6))
    return merge(parts)


def rock(seed, subdiv=2, bumpiness=0.22):
    """Faceted, lumpy unit rock (radius ~1) from a noisy icosphere."""
    rng = np.random.default_rng(seed)
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    v = [np.array(p, dtype=np.float64) / np.linalg.norm(p) for p in v]
    for _ in range(subdiv):
        cache, nf = {}, []

        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                m = v[a] + v[b]
                v.append(m / np.linalg.norm(m))
                cache[key] = len(v) - 1
            return cache[key]

        for a, b, c in f:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    V = np.array(v)
    # Smooth-ish lumps: blend a few random directional bumps, then flatten the bottom a little.
    bumps = rng.normal(size=(6, 3))
    bumps /= np.linalg.norm(bumps, axis=1, keepdims=True)
    amp = rng.uniform(0.3, 1.0, size=6)
    disp = 1 + bumpiness * (np.maximum(V @ bumps.T, 0) ** 3 @ amp - 0.3) + rng.uniform(-0.05, 0.05, size=len(V))
    V = V * disp[:, None]
    V[:, 1] = np.where(V[:, 1] < -0.55, -0.55 - (V[:, 1] + 0.55) * 0.2, V[:, 1])
    F = np.array(f, dtype=np.int64)
    # Make sure faces point outward.
    fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]])
    centers = V[F].mean(axis=1)
    flip = (fn * centers).sum(axis=1) < 0
    F[flip] = F[flip][:, [0, 2, 1]]
    return flat(Geo(V, np.zeros_like(V), F))


# ----------------------------------------------------------------------------- models

class Part:
    def __init__(self, name, pivot=(0, 0, 0)):
        self.name = name
        self.pivot = np.asarray(pivot, dtype=np.float64)
        self.groups = {}  # material -> [Geo]

    def add(self, material, *geos):
        self.groups.setdefault(material, []).extend(geos)
        return self


class Model:
    def __init__(self, name):
        self.name = name
        self.parts = []
        self.markers = {}

    def part(self, name, pivot=(0, 0, 0)):
        p = Part(name, pivot)
        self.parts.append(p)
        return p

    def marker(self, name, pos):
        self.markers[name] = np.asarray(pos, dtype=np.float64)

    def stats(self):
        verts = sum(len(g.v) for p in self.parts for gs in p.groups.values() for g in gs)
        tris = sum(len(g.f) for p in self.parts for gs in p.groups.values() for g in gs)
        return verts, tris

    def bounds(self):
        allv = np.vstack([g.v for p in self.parts for gs in p.groups.values() for g in gs])
        return allv.min(axis=0), allv.max(axis=0)

    # Binary format "SGM1" (little endian), read by ArenaShooter.Core.ModelFormat:
    #   magic, int partCount, parts[name, float3 pivot, int vc, float3[vc] pos (pivot-relative),
    #   float3[vc] normals, int subCount, sub[material, int ic, int[ic]]], int markerCount, markers[name, float3]
    def to_bytes(self):
        out = bytearray(b"SGM1")

        def s(text):
            b = text.encode("utf-8")
            out.extend(struct.pack("<i", len(b)))
            out.extend(b)

        out.extend(struct.pack("<i", len(self.parts)))
        for p in self.parts:
            s(p.name)
            out.extend(struct.pack("<3f", *p.pivot))
            mats = list(p.groups.keys())
            merged = [merge(p.groups[m]) for m in mats]
            base, vs, ns, subs = 0, [], [], []
            for m, g in zip(mats, merged):
                vs.append(g.v - p.pivot)
                ns.append(g.n)
                subs.append((m, (g.f + base).reshape(-1)))
                base += len(g.v)
            V = np.vstack(vs).astype("<f4")
            N = np.vstack(ns).astype("<f4")
            out.extend(struct.pack("<i", len(V)))
            out.extend(V.tobytes())
            out.extend(N.tobytes())
            out.extend(struct.pack("<i", len(subs)))
            for m, idx in subs:
                s(m)
                out.extend(struct.pack("<i", len(idx)))
                out.extend(idx.astype("<i4").tobytes())
        out.extend(struct.pack("<i", len(self.markers)))
        for name, pos in self.markers.items():
            s(name)
            out.extend(struct.pack("<3f", *pos))
        return bytes(out)
