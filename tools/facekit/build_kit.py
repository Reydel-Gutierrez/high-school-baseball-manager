"""Build the HSBM face kit from MakeHuman CC0 data.

Output facekit.bin (little-endian):
  'HSBMFK02'
  i32 nSub   original (welded) vertices in the bust subset
  f32[nSub*3] base positions (meters, y up, face looks +z)
  i32 nRender  render vertices (unwelded on UV seams)
  per render vertex: i32 sub, f32 u, f32 v
  i32 nSurfaces; per surface: str name, i32 nTris, i32[nTris*3]
  i32 nGroups; per group: str name, i32 n, i32[n] sub indices   (helper eyes etc.)
  i32 nTargets; per target: str name, i32 n, then n*(i32 sub, f32 dx,dy,dz)
str = i32 byteLen + utf8
"""
import json, os, re, struct, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from objload import load_obj, load_target

MH = '/home/claude/mh/makehuman-master/makehuman/data'
SCALE = 0.1  # MakeHuman units are decimeters

V, VT, F, FT, G = load_obj(f'{MH}/3dobjs/base.obj')
F = np.array(F); FT = np.array(FT); G = np.array(G)

def in_bust(p):
    return (p[:, 1] > 3.7) & (np.abs(p[:, 0]) < 2.35)

body = np.where(G == 'body')[0]
keep = [f for f in body if in_bust(V[F[f]]).all()]
surfaces = {'skin': keep}
for side in ('l', 'r'):
    for k in (1, 2):
        surfaces.setdefault('lashes', []).extend(np.where(G == f'helper-{side}-eyelashes-{k}')[0].tolist())
helper_groups = {
    'eye_l': np.unique(F[G == 'helper-l-eye']),
    'eye_r': np.unique(F[G == 'helper-r-eye']),
    'hair': np.unique(F[G == 'helper-hair']),
}

used = set()
for faces in surfaces.values():
    for f in faces:
        used.update(F[f].tolist())
for idx in helper_groups.values():
    used.update(idx.tolist())
sub_of = {}
sub_list = sorted(used)
for i, v in enumerate(sub_list):
    sub_of[v] = i
sub_list = np.array(sub_list)

render = {}
render_list = []
surf_tris = {}
for name, faces in surfaces.items():
    tris = []
    for f in faces:
        quad = []
        for v, t in zip(F[f], FT[f]):
            key = (int(v), int(t))
            if key not in render:
                render[key] = len(render_list)
                uv = VT[t] if t >= 0 else (0.0, 0.0)
                render_list.append((sub_of[int(v)], float(uv[0]), float(uv[1])))
            quad.append(render[key])
        # MakeHuman quads are CCW seen from outside; Godot wants clockwise front faces.
        tris.append((quad[0], quad[2], quad[1]))
        tris.append((quad[0], quad[3], quad[2]))
    surf_tris[name] = tris

mods = json.load(open(f'{MH}/modifiers/modeling_modifiers.json'))
wanted_groups = {'head', 'forehead', 'eyebrows', 'neck', 'eyes', 'nose', 'mouth', 'ears', 'chin', 'cheek'}
targets = {}

def add_target(name, path):
    idx, d = load_target(path)
    m = np.array([i in sub_of for i in idx])
    if not m.any():
        return
    s = np.array([sub_of[i] for i in idx[m]], dtype=np.int32)
    dd = d[m] * SCALE
    if name in targets:
        s0, d0 = targets[name]
        targets[name] = (np.concatenate([s0, s]), np.concatenate([d0, dd]))
    else:
        targets[name] = (s, dd)

for g in mods:
    if g['group'] not in wanted_groups:
        continue
    for m in g['modifiers']:
        t = m['target']
        folder = g['group']
        ends = [m['min'], m['max']] if 'min' in m else [None]
        for e in ends:
            fname = f'{t}-{e}.target' if e else f'{t}.target'
            path = f'{MH}/targets/{folder}/{fname}'
            if not os.path.exists(path):
                print('missing', path); continue
            # merge left/right into one symmetric control
            name = re.sub(r'^(l|r)-', '', fname[:-7])
            add_target(name, path)

for eth in ('african', 'asian', 'caucasian'):
    for age in ('child', 'young', 'old'):
        add_target(f'macro-{eth}-male-{age}', f'{MH}/targets/macrodetails/{eth}-male-{age}.target')
for age in ('child', 'young', 'old'):
    for mus in ('minmuscle', 'averagemuscle', 'maxmuscle'):
        for wt in ('minweight', 'averageweight', 'maxweight'):
            add_target(f'universal-male-{age}-{mus}-{wt}',
                       f'{MH}/targets/macrodetails/universal-male-{age}-{mus}-{wt}.target')

def wstr(f, s):
    b = s.encode('utf-8'); f.write(struct.pack('<i', len(b))); f.write(b)

out = os.path.join(os.path.dirname(__file__), 'out', 'facekit.bin')
with open(out, 'wb') as f:
    f.write(b'HSBMFK02')
    pos = V[sub_list] * SCALE
    f.write(struct.pack('<i', len(sub_list))); f.write(pos.astype('<f4').tobytes())
    f.write(struct.pack('<i', len(render_list)))
    f.write(np.array([(s,) for s, _, _ in render_list], dtype='<i4').tobytes() if False else b'')
    for s, u, v in render_list:
        f.write(struct.pack('<iff', s, u, v))
    f.write(struct.pack('<i', len(surf_tris)))
    for name, tris in surf_tris.items():
        wstr(f, name); f.write(struct.pack('<i', len(tris))); f.write(np.array(tris, dtype='<i4').tobytes())
    f.write(struct.pack('<i', len(helper_groups)))
    for name, idx in helper_groups.items():
        wstr(f, name); arr = np.array([sub_of[int(i)] for i in idx], dtype='<i4')
        f.write(struct.pack('<i', len(arr))); f.write(arr.tobytes())
    f.write(struct.pack('<i', len(targets)))
    for name, (s, d) in sorted(targets.items()):
        wstr(f, name); f.write(struct.pack('<i', len(s)))
        rec = np.zeros(len(s), dtype=[('i', '<i4'), ('d', '<f4', 3)]); rec['i'] = s; rec['d'] = d
        f.write(rec.tobytes())
print('sub', len(sub_list), 'render', len(render_list), {k: len(v) for k, v in surf_tris.items()}, 'targets', len(targets), 'bytes', os.path.getsize(out))
# save for other tools
np.savez(os.path.join(os.path.dirname(__file__), 'out', 'kit.npz'), pos=V[sub_list]*SCALE,
         render=np.array(render_list), skin=np.array(surf_tris['skin']), lashes=np.array(surf_tris['lashes']),
         eye_l=np.array([sub_of[int(i)] for i in helper_groups['eye_l']]),
         eye_r=np.array([sub_of[int(i)] for i in helper_groups['eye_r']]),
         hair=np.array([sub_of[int(i)] for i in helper_groups['hair']]))
import pickle
pickle.dump({k: (v[0], v[1]) for k, v in targets.items()}, open(os.path.join(os.path.dirname(__file__), 'out', 'targets.pkl'), 'wb'))
