#version 300 es
precision highp float;
// Structured wool/poly ball cap: six panels with double-stitched seams,
// eyelet rows, front monogram, stitched brim with gray underbrim.
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uEye; uniform vec3 uCap; uniform vec3 uLogo; uniform sampler2D ulogo; uniform float uHasLogo;
out vec4 frag;
float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float vnoise(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x), f.y); }
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
void main(){
  vec3 n = normalize(vNor);
  vec3 C0 = vec3(0.0, 0.030, -0.060);
  float part = vUv.y;                 // 0..1 crown, 2 brim top, 2.5 edge, 3 brim bottom, -1 button
  if (part < 1.5 && part >= 0.0) { if (dot(n, vPos - C0) < 0.0) n = -n; }
  else if (part > 1.9 && part < 2.2) { if (n.y < 0.0) n = -n; }
  else if (part > 2.8) { if (n.y > 0.0) n = -n; }
  vec3 v = normalize(uEye - vPos);
  vec3 alb = toLin(uCap);
  float weave = 0.5 + 0.5 * sin(vPos.x * 4200.0 + vPos.y * 3100.0) * sin(vPos.z * 4200.0 - vPos.y * 2900.0);
  alb *= 0.92 + 0.08 * weave + 0.05 * (vnoise(vPos.xz * 900.0) - 0.5);
  float ao = 1.0;
  if (part >= 0.0 && part < 1.5) {
    float th = vUv.x * 180.0;                          // degrees, 0 = front
    float s = vUv.y;
    // panel seams at +-30, +-90, +-150 (front panels meet over the logo)
    float seamDeg = abs(fract((th + 30.0) / 60.0) - 0.5) * 60.0;   // distance to nearest seam
    float seam = 1.0 - smoothstep(0.25, 1.4, seamDeg * (0.4 + s));
    float stitch = (1.0 - smoothstep(0.2, 0.5, abs(seamDeg * (0.4 + s) - 1.3))) * step(0.5, fract(s * 90.0));
    alb *= 1.0 - seam * 0.45;
    alb = mix(alb, alb * 1.25, stitch * 0.5 * smoothstep(0.08, 0.2, s));
    ao *= 1.0 - seam * 0.25;
    // eyelets: one per side panel near the top
    float eyelet = 0.0;
    for (int k = -2; k <= 3; k++) { float c = float(k) * 60.0; if (abs(abs(c) - 0.0) < 1.0) continue;
      float dd = length(vec2((th - c) * 0.012 * s * 6.0, (s - 0.32) * 0.9)); eyelet = max(eyelet, 1.0 - smoothstep(0.012, 0.018, dd)); }
    alb = mix(alb, alb * 0.35, eyelet);
    // band at the rim is slightly pinched
    ao *= mix(1.0, 0.82, smoothstep(0.92, 1.0, s));
    // front logo
    vec2 luv = vec2(th / 46.0 + 0.5, (s - 0.36) / 0.40);
    if (uHasLogo > 0.5 && luv.x > 0.0 && luv.x < 1.0 && luv.y > 0.0 && luv.y < 1.0) {
      vec4 lg = texture(ulogo, vec2(luv.x, luv.y));
      alb = mix(alb, toLin(uLogo), lg.a);
      ao *= 1.0 - (1.0 - lg.a) * smoothstep(0.0, 0.5, lg.a) * 0.0;
    }
  } else if (part > 1.9 && part < 2.2) {
    // brim top: concentric stitch rows near the edge
    float rows = 0.0;
    alb *= 0.96 + 0.04 * weave;
  } else if (part > 2.2 && part < 2.8) {
    alb *= 0.85;
  } else if (part > 2.8) {
    alb = toLin(vec3(0.20, 0.21, 0.21)) * (0.9 + 0.1 * weave);   // gray underbrim
  }
  vec3 keyDir = normalize(vec3(0.55, 0.45, 0.75)), fillDir = normalize(vec3(-0.8, 0.05, 0.55));
  vec3 rimL = normalize(vec3(-0.75, 0.35, -0.6)), rimR = normalize(vec3(0.8, 0.3, -0.55));
  float dk = clamp((dot(n, keyDir) + 0.15) / 1.15, 0.0, 1.0), df = clamp((dot(n, fillDir) + 0.15) / 1.15, 0.0, 1.0);
  float up = n.y * 0.5 + 0.5;
  vec3 col = alb * (dk * vec3(1.0, 0.95, 0.88) * 3.1 + df * vec3(0.8, 0.88, 1.0) * 0.75 + mix(vec3(0.14,0.12,0.11), vec3(0.30, 0.33, 0.38), up)) * ao;
  float edge = pow(1.0 - max(dot(n, v), 0.0), 2.5);
  col += alb * vec3(0.95, 0.97, 1.0) * 1.6 * edge * (max(dot(n, rimL), 0.0) + max(dot(n, rimR), 0.0)) * 0.6;
  vec3 h = normalize(keyDir + v);
  col += vec3(pow(max(dot(n, h), 0.0), 24.0)) * 0.06 * dk;
  col = aces(col * 0.95);
  frag = vec4(pow(col, vec3(1.0/2.2)), 1.0);
}
