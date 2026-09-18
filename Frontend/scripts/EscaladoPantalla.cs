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
	// Por debajo de este umbral se considera "pantalla no alargada" (PC/tablet) y se activa el
	// letterbox; por encima (celulares largos tipo 20:9) se deja el comportamiento original.
	private const float UMBRAL_ASPECTO_LARGO = 1.9f;

	// Caja objetivo 20:9 real para el letterbox — misma proporción que window_width/height_override
	// (1200x540) en project.godot, pero al doble de resolución para que no se vea borroso.
	private static readonly Vector2I CAJA_20_9 = new Vector2I(2400, 1080);

	public override void _Ready()
	{
		// OJO: antes usaba DisplayServer.ScreenGetSize(), que devuelve la resolución del MONITOR
		// físico, no la de la ventana real del juego. En un celular ambas coinciden (la ventana es
		// pantalla completa), pero al probar en PC con "Sobrescribir" (Project Settings > Display >
		// Window > Size) o con la ventana en un tamaño distinto al del monitor, ScreenGetSize()
		// seguía viendo el monitor (normalmente 16:9) e ignoraba por completo la forma real de la
		// ventana — activaba o dejaba de activar el letterbox según el monitor, no según lo que el
		// juego en verdad estaba dibujando, y por eso Campo1 (que sí tiene una Camera2D pensada para
		// una forma de canvas específica) se veía desalineado/ovalado con cualquier Sobrescribir.
		// WindowGetSize() sí refleja el tamaño real de la ventana (el Sobrescribir en editor/PC, o
		// la pantalla completa en un celular real), que es lo que esta lógica necesita mirar.
		Vector2I tam = DisplayServer.WindowGetSize();
		float mayor = Mathf.Max(tam.X, tam.Y);
		float menor = Mathf.Min(tam.X, tam.Y);
		if (menor <= 0f) return;
		float aspecto = mayor / menor;

		if (aspecto < UMBRAL_ASPECTO_LARGO)
		{
			GetTree().Root.ContentScaleSize   = CAJA_20_9;
			GetTree().Root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
			GD.Print($"[EscaladoPantalla] Pantalla {tam.X}x{tam.Y} (aspecto {aspecto:0.00}) → letterbox 20:9 activado.");
		}
		else
		{
			GD.Print($"[EscaladoPantalla] Pantalla {tam.X}x{tam.Y} (aspecto {aspecto:0.00}) → sin cambios (celular largo).");
		}
	}
}
