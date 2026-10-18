#version 300 es
precision highp float;
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uEye; uniform vec3 uCenter; uniform vec3 uIris; uniform vec3 uLook;
out vec4 frag;
float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float vnoise(vec2 p){ vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),f.x), mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x), f.y); }
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
void main(){
  vec3 n = normalize(vNor); vec3 v = normalize(uEye - vPos);
  vec3 d = normalize(vPos - uCenter);
  vec3 fwd = normalize(uLook);
  float c = dot(d, fwd);
  float ang = acos(clamp(c, -1.0, 1.0));
  // local polar angle around the gaze axis for iris fibers
  vec3 up = vec3(0,1,0); vec3 rt = normalize(cross(up, fwd)); vec3 u2 = cross(fwd, rt);
  float phi = atan(dot(d, u2), dot(d, rt));
  float irisR = 0.50, pupilR = 0.20;
  vec3 sclera = toLin(vec3(0.93, 0.90, 0.86)) * (0.85 + 0.15 * smoothstep(1.4, 0.6, ang));
  sclera = mix(sclera, toLin(vec3(0.85, 0.55, 0.52)), smoothstep(1.1, 1.6, ang) * 0.6);
  float fib = vnoise(vec2(phi * 18.0, ang * 30.0));
  vec3 iris = toLin(uIris) * (0.55 + 0.75 * fib);
  iris = mix(iris, iris * 1.5 + 0.03, smoothstep(irisR * 0.9, pupilR * 1.3, ang) * 0.45);   // lighter collarette
  iris *= 1.0 - 0.55 * smoothstep(irisR * 0.80, irisR, ang);                              // dark limbal ring
  vec3 alb = mix(sclera, iris, 1.0 - smoothstep(irisR - 0.03, irisR + 0.01, ang));
  alb = mix(alb, vec3(0.008), 1.0 - smoothstep(pupilR - 0.02, pupilR + 0.01, ang));
  vec3 key = normalize(vec3(0.55, 0.45, 0.75));
  float dk = max(dot(n, key), 0.0) * 0.8 + 0.25;
  vec3 col = alb * dk * 1.4;
  // cornea highlight
  vec3 h = normalize(key + v);
  col += vec3(pow(max(dot(n, h), 0.0), 400.0)) * 3.0;
  col += vec3(pow(max(dot(n, normalize(vec3(-0.6,0.2,0.8) + v)), 0.0), 300.0)) * 0.6;
  col = col / (col + vec3(0.6)) * 1.35;
  frag = vec4(pow(col, vec3(1.0/2.2)), 1.0);
}
