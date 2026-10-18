"""Bake per-texel 3D fields for the skin shader into UV-space PNGs.

masks.png   R lips   G brows (strand-detailed)   B beard zone   A mustache zone
detail.png  R redness  G cavity/AO  B chin/goatee zone  A pore noise
hair.png    R hair coverage field (0.5 = natural hairline, >0.5 inside)
            G crown (1 = top of head, for balding)  B fade height (0 top, 1 low sides/nape)
            A strand phase noise
"""
import numpy as np, sys
from PIL import Image
S = int(sys.argv[1]) if len(sys.argv) > 1 else 2048
k = np.load('out/kit.npz')
pos = k['pos']; r = k['render']; skin = k['skin']
uv = r[:, 1:3]; sub = r[:, 0].astype(int)
P = pos[sub].copy()
eye_mid = (pos[k['eye_l']].mean(0) + pos[k['eye_r']].mean(0)) / 2
P -= eye_mid   # eye-centred frame: x right(+ = model's left), y up, z forward
# vertex normals (welded)
nrm = np.zeros_like(pos)
for t in skin:
    a, b, c = sub[t]
    n = np.cross(pos[c] - pos[a], pos[b] - pos[a])
    nrm[a] += n; nrm[b] += n; nrm[c] += n
nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-9
N = nrm[sub]

X = np.zeros((S, S, 3), np.float32); NN = np.zeros((S, S, 3), np.float32)
cov = np.zeros((S, S), bool); island = np.zeros((S, S), np.int8)
# raster
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
    p = w0[m, None] * P[t[0]] + w1[m, None] * P[t[1]] + w2[m, None] * P[t[2]]
    n = w0[m, None] * N[t[0]] + w1[m, None] * N[t[1]] + w2[m, None] * N[t[2]]
    X[yy, xx] = p; NN[yy, xx] = n; cov[yy, xx] = True
    island[yy, xx] = 1 if uv[t[0], 0] < 0.2 else (2 if (uv[t[0], 1] < 0.17 and uv[t[0], 0] > 0.7) else 0)

x, y, z = X[..., 0], X[..., 1], X[..., 2]
ax_ = np.abs(x)
ear = island == 1; mouth_in = island == 2
front_hard = z > 0.0

def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

front = smooth(-0.02, 0.025, z)

def hash2(a, b):
    return np.modf(np.sin(a * 127.1 + b * 311.7) * 43758.5453)[0] % 1.0

def vnoise(a, b, scale):
    a = a * scale; b = b * scale
    ia = np.floor(a); ib = np.floor(b); fa = a - ia; fb = b - ib
    fa = fa * fa * (3 - 2 * fa); fb = fb * fb * (3 - 2 * fb)
    v00 = hash2(ia, ib); v10 = hash2(ia + 1, ib); v01 = hash2(ia, ib + 1); v11 = hash2(ia + 1, ib + 1)
    return (v00 * (1 - fa) + v10 * fa) * (1 - fb) + (v01 * (1 - fa) + v11 * fa) * fb

# ---- lips (eye-relative). stomion ~ y -0.0688
ys = -0.0688
upper_h = 0.0082 - 0.0030 * np.exp(-(x / 0.0045) ** 2)  # cupid's bow dip
upper = ((x / 0.0250) ** 2 + ((y - ys) / upper_h) ** 2)
lower = ((x / 0.0235) ** 2 + ((y - ys) / 0.0105) ** 2)
lip_field = np.where(y >= ys, upper, lower)
lips = (1 - smooth(0.70, 1.05, lip_field)) * smooth(0.01, 0.03, z) * (~ear)

# ---- brows: arc above each eye
bx = ax_
arc_y = 0.0180 + 0.0060 * np.sin(np.clip((bx - 0.010) / 0.045, 0, 1) * np.pi * 0.85) - 0.0025 * smooth(0.035, 0.058, bx)
thick = 0.0058 - 0.0030 * smooth(0.012, 0.055, bx)
band = 1 - smooth(0.55, 1.0, np.abs(y - arc_y) / thick)
band *= smooth(0.006, 0.013, bx) * (1 - smooth(0.052, 0.062, bx)) * front
# strand direction: up near the nose, sweeping outward along the brow
ang = np.where(bx < 0.018, 1.25, 0.25 + 0.15 * (y > arc_y))
dirx = np.cos(ang) * np.sign(x + 1e-9); diry = np.sin(ang)
perp = -diry * x + dirx * y
along = dirx * x + diry * y
strand = 0.5 + 0.5 * np.sin(perp * 1500 + vnoise(along, perp, 250) * 5.0)
strand = smooth(0.2, 0.9, strand) * (0.7 + 0.3 * vnoise(x, y, 500))
brows = np.clip(band * (0.55 + 0.5 * strand), 0, 1) * (~ear)

