using Godot;

/// <summary>
/// ScrollContainer con scroll TÁCTIL "arrastra en cualquier parte" (como en móvil), en vez de tener
/// que agarrar la barra lateral estilo PC.
///
/// Dos piezas:
///  1) Propaga mouse_filter = Pass a los hijos NO interactivos (labels, paneles, contenedores,
///     imágenes) para que el gesto de arrastre atraviese el contenido y llegue hasta este contenedor.
///     Los controles interactivos (botones, sliders, campos de texto) se dejan como están para que
///     el toque siga funcionando sobre ellos.
///  2) En _GuiInput convierte el arrastre (dedo o mouse emulado) en desplazamiento vertical. No llama
///     a base para el arrastre, así no se duplica con el manejo nativo del ScrollContainer.
///
/// Se usa igual que un ScrollContainer: como tipo de nodo en una escena (adjuntando este script) o
/// creándolo por código (new ScrollTactil()). Recalcula los filtros solo tras _Ready (diferido), así
/// también cubre el contenido agregado dinámicamente en el mismo frame (p. ej. la Tienda).
/// </summary>
public partial class ScrollTactil : ScrollContainer
{
	public override void _Ready()
	{
		// Diferido: corre al final del frame, cuando el contenido ya se agregó (aunque sea por código).
		CallDeferred(nameof(RefrescarFiltros));
	}

	/// <summary>Vuelve a dejar pasar el arrastre por el contenido. Llamar de nuevo si se agrega
	/// contenido nuevo mucho después de crear el contenedor.</summary>
	public void RefrescarFiltros() => Propagar(this);

	private void Propagar(Node n)
	{
		foreach (Node h in n.GetChildren())
		{
			if (h is Control c && c != this
				&& c is not BaseButton && c is not Godot.Range && c is not LineEdit && c is not TextEdit
				&& c.MouseFilter == MouseFilterEnum.Stop)
				c.MouseFilter = MouseFilterEnum.Pass;
			Propagar(h);
		}
	}

	public override void _GuiInput(InputEvent e)
	{
		// Dedo real o mouse emulado como toque (emulate_touch_from_mouse está activo en el proyecto).
		if (e is InputEventScreenDrag d)
		{
			ScrollVertical -= (int)d.Relative.Y;
			AcceptEvent();
			return; // sin base: evita el doble desplazamiento con el scroll nativo
		}
		base._GuiInput(e);
	}
}
