shader_type canvas_item;

void fragment() {
	vec2 centered = UV * 2.0 - 1.0;
	float dist = length(centered);
	vec4 col = texture(TEXTURE, UV);
	col.a *= 1.0 - smoothstep(0.92, 1.0, dist);
	COLOR = col;
}
