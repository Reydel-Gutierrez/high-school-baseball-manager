// HSBM ball cap: six panels with seams and stitching, eyelets, front
// monogram (logo texture alpha), gray underbrim. Mirrors web/cap.frag.
// UV.y: 0..1 crown (0 top, 1 band), 2 brim top, 2.5 brim edge, 3 underbrim, -1 button.
shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 cap_color = vec3(0.08, 0.12, 0.35);
uniform vec3 logo_color = vec3(0.9, 0.6, 0.1);
uniform sampler2D logo : filter_linear_mipmap, repeat_disable;
uniform float has_logo = 0.0;

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

void vertex() {
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	w_nor = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec3 n = normalize(w_nor);
	vec3 c0 = vec3(0.0, 0.030, -0.060);
	float part = UV.y;
	if (part < 1.5 && part >= 0.0) {
		if (dot(n, w_pos - c0) < 0.0) { n = -n; }
	} else if (part > 1.9 && part < 2.2) {
		if (n.y < 0.0) { n = -n; }
	} else if (part > 2.8) {
		if (n.y > 0.0) { n = -n; }
	}
	vec3 v = normalize(INV_VIEW_MATRIX[3].xyz - w_pos);
	vec3 alb = to_lin(cap_color);
	float weave = 0.5 + 0.5 * sin(w_pos.x * 4200.0 + w_pos.y * 3100.0) * sin(w_pos.z * 4200.0 - w_pos.y * 2900.0);
	alb *= 0.92 + 0.08 * weave + 0.05 * (vnoise(w_pos.xz * 900.0) - 0.5);
	float ao = 1.0;
	if (part >= 0.0 && part < 1.5) {
		float th = UV.x * 180.0;
		float s = UV.y;
		float seam_deg = abs(fract((th + 30.0) / 60.0) - 0.5) * 60.0;
		float seam = 1.0 - smoothstep(0.25, 1.4, seam_deg * (0.4 + s));
		float stitch = (1.0 - smoothstep(0.2, 0.5, abs(seam_deg * (0.4 + s) - 1.3))) * step(0.5, fract(s * 90.0));
		alb *= 1.0 - seam * 0.45;
		alb = mix(alb, alb * 1.25, stitch * 0.5 * smoothstep(0.08, 0.2, s));
		ao *= 1.0 - seam * 0.25;
		float eyelet = 0.0;
		for (int k = -2; k <= 3; k++) {
			float c = float(k) * 60.0;
			if (k == 0) { continue; }
			float dd = length(vec2((th - c) * 0.012 * s * 6.0, (s - 0.32) * 0.9));
			eyelet = max(eyelet, 1.0 - smoothstep(0.012, 0.018, dd));
		}
		alb = mix(alb, alb * 0.35, eyelet);
		ao *= mix(1.0, 0.82, smoothstep(0.92, 1.0, s));
		vec2 luv = vec2(th / 46.0 + 0.5, (s - 0.36) / 0.40);
		if (has_logo > 0.5 && luv.x > 0.0 && luv.x < 1.0 && luv.y > 0.0 && luv.y < 1.0) {
			float lg = texture(logo, luv).a;
			alb = mix(alb, to_lin(logo_color), lg);
		}
	} else if (part > 2.2 && part < 2.8) {
		alb *= 0.85;
	} else if (part > 2.8) {
		alb = to_lin(vec3(0.20, 0.21, 0.21)) * (0.9 + 0.1 * weave);
	}
	vec3 key_dir = normalize(vec3(0.55, 0.45, 0.75));
	vec3 fill_dir = normalize(vec3(-0.8, 0.05, 0.55));
	vec3 rim_l = normalize(vec3(-0.75, 0.35, -0.6));
	vec3 rim_r = normalize(vec3(0.8, 0.3, -0.55));
	float dk = clamp((dot(n, key_dir) + 0.15) / 1.15, 0.0, 1.0);
	float df = clamp((dot(n, fill_dir) + 0.15) / 1.15, 0.0, 1.0);
	float up = n.y * 0.5 + 0.5;
	vec3 col = alb * (dk * vec3(1.0, 0.95, 0.88) * 3.1 + df * vec3(0.8, 0.88, 1.0) * 0.75 + mix(vec3(0.14, 0.12, 0.11), vec3(0.30, 0.33, 0.38), up)) * ao;
	float edge = pow(1.0 - max(dot(n, v), 0.0), 2.5);
	col += alb * vec3(0.95, 0.97, 1.0) * 1.6 * edge * (max(dot(n, rim_l), 0.0) + max(dot(n, rim_r), 0.0)) * 0.6;
	vec3 h = normalize(key_dir + v);
	col += vec3(pow(max(dot(n, h), 0.0), 24.0)) * 0.06 * dk;
	col = aces(col * 0.95);
	ALBEDO = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
}
