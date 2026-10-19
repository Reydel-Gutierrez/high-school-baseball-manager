#version 300 es
// Hair/beard/brow shells: the head surface pushed out in layers. Length is
// the per-vertex upper bound; the fragment shader decides real coverage.
in vec3 aPos; in vec3 aNor; in vec2 aUv;
uniform mat4 uProj, uView;
uniform sampler2D umasks, udetail, uhair;
uniform vec4 uHairStyle, uBeard; uniform float uHairLen, uShell, uShells, uCap;
out vec3 vPos; out vec3 vNor; out vec2 vUv; out float vH; out float vLen;
void main(){
  vec2 t = vec2(aUv.x, 1.0 - aUv.y);
  vec4 M = textureLod(umasks, t, 2.0); vec4 D = textureLod(udetail, t, 2.0); vec4 H = textureLod(uhair, t, 2.0);
  float line = 0.5 - uHairStyle.x;
  float cover = smoothstep(line - 0.08, line + 0.04, H.r) * step(0.05, uHairStyle.y);
  float hairLen = uHairLen * mix(1.0, 0.35, H.b * uHairStyle.z) * cover * mix(1.0, 0.5, uCap);
  float beardLen = M.b * uBeard.y * 0.009 + M.a * uBeard.z * 0.0045 + D.b * uBeard.w * 0.009;
  float browLen = smoothstep(0.1, 0.4, M.g) * 0.0010;
  float len = max(max(hairLen, beardLen), browLen);
  float h = uShell / uShells;
  vec3 g = vec3(0.0, -1.0, -0.35);
  vec3 p = aPos + aNor * len * h + g * len * h * h * 0.6;
  vPos = p; vNor = aNor; vUv = aUv; vH = h; vLen = len;
  gl_Position = uProj * uView * vec4(p, 1.0);
}
