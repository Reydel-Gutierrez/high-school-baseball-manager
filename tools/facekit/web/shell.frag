#version 300 es
precision highp float;
// Hair as a soft volume: anisotropic noise stretched along the comb
// direction, coverage thinning toward the tips. Sized for ~1 mm pixels.
in vec3 vPos; in vec3 vNor; in vec2 vUv; in float vH; in float vLen;
uniform vec3 uEye; uniform vec3 uHairColor, uBrowColor;
uniform sampler2D ulight, umasks, udetail, uhair;
uniform vec4 uHairStyle, uBeard; uniform float uCap;
out vec4 frag;
float hash3(vec3 p){ return fract(sin(dot(p, vec3(127.1,311.7,74.7)))*43758.5453); }
float noise3(vec3 p){ vec3 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(mix(hash3(i),hash3(i+vec3(1,0,0)),f.x), mix(hash3(i+vec3(0,1,0)),hash3(i+vec3(1,1,0)),f.x), f.y),
             mix(mix(hash3(i+vec3(0,0,1)),hash3(i+vec3(1,0,1)),f.x), mix(hash3(i+vec3(0,1,1)),hash3(i+vec3(1,1,1)),f.x), f.y), f.z); }
float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float vnoise(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x), f.y); }
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
void main(){
  if (vLen < 0.0003) discard;
  vec2 tuv = vec2(vUv.x, 1.0 - vUv.y);
  vec4 M = texture(umasks, tuv); vec4 D = texture(udetail, tuv); vec4 H = texture(uhair, tuv);
  // exact coverage per region (length fraction this texel reaches)
  float line = 0.5 - uHairStyle.x;
  float jag = (vnoise(vUv * 700.0) - 0.5) * 0.08;
  float hairC = smoothstep(line - 0.03, line + 0.06, H.r + jag) * (1.0 - H.g * uHairStyle.w) * uHairStyle.y;
  float hairReach = hairC * mix(1.0, 0.35, H.b * uHairStyle.z);
  float beardDen = M.b * uBeard.y + M.a * uBeard.z + D.b * uBeard.w;
  float beardReach = clamp(beardDen * 1.1, 0.0, 1.0);
  float browReach = smoothstep(0.25, 0.95, M.g) * 0.6;
  float reach = max(max(hairReach, beardReach), browReach);
  bool isBrow = browReach > max(hairReach, beardReach);
  vec3 n = normalize(vNor);
  vec3 grav = isBrow ? normalize(vec3(sign(vPos.x) * 1.0, 0.25, 0.0)) : vec3(0.0, -1.0, -0.35);
  vec3 T = normalize(grav - n * dot(grav, n) + vec3(1e-4));
  vec3 B = normalize(cross(n, T));
  float s = isBrow ? 1.4 : 1.0;
  vec3 q = vec3(dot(vPos, B) * 2600.0 * s, dot(vPos, T) * 260.0 * s, vH * 3.0);
  float strand = noise3(q) * 0.65 + noise3(q * vec3(2.3, 1.7, 1.0) + 11.0) * 0.35;
  float clump = noise3(vec3(dot(vPos, B) * 500.0, dot(vPos, T) * 120.0, 0.0));
  float threshold = vH / max(reach, 1e-3);
  float a = smoothstep(threshold - 0.10, threshold + 0.10, strand * 0.75 + clump * 0.35 + 0.05);
  a *= 1.0 - smoothstep(0.85, 1.0, vH);
  if (a < 0.02) discard;
  vec4 L = texture(ulight, tuv);
  vec3 base = toLin(isBrow ? uBrowColor : uHairColor);
  base *= 0.80 + 0.45 * clump;
  base = mix(base * 0.45, base * 1.10, smoothstep(0.0, 0.7, vH));
  vec3 v = normalize(uEye - vPos);
  vec3 keyDir = normalize(vec3(0.55, 0.45, 0.75)), fillDir = normalize(vec3(-0.8, 0.05, 0.55));
  float ao = mix(0.30, 1.0, vH) * mix(1.0, L.r, 0.7);
  float keyVis = L.g * (1.0 - uCap * smoothstep(0.0, 0.05, vPos.y) * 0.8);
  float dk = clamp(dot(n, keyDir) * 0.5 + 0.5, 0.0, 1.0) * mix(0.2, 1.0, keyVis);
  float df = clamp(dot(n, fillDir) * 0.5 + 0.5, 0.0, 1.0) * L.b;
  vec3 col = base * (dk * vec3(1.0, 0.95, 0.88) * 2.6 + df * vec3(0.8, 0.88, 1.0) * 0.6 + vec3(0.22) * ao) * ao;
  vec3 hv = normalize(keyDir + v);
  float th1 = dot(normalize(T + n * 0.10), hv), th2 = dot(normalize(T - n * 0.15), hv);
  float s1 = pow(sqrt(max(1.0 - th1 * th1, 0.0)), 70.0), s2 = pow(sqrt(max(1.0 - th2 * th2, 0.0)), 25.0);
  col += (vec3(s1) * 0.12 + base * s2 * 0.5) * keyVis * vH * (0.6 + 0.8 * strand);
  col = aces(col * 0.9);
  frag = vec4(pow(col, vec3(1.0/2.2)), a * (isBrow ? 0.55 : 1.0));
}
