shader_type canvas_item;

// Tinte en degradado vertical para el hechizo Encebollado: amarillo arriba, azul al medio y
// rojo abajo, todo mezclado de forma continua (smoothstep) para que no se vea "entrecortado".
//
// Ojo: NO se usa UV para saber la altura del pixel. Las tropas dibujan sus frames con
// AtlasTexture (un recorte de una hoja grande), así que UV no va de 0 a 1 sobre el personaje
// sino sobre la hoja entera — el degradado saldría distinto en cada frame de la animación. En
// su lugar se usa la posición LOCAL del vértice (VERTEX.y), que sí va de -alto/2 a +alto/2
// sobre el sprite dibujado, sin importar de qué parte del atlas venga el recorte.

uniform float intensidad : hint_range(0.0, 1.0) = 0.0;
uniform float altura_px = 200.0;

varying float vy;

void vertex() {
	vy = VERTEX.y;
}

void fragment() {
	vec4 col = texture(TEXTURE, UV);

	float t = clamp(vy / max(altura_px, 1.0) + 0.5, 0.0, 1.0);

	vec3 amarillo = vec3(1.35, 1.15, 0.20);
	vec3 azul     = vec3(0.30, 0.60, 1.50);
	vec3 rojo     = vec3(1.50, 0.30, 0.25);

	// Proporciones pedidas: la franja amarilla es la más GRUESA (ocupa casi la mitad de arriba),
	// el azul queda como una banda mediana en el medio y el rojo como una banda algo más chica
	// abajo. Las transiciones se solapan con smoothstep para que sea una mezcla continua.
	vec3 tinte = mix(amarillo, azul, smoothstep(0.42, 0.72, t));
	tinte = mix(tinte, rojo, smoothstep(0.78, 1.0, t));

	col.rgb *= mix(vec3(1.0), tinte, intensidad);
	COLOR = col;
}
