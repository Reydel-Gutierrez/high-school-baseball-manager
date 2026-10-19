// HSBM hair/beard/brow shells: the head pushed out in layers (shell = 1..N).
// Each layer keeps only the texels where hair still reaches that height,
// with anisotropic noise stretched along the comb direction. Mirrors
// tools/facekit/web/shell.vert + shell.frag.
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_always;

uniform sampler2D masks : filter_linear_mipmap, repeat_disable;
uniform sampler2D detail : filter_linear_mipmap, repeat_disable;
uniform sampler2D hair_map : filter_linear_mipmap, repeat_disable;
uniform sampler2D light_map : filter_linear_mipmap, repeat_disable;
uniform vec3 hair_color = vec3(0.10, 0.07, 0.05);
uniform vec3 brow_color = vec3(0.10, 0.07, 0.05);
uniform vec4 hair_style = vec4(0.0, 0.9, 0.5, 0.0);
uniform vec4 beard = vec4(0.0);
uniform float hair_length = 0.005;
uniform float cap = 0.0;
uniform float shell = 1.0;
uniform float shells = 16.0;

varying vec3 w_pos;
varying vec3 w_nor;
varying float v_len;

float hash3(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }
float noise3(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1.0, 0.0, 0.0)), f.x), mix(hash3(i + vec3(0.0, 1.0, 0.0)), hash3(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
		mix(mix(hash3(i + vec3(0.0, 0.0, 1.0)), hash3(i + vec3(1.0, 0.0, 1.0)), f.x), mix(hash3(i + vec3(0.0, 1.0, 1.0)), hash3(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
}
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
	vec4 M = textureLod(masks, UV, 2.0);
	vec4 D = textureLod(detail, UV, 2.0);
	vec4 H = textureLod(hair_map, UV, 2.0);
	float line = 0.5 - hair_style.x;
	float cover = smoothstep(line - 0.08, line + 0.04, H.r) * step(0.05, hair_style.y);
	float hair_len = hair_length * mix(1.0, 0.35, H.b * hair_style.z) * cover * mix(1.0, 0.5, cap);
	float beard_len = M.b * beard.y * 0.009 + M.a * beard.z * 0.007 + D.b * beard.w * 0.009;
	float brow_len = smoothstep(0.1, 0.4, M.g) * 0.0010;
	float len = max(max(hair_len, beard_len), brow_len);
	float h = shell / shells;
	vec3 g = vec3(0.0, -1.0, -0.35);
	VERTEX += NORMAL * len * h + g * len * h * h * 0.6;
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	w_nor = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	v_len = len;
}

void fragment() {
	if (v_len < 0.0003) {
		discard;
	}
	float vh = shell / shells;
	vec2 raw = vec2(UV.x, 1.0 - UV.y);
	vec4 M = texture(masks, UV);
	vec4 D = texture(detail, UV);
	vec4 H = texture(hair_map, UV);
	float line = 0.5 - hair_style.x;
	float jag = (vnoise(raw * 700.0) - 0.5) * 0.08;
	float hair_c = smoothstep(line - 0.03, line + 0.06, H.r + jag) * (1.0 - H.g * hair_style.w) * hair_style.y;
	float hair_reach = hair_c * mix(1.0, 0.35, H.b * hair_style.z);
	float beard_den = M.b * beard.y + M.a * beard.z + D.b * beard.w;
	float beard_reach = clamp(beard_den * 1.1, 0.0, 1.0);
	float brow_reach = smoothstep(0.25, 0.95, M.g) * 0.6;
	float reach = max(max(hair_reach, beard_reach), brow_reach);
	bool is_brow = brow_reach > max(hair_reach, beard_reach);
	vec3 n = normalize(w_nor);
	vec3 grav = is_brow ? normalize(vec3(sign(w_pos.x), 0.25, 0.0)) : vec3(0.0, -1.0, -0.35);
	vec3 t = normalize(grav - n * dot(grav, n) + vec3(1e-4));
	vec3 b = normalize(cross(n, t));
	float s = is_brow ? 1.4 : 1.0;
	vec3 q = vec3(dot(w_pos, b) * 2600.0 * s, dot(w_pos, t) * 260.0 * s, vh * 3.0);
	float strand = noise3(q) * 0.65 + noise3(q * vec3(2.3, 1.7, 1.0) + 11.0) * 0.35;
	float clump = noise3(vec3(dot(w_pos, b) * 500.0, dot(w_pos, t) * 120.0, 0.0));
	float threshold = vh / max(reach, 1e-3);
	float a = smoothstep(threshold - 0.10, threshold + 0.10, strand * 0.75 + clump * 0.35 + 0.05);
	a *= 1.0 - smoothstep(0.85, 1.0, vh);
	if (a < 0.02) {
		discard;
	}
	vec4 L = texture(light_map, UV);
	vec3 base = to_lin(is_brow ? brow_color : hair_color);
	base *= 0.80 + 0.45 * clump;
	base = mix(base * 0.45, base * 1.10, smoothstep(0.0, 0.7, vh));
	vec3 v = normalize(INV_VIEW_MATRIX[3].xyz - w_pos);
	vec3 key_dir = normalize(vec3(0.55, 0.45, 0.75));
	vec3 fill_dir = normalize(vec3(-0.8, 0.05, 0.55));
	float ao = mix(0.30, 1.0, vh) * mix(1.0, L.r, 0.7);
	float key_vis = L.g * (1.0 - cap * smoothstep(0.0, 0.05, w_pos.y) * 0.8);
	float dk = clamp(dot(n, key_dir) * 0.5 + 0.5, 0.0, 1.0) * mix(0.2, 1.0, key_vis);
	float df = clamp(dot(n, fill_dir) * 0.5 + 0.5, 0.0, 1.0) * L.b;
	vec3 col = base * (dk * vec3(1.0, 0.95, 0.88) * 2.6 + df * vec3(0.8, 0.88, 1.0) * 0.6 + vec3(0.22) * ao) * ao;
	vec3 hv = normalize(key_dir + v);
	float th1 = dot(normalize(t + n * 0.10), hv);
	float th2 = dot(normalize(t - n * 0.15), hv);
	float s1 = pow(sqrt(max(1.0 - th1 * th1, 0.0)), 70.0);
	float s2 = pow(sqrt(max(1.0 - th2 * th2, 0.0)), 25.0);
	col += (vec3(s1) * 0.12 + base * s2 * 0.5) * key_vis * vh * (0.6 + 0.8 * strand);
	col = aces(col * 0.9);
	ALBEDO = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
	ALPHA = a * (is_brow ? 0.55 : 1.0);
}
