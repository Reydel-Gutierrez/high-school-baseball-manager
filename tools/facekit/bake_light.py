"""Bake light transport for the studio headshot into UV space.

light.png  R ambient occlusion (1 = open sky, 0 = fully occluded)
           G key-light visibility (soft shadow from the studio key)
           B fill-light visibility (soft shadow from the fill)
           A cavity (sharp local occlusion, creases and nostrils)

The head is voxelized (2 mm) and rays are marched from every covered
texel. Baked on the average base head; morphs move features a few mm,
which the soft shadows tolerate.
"""
import sys
import numpy as np
from PIL import Image

S = int(sys.argv[1]) if len(sys.argv) > 1 else 1024
KEY = np.array([0.55, 0.45, 0.75]); KEY /= np.linalg.norm(KEY)
FILL = np.array([-0.8, 0.05, 0.55]); FILL /= np.linalg.norm(FILL)

k = np.load('out/kit.npz')
pos = k['pos']; r = k['render']; skin = k['skin']
uv = r[:, 1:3]; sub = r[:, 0].astype(int)
eye_mid = (pos[k['eye_l']].mean(0) + pos[k['eye_r']].mean(0)) / 2
P = pos - eye_mid
nrm = np.zeros_like(P)
for t in skin:
    a, b, c = sub[t]
    n = np.cross(P[c] - P[a], P[b] - P[a])
    nrm[a] += n; nrm[b] += n; nrm[c] += n
nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-9

# ---- voxelize skin + eyeballs (eyes occlude the lids' undersides)
VOX = 0.002
lo = P.min(0) - 0.02; hi = P.max(0) + 0.02
dims = np.ceil((hi - lo) / VOX).astype(int) + 1
occ = np.zeros(dims, bool)
def mark(pts):
    ij = np.floor((pts - lo) / VOX).astype(int)
    ij = np.clip(ij, 0, dims - 1)
    occ[ij[:, 0], ij[:, 1], ij[:, 2]] = True
for t in skin:
    a, b, c = P[sub[t]]
    edge = max(np.linalg.norm(b - a), np.linalg.norm(c - a), np.linalg.norm(c - b))
    m = max(2, int(edge / (VOX * 0.5)) + 1)
    u, v = np.meshgrid(np.linspace(0, 1, m), np.linspace(0, 1, m))
    keep = (u + v) <= 1
    u = u[keep][:, None]; v = v[keep][:, None]
    mark(a + u * (b - a) + v * (c - a))
