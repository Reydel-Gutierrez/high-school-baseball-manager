// HSBM headshot eye: sclera, iris fibers, pupil, cornea highlight.
shader_type spatial;
render_mode unshaded, cull_back;

uniform vec3 iris_color = vec3(0.35, 0.22, 0.12);
uniform vec3 look_dir = vec3(0.0, 0.0, 1.0);

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

void vertex() {
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	w_nor = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	w_center = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
}

void fragment() {
	vec3 n = normalize(w_nor);
	vec3 v = normalize(INV_VIEW_MATRIX[3].xyz - w_pos);
	vec3 d = normalize(w_pos - w_center);
	vec3 fwd = normalize(look_dir);
	float ang = acos(clamp(dot(d, fwd), -1.0, 1.0));
	vec3 rt = normalize(cross(vec3(0.0, 1.0, 0.0), fwd));
	vec3 u2 = cross(fwd, rt);
	float phi = atan(dot(d, u2), dot(d, rt));
	float iris_r = 0.50;
	float pupil_r = 0.20;
	vec3 sclera = to_lin(vec3(0.93, 0.90, 0.86)) * (0.85 + 0.15 * smoothstep(1.4, 0.6, ang));
	sclera = mix(sclera, to_lin(vec3(0.85, 0.55, 0.52)), smoothstep(1.1, 1.6, ang) * 0.6);
	float fib = vnoise(vec2(phi * 18.0, ang * 30.0));
	vec3 iris = to_lin(iris_color) * (0.55 + 0.75 * fib);
	iris = mix(iris, iris * 1.5 + 0.03, smoothstep(iris_r * 0.9, pupil_r * 1.3, ang) * 0.45);
	iris *= 1.0 - 0.55 * smoothstep(iris_r * 0.80, iris_r, ang);
	vec3 alb = mix(sclera, iris, 1.0 - smoothstep(iris_r - 0.03, iris_r + 0.01, ang));
	alb = mix(alb, vec3(0.008), 1.0 - smoothstep(pupil_r - 0.02, pupil_r + 0.01, ang));
	vec3 key = normalize(vec3(0.55, 0.45, 0.75));
	float dk = max(dot(n, key), 0.0) * 0.8 + 0.25;
	vec3 col = alb * dk * 1.4;
	vec3 h = normalize(key + v);
	col += vec3(pow(max(dot(n, h), 0.0), 400.0)) * 3.0;
	col += vec3(pow(max(dot(n, normalize(vec3(-0.6, 0.2, 0.8) + v)), 0.0), 300.0)) * 0.6;
	col = col / (col + vec3(0.6)) * 1.35;
	ALBEDO = col;
}
