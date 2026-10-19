"""Shared UV rasterizer: per-texel eye-relative position, normal, coverage."""
import numpy as np


def raster(S):
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
    Pr = P[sub]; Nr = nrm[sub]
    X = np.zeros((S, S, 3), np.float32); NN = np.zeros((S, S, 3), np.float32)
    cov = np.zeros((S, S), bool); island = np.zeros((S, S), np.int8)
    for t in skin:
        tuv = uv[t] * [S, S]; tuv[:, 1] = S - tuv[:, 1]
        x0, y0 = np.floor(tuv.min(0)).astype(int); x1, y1 = np.ceil(tuv.max(0)).astype(int)
        x0 = max(x0, 0); y0 = max(y0, 0); x1 = min(x1, S - 1); y1 = min(y1, S - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = tuv
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        w0 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
        w1 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
        w2 = 1 - w0 - w1
        m = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not m.any():
            continue
        yy = ys[m].astype(int); xx = xs[m].astype(int)
        X[yy, xx] = w0[m, None] * Pr[t[0]] + w1[m, None] * Pr[t[1]] + w2[m, None] * Pr[t[2]]
        NN[yy, xx] = w0[m, None] * Nr[t[0]] + w1[m, None] * Nr[t[1]] + w2[m, None] * Nr[t[2]]
        cov[yy, xx] = True
        island[yy, xx] = 1 if uv[t[0], 0] < 0.2 else (2 if (uv[t[0], 1] < 0.17 and uv[t[0], 0] > 0.7) else 0)
    NN /= np.linalg.norm(NN, axis=2, keepdims=True) + 1e-9
    return X, NN, cov, island


def smooth(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def to8(a):
    return (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)


def save_rgba(chs, cov, path, signed=False):
    from PIL import Image
    if signed:
        chs = [c * 0.5 + 0.5 for c in chs]
    img = np.stack([to8(np.where(cov, c, 0.5 if signed else 0.0)) for c in chs], -1)
    filled = cov.copy()
    for _ in range(48):
        grow = filled.copy()
        for a in (0, 1):
            for s in (1, -1):
                im2 = np.roll(img, s, axis=a); c2 = np.roll(filled, s, axis=a)
                m = (~grow) & c2
                img[m] = im2[m]; grow |= m
        filled = grow
    Image.fromarray(img, 'RGBA').save(path)
