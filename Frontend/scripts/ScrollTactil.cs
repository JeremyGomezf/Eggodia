using Godot;

/// <summary>
/// ScrollContainer con scroll TÁCTIL "arrastra en cualquier parte" (como en móvil), en vez de tener
/// que agarrar la barra lateral estilo PC. Y SIN la barra gris de desplazamiento (el "track"): en el
/// celular estorba, la lista se mueve solo con el dedo.
///
/// Cómo funciona:
///  1) El arrastre se lee en _Input (ANTES que la interfaz): así funciona aunque el dedo empiece sobre
///     un botón o una carta, que se quedarían con el toque. Hasta UMBRAL_PX es un toque normal (el
///     botón/carta responde como siempre); pasado eso es arrastre: la lista sigue al dedo, se avisa a
///     los botones de adentro (NotificationScrollBegin) para que no se activen al soltar, y
///     UltimoGestoFueArrastre queda en true para los controles propios que deciden al soltar
///     (p. ej. CartaMini). Al soltar un arrastre rápido sigue un poco por inercia.
///  2) El arrastre nativo del ScrollContainer se apaga (zona muerta enorme) para que no se sumen los
///     dos. La rueda del mouse sigue funcionando en PC.
///  3) Propaga mouse_filter = Pass a los hijos NO interactivos (labels, paneles, contenedores,
///     imágenes), como antes, para que los toques sobre el contenido lleguen al contenedor.
///
/// Se usa igual que un ScrollContainer: como tipo de nodo en una escena (adjuntando este script) o
/// creándolo por código (new ScrollTactil()). Recalcula los filtros tras _Ready (diferido), así
/// también cubre el contenido agregado dinámicamente en el mismo frame (p. ej. la Tienda).
/// </summary>
public partial class ScrollTactil : ScrollContainer
{
	private const float UMBRAL_PX = 14f;
	private const float FRENADO = 5.5f;      // cuánto frena la inercia (más = frena antes)
	private const float VELOCIDAD_MIN = 25f; // px/s: por debajo, la inercia se corta

	/// <summary>El último toque (en cualquier lista) terminó siendo un arrastre, no un toque.</summary>
	public static bool UltimoGestoFueArrastre { get; private set; }

	private bool _tocando, _arrastrando;
	private int _dedo = -1;
	private Vector2 _inicio, _scrollInicio, _velocidad, _ultimaPos;
	private ulong _ultimoMs;

	public override void _Ready()
	{
		// Sin barra visible (se puede seguir desplazando: dedo, rueda del mouse).
		if (VerticalScrollMode != ScrollMode.Disabled) VerticalScrollMode = ScrollMode.ShowNever;
		if (HorizontalScrollMode != ScrollMode.Disabled) HorizontalScrollMode = ScrollMode.ShowNever;
		ScrollDeadzone = 1_000_000; // apaga el arrastre nativo: lo maneja _Input
		SetProcess(false);          // _Process (inercia) solo corre mientras la lista se desliza sola
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

	// Dedo real o mouse emulado como toque (emulate_touch_from_mouse está activo en el proyecto).
	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree()) { _tocando = false; return; }

		if (e is InputEventScreenTouch toque)
		{
			if (toque.Pressed)
			{
				if (_tocando || !ContienePunto(toque.Position)) return;
				_tocando = true;
				_arrastrando = false;
				UltimoGestoFueArrastre = false;
				_dedo = toque.Index;
				_inicio = _ultimaPos = toque.Position;
				_scrollInicio = new Vector2(ScrollHorizontal, ScrollVertical);
				_velocidad = Vector2.Zero;
				_ultimoMs = Time.GetTicksMsec();
			}
			else if (_tocando && toque.Index == _dedo)
			{
				_tocando = false;
				UltimoGestoFueArrastre = _arrastrando;
				if (Time.GetTicksMsec() - _ultimoMs > 80) _velocidad = Vector2.Zero; // el dedo ya estaba quieto
				SetProcess(_velocidad.LengthSquared() >= VELOCIDAD_MIN * VELOCIDAD_MIN);
			}
			return;
		}

		if (e is InputEventScreenDrag d && _tocando && d.Index == _dedo)
		{
			// Traduce el arrastre al eje (o ejes) que este contenedor tenga habilitado — vertical para
			// la Tienda o el constructor, horizontal para el selector de skins del menú principal.
			Vector2 delta = d.Position - _inicio;
			if (!_arrastrando)
			{
				if (delta.Length() < UMBRAL_PX) return;
				_arrastrando = true;
				UltimoGestoFueArrastre = true;
				// Los botones de adentro que estaban "a medio apretar" se sueltan sin activarse.
				PropagateNotification((int)NotificationScrollBegin);
			}
			if (VerticalScrollMode != ScrollMode.Disabled) ScrollVertical = (int)(_scrollInicio.Y - delta.Y);
			if (HorizontalScrollMode != ScrollMode.Disabled) ScrollHorizontal = (int)(_scrollInicio.X - delta.X);

			ulong ahora = Time.GetTicksMsec();
			float dt = Mathf.Max(1, ahora - _ultimoMs) / 1000f;
			_velocidad = _velocidad.Lerp((d.Position - _ultimaPos) / dt, 0.5f);
			_ultimaPos = d.Position;
			_ultimoMs = ahora;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		// Inercia: al soltar un arrastre rápido la lista sigue un poco y frena sola.
		if (_tocando || _velocidad.LengthSquared() < VELOCIDAD_MIN * VELOCIDAD_MIN) { SetProcess(false); return; }
		float dt = (float)delta;
		if (VerticalScrollMode != ScrollMode.Disabled) ScrollVertical -= (int)(_velocidad.Y * dt);
		if (HorizontalScrollMode != ScrollMode.Disabled) ScrollHorizontal -= (int)(_velocidad.X * dt);
		_velocidad *= Mathf.Max(0f, 1f - FRENADO * dt);
	}

	// Rect de la lista en coordenadas de pantalla (incluye CanvasLayer/escala de los padres).
	private bool ContienePunto(Vector2 posPantalla) =>
		(GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, Size)).HasPoint(posPantalla);
}
