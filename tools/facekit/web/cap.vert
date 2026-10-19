#version 300 es
in vec3 aPos; in vec3 aNor; in vec2 aUv;
uniform mat4 uProj, uView;
out vec3 vPos; out vec3 vNor; out vec2 vUv;
void main(){ vPos = aPos; vNor = aNor; vUv = aUv; gl_Position = uProj * uView * vec4(aPos, 1.0); }
