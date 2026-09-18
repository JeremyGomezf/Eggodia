using Godot;

/// <summary>Ajusta el escalado de pantalla en TIEMPO DE EJECUCIÓN según el dispositivo, sin tocar
/// las opciones de Window/Aspect de project.godot (esas quedan fijas: viewport 1920x1080,
/// stretch canvas_items + keep_height, tal como ya están afinadas para que la Camera2D y la
/// interfaz de Campo1 no se desajusten).
///
/// El área visible se fija en una caja 20:9 (2400x1080) IGUAL para todos. Antes esto solo se
/// aplicaba a pantallas "no alargadas" y las demás quedaban con el "keep_height" crudo, donde el
/// ancho visible depende de la forma de TU ventana (1080 × proporción): dos personas con el mismo
/// código pero ventanas de distinta proporción veían porciones distintas del tablero — a uno le
/// entraba completo y a otro le quedaba recortado por los costados. Con la caja fija, todos ven
/// exactamente lo mismo y lo que sobra se rellena con barras negras; en un celular 20:9 la caja
/// coincide exacto y ocupa la pantalla completa, sin barras.
/// </summary>
public partial class EscaladoPantalla : Node
{
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

		// La caja 20:9 se aplica SIEMPRE, en toda pantalla y en todo dispositivo. Antes solo se
		// aplicaba a pantallas "no alargadas": las alargadas quedaban con el "keep_height" crudo,
		// donde lo que se ve de ancho es 1080 × (proporción de TU ventana) — o sea, cada máquina
		// veía una porción distinta del tablero y por eso a uno le salía completo y a otro
		// recortado, aun teniendo exactamente el mismo código. Fijando la caja para todos, el
		// área visible es idéntica en cualquier pantalla y lo que sobra se rellena con barras
		// negras (en un celular 20:9 no sobra nada: coincide exacto y llena la pantalla).
		GetTree().Root.ContentScaleSize   = CAJA_20_9;
		GetTree().Root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
		GD.Print($"[EscaladoPantalla] Ventana {tam.X}x{tam.Y} (aspecto {aspecto:0.00}) → caja fija 20:9 (2400x1080) para que se vea igual en todos lados.");
	}
}
