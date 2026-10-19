// HSBM eyelash cards: dense tapered upper lashes (lower lashes hidden).
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;

uniform vec3 lash_color = vec3(0.025, 0.018, 0.014);
varying vec3 w_pos;

float hash(float p) { return fract(sin(p * 127.1) * 43758.5453); }

void vertex() {
	w_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float raw_v = 1.0 - UV.y;
	if (w_pos.y < -0.003) {
		discard;
	}
	float along = clamp((raw_v - 0.927) / 0.058, 0.0, 1.0);
	float x = UV.x * 1500.0;
	float i = floor(x);
	float s = fract(x) - 0.5 + (hash(i) - 0.5) * 0.5 + along * (hash(i + 7.0) - 0.5) * 0.8;
	float width = mix(0.30, 0.06, along);
	float a = 1.0 - smoothstep(width * 0.5, width, abs(s));
	float len = 0.40 + 0.35 * hash(i + 3.0);
	a *= 1.0 - smoothstep(len - 0.15, len, along);
	ALBEDO = pow(lash_color, vec3(1.0 / 2.2));
	// soft lash line: strands only near the root, fading quickly
	a = mix(a, 1.0, 0.35) * pow(1.0 - along, 2.0);
	ALPHA = a * 0.65;
}
