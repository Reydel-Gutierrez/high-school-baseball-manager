// HSBM headshot skin. Lighting is computed here (unshaded) so the studio
// look is identical on every renderer, including GL Compatibility.
// Mirrors tools/facekit/web/skin.frag (the browser harness used to tune it).
//
// Baked inputs (UV space, see tools/facekit/bake_*.py):
//   masks   R lips  G brows  B beard zone  A mustache
//   detail  R redness  G cavity  B goatee
//   hair_map R coverage  G crown  B fade
//   cloth   R jersey  G collar trim  B placket
//   light   R ambient occlusion  G key-light visibility  B fill visibility  A cavity
//   wrinkle R forehead/frown  G crow's feet/under-eye  B folds/neck (signed)  A oiliness
//   tone    R under-eye darkness  G forehead warmth  A translucency (ears, nostrils)
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform sampler2D masks : filter_linear_mipmap, repeat_disable;
uniform sampler2D detail : filter_linear_mipmap, repeat_disable;
uniform sampler2D hair_map : filter_linear_mipmap, repeat_disable;
uniform sampler2D cloth : filter_linear_mipmap, repeat_disable;
uniform sampler2D light_map : filter_linear_mipmap, repeat_disable;
uniform sampler2D wrinkle : filter_linear_mipmap, repeat_disable;
uniform sampler2D tone : filter_linear_mipmap, repeat_disable;

uniform vec3 skin_color = vec3(0.86, 0.66, 0.55);   // sRGB
uniform vec3 hair_color = vec3(0.10, 0.07, 0.05);
uniform vec3 brow_color = vec3(0.10, 0.07, 0.05);
uniform vec4 hair_style = vec4(0.0, 0.9, 0.5, 0.0);  // hairline lift, density, fade, balding
uniform vec4 beard = vec4(0.0);                      // stubble, full, mustache, goatee
uniform vec3 jersey_color = vec3(0.08, 0.12, 0.35);
uniform vec3 trim_color = vec3(0.9, 0.6, 0.1);
uniform float show_jersey = 1.0;
uniform float age = 30.0;
uniform float cap = 0.0;

varying vec3 w_pos;
varying vec3 w_nor;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
vec3 to_lin(vec3 c) { return pow(c, vec3(2.2)); }
vec3 aces(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }
float ggx(vec3 n, vec3 h, float rough) {
	float a = rough * rough;
	float a2 = a * a;
	float nh = max(dot(n, h), 0.0);
	float d = nh * nh * (a2 - 1.0) + 1.0;
	return a2 / (3.14159 * d * d);
}
vec3 wrap_diffuse(float ndl, vec3 w) { return clamp((vec3(ndl) + w) / (1.0 + w), 0.0, 1.0) / (1.0 + w); }
float ramp(float a, float b, float v) { return clamp((v - a) / (b - a), 0.0, 1.0); }