for g in ('eye_l', 'eye_r'):
    ids = k[g]; ctr = P[ids].mean(0); rad = np.linalg.norm(P[ids] - ctr, axis=1).mean() * 0.96
    d = np.random.default_rng(1).normal(size=(4000, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
    for rr in np.linspace(0.3, 1.0, 5):
        mark(ctr + d * rad * rr)
print('voxels', dims, occ.sum())

# ---- rasterize texel positions / normals (same as bake_masks)
X = np.zeros((S, S, 3), np.float32); NN = np.zeros((S, S, 3), np.float32); cov = np.zeros((S, S), bool)
Pr = P[sub]; Nr = nrm[sub]
for t in skin:
    tuv = uv[t] * [S, S]; tuv[:, 1] = S - tuv[:, 1]
    x0, y0 = np.floor(tuv.min(0)).astype(int); x1, y1 = np.ceil(tuv.max(0)).astype(int)
    x0 = max(x0, 0); y0 = max(y0, 0); x1 = min(x1, S - 1); y1 = min(y1, S - 1)
    if x1 < x0 or y1 < y0: continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = tuv
    den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(den) < 1e-12: continue
    w0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
    w1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
    w2 = 1 - w0 - w1
    m = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
    if not m.any(): continue
    yy = ys[m].astype(int); xx = xs[m].astype(int)
    X[yy, xx] = w0[m, None] * Pr[t[0]] + w1[m, None] * Pr[t[1]] + w2[m, None] * Pr[t[2]]
    NN[yy, xx] = w0[m, None] * Nr[t[0]] + w1[m, None] * Nr[t[1]] + w2[m, None] * Nr[t[2]]
    cov[yy, xx] = True
NN /= np.linalg.norm(NN, axis=2, keepdims=True) + 1e-9

idx = np.argwhere(cov)
pts = X[cov]; nor = NN[cov]
print('texels', len(pts))
rng = np.random.default_rng(7)

def march(origins, dirs, max_d, step=VOX * 0.9):
    """Fraction of rays that escape within max_d (1 = unoccluded)."""
    n = len(origins)
    hit = np.zeros(n, bool)
    o = origins + dirs * (VOX * 2.2)          # leave the surface's own voxels
    steps = int(max_d / step)
    for s in range(steps):
        q = o + dirs * (s * step)
        ij = np.floor((q - lo) / VOX).astype(int)
        inside = np.all((ij >= 0) & (ij < dims), axis=1)
        h = np.zeros(n, bool)
        ii = ij[inside]
        h[inside] = occ[ii[:, 0], ii[:, 1], ii[:, 2]]
        hit |= h
    return ~hit

def hemisphere(normals, count):
    """Cosine-weighted directions around each normal: (n, count, 3)."""
    u1 = rng.random((len(normals), count)); u2 = rng.random((len(normals), count))
    rr = np.sqrt(u1); ph = 2 * np.pi * u2
    lx = rr * np.cos(ph); ly = rr * np.sin(ph); lz = np.sqrt(1 - u1)
    t = np.where(np.abs(normals[:, :1]) < 0.9, np.array([[1, 0, 0]]), np.array([[0, 1, 0]]))
    tx = np.cross(normals, t); tx /= np.linalg.norm(tx, axis=1, keepdims=True)
    ty = np.cross(normals, tx)
    return lx[..., None] * tx[:, None] + ly[..., None] * ty[:, None] + lz[..., None] * normals[:, None]

def cone(direction, count, spread):
    d = rng.normal(size=(count, 3)) * spread + direction
    return d / np.linalg.norm(d, axis=1, keepdims=True)

def accumulate(dir_fn, rays, max_d, chunk=6000):
    out = np.zeros(len(pts))
    for s in range(0, len(pts), chunk):
        o = pts[s:s + chunk]; nn = nor[s:s + chunk]
        dirs = dir_fn(nn, rays)                     # (m, rays, 3)
        m = len(o)
        oo = np.repeat(o, rays, axis=0); dd = dirs.reshape(-1, 3)
        vis = march(oo, dd, max_d).reshape(m, rays)
        out[s:s + chunk] = vis.mean(1)
    return out

ao = accumulate(lambda nn, c: hemisphere(nn, c), 40, 0.10)
cavity = accumulate(lambda nn, c: hemisphere(nn, c), 24, 0.012)
def light_dirs(L, spread):
    def f(nn, c):
        d = np.broadcast_to(cone(L, c, spread), (len(nn), c, 3)).copy()
        # rays that point into the surface count as shadowed by the surface itself
        return d
    return f
key = accumulate(light_dirs(KEY, 0.16), 28, 0.35)
fill = accumulate(light_dirs(FILL, 0.30), 20, 0.35)
print('ao', ao.mean(), 'key', key.mean())

def to8(a): return (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
img = np.zeros((S, S, 4), np.uint8)
for ch, val in enumerate((ao, key, fill, cavity)):
    img[idx[:, 0], idx[:, 1], ch] = to8(val)
filled = cov.copy()
for _ in range(48):
    grow = filled.copy()
    for a in (0, 1):
        for s in (1, -1):
            im2 = np.roll(img, s, axis=a); c2 = np.roll(filled, s, axis=a)
            m = (~grow) & c2
            img[m] = im2[m]; grow |= m
    filled = grow
# soften: a light blur keeps texel noise out of the shading
im = Image.fromarray(img, 'RGBA')
from PIL import ImageFilter
r_, g_, b_, a_ = im.split()
r_ = r_.filter(ImageFilter.GaussianBlur(1.6)); g_ = g_.filter(ImageFilter.GaussianBlur(1.2))
b_ = b_.filter(ImageFilter.GaussianBlur(2.0)); a_ = a_.filter(ImageFilter.GaussianBlur(0.8))
Image.merge('RGBA', (r_, g_, b_, a_)).save('out/light.png')
print('baked light', S)
