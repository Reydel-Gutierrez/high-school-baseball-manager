// HSBM headshot eye: sclera with vessels and warm corners, recessed iris seen
// through a glossy cornea (parallax), upper-lid shadow, studio catchlights.
// Mirrors tools/facekit/web/eye.frag.
shader_type spatial;
render_mode unshaded, cull_back;

uniform vec3 iris_color = vec3(0.35, 0.22, 0.12);
uniform vec3 look_dir = vec3(0.0, 0.0, 1.0);
uniform float cap = 0.0;

varying vec3 w_pos;
varying vec3 w_nor;
varying vec3 w_center;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
vec3 to_lin(vec3 c) { return pow(c, vec3(2.2)); }
vec3 aces(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }

void vertex() {
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	w_nor = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	w_center = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
}

void fragment() {
	vec3 n = normalize(w_nor);
	vec3 v = normalize(INV_VIEW_MATRIX[3].xyz - w_pos);
	vec3 fwd = normalize(look_dir);
	vec3 up = vec3(0.0, 1.0, 0.0);
	vec3 rt = normalize(cross(up, fwd));
	vec3 u2 = cross(fwd, rt);
	vec3 d = normalize(w_pos - w_center);
	float c0 = dot(d, fwd);
	vec3 refr = refract(-v, n, 1.0 / 1.376);
	float depth = 0.22 * smoothstep(0.80, 0.95, c0);
	vec3 di = normalize(d + refr * depth);
	float ang = acos(clamp(dot(di, fwd), -1.0, 1.0));
	float phi = atan(dot(di, u2), dot(di, rt));
	float iris_r = 0.56;
	float pupil_r = 0.21;
	float ang_s = acos(clamp(c0, -1.0, 1.0));
	vec3 sclera = to_lin(vec3(0.80, 0.76, 0.72));
	float vessels = smoothstep(0.62, 0.9, vnoise(vec2(phi * 7.0, ang_s * 22.0)) * vnoise(vec2(phi * 23.0, ang_s * 9.0)) * 2.0);
	sclera = mix(sclera, to_lin(vec3(0.78, 0.40, 0.38)), vessels * smoothstep(0.7, 1.3, ang_s) * 0.35);
	float side = abs(dot(d, rt));
	sclera = mix(sclera, to_lin(vec3(0.88, 0.62, 0.58)), smoothstep(0.75, 1.0, side) * 0.55);
	float r = ang / iris_r;
	float fib = vnoise(vec2(phi * 26.0, r * 6.0)) * 0.55 + vnoise(vec2(phi * 70.0, r * 14.0)) * 0.45;
	float crypt = smoothstep(0.55, 0.85, vnoise(vec2(phi * 9.0, r * 5.0))) * smoothstep(0.45, 0.6, r) * (1.0 - smoothstep(0.75, 0.9, r));
	vec3 iris_base = to_lin(iris_color);
	vec3 iris = iris_base * (0.55 + 0.8 * fib);
	iris = mix(iris, iris_base * 1.6 + 0.02, (1.0 - smoothstep(0.45, 0.62, r)) * 0.55);
	iris *= 1.0 - crypt * 0.45;
	iris *= 1.0 - 0.75 * smoothstep(0.80, 1.0, r);
	float in_iris = 1.0 - smoothstep(iris_r - 0.035, iris_r + 0.01, ang);
	vec3 alb = mix(sclera, iris, in_iris);
	alb = mix(alb, vec3(0.006), 1.0 - smoothstep(pupil_r - 0.02, pupil_r + 0.008, ang));
	vec3 key_dir = normalize(vec3(0.55, 0.45, 0.75));
	vec3 fill_dir = normalize(vec3(-0.8, 0.05, 0.55));
	float lid = smoothstep(0.05, 0.55, dot(d, u2));
	float lid_shadow = 1.0 - lid * 0.75;
	float socket = 0.55 + 0.45 * smoothstep(0.55, 1.0, c0);
	float brim = cap * 0.45;
	vec3 col = alb * (max(dot(d, key_dir), 0.0) * vec3(1.0, 0.95, 0.88) * 2.2 * (1.0 - brim) + max(dot(d, fill_dir), 0.0) * 0.6 + 0.30) * lid_shadow * socket;
	vec3 h = normalize(key_dir + v);
	vec3 hf = normalize(fill_dir + v);
	float fres = 0.04 + 0.96 * pow(1.0 - max(dot(n, v), 0.0), 5.0);
	col += vec3(pow(max(dot(n, h), 0.0), 900.0)) * 6.0 * (1.0 - lid * 0.9) * (1.0 - brim);
	col += vec3(pow(max(dot(n, hf), 0.0), 500.0)) * 1.2;
	col += vec3(0.25, 0.28, 0.32) * fres * 0.6;
	col = aces(col * 0.85);
	ALBEDO = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
}
