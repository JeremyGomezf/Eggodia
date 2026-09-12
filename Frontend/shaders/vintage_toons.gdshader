shader_type canvas_item;

// Desaturación + tinte sepia + viñeta suave — look "cartoon 1930s". "intensidad" en 0 = pantalla
// normal a color; en 1 = blanco y negro/sepia casi total. Se controla desde C# (ver
// EfectoVintageToons.cs) para poder usarlo tanto fuerte (MenuConstructor) como muy sutil (Campo1).
uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;
uniform float intensidad : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 color = texture(screen_tex, SCREEN_UV);
	float gris = dot(color.rgb, vec3(0.299, 0.587, 0.114));
	vec3 sepia = vec3(gris * 1.08, gris * 0.95, gris * 0.78);

	vec2 centrado = SCREEN_UV - vec2(0.5);
	float vineta = smoothstep(0.85, 0.35, length(centrado));
	sepia *= mix(1.0, vineta * 0.5 + 0.5, 0.5);

	vec3 final_color = mix(color.rgb, sepia, intensidad);
	COLOR = vec4(final_color, color.a);
}
