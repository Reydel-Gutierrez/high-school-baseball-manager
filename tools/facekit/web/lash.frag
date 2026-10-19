#version 300 es
precision highp float;
// Eyelash cards: dense tapered upper lashes, sparse faint lower lashes.
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uLashColor;
out vec4 frag;
float hash(float p){ return fract(sin(p * 127.1) * 43758.5453); }
void main(){
  bool lower = vPos.y < -0.003;
  float along = clamp((vUv.y - 0.927) / 0.058, 0.0, 1.0);     // 0 root .. 1 tip
  float x = vUv.x * (lower ? 700.0 : 1500.0);
  float i = floor(x);
  float s = fract(x) - 0.5 + (hash(i) - 0.5) * 0.5 + along * (hash(i + 7.0) - 0.5) * 0.8;
  float width = mix(0.30, 0.06, along);
  float a = 1.0 - smoothstep(width * 0.5, width, abs(s));
  float len = 0.55 + 0.45 * hash(i + 3.0);
  a *= 1.0 - smoothstep(len - 0.15, len, along);
  a *= lower ? 0.0 : 0.95;
  frag = vec4(pow(uLashColor, vec3(1.0/2.2)), a);
}
