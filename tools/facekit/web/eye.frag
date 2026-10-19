#version 300 es
precision highp float;
// Eyeball: sclera with faint vessels and warm corners, recessed iris seen
// through a glossy cornea (parallax), upper-lid shadow, studio catchlights.
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uEye; uniform vec3 uCenter; uniform float uRadius; uniform vec3 uIris; uniform vec3 uLook; uniform float uCap;
out vec4 frag;
float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float vnoise(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x), f.y); }
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
void main(){
  vec3 n = normalize(vNor); vec3 v = normalize(uEye - vPos);
  vec3 fwd = normalize(uLook);
  vec3 up = vec3(0,1,0); vec3 rt = normalize(cross(up, fwd)); vec3 u2 = cross(fwd, rt);
  // parallax: the iris plane sits ~0.25 r behind the cornea
  vec3 d = normalize(vPos - uCenter);
  float c0 = dot(d, fwd);
  vec3 refr = refract(-v, n, 1.0 / 1.376);
  float depth = 0.22 * smoothstep(0.80, 0.95, c0);
  vec3 dI = normalize(d + refr * depth);
  float ang = acos(clamp(dot(dI, fwd), -1.0, 1.0));
  float phi = atan(dot(dI, u2), dot(dI, rt));
  float irisR = 0.53, pupilR = 0.21;
  // sclera
  float angS = acos(clamp(c0, -1.0, 1.0));
  vec3 sclera = toLin(vec3(0.66, 0.62, 0.58));
  float vessels = smoothstep(0.62, 0.9, vnoise(vec2(phi * 7.0, angS * 22.0)) * vnoise(vec2(phi * 23.0, angS * 9.0)) * 2.0);
  sclera = mix(sclera, toLin(vec3(0.78, 0.40, 0.38)), vessels * smoothstep(0.7, 1.3, angS) * 0.35);
  float side = abs(dot(d, rt));
  sclera = mix(sclera, toLin(vec3(0.88, 0.62, 0.58)), smoothstep(0.75, 1.0, side) * 0.55);   // warm corners
  // iris: radial fibers, crypts, collarette, limbal ring
  float r = ang / irisR;
  float fib = vnoise(vec2(phi * 26.0, r * 6.0)) * 0.55 + vnoise(vec2(phi * 70.0, r * 14.0)) * 0.45;
  float crypt = smoothstep(0.55, 0.85, vnoise(vec2(phi * 9.0, r * 5.0))) * smoothstep(0.45, 0.6, r) * (1.0 - smoothstep(0.75, 0.9, r));
  vec3 irisBase = toLin(uIris);
  vec3 iris = irisBase * (0.55 + 0.8 * fib);
  iris = mix(iris, irisBase * 1.6 + 0.02, (1.0 - smoothstep(0.45, 0.62, r)) * 0.55);       // lighter collarette
  iris *= 1.0 - crypt * 0.45;
  iris *= 1.0 - 0.75 * smoothstep(0.80, 1.0, r);                                         // limbal ring
  float inIris = 1.0 - smoothstep(irisR - 0.035, irisR + 0.01, ang);
  vec3 alb = mix(sclera, iris, inIris);
  alb = mix(alb, vec3(0.006), 1.0 - smoothstep(pupilR - 0.02, pupilR + 0.008, ang));
  // lighting: soft key + fill, upper-lid shadow, socket occlusion
  vec3 keyDir = normalize(vec3(0.55, 0.45, 0.75)), fillDir = normalize(vec3(-0.8, 0.05, 0.55));
  float lid = smoothstep(-0.12, 0.50, dot(d, u2));                // shadow under the upper lid
  float lidShadow = (1.0 - lid * 0.78) * (1.0 - smoothstep(-0.25, -0.6, dot(d, u2)) * 0.35);
  float socket = 0.55 + 0.45 * smoothstep(0.55, 1.0, c0);
  float brim = uCap * 0.45;
  vec3 col = alb * (max(dot(d, keyDir), 0.0) * vec3(1.0, 0.95, 0.88) * 2.2 * (1.0 - brim) + max(dot(d, fillDir), 0.0) * 0.6 + 0.30) * lidShadow * socket;
  // wet cornea: sharp catchlights from key and fill (softbox shape)
  vec3 h = normalize(keyDir + v), hf = normalize(fillDir + v);
  float fres = 0.04 + 0.96 * pow(1.0 - max(dot(n, v), 0.0), 5.0);
  col += vec3(pow(max(dot(n, h), 0.0), 900.0)) * 6.0 * (1.0 - lid * 0.9) * (1.0 - brim);
  col += vec3(pow(max(dot(n, hf), 0.0), 500.0)) * 1.2;
  col += vec3(0.25, 0.28, 0.32) * fres * 0.6;
  col = aces(col * 0.85);
  frag = vec4(pow(col, vec3(1.0/2.2)), 1.0);
}
