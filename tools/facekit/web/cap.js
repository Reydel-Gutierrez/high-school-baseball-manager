// Ball cap built on the morphed head: crown shell + curved brim.
// base: eye-centred base positions (Float32Array), p: morphed eye-centred positions.
export function buildCap(kit, baseRel, p, nrm) {
  const hatY = (th) => { const a = Math.abs(th); return a < 60 ? 0.058 - 0.012 * (a / 60) : a < 110 ? 0.046 - 0.026 * ((a - 60) / 50) : 0.020 - 0.022 * ((a - 110) / 70); };
  const theta = (x, z) => Math.atan2(x, z + 0.075) * 180 / Math.PI;
  const inCap = new Uint8Array(kit.nSub);
  for (let i = 0; i < kit.nSub; i++) {
    const x = baseRel[i*3], y = baseRel[i*3+1], z = baseRel[i*3+2];
    if (y > hatY(theta(x, z))) inCap[i] = 1;
  }
  // exclude ears (|x| big and y low)
  const tris = kit.surfaces.skin; const idx = []; const map = new Map(); const pos = []; const nor = []; const uvs = [];
  const add = (s) => {
    if (map.has(s)) return map.get(s);
    const k = pos.length / 3; map.set(s, k);
    const y = baseRel[s*3+1];
    const top = Math.min(1, Math.max(0, (y - 0.03) / 0.07));
    const off = 0.009 + 0.010 * top * top;
    pos.push(p[s*3] + nrm[s*3]*off, p[s*3+1] + nrm[s*3+1]*off + 0.004*top, p[s*3+2] + nrm[s*3+2]*off);
    nor.push(nrm[s*3], nrm[s*3+1], nrm[s*3+2]);
    uvs.push(theta(baseRel[s*3], baseRel[s*3+2]) / 180, y);
    return k;
  };
  for (let t = 0; t < tris.length; t += 3) {
    const a = kit.rSub[tris[t]], b = kit.rSub[tris[t+1]], c = kit.rSub[tris[t+2]];
    if (inCap[a] && inCap[b] && inCap[c]) idx.push(add(a), add(b), add(c));
  }
  // brim: find the front rim of the shell
  const crownStart = pos.length / 3;
  const rim = [];
  for (let th = -78; th <= 78.01; th += 6) {
    let best = -1, bd = 1e9;
    for (const [s, k] of map) {
      const x = baseRel[s*3], y = baseRel[s*3+1], z = baseRel[s*3+2];
      const dt = Math.abs(theta(x, z) - th), dy = Math.abs(y - hatY(th));
      const d = dt * 0.004 + dy;
      if (d < bd) { bd = d; best = k; }
    }
    rim.push([th, best]);
  }
  const brimStart = pos.length / 3;
  for (const [th, k] of rim) {
    const px = pos[k*3], py = pos[k*3+1] - 0.002, pz = pos[k*3+2];
    const r = Math.cos(th * Math.PI / 180);
    const len = 0.012 + 0.068 * Math.pow(Math.max(0, r), 1.6);
    let dx = px, dz = pz + 0.075; const l = Math.hypot(dx, dz); dx /= l; dz /= l;
    const curl = -0.012 * Math.pow(Math.abs(th) / 78, 2);
    pos.push(px, py, pz); nor.push(0, 1, 0); uvs.push(th / 180, 2.0);
    pos.push(px + dx * len, py - 0.016 * len / 0.08 + curl, pz + dz * len); nor.push(0, 1, 0); uvs.push(th / 180, 3.0);
  }
  for (let i = 0; i < rim.length - 1; i++) {
    const a = brimStart + i * 2, b = a + 1, c = a + 2, d = a + 3;
    idx.push(a, c, b, b, c, d);   // top
    idx.push(a, b, c, b, d, c);   // underside
  }
  return { pos: new Float32Array(pos), nor: new Float32Array(nor), uv: new Float32Array(uvs), idx: new Uint32Array(idx) };
}
