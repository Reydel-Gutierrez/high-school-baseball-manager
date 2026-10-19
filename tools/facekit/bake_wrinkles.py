"""Bake age-driven creases and skin-character fields into UV space.

wrinkle.png (signed heights, 0.5 = flat; grooves < 0.5, ridges > 0.5)
  R forehead lines + glabella (frown) lines
  G crow's feet + under-eye creases / bags
  B nasolabial folds + marionette lines + neck rings
  A oiliness (T-zone sheen: forehead, nose, chin), unsigned

Coordinates are eye-relative meters on the base head: eyes at x = +-0.031,
nose tip y = -0.0375, stomion y = -0.069, chin ~ y = -0.115.
"""
import sys
import numpy as np
from raster import raster, smooth, save_rgba

S = int(sys.argv[1]) if len(sys.argv) > 1 else 1024
X, NN, cov, island = raster(S)
x, y, z = X[..., 0], X[..., 1], X[..., 2]
ax = np.abs(x)
ear = island == 1; mouth_in = island == 2
front = smooth(-0.015, 0.02, z)
skin = cov & ~ear & ~mouth_in

def groove(d, w):
    """Narrow V crease with a soft shoulder either side (signed)."""
    return -np.exp(-(d / w) ** 2) + 0.35 * np.exp(-(d / (w * 2.6)) ** 2)

def hashn(a, b):
    return np.modf(np.sin(a * 127.1 + b * 311.7) * 43758.5453)[0] % 1.0

# ---- forehead: 3-4 horizontal lines that bow up toward the temples
fy = y - 0.0006 * 0 - 9.0 * x ** 2
period = 0.0105
phase = (fy - 0.036) / period
k = np.round(phase)
d = (phase - k) * period
lineset = (k >= 0) & (k <= 3)
wob = 0.0007 * np.sin(x * 160 + k * 1.7)
fore = groove(d + wob, 0.0011) * lineset
fore *= smooth(0.030, 0.038, y) * (1 - smooth(0.072, 0.080, y))
fore *= 1 - smooth(0.040, 0.058, ax)
fore *= (0.7 + 0.3 * np.cos(x * 90 + k)) * front
# glabella: two short vertical lines between the brows
gl = groove(ax - 0.0055, 0.0009) * smooth(0.008, 0.014, y) * (1 - smooth(0.026, 0.034, y)) * front
R = fore + gl * 0.9

# ---- crow's feet: fan of lines from the outer canthus
cx, cy = 0.047, -0.002
dxc = ax - cx; dyc = y - cy
rad = np.hypot(dxc, dyc)
ang = np.arctan2(dyc, dxc)
fan = (np.abs(ang) < 1.1) & (dxc > -0.002)
spokes = np.cos(ang * 7.5) ** 24
crow = -spokes * smooth(0.004, 0.008, rad) * (1 - smooth(0.016, 0.024, rad)) * fan
# lower-lid crease and a soft under-eye bag
lid = groove(np.hypot((ax - 0.031) / 1.6, 0) * 0 + (y + 0.0125 + 30 * (ax - 0.031) ** 2), 0.0010)
lid *= (1 - smooth(0.012, 0.022, np.abs(ax - 0.031))) * front
bag = 0.6 * np.exp(-(((ax - 0.031) / 0.012) ** 2 + ((y + 0.0085) / 0.0045) ** 2)) * front
tear = groove(y + 0.010 + 0.55 * (ax - 0.020), 0.0012) * (1 - smooth(0.014, 0.026, ax)) * smooth(0.010, 0.014, ax) * front
G = crow + lid * 0.9 + bag * 0.5 + tear * 0.8

# ---- nasolabial fold: nose wing to beyond the mouth corner
t = np.clip((-0.040 - y) / 0.040, 0, 1)
curve_x = 0.019 + 0.017 * t + 0.004 * t * t
d = ax - curve_x
nlf = (-np.exp(-(d / 0.0016) ** 2) + 0.9 * np.exp(-((d - 0.0035) / 0.0032) ** 2)) \
    * smooth(-0.090, -0.078, y) * (1 - smooth(-0.044, -0.038, y)) * front
# marionette lines from the mouth corners toward the jaw
t2 = np.clip((-0.074 - y) / 0.030, 0, 1)
mar = groove(ax - (0.030 - 0.003 * t2), 0.0013) * smooth(-0.106, -0.096, y) * (1 - smooth(-0.078, -0.072, y)) * front
# neck rings
neck = groove(np.modf((y + 0.150) / 0.016)[0] * 0.016 - 0.008, 0.0012) * smooth(-0.175, -0.165, y) * (1 - smooth(-0.135, -0.125, y))
neck *= smooth(0.0, 0.03, z + 0.03) * (1 - smooth(0.04, 0.06, ax))
# vertical lip lines (smoker's lines, faint)
lipl = -np.cos((x / 0.0024) * np.pi) ** 30 * (1 - smooth(0.006, 0.012, np.abs(y + 0.0605))) * (1 - smooth(0.012, 0.018, ax)) * front
B = nlf + mar * 0.8 + neck * 0.6 + lipl * 0.4

# ---- oiliness: T-zone
tz = np.exp(-((x / 0.022) ** 2 + ((y - 0.050) / 0.030) ** 2)) \
   + np.exp(-((x / 0.010) ** 2 + ((y + 0.020) / 0.026) ** 2)) \
   + 0.6 * np.exp(-((x / 0.016) ** 2 + ((y + 0.110) / 0.012) ** 2))
A = np.clip(tz, 0, 1) * front

R *= skin; G *= skin; B *= skin
save_rgba([np.clip(R, -1, 1), np.clip(G, -1, 1), np.clip(B, -1, 1), A * 2 - 1], cov, 'out/wrinkle.png', signed=True)
print('baked wrinkles', S)

# ---- tone.png: albedo variation fields (unsigned)
#   R under-eye darkness (periorbital)   G forehead/temple warmth (yellow)
#   B reserved   A translucency (ears, nostril wings)
under = np.exp(-(((ax - 0.030) / 0.016) ** 2 + ((y + 0.011) / 0.0065) ** 2)) * front
inner = np.exp(-(((ax - 0.016) / 0.006) ** 2 + ((y + 0.004) / 0.008) ** 2)) * front
Rt = np.clip(under + 0.6 * inner, 0, 1)
Gt = smooth(0.025, 0.065, y) * (1 - smooth(0.095, 0.115, y)) * front
Bt = np.zeros_like(Rt)  # reserved; the beard mask (masks.B) drives jaw shadow
wing = np.exp(-(((ax - 0.015) / 0.005) ** 2 + ((y + 0.036) / 0.006) ** 2)) * front
At = np.clip(ear.astype(float) * 0.9 + wing * 0.7, 0, 1)
for ch in (Rt, Gt, Bt):
    ch *= (~ear) * (~mouth_in)
save_rgba([Rt, Gt, Bt, At], cov, 'out/tone.png')
print('baked tone', S)
