shader_type canvas_item;

// Glitch corto (desplazamiento de franjas horizontales + separación de canales RGB) usado solo
// sobre el Sprite2D "FONDO" del mapa Digital cada vez que aparece FONDO2 — ver
// Campo1.SecuenciaDigital.cs. "strength" va de 0 (sin efecto, se ve la imagen normal) a 1 (glitch
// al máximo); se anima con un Tween desde el código, no acá.
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform float time_seed = 0.0;

float rand(float n) { return fract(sin(n) * 43758.5453123); }

void fragment()
{
	vec2 uv = UV;

	// Franjas horizontales: cada "banda" de alto pseudoaleatorio se corre un poco en X.
	float bandas = 24.0;
	float banda_idx = floor(uv.y * bandas);
	float ruido_banda = rand(banda_idx + time_seed);
	float desplazamiento = (ruido_banda - 0.5) * 0.06 * strength;
	// Solo una fracción de las franjas se desplaza cada instante, para que parezca un glitch
	// intermitente y no un shake constante parejo.
	if (ruido_banda > 0.72) uv.x += desplazamiento;

	// Separación de canales RGB (chromatic aberration), más notoria mientras más fuerte el glitch.
	float aberracion = 0.008 * strength;
	float r = texture(TEXTURE, uv + vec2(aberracion, 0.0)).r;
	float g = texture(TEXTURE, uv).g;
	float b = texture(TEXTURE, uv - vec2(aberracion, 0.0)).b;
	float a = texture(TEXTURE, uv).a;

	COLOR = vec4(r, g, b, a);
}
