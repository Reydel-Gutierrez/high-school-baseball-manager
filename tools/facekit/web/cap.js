// Fitted ball cap, generated per head (mirrors Godot BallCap.cs).
// Crown: a parametric dome over the scalp. For each azimuth (theta, 0 =
// straight ahead) and polar angle (phi, 0 = top) we read the head's outer
// radius from a direction histogram of its vertices, then push it out by
// the cap's thickness. The rim follows a hat line (low at the back, above
// the ears, high on the forehead). Brim: pre-curved bill with thickness.
// UV: crown u = theta/180 (-1..1), v = s (0 top .. 1 rim). Brim v = 2..3.
const C0 = [0, 0.030, -0.060];
const NT = 72, NP = 40;

export function hatLine(thDeg) {
  const a = Math.abs(thDeg);
  const pts = [[0, 0.043], [30, 0.042], [60, 0.035], [90, 0.022], [120, 0.008], [150, -0.006], [180, -0.012]];
  for (let i = 0; i < pts.length - 1; i++) {
    if (a <= pts[i + 1][0]) { const t = (a - pts[i][0]) / (pts[i + 1][0] - pts[i][0]); return pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t; }
  }
  return pts[pts.length - 1][1];
}

function radialField(p, n) {
  const R = new Float32Array((NT + 1) * (NP + 1));
  for (let i = 0; i < n; i++) {
    const x = p[i*3] - C0[0], y = p[i*3+1] - C0[1], z = p[i*3+2] - C0[2];
    const r = Math.hypot(x, y, z); if (r < 1e-6) continue;
    const phi = Math.acos(Math.max(-1, Math.min(1, y / r)));
    if (phi > Math.PI * 0.75) continue;
    const th = Math.atan2(x, z);
    const ti = Math.round((th / Math.PI + 1) * NT / 2), pj = Math.round(phi / (Math.PI * 0.75) * NP);
    const k = ti * (NP + 1) + pj;
    if (r > R[k]) R[k] = r;
  }
  // fill holes + smooth (wrap in theta)
  for (let pass = 0; pass < 16; pass++) {
    const out = new Float32Array(R);
    for (let ti = 0; ti <= NT; ti++) for (let pj = 0; pj <= NP; pj++) {
      let s = 0, c = 0;
      for (let dt = -1; dt <= 1; dt++) for (let dp = -1; dp <= 1; dp++) {
        const t2 = (ti + dt + NT) % NT, p2 = Math.min(NP, Math.max(0, pj + dp));
        const v = R[t2 * (NP + 1) + p2]; if (v > 0) { s += v; c++; }
      }
      const k = ti * (NP + 1) + pj;
      out[k] = pass > 2 ? (c ? (R[k] > 0 ? R[k] * 0.3 + (s / c) * 0.7 : s / c) : R[k]) : (c ? Math.max(R[k], s / c) : R[k]);
    }
    R.set(out);
  }
  return (th, phi) => {
    const tf = (th / Math.PI + 1) * NT / 2, pf = Math.min(NP, Math.max(0, phi / (Math.PI * 0.75) * NP));
    const t0 = Math.floor(tf), p0 = Math.floor(pf), ft = tf - t0, fp = pf - p0;
    const g = (a, b) => R[(((a % NT) + NT) % NT) * (NP + 1) + Math.min(NP, b)];
    return (g(t0, p0) * (1 - ft) + g(t0 + 1, p0) * ft) * (1 - fp) + (g(t0, p0 + 1) * (1 - ft) + g(t0 + 1, p0 + 1) * ft) * fp;
  };
}