# ---- beard zone (cheeks below cheekbone, jaw, chin, upper lip, under jaw)
theta = np.degrees(np.arctan2(x, z + 0.075))
cheek_top = -0.020 - 0.012 * smooth(0.02, 0.05, ax_)
beard = smooth(0.0, 0.012, cheek_top - y)  # below the cheek line
beard *= 1 - smooth(55, 68, np.abs(theta))  # stop at the sideburn, in front of the ear
beard *= 1 - lips * 1.2
beard *= smooth(-0.135, -0.105, y + 0.02 * smooth(0.0, 0.06, -z))  # stop under the jaw
beard = np.clip(beard * (~ear) * (~mouth_in), 0, 1)
mustache = (smooth(-0.066, -0.060, y) * (1 - smooth(-0.050, -0.045, y)) * (1 - smooth(0.020, 0.028, ax_)) * front)
mustache = np.clip(mustache * (1 - lips * 1.5), 0, 1)
goatee = (1 - smooth(-0.090, -0.080, y)) * smooth(-0.125, -0.110, y) * (1 - smooth(0.016, 0.026, ax_)) * front
goatee = np.clip(goatee * (1 - lips * 1.5), 0, 1)

# ---- redness / cavity / pores
red = 0.35 * lips + 0.25 * np.exp(-((ax_ - 0.035) ** 2 + (y + 0.032) ** 2) / 0.0006) * front \
      + 0.30 * np.exp(-(x ** 2 + (y + 0.040) ** 2 + (z - 0.045) ** 2) / 0.0003) + 0.35 * ear
red = np.clip(red, 0, 1)
cav = 0.6 * np.exp(-((ax_ - 0.032) ** 2) / 0.0002 - ((y - 0.002) ** 2) / 0.00025) * front * (1 - smooth(0.025, 0.045, z))
cav += 0.35 * mouth_in + 0.25 * ear * vnoise(x, y, 300)
cav = np.clip(cav, 0, 1)
pores = vnoise(x + z, y, 3000) * 0.6 + vnoise(x, y + z, 9000) * 0.4

# ---- hair fields
th = np.abs(theta)
hairline = np.interp(th, [0, 18, 30, 45, 55, 62, 75, 95, 120, 150, 180],
                     [0.083, 0.080, 0.072, 0.052, 0.025, -0.012, 0.005, -0.005, -0.050, -0.078, -0.088])
coverage = np.clip(0.5 + (y - hairline) / 0.03, 0, 1) * (~ear) * (~mouth_in)
crown = smooth(0.06, 0.12, y + 0.02 * (z < 0) ) * smooth(-0.06, 0.02, -z + 0.05)
fade = 1 - smooth(-0.04, 0.07, y)
phase = vnoise(x * 3 + z, y * 2, 700)

def to8(a):
    return (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
def save(chs, name):
    img = np.stack([to8(c * cov) for c in chs], -1)
    # dilate a few px so mip/bilinear sampling across island edges stays valid
    filled = cov.copy()
    for _ in range(48):
        sh = [np.roll(img, s, axis=a) for a in (0, 1) for s in (1, -1)]
        shc = [np.roll(filled, s, axis=a) for a in (0, 1) for s in (1, -1)]
        grow = filled.copy()
        for im2, c2 in zip(sh, shc):
            m = (~grow) & c2
            img[m] = im2[m]; grow |= m
        filled = grow
    Image.fromarray(img, 'RGBA').save(f'out/{name}.png')

# ---- jersey: everything below the collar line. V-neck button front.
thc = np.degrees(np.arctan2(x, z + 0.06))
collar_y = np.interp(np.abs(thc), [0, 20, 45, 90, 135, 180], [-0.170, -0.168, -0.160, -0.150, -0.135, -0.128])
vdepth = 0.045 * (1 - smooth(0.0, 0.030, ax_)) * smooth(-0.01, 0.03, z)   # V opening at the front
collar_line = collar_y - vdepth
jersey = 1 - smooth(-0.002, 0.004, y - collar_line)
trim = (1 - smooth(0.004, 0.009, collar_line - y)) * jersey          # contrast collar piping
placket = (1 - smooth(0.006, 0.009, ax_)) * smooth(0.0, 0.02, z) * (y < collar_line) * jersey
save([lips, brows, beard, mustache], 'masks')
save([jersey, trim, placket, np.clip(y + 0.5, 0, 1)], 'cloth')
save([red, cav, goatee, np.zeros_like(red)], 'detail')
save([coverage, crown, fade, np.zeros_like(fade)], 'hair')
print('baked', S, cov.mean())
