using Godot;

/// <summary>Ajusta el escalado de pantalla en TIEMPO DE EJECUCIÓN según el dispositivo, sin tocar
/// las opciones de Window/Aspect de project.godot (esas quedan fijas: viewport 1920x1080,
/// stretch canvas_items + keep_height, tal como ya están afinadas para que la Camera2D y la
/// interfaz de Campo1 no se desajusten).
///
/// - Pantallas largas y angostas (celulares tipo 20:9, relación ≈1.9-2.4): se dejan TAL CUAL —
///   "keep_height" ya las hace ocupar el 100% de la pantalla y se ven bien en cualquier celular.
/// - Pantallas más "cuadradas" (PC ~16:9=1.78, tablets ~4:3=1.33 o 16:10=1.6): se les activa
///   letterbox (barras negras a los lados), pero usando como referencia el 20:9 REAL (2400x1080,
///   el mismo que window_width/height_override ya usan en project.godot) en vez del 1920x1080
///   declarado — que es 16:9, no 20:9. Usar el 16:9 como caja objetivo era el bug: en vez de
///   barras negras limpias, el juego se veía recortado/con zoom porque la caja tenía la forma
///   equivocada. Con la caja 20:9 correcta, en PC/tablet se ve "como un celular metido en el
///   medio de la pantalla", que es justo lo pedido.
/// </summary>
public partial class EscaladoPantalla : Node
{
	// Base de diseño (la misma del viewport del proyecto). Con "Expand" y esta base, cualquier
	// pantalla ANCHA (≥16:9: TODOS los celulares y los monitores 16:9) se comporta EXACTAMENTE igual
	// que el "keep_height" del proyecto — o sea, los celulares no cambian nada.
	private static readonly Vector2I BASE_DISENO = new Vector2I(1920, 1080);

	public override void _Ready()
	{
		// LLENAR SIEMPRE la pantalla, sin barras "modo cine", manteniendo la proporción de los
		// elementos. "Expand" nunca distorsiona ni recorta la interfaz/botonera: en pantallas más
		// cuadradas que 16:9 (PC, app de escritorio, tablets) simplemente muestra un poco más de área
		// en lugar de poner barras negras. Reemplaza el letterbox anterior, que hacía que el combate
		// (VS BOT y online) se viera "modo cine" en esas pantallas.
		//
		// Importante: para toda pantalla ≥16:9 (celulares 19.5:9/20:9, monitores 16:9) esto da el mismo
		// resultado que el keep_height del proyecto → las posiciones, la botonera y la cámara del
		// combate quedan idénticas; solo cambia (a mejor) en pantallas más cuadradas.
		GetTree().Root.ContentScaleSize   = BASE_DISENO;
		GetTree().Root.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;

		Vector2I tam = DisplayServer.ScreenGetSize();
		GD.Print($"[EscaladoPantalla] Pantalla {tam.X}x{tam.Y} → Expand (llenar sin barras, sin distorsionar).");
	}
}
