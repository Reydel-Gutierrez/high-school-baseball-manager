#version 300 es
precision highp float;
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uEye; uniform vec3 uSkin; uniform float uDebug;
uniform sampler2D umasks, udetail, uhair, ucloth; uniform vec3 uJersey, uTrim;
uniform vec3 uHairColor, uBrowColor; uniform vec4 uHairStyle, uBeard;
out vec4 frag;
float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float vnoise(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x), f.y); }
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
void main(){
  vec2 uv = vec2(vUv.x, 1.0 - vUv.y);
  vec4 M = texture(umasks, uv); vec4 D = texture(udetail, uv); vec4 H = texture(uhair, uv);
  if (uDebug > 0.5 && uDebug < 1.5) { frag = vec4(M.r, M.g, M.b, 1.0) + vec4(M.a*0.5); return; }
  if (uDebug > 1.5 && uDebug < 2.5) { frag = vec4(D.r, D.g, D.b, 1.0); return; }
  if (uDebug > 2.5 && uDebug < 3.5) { frag = vec4(H.r, H.g, H.b, 1.0); return; }
  vec3 n = normalize(vNor); vec3 v = normalize(uEye - vPos);

  // ---- albedo
  vec3 skin = toLin(uSkin);
  float pore = vnoise(vUv * 3200.0) * 0.6 + vnoise(vUv * 7000.0) * 0.4;
  skin *= 0.94 + 0.10 * pore;
  skin = mix(skin, skin * vec3(1.05, 0.72, 0.70), D.r * 0.45);       // redness
  skin *= 1.0 - D.g * 0.35;                                           // cavities
  vec3 lip = skin * vec3(0.92, 0.58, 0.58);
  vec3 alb = mix(skin, lip, M.r);
  // stubble / beard
  float hairNoise = vnoise(vUv * 1400.0) * 0.6 + vnoise(vUv * 3000.0) * 0.4;
  float beardAmt = M.b * max(uBeard.x, uBeard.y) + M.a * uBeard.z + D.b * uBeard.w;
  float beardCov = clamp(beardAmt * (0.55 + 0.9 * hairNoise), 0.0, 1.0);
  vec3 hairLin = toLin(uHairColor);
  alb = mix(alb, mix(alb, hairLin, 0.85), beardCov * (uBeard.y > 0.0 ? 1.0 : 0.6));
  // brows
  alb = mix(alb, toLin(uBrowColor), clamp(M.g * 1.15, 0.0, 1.0) * 0.92);
  // scalp hair (painted short cut)
  float line = 0.5 - uHairStyle.x;                   // raise for a receding hairline
  float cover = smoothstep(line - 0.04, line + 0.10, H.r);
  cover *= 1.0 - H.g * uHairStyle.w;                 // balding crown
  float fadeDensity = mix(1.0, 0.35, H.b * uHairStyle.z);
  float strands = 0.6 * vnoise(vec2(vUv.x * 900.0, vUv.y * 260.0) + vnoise(vUv * 300.0) * 8.0) + 0.4 * vnoise(vec2(vUv.x * 2200.0, vUv.y * 500.0));
  float hairA = clamp(cover * uHairStyle.y * fadeDensity * (0.85 + 0.25 * strands), 0.0, 1.0);
  alb = mix(alb, hairLin * (0.75 + 0.5 * strands), hairA);

  // ---- jersey (double-knit polyester: fine weave + soft sheen)
  vec4 C = texture(ucloth, uv);
  float weave = 0.5 + 0.5 * sin(vPos.x * 5200.0) * sin(vPos.y * 5200.0);
  vec3 cloth = toLin(uJersey) * (0.92 + 0.08 * weave);
  cloth = mix(cloth, toLin(uTrim), C.g);
  float buttons = C.b * step(0.75, fract((vPos.y + 0.2) * 22.0)) * step(abs(vPos.x), 0.004);
  cloth = mix(cloth, toLin(uTrim) * 0.9, C.b * 0.25);
  cloth = mix(cloth, vec3(0.85), buttons);
  float isCloth = step(0.5, C.r);
  alb = mix(alb, cloth, isCloth);
  if (uDebug > 3.5) alb = vec3(0.5);
  if (uDebug > 4.5) { frag = vec4(n*0.5+0.5,1.0); return; }
  // ---- lighting: key, fill, rim + wrapped diffuse with a warm subsurface term
  vec3 key = normalize(vec3(0.55, 0.45, 0.75)); vec3 fill = normalize(vec3(-0.8, 0.05, 0.55)); vec3 rim = normalize(vec3(-0.35, 0.5, -0.8));
  float ndk = dot(n, key);
  float wrapK = 0.45;
  float dk = clamp((ndk + wrapK) / (1.0 + wrapK), 0.0, 1.0);
  float df = clamp((dot(n, fill) + wrapK) / (1.0 + wrapK), 0.0, 1.0);
  vec3 sssTint = vec3(1.0, 0.42, 0.30);
  vec3 diffuse = alb * (dk * vec3(1.0, 0.96, 0.90) * 1.45 + df * vec3(0.70, 0.80, 1.0) * 0.40 + vec3(0.06, 0.065, 0.075));
  diffuse += (1.0 - isCloth) * alb * sssTint * 0.55 * (smoothstep(-wrapK, 0.2, ndk) - smoothstep(0.2, 0.9, ndk));
  float rough = mix(mix(0.42, 0.30, M.r) + 0.12 * hairA + 0.1 * pore, 0.55, isCloth);
  vec3 hvec = normalize(key + v);
  float a2 = rough * rough * rough * rough;
  float nh = max(dot(n, hvec), 0.0);
  float dd = nh * nh * (a2 - 1.0) + 1.0;
  float ggx = a2 / (3.14159 * dd * dd);
  float fres = 0.028 + 0.972 * pow(1.0 - max(dot(hvec, v), 0.0), 5.0);
  vec3 spec = vec3(ggx * fres * max(ndk, 0.0)) * 0.55 * (1.0 - hairA * 0.7);
  float rimT = pow(1.0 - max(dot(n, v), 0.0), 3.0) * clamp(dot(n, rim) + 0.4, 0.0, 1.0);
  vec3 col = diffuse + spec + vec3(0.65, 0.75, 0.9) * rimT * 0.35;
  // filmic-ish tonemap
  col = col / (col + vec3(0.55)) * 1.25;
  frag = vec4(pow(col, vec3(1.0/2.2)), 1.0);
}