export function buildCap(kit, baseRel, p) {
  const radius = radialField(p, kit.nSub);
  const pos = [], nor = [], uv = [], idx = [];
  const point = (th, phi, off) => {
    const r = radius(th, phi) + off;
    return [C0[0] + r * Math.sin(phi) * Math.sin(th), C0[1] + r * Math.cos(phi), C0[2] + r * Math.sin(phi) * Math.cos(th)];
  };
  // rim polar angle per azimuth: where the dome meets the hat line
  const rimPhi = (th) => {
    const target = hatLine(th * 180 / Math.PI);
    let lo = 0.2, hi = Math.PI * 0.74;
    for (let i = 0; i < 30; i++) { const m = (lo + hi) / 2; const y = point(th, m, 0.008)[1]; if (y > target) lo = m; else hi = m; }
    return (lo + hi) / 2;
  };
  const SEG = 96, RING = 28;
  const crown = [];
  for (let i = 0; i <= SEG; i++) {
    const th = -Math.PI + 2 * Math.PI * i / SEG;
    const pr = rimPhi(th);
    for (let j = 0; j <= RING; j++) {
      const s = j / RING;
      const phi = pr * s;
      // thickness: snug at the band, puffed at the crown (structured front)
      const front = Math.max(0, Math.cos(th));
      const off = 0.0060 + 0.0070 * Math.sin(Math.PI * Math.min(1, s * 1.1)) * (0.5 + 0.5 * front) + 0.0025 * (1 - s);
      const q = point(th, phi, off);
      pos.push(...q); uv.push(th / Math.PI, s); nor.push(0, 0, 0);
      crown.push(q);
    }
  }
  const at = (i, j) => i * (RING + 1) + j;
  for (let i = 0; i < SEG; i++) for (let j = 0; j < RING; j++) {
    idx.push(at(i, j), at(i + 1, j), at(i, j + 1), at(i + 1, j), at(i + 1, j + 1), at(i, j + 1));
  }
  smoothNormals(pos, idx, nor);
  // top button
  const top = point(0, 0, 0.0085);
  const bStart = pos.length / 3;
  for (let j = 0; j <= 6; j++) for (let i = 0; i <= 16; i++) {
    const a = i / 16 * 2 * Math.PI, e = j / 6 * Math.PI / 2;
    const rr = 0.0042 * Math.cos(e);
    pos.push(top[0] + rr * Math.sin(a), top[1] + 0.0028 * Math.sin(e), top[2] + rr * Math.cos(a));
    nor.push(Math.cos(e) * Math.sin(a), Math.sin(e), Math.cos(e) * Math.cos(a)); uv.push(0, -1);
  }
  for (let j = 0; j < 6; j++) for (let i = 0; i < 16; i++) { const a = bStart + j * 17 + i; idx.push(a, a + 1, a + 17, a + 1, a + 18, a + 17); }
  // brim: from the front rim, pre-curved, 3 mm thick
  const brimStart = pos.length / 3;
  const B = 40, D = 10, TH = 0.003;
  const span = 64 * Math.PI / 180;
  const grid = [];
  for (let i = 0; i <= B; i++) {
    const th = -span + 2 * span * i / B;
    const pr = rimPhi(th);
    const base = point(th, pr, 0.0075);
    // bills point forward (D-shaped outline), not radially out to the sides
    const rad = [Math.sin(th), 0, Math.cos(th)];
    const fwd = norm([rad[0] * 0.25, 0, rad[2] * 0.25 + 0.75]);
    const len = 0.074 * Math.sqrt(Math.max(0, 1 - (th / span) ** 2)) + 0.002;
    const row = [];
    for (let k = 0; k <= D; k++) {
      const t = k / D;
      const out = len * t;
      const curl = -0.032 * Math.pow(Math.abs(th) / span, 1.5) * Math.pow(t, 0.8);    // sides bend down
      const droop = -Math.tan(9 * Math.PI / 180) * out;               // bill tilts down
      row.push([base[0] + fwd[0] * out, base[1] + droop + curl, base[2] + fwd[2] * out]);
    }
    grid.push(row);
  }
  for (const side of [0, 1]) {
    const start = pos.length / 3;
    for (let i = 0; i <= B; i++) for (let k = 0; k <= D; k++) {
      const q = grid[i][k];
      pos.push(q[0], q[1] - side * TH, q[2]); nor.push(0, side ? -1 : 1, 0); uv.push((i / B) * 2 - 1, side ? 3 : 2);
    }
    for (let i = 0; i < B; i++) for (let k = 0; k < D; k++) {
      const a = start + i * (D + 1) + k, b = a + D + 1;
      if (side) idx.push(a, a + 1, b, a + 1, b + 1, b); else idx.push(a, b, a + 1, a + 1, b, b + 1);
    }
  }
  // brim edge strip
  const eStart = pos.length / 3;
  for (let i = 0; i <= B; i++) { const q = grid[i][D]; pos.push(q[0], q[1], q[2], q[0], q[1] - TH, q[2]); const f = norm([q[0], 0, q[2] + 0.06]); nor.push(...f, ...f); uv.push(0, 2.5, 0, 2.5); }
  for (let i = 0; i < B; i++) { const a = eStart + i * 2; idx.push(a, a + 1, a + 2, a + 1, a + 3, a + 2); }
  // brim normals from geometry (top/bottom surfaces)
  smoothNormals(pos, idx, nor, brimStart, eStart);
  return { pos: new Float32Array(pos), nor: new Float32Array(nor), uv: new Float32Array(uv), idx: new Uint32Array(idx) };
}

function norm(a) { const l = Math.hypot(...a) || 1; return a.map(v => v / l); }

function smoothNormals(pos, idx, nor, from = 0, to = Infinity) {
  const acc = new Float32Array(pos.length);
  for (let t = 0; t < idx.length; t += 3) {
    const a = idx[t], b = idx[t + 1], c = idx[t + 2];
    if (a < from || a >= to) continue;
    const e1 = [pos[b*3]-pos[a*3], pos[b*3+1]-pos[a*3+1], pos[b*3+2]-pos[a*3+2]];
    const e2 = [pos[c*3]-pos[a*3], pos[c*3+1]-pos[a*3+1], pos[c*3+2]-pos[a*3+2]];
    const n = [e1[1]*e2[2]-e1[2]*e2[1], e1[2]*e2[0]-e1[0]*e2[2], e1[0]*e2[1]-e1[1]*e2[0]];
    for (const i of [a, b, c]) { acc[i*3] += n[0]; acc[i*3+1] += n[1]; acc[i*3+2] += n[2]; }
  }
  for (let i = from; i < Math.min(to, pos.length / 3); i++) {
    const l = Math.hypot(acc[i*3], acc[i*3+1], acc[i*3+2]);
    if (l > 0) { nor[i*3] = acc[i*3] / l; nor[i*3+1] = acc[i*3+1] / l; nor[i*3+2] = acc[i*3+2] / l; }
  }
}
