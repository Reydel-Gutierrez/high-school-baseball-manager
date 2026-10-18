#version 300 es
precision highp float;
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uLashColor;
out vec4 frag;
void main(){
  float s = fract(vUv.x * 520.0);
  float a = smoothstep(0.0, 0.25, s) * (1.0 - smoothstep(0.45, 0.7, s));
  float along = fract((vUv.y - 0.926) / 0.03);
  frag = vec4(pow(uLashColor, vec3(1.0)), a * 0.85);
}