void vertex() {
	float j = textureLod(cloth, UV, 0.0).r * show_jersey;
	VERTEX += NORMAL * (j * 0.007);
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	w_nor = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = UV;
	vec2 raw = vec2(UV.x, 1.0 - UV.y);
	vec4 M = texture(masks, uv);
	vec4 D = texture(detail, uv);
	vec4 H = texture(hair_map, uv);
	vec4 L = texture(light_map, uv);
	vec4 W = texture(wrinkle, uv) * 2.0 - 1.0;
	vec4 T = texture(tone, uv);
	vec4 C = texture(cloth, uv);
	float is_cloth = step(0.5, C.r) * show_jersey;

	// ---- age-weighted crease height (meters) -> bump the normal
	float w_fore = ramp(24.0, 50.0, age);
	float w_eye = ramp(28.0, 55.0, age);
	float w_fold = 0.25 + 0.75 * ramp(20.0, 50.0, age);
	float hgt = (W.r * w_fore + W.g * w_eye + W.b * w_fold) * 0.0009;
	hgt += (vnoise(raw * 2600.0) - 0.5) * 0.00005;
	vec3 n = normalize(w_nor);
	if (!FRONT_FACING) {
		n = -n;
	}
	vec3 dpdx = dFdx(w_pos);
	vec3 dpdy = dFdy(w_pos);
	float dhx = dFdx(hgt);
	float dhy = dFdy(hgt);
	vec3 r1 = cross(dpdy, n);
	vec3 r2 = cross(n, dpdx);
	float det = dot(dpdx, r1);
	vec3 grad = sign(det) * (dhx * r1 + dhy * r2);
	n = normalize(abs(det) * n - grad);
	vec3 v = normalize(INV_VIEW_MATRIX[3].xyz - w_pos);

	// ---- albedo
	vec3 base = to_lin(skin_color);
	float lum = dot(base, vec3(0.3, 0.59, 0.11));
	vec3 skin = base;
	skin = mix(skin, skin * vec3(1.03, 1.0, 0.90), T.g * 0.5);
	skin = mix(skin, skin * vec3(1.05, 0.70, 0.68), clamp(D.r * 1.1, 0.0, 1.0));
	skin = mix(skin, skin * vec3(0.78, 0.70, 0.78), T.r * 0.55);
	float blotch = vnoise(raw * 90.0) * 0.6 + vnoise(raw * 260.0) * 0.4;
	skin *= 0.93 + 0.12 * blotch;
	float freckle = step(0.80, vnoise(raw * 700.0)) * (1.0 - smoothstep(0.15, 0.35, lum)) * 0.25 * D.r;
	skin *= 1.0 - freckle;
	float shadow_amt = M.b * mix(0.0, 0.55, ramp(15.0, 22.0, age)) * (0.5 + beard.x);
	skin = mix(skin, skin * vec3(0.80, 0.82, 0.86), shadow_amt * 0.6);
	float lip_lines = 0.85 + 0.15 * vnoise(vec2(raw.x * 1800.0, raw.y * 120.0));
	vec3 lip = base * mix(vec3(0.80, 0.46, 0.47), vec3(0.72, 0.52, 0.56), 1.0 - smoothstep(0.08, 0.3, lum)) * lip_lines;
	vec3 alb = mix(skin, lip, smoothstep(0.15, 0.85, M.r));
	vec3 hair_lin = to_lin(hair_color);
	float hair_noise = vnoise(raw * 1400.0) * 0.6 + vnoise(raw * 3000.0) * 0.4;
	float beard_amt = M.b * max(beard.x * 0.55, beard.y) + M.a * beard.z + D.b * beard.w;
	float beard_cov = clamp(beard_amt * (0.45 + 0.9 * hair_noise), 0.0, 1.0);
	alb = mix(alb, hair_lin, beard_cov * mix(0.45, 0.92, beard.y));
	float brow = clamp(M.g * 1.2, 0.0, 1.0);
	alb = mix(alb, to_lin(brow_color), brow * 0.62);
	vec4 style = cap > 0.5 ? vec4(hair_style.x, hair_style.y, 1.0, 1.0) : hair_style;
	float line = 0.5 - style.x;
	float cover = smoothstep(line - 0.04, line + 0.10, H.r);
	cover *= 1.0 - H.g * style.w;
	float fade_density = mix(1.0, 0.30, H.b * style.z);
	float strands = 0.6 * vnoise(vec2(raw.x * 900.0, raw.y * 260.0) + vnoise(raw * 300.0) * 8.0) + 0.4 * vnoise(vec2(raw.x * 2200.0, raw.y * 500.0));
	float hair_a = clamp(cover * style.y * fade_density * (0.80 + 0.30 * strands), 0.0, 1.0);
	alb = mix(alb, hair_lin * (0.55 + 0.45 * strands), hair_a);

	// ---- jersey
	float weave = 0.5 + 0.5 * sin(w_pos.x * 5200.0) * sin(w_pos.y * 5200.0);
	vec3 cloth_c = to_lin(jersey_color) * (0.90 + 0.10 * weave);
	cloth_c = mix(cloth_c, to_lin(trim_color), C.g);
	float buttons = C.b * step(0.75, fract((w_pos.y + 0.2) * 22.0)) * step(abs(w_pos.x), 0.004);
	cloth_c = mix(cloth_c, to_lin(trim_color) * 0.9, C.b * 0.25);
	cloth_c = mix(cloth_c, vec3(0.85), buttons);
	alb = mix(alb, cloth_c, is_cloth);
	float collar_ao = 1.0 - (1.0 - is_cloth) * smoothstep(0.30, 0.5, C.r) * 0.55 * show_jersey;

	// ---- light transport
	float ao = mix(1.0, L.r, 0.9) * collar_ao;
	float cav = mix(1.0, L.a, 0.6);
	float key_vis = L.g;
	float fill_vis = L.b;
	if (cap > 0.5) {
		float brim = smoothstep(-0.005, 0.02, w_pos.y) * (1.0 - smoothstep(0.065, 0.085, w_pos.y)) * smoothstep(-0.03, 0.02, w_pos.z);
		key_vis *= 1.0 - brim * 0.75;
		fill_vis *= 1.0 - brim * 0.35;
		ao *= 1.0 - brim * 0.25;
	}
	vec3 key_dir = normalize(vec3(0.55, 0.45, 0.75));
	vec3 fill_dir = normalize(vec3(-0.8, 0.05, 0.55));
	vec3 rim_l = normalize(vec3(-0.75, 0.35, -0.6));
	vec3 rim_r = normalize(vec3(0.8, 0.3, -0.55));
	vec3 key_col = vec3(1.0, 0.95, 0.88) * 2.6;
	vec3 fill_col = vec3(0.80, 0.88, 1.0) * 0.75;
	vec3 rim_col = vec3(0.95, 0.97, 1.0) * 1.6;
	float is_skin = 1.0 - is_cloth;
	vec3 wrap_w = mix(vec3(0.05), vec3(0.42, 0.20, 0.14), is_skin);
	vec3 key_shadow = mix(vec3(key_vis), vec3(pow(key_vis, 0.55), pow(key_vis, 1.0), pow(key_vis, 1.2)), is_skin);
	vec3 diff = wrap_diffuse(dot(n, key_dir), wrap_w) * key_col * key_shadow;
	diff += wrap_diffuse(dot(n, fill_dir), wrap_w) * fill_col * fill_vis;
	float up = n.y * 0.5 + 0.5;
	vec3 ambient = mix(vec3(0.13, 0.11, 0.10), vec3(0.24, 0.27, 0.31), up) * ao;
	vec3 col = alb * (diff + ambient) * cav;
	float back = max(dot(-n, rim_l), 0.0) + max(dot(-n, rim_r), 0.0);
	col += alb * vec3(1.0, 0.30, 0.20) * T.a * is_skin * (0.25 + 0.5 * back) * 0.6;
	float edge = pow(1.0 - max(dot(n, v), 0.0), 2.5);
	col += alb * rim_col * edge * (max(dot(n, rim_l), 0.0) + max(dot(n, rim_r), 0.0)) * mix(0.7, 0.35, is_cloth);

	// ---- specular
	float oil = W.a * 0.5 + 0.5;
	float rough = 0.60 - 0.10 * oil - 0.12 * M.r + 0.10 * hair_a + 0.06 * (hair_noise - 0.5);
	rough = mix(rough, 0.75, is_cloth);
	vec3 h = normalize(key_dir + v);
	float fres = 0.028 + 0.972 * pow(1.0 - max(dot(h, v), 0.0), 5.0);
	float ndl = max(dot(n, key_dir), 0.0);
	float spec_k = (ggx(n, h, rough) * 0.75 + ggx(n, h, rough * 0.55) * 0.25) * fres * ndl;
	vec3 hf = normalize(fill_dir + v);
	float spec_f = ggx(n, hf, rough) * (0.028 + 0.972 * pow(1.0 - max(dot(hf, v), 0.0), 5.0)) * max(dot(n, fill_dir), 0.0);
	float spec_occ = key_vis * cav * (1.0 - hair_a * 0.8) * (1.0 - beard_cov * 0.7);
	col += key_col * spec_k * spec_occ * mix(0.42, 0.12, is_cloth) + fill_col * spec_f * fill_vis * 0.18 * (1.0 - is_cloth);

	col = aces(col * 0.80);
	ALBEDO = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
}
