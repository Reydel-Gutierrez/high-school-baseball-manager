// HSBM headshot skin. Lighting is computed here (unshaded) so the studio
// look is identical on every renderer, including GL Compatibility.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform sampler2D masks : filter_linear_mipmap, repeat_disable;   // R lips  G brows  B beard  A mustache
uniform sampler2D detail : filter_linear_mipmap, repeat_disable;  // R redness  G cavity  B goatee
uniform sampler2D hair_map : filter_linear_mipmap, repeat_disable; // R coverage  G crown  B fade
uniform sampler2D cloth : filter_linear_mipmap, repeat_disable;   // R jersey  G collar trim  B placket

uniform vec3 skin_color = vec3(0.86, 0.66, 0.55);   // sRGB
uniform vec3 hair_color = vec3(0.10, 0.07, 0.05);
uniform vec3 brow_color = vec3(0.10, 0.07, 0.05);
uniform vec4 hair_style = vec4(0.0, 0.9, 0.5, 0.0);  // hairline lift, density, fade, balding
uniform float hair_length = 0.006;
uniform vec4 beard = vec4(0.0);                      // stubble, full, mustache, goatee
uniform vec3 jersey_color = vec3(0.08, 0.12, 0.35);
uniform vec3 trim_color = vec3(0.9, 0.6, 0.1);
uniform float show_jersey = 1.0;
uniform float age_lines = 0.0;

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

void vertex() {
	float j = textureLod(cloth, UV, 0.0).r * show_jersey;
	vec4 h = textureLod(hair_map, UV, 0.0);
	float line = 0.5 - hair_style.x;
	float cover = smoothstep(line - 0.02, line + 0.12, h.r) * (1.0 - h.g * hair_style.w) * hair_style.y;
	float len = hair_length * mix(1.0, 0.35, h.b * hair_style.z);
	VERTEX += NORMAL * (j * 0.007 + cover * len);
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	w_nor = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = UV;
	vec4 M = texture(masks, uv);
	vec4 D = texture(detail, uv);
	vec4 H = texture(hair_map, uv);
	vec4 C = texture(cloth, uv);
	vec3 n = normalize(w_nor);
	if (!FRONT_FACING) {
		n = -n;
	}
	vec3 cam = INV_VIEW_MATRIX[3].xyz;
	vec3 v = normalize(cam - w_pos);

	vec3 skin = to_lin(skin_color);
	float pore = vnoise(uv * 3200.0) * 0.6 + vnoise(uv * 7000.0) * 0.4;
	skin *= 0.94 + 0.10 * pore;
	skin = mix(skin, skin * vec3(1.05, 0.72, 0.70), D.r * 0.45);
	skin *= 1.0 - D.g * (0.35 + 0.25 * age_lines);
	vec3 lip = skin * vec3(0.92, 0.58, 0.58);
	vec3 alb = mix(skin, lip, M.r);

	float hair_noise = vnoise(uv * 1400.0) * 0.6 + vnoise(uv * 3000.0) * 0.4;
	float beard_amt = M.b * max(beard.x, beard.y) + M.a * beard.z + D.b * beard.w;
	float beard_cov = clamp(beard_amt * (0.55 + 0.9 * hair_noise), 0.0, 1.0);
	vec3 hair_lin = to_lin(hair_color);
	alb = mix(alb, mix(alb, hair_lin, 0.85), beard_cov * (beard.y > 0.0 ? 1.0 : 0.6));
	alb = mix(alb, to_lin(brow_color), clamp(M.g * 1.15, 0.0, 1.0) * 0.92);

	float line = 0.5 - hair_style.x;
	float cover = smoothstep(line - 0.04, line + 0.10, H.r);
	cover *= 1.0 - H.g * hair_style.w;
	float fade_density = mix(1.0, 0.35, H.b * hair_style.z);
	float strands = 0.6 * vnoise(vec2(uv.x * 900.0, uv.y * 260.0) + vnoise(uv * 300.0) * 8.0) + 0.4 * vnoise(vec2(uv.x * 2200.0, uv.y * 500.0));
	float hair_a = clamp(cover * hair_style.y * fade_density * (0.85 + 0.25 * strands), 0.0, 1.0);
	alb = mix(alb, hair_lin * (0.75 + 0.5 * strands), hair_a);

	float is_cloth = step(0.5, C.r) * show_jersey;
	vec3 cloth_c = to_lin(jersey_color) * (0.92 + 0.08 * (0.5 + 0.5 * sin(w_pos.x * 5200.0) * sin(w_pos.y * 5200.0)));
	cloth_c = mix(cloth_c, to_lin(trim_color), C.g);
	cloth_c = mix(cloth_c, to_lin(trim_color) * 0.9, C.b * 0.25);
	alb = mix(alb, cloth_c, is_cloth);

	vec3 key = normalize(vec3(0.55, 0.45, 0.75));
	vec3 fill = normalize(vec3(-0.8, 0.05, 0.55));
	vec3 rim = normalize(vec3(-0.35, 0.5, -0.8));
	float ndk = dot(n, key);
	float wrap_k = 0.45;
	float dk = clamp((ndk + wrap_k) / (1.0 + wrap_k), 0.0, 1.0);
	float df = clamp((dot(n, fill) + wrap_k) / (1.0 + wrap_k), 0.0, 1.0);
	vec3 sss_tint = vec3(1.0, 0.42, 0.30);
	vec3 diffuse = alb * (dk * vec3(1.0, 0.96, 0.90) * 1.45 + df * vec3(0.70, 0.80, 1.0) * 0.40 + vec3(0.06, 0.065, 0.075));
	diffuse += (1.0 - is_cloth) * alb * sss_tint * 0.55 * (smoothstep(-wrap_k, 0.2, ndk) - smoothstep(0.2, 0.9, ndk));
	float rough = mix(mix(0.42, 0.30, M.r) + 0.12 * hair_a + 0.1 * pore, 0.55, is_cloth);
	vec3 hv = normalize(key + v);
	float a2 = rough * rough * rough * rough;
	float nh = max(dot(n, hv), 0.0);
	float dd = nh * nh * (a2 - 1.0) + 1.0;
	float ggx = a2 / (3.14159 * dd * dd);
	float fres = 0.028 + 0.972 * pow(1.0 - max(dot(hv, v), 0.0), 5.0);
	vec3 spec = vec3(ggx * fres * max(ndk, 0.0)) * 0.55 * (1.0 - hair_a * 0.7);
	float rim_t = pow(1.0 - max(dot(n, v), 0.0), 3.0) * clamp(dot(n, rim) + 0.4, 0.0, 1.0);
	vec3 col = diffuse + spec + vec3(0.65, 0.75, 0.9) * rim_t * 0.35;
	col = col / (col + vec3(0.55)) * 1.25;
	ALBEDO = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
}
