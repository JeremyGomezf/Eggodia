using Godot;

/// <summary>Guía de primera vez de la Tienda (ver GuiaPasos): monedas, cada sección y cómo volver.
/// La lista se desplaza sola hasta la sección que se está explicando.</summary>
public partial class Tienda : Control
{
	// Orden de las secciones tal como las arma LlenarContenido: cada una es título + separador + grilla.
	private const int SECCION_SKINS = 0, SECCION_TRONOS = 1, SECCION_TROPAS = 2, SECCION_ARDIDES = 3;

	private void ProgramarGuia()
	{
		if (Preferencias.GuiaVista("tienda")) return;
		GetTree().CreateTimer(0.5, false).Timeout += () =>
		{
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
			GuiaPasos.Iniciar(this, "tienda", new[]
			{
				new GuiaPasos.Paso("Estas son tus monedas. Las ganas jugando partidas contra el bot y " +
					"en línea.", () => GuiaPasos.RectDe(_chipMonedas, 8f)),
				new GuiaPasos.Paso("SKINS DE HUEVO: cambia el aspecto de tu huevo. Toca el precio para " +
					"comprarla (se equipa sola) o EQUIPAR si ya la tienes.",
					() => RectSeccion(SECCION_SKINS), () => DesplazarASeccion(SECCION_SKINS)),
				new GuiaPasos.Paso("TRONOS: el pedestal de tu huevo en la batalla. Se compran y se " +
					"equipan igual que las skins.",
					() => RectSeccion(SECCION_TRONOS), () => DesplazarASeccion(SECCION_TRONOS)),
				new GuiaPasos.Paso("TROPAS: desbloquea personajes nuevos. Los que compres aparecen en " +
					"CARTAS para sumarlos a tu mazo.",
					() => RectSeccion(SECCION_TROPAS), () => DesplazarASeccion(SECCION_TROPAS)),
				new GuiaPasos.Paso("ARDIDES Y HECHIZOS: cartas de apoyo para la batalla. También se " +
					"suman a tu mazo desde CARTAS.",
					() => RectSeccion(SECCION_ARDIDES), () => DesplazarASeccion(SECCION_ARDIDES)),
				new GuiaPasos.Paso("VOLVER te lleva de nuevo al menú principal.",
					() => GuiaPasos.RectDe(_btnVolver, 8f), () => DesplazarASeccion(SECCION_SKINS)),
			});
		};
	}

	private ScrollContainer ScrollTienda() => _contenidoScroll?.GetParent() as ScrollContainer;

	private Control TituloSeccion(int seccion)
	{
		if (_contenidoScroll == null || !IsInstanceValid(_contenidoScroll)) return null;
		int i = seccion * 3;
		return i < _contenidoScroll.GetChildCount() ? _contenidoScroll.GetChild(i) as Control : null;
	}

	private void DesplazarASeccion(int seccion)
	{
		var scroll = ScrollTienda();
		var titulo = TituloSeccion(seccion);
		if (scroll == null || titulo == null) return;
		scroll.ScrollVertical = Mathf.Max(0, (int)titulo.Position.Y - 6);
	}

	/// <summary>Título + grilla de la sección, recortado a la parte que se ve de la lista.</summary>
	private Rect2? RectSeccion(int seccion)
	{
		var titulo = TituloSeccion(seccion);
		int iGrilla = seccion * 3 + 2;
		Node grilla = (_contenidoScroll != null && iGrilla < _contenidoScroll.GetChildCount())
			? _contenidoScroll.GetChild(iGrilla) : null;
		Rect2? r = GuiaPasos.RectDeVarios(10f, titulo, grilla);
		Rect2? visible = GuiaPasos.RectDe(ScrollTienda(), 12f);
		if (!r.HasValue || !visible.HasValue) return r;
		Rect2 recorte = r.Value.Intersection(visible.Value);
		return recorte.HasArea() ? recorte : r;
	}
}
