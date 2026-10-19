#version 300 es
precision highp float;
// Studio headshot skin. Linear lighting, baked light transport, per-channel
// wrapped diffuse (cheap subsurface), age creases as derivative bump, dual-lobe
// GGX with T-zone oiliness, ACES tonemap. Mirrors assets/facekit/skin.gdshader.
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uEye; uniform vec3 uSkin; uniform float uDebug;
uniform sampler2D umasks, udetail, uhair, ucloth, ulight, uwrinkle, utone;
uniform vec3 uJersey, uTrim;
uniform vec3 uHairColor, uBrowColor; uniform vec4 uHairStyle, uBeard;
uniform float uAge; uniform float uCap;
out vec4 frag;

float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float vnoise(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x), f.y); }
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
float ggx(vec3 n, vec3 h, float rough){ float a = rough*rough; float a2 = a*a; float nh = max(dot(n,h),0.0);
  float d = nh*nh*(a2-1.0)+1.0; return a2/(3.14159*d*d); }
vec3 wrapDiffuse(float ndl, vec3 w){ return clamp((vec3(ndl)+w)/(1.0+w), 0.0, 1.0) / (1.0 + w); }
float ramp(float a, float b, float v){ return clamp((v-a)/(b-a), 0.0, 1.0); }

void main(){
  vec2 uv = vec2(vUv.x, 1.0 - vUv.y);
  vec4 M = texture(umasks, uv); vec4 D = texture(udetail, uv); vec4 H = texture(uhair, uv);
  vec4 L = texture(ulight, uv); vec4 W = texture(uwrinkle, uv) * 2.0 - 1.0; vec4 T = texture(utone, uv);
  vec4 C = texture(ucloth, uv);
  float isCloth = step(0.5, C.r);
  if (uDebug > 0.5 && uDebug < 1.5) { frag = vec4(L.rgb, 1.0); return; }
  if (uDebug > 1.5 && uDebug < 2.5) { frag = vec4(W.rgb*0.5+0.5, 1.0); return; }

  // ---- age-weighted crease height (meters) -> bump the normal
  float a = uAge;
  float wFore = ramp(24.0, 50.0, a), wEye = ramp(28.0, 55.0, a), wFold = 0.10 + 0.90 * ramp(26.0, 55.0, a);
  float wNeck = ramp(40.0, 65.0, a);
  float hgt = (W.r * wFore + W.g * wEye + W.b * wFold) * 0.0009;
  hgt += (vnoise(vUv * 2600.0) - 0.5) * 0.00005;               // fine skin texture
  vec3 n = normalize(vNor);
  vec3 dpdx = dFdx(vPos), dpdy = dFdy(vPos);
  float dhx = dFdx(hgt), dhy = dFdy(hgt);
  vec3 r1 = cross(dpdy, n), r2 = cross(n, dpdx);
  float det = dot(dpdx, r1);
  vec3 grad = sign(det) * (dhx * r1 + dhy * r2);
  n = normalize(abs(det) * n - grad);
  vec3 v = normalize(uEye - vPos);

  // ---- albedo
  vec3 base = toLin(uSkin);
  float lum = dot(base, vec3(0.3,0.59,0.11));
  vec3 skin = base;
  skin = max(mix(vec3(lum), skin, 1.08), 0.0);                                  // keep skin saturated through the tonemap
  skin = mix(skin, skin * vec3(1.03, 1.0, 0.90), T.g * 0.5);                 // warmer forehead
  skin = mix(skin, skin * vec3(1.05, 0.70, 0.68), clamp(D.r * 1.1, 0.0, 1.0)); // cheeks, nose, ears flush
  skin = mix(skin, skin * vec3(0.78, 0.70, 0.78), T.r * 0.55);                // periorbital darkening
  float blotch = vnoise(vUv * 90.0) * 0.6 + vnoise(vUv * 260.0) * 0.4;
  skin *= 0.93 + 0.12 * blotch;                                               // uneven tone
  float freckle = step(0.80, vnoise(vUv * 700.0)) * (1.0 - smoothstep(0.15, 0.35, lum)) * 0.25 * D.r;
  skin *= 1.0 - freckle;
  // shaved beard shadow (grows with age/maturity)
  float shadowAmt = M.b * mix(0.0, 0.55, ramp(15.0, 22.0, a)) * (0.5 + uBeard.x);
  skin = mix(skin, skin * vec3(0.80, 0.82, 0.86), shadowAmt * 0.6);
  // lips: deeper, redder, with vertical texture
  float lipLines = 0.85 + 0.15 * vnoise(vec2(vUv.x * 1800.0, vUv.y * 120.0));
  vec3 lip = base * mix(vec3(0.80, 0.46, 0.47), vec3(0.72, 0.52, 0.56), 1.0 - smoothstep(0.08, 0.3, lum)) * lipLines;
  vec3 alb = mix(skin, lip, smoothstep(0.15, 0.85, M.r));
  // beard and stubble (painted under the shells)
  vec3 hairLin = toLin(uHairColor);
  float hairNoise = vnoise(vUv * 1400.0) * 0.6 + vnoise(vUv * 3000.0) * 0.4;
  float beardAmt = M.b * max(uBeard.x * 0.55, uBeard.y) + M.a * uBeard.z + D.b * uBeard.w;
  float beardCov = clamp(beardAmt * (0.45 + 0.9 * hairNoise), 0.0, 1.0);
  alb = mix(alb, hairLin, beardCov * mix(0.45, 0.92, uBeard.y));
  // brows
  float brow = clamp(M.g * 1.2, 0.0, 1.0);
  alb = mix(alb, toLin(uBrowColor), brow * 0.62);
  // scalp hair base (shells add volume on top)
  float line = 0.5 - uHairStyle.x;
  float cover = smoothstep(line - 0.04, line + 0.10, H.r);
  cover *= 1.0 - H.g * uHairStyle.w;
  float fadeDensity = mix(1.0, 0.30, H.b * uHairStyle.z);
  float strands = 0.6 * vnoise(vec2(vUv.x * 900.0, vUv.y * 260.0) + vnoise(vUv * 300.0) * 8.0) + 0.4 * vnoise(vec2(vUv.x * 2200.0, vUv.y * 500.0));
  float hairA = clamp(cover * uHairStyle.y * fadeDensity * (0.80 + 0.30 * strands), 0.0, 1.0);
  alb = mix(alb, hairLin * (0.55 + 0.45 * strands), hairA);

  // ---- jersey
  float weave = 0.5 + 0.5 * sin(vPos.x * 5200.0) * sin(vPos.y * 5200.0);
  vec3 cloth = toLin(uJersey) * (0.90 + 0.10 * weave);
  cloth = mix(cloth, toLin(uTrim), C.g);
  float buttons = C.b * step(0.75, fract((vPos.y + 0.2) * 22.0)) * step(abs(vPos.x), 0.004);
  cloth = mix(cloth, toLin(uTrim) * 0.9, C.b * 0.25);
  cloth = mix(cloth, vec3(0.85), buttons);
  alb = mix(alb, cloth, isCloth);
  // contact shadow where the collar sits on the neck
  float collarAO = 1.0 - (1.0 - isCloth) * smoothstep(0.30, 0.5, C.r) * 0.55;

  // ---- light transport
  float ao = mix(1.0, L.r, 0.9) * collarAO;
  float cav = mix(1.0, L.a, 0.6);
  float keyVis = L.g, fillVis = L.b;
  if (uCap > 0.5) { // the brim shades the brow and eyes
    float brim = smoothstep(-0.005, 0.02, vPos.y) * (1.0 - smoothstep(0.065, 0.085, vPos.y)) * smoothstep(-0.03, 0.02, vPos.z);
    keyVis *= 1.0 - brim * 0.75; fillVis *= 1.0 - brim * 0.35; ao *= 1.0 - brim * 0.25;
  }
  vec3 keyDir = normalize(vec3(0.55, 0.45, 0.75));
  vec3 fillDir = normalize(vec3(-0.8, 0.05, 0.55));
  vec3 rimL = normalize(vec3(-0.75, 0.35, -0.6)), rimR = normalize(vec3(0.8, 0.3, -0.55));
  vec3 keyCol = vec3(1.0, 0.95, 0.88) * 2.6, fillCol = vec3(0.80, 0.88, 1.0) * 0.75, rimCol = vec3(0.95, 0.97, 1.0) * 1.6;
  float isSkin = 1.0 - isCloth;
  vec3 wrapW = mix(vec3(0.05), vec3(0.42, 0.20, 0.14), isSkin);
  // red penumbra: shadow edges stay warm in skin
  vec3 keyShadow = mix(vec3(keyVis), vec3(pow(keyVis, 0.55), pow(keyVis, 1.0), pow(keyVis, 1.2)), isSkin);
  vec3 diff = wrapDiffuse(dot(n, keyDir), wrapW) * keyCol * keyShadow;
  diff += wrapDiffuse(dot(n, fillDir), wrapW) * fillCol * fillVis;
  float up = n.y * 0.5 + 0.5;
  vec3 ambient = mix(vec3(0.13, 0.11, 0.10), vec3(0.24, 0.27, 0.31), up) * ao;
  vec3 col = alb * (diff + ambient) * cav;
  // translucency: ears and nostrils glow with the rims behind
  float back = max(dot(-n, rimL), 0.0) + max(dot(-n, rimR), 0.0);
  col += alb * vec3(1.0, 0.30, 0.20) * T.a * isSkin * (0.25 + 0.5 * back) * 0.6;
  // rims (edge light from behind)
  float edge = pow(1.0 - max(dot(n, v), 0.0), 2.5);
  col += alb * rimCol * edge * (max(dot(n, rimL), 0.0) + max(dot(n, rimR), 0.0)) * mix(0.7, 0.35, isCloth);

  // ---- specular
  float oil = (W.a * 0.5 + 0.5);
  float rough = 0.67 - 0.10 * oil - 0.12 * M.r + 0.10 * hairA + 0.06 * (hairNoise - 0.5);
  rough = mix(rough, 0.75, isCloth);
  vec3 h = normalize(keyDir + v);
  float fres = 0.028 + 0.972 * pow(1.0 - max(dot(h, v), 0.0), 5.0);
  float ndl = max(dot(n, keyDir), 0.0);
  float specK = (ggx(n, h, rough) * 0.75 + ggx(n, h, rough * 0.55) * 0.25) * fres * ndl;
  vec3 hf = normalize(fillDir + v);
  float specF = ggx(n, hf, rough) * (0.028 + 0.972 * pow(1.0 - max(dot(hf, v), 0.0), 5.0)) * max(dot(n, fillDir), 0.0);
  float specOcc = keyVis * cav * (1.0 - hairA * 0.8) * (1.0 - beardCov * 0.7);
  col += keyCol * specK * specOcc * mix(0.30, 0.12, isCloth) + fillCol * specF * fillVis * 0.18 * (1.0 - isCloth);

  if (uDebug > 2.5) { col = vec3(0.5) * (diff + ambient) * cav; }
  col = aces(col * 0.70);
  frag = vec4(pow(col, vec3(1.0/2.2)), 1.0);
}
