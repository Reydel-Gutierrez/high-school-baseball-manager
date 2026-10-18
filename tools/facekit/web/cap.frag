#version 300 es
precision highp float;
in vec3 vPos; in vec3 vNor; in vec2 vUv;
uniform vec3 uEye; uniform vec3 uCap; uniform vec3 uLogo;
out vec4 frag;
vec3 toLin(vec3 c){ return pow(c, vec3(2.2)); }
void main(){
  vec3 n = normalize(vNor);
  if (!gl_FrontFacing) n = -n;
  vec3 v = normalize(uEye - vPos);
  bool brim = vUv.y > 1.5;
  vec3 alb = toLin(uCap);
  if (!brim) {
    // six panels: seams every 60 degrees of theta, stitched
    float th = vUv.x * 180.0;
    float seam = abs(fract((th + 30.0) / 60.0) - 0.5) * 60.0;
    alb *= 1.0 - 0.35 * (1.0 - smoothstep(0.4, 1.6, seam)) * smoothstep(0.03, 0.07, vUv.y);
    // front logo patch (letter drawn in Godot); here a simple block letter area
    float lx = th, ly = vUv.y;
    float logo = step(abs(lx), 11.0) * step(0.062, ly) * step(ly, 0.098);
    alb = mix(alb, toLin(uLogo), logo * 0.0);
    // crown button
    alb = mix(alb, alb * 0.7, smoothstep(0.112, 0.118, vUv.y));
  } else if (!gl_FrontFacing) {
    alb = toLin(vec3(0.12, 0.13, 0.12));
  }
  vec3 key = normalize(vec3(0.55, 0.45, 0.75)); vec3 fill = normalize(vec3(-0.8, 0.05, 0.55));
  float dk = max(dot(n, key), 0.0), df = max(dot(n, fill), 0.0);
  vec3 col = alb * (dk * 1.45 + df * 0.4 + 0.08);
  vec3 h = normalize(key + v);
  col += vec3(pow(max(dot(n, h), 0.0), 18.0) * 0.05);
  col = col / (col + vec3(0.55)) * 1.25;
  frag = vec4(pow(col, vec3(1.0/2.2)), 1.0);
}
