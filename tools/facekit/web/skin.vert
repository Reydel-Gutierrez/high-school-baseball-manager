#version 300 es
in vec3 aPos; in vec3 aNor; in vec2 aUv;
uniform mat4 uProj, uView; uniform sampler2D ucloth; uniform sampler2D uhair; uniform vec4 uHairStyle; uniform float uHairLen;
out vec3 vPos; out vec3 vNor; out vec2 vUv;
void main(){
  float j = textureLod(ucloth, vec2(aUv.x, 1.0 - aUv.y), 0.0).r;
  vec4 H = textureLod(uhair, vec2(aUv.x, 1.0 - aUv.y), 0.0);
  float line = 0.5 - uHairStyle.x;
  float cover = smoothstep(line - 0.02, line + 0.12, H.r) * (1.0 - H.g * uHairStyle.w) * uHairStyle.y;
  float len = uHairLen * mix(1.0, 0.35, H.b * uHairStyle.z);
  vec3 p = aPos + aNor * (j * 0.007 + cover * len);
  vPos=p; vNor=aNor; vUv=aUv; gl_Position=uProj*uView*vec4(p,1.0); }
