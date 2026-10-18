// Face kit loader + morph evaluator (mirrors the C# FaceKit).
export async function loadKit(url) {
  const buf = await (await fetch(url)).arrayBuffer();
  const dv = new DataView(buf); let o = 8;
  const i32 = () => { const v = dv.getInt32(o, true); o += 4; return v; };
  const f32 = () => { const v = dv.getFloat32(o, true); o += 4; return v; };
  const str = () => { const n = i32(); const s = new TextDecoder().decode(new Uint8Array(buf, o, n)); o += n; return s; };
  const nSub = i32(); const base = new Float32Array(buf.slice(o, o + nSub * 12)); o += nSub * 12;
  const nR = i32(); const rSub = new Int32Array(nR); const uv = new Float32Array(nR * 2);
  for (let i = 0; i < nR; i++) { rSub[i] = i32(); uv[i*2] = f32(); uv[i*2+1] = f32(); }
  const surfaces = {}; const ns = i32();
  for (let s = 0; s < ns; s++) { const name = str(); const n = i32(); surfaces[name] = new Int32Array(buf.slice(o, o + n * 12)); o += n * 12; }
  const groups = {}; const ng = i32();
  for (let g = 0; g < ng; g++) { const name = str(); const n = i32(); groups[name] = new Int32Array(buf.slice(o, o + n * 4)); o += n * 4; }
  const targets = {}; const nt = i32();
  for (let t = 0; t < nt; t++) {
    const name = str(); const n = i32(); const idx = new Int32Array(n); const d = new Float32Array(n * 3);
    for (let k = 0; k < n; k++) { idx[k] = i32(); d[k*3] = f32(); d[k*3+1] = f32(); d[k*3+2] = f32(); }
    targets[name] = { idx, d };
  }
  return { nSub, base, nR, rSub, uv, surfaces, groups, targets };
}

export function ageWeights(age) {
  if (age <= 11) return { child: 1, young: 0, old: 0 };
  if (age <= 25) { const t = (age - 11) / 14; return { child: 1 - t, young: t, old: 0 }; }
  const t = Math.min(1, (age - 25) / 65); return { child: 0, young: 1 - t, old: t };
}
function tri(v) { // 0..1 slider -> min/average/max weights
  if (v < 0.5) { const m = (0.5 - v) * 2; return { min: m, average: 1 - m, max: 0 }; }
  const m = (v - 0.5) * 2; return { min: 0, average: 1 - m, max: m };
}

// spec: {age, african, asian, caucasian, muscle, weight, details:{name:weight}}
export function weightsFor(spec) {
  const w = {}; const a = ageWeights(spec.age);
  const races = { african: spec.african, asian: spec.asian, caucasian: spec.caucasian };
  const tot = races.african + races.asian + races.caucasian || 1;
  for (const r in races) for (const ag in a) { const v = races[r] / tot * a[ag]; if (v) w[`macro-${r}-male-${ag}`] = v; }
  const mu = tri(spec.muscle), we = tri(spec.weight);
  for (const ag in a) for (const m in mu) for (const x in we) {
    const v = a[ag] * mu[m] * we[x]; if (v) w[`universal-male-${ag}-${m}muscle-${x}weight`] = v;
  }
  for (const k in (spec.details || {})) {
    const v = spec.details[k]; if (!v) continue;
    // detail sliders are signed: -1..1 -> "decr"/"incr" style pair named in the spec
    w[k] = (w[k] || 0) + v;
  }
  return w;
}

export function morph(kit, weights) {
  const p = new Float32Array(kit.base);
  for (const name in weights) {
    const t = kit.targets[name]; if (!t) { console.warn('no target', name); continue; }
    const wv = weights[name];
    for (let k = 0; k < t.idx.length; k++) { const i = t.idx[k] * 3; p[i] += t.d[k*3] * wv; p[i+1] += t.d[k*3+1] * wv; p[i+2] += t.d[k*3+2] * wv; }
  }
  return p;
}
