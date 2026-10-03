using Godot;
using System;

public partial class PantallaDerrota : CanvasLayer
{
	public int MonedasGanadas { get; set; }
	public int DañoInfligido  { get; set; }
	public int BajasEnemigas  { get; set; }

	// Mismo tamaño que en Victoria: el chip de monedas se pidió bien grande en las dos pantallas.
	private const int TAM_FUENTE_MONEDAS = 38;

	/// <summary>Lo fija Campo1 al abrir la pantalla: cambia qué hace el botón REINTENTAR.</summary>
	public bool EsOnline { get; set; }
	// En línea: por qué terminó ("Perdiste por inactividad…"). Reemplaza el subtítulo genérico.
	public string MotivoFin { get; set; } = "";

	public string    MvtNombre      { get; set; }
	public int       MvtDaño        { get; set; }
	public Texture2D MvtIlustracion { get; set; }

	public override async void _Ready()
	{
		var btnReintentar = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnReintentar");
		var btnMenu       = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnMenu");

		EstiloUI.AgrandarResumenFinPartida(GetNodeOrNull<Control>("Overlay/CentroVBox/VBox"));
		MostrarRecompensa();
		MostrarStats();
		MostrarMVT();
		if (!string.IsNullOrEmpty(MotivoFin) && GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/Mensaje") is Label lblMotivo)
			lblMotivo.Text = MotivoFin;

		// VS BOT: REINTENTAR vuelve a jugar la misma partida de una. ONLINE: no hay partida que repetir,
		// así que pasa a ser RE-ARMAR MAZO y lleva al constructor.
		if (btnReintentar != null)
		{
			if (EsOnline) btnReintentar.Text = "RE-ARMAR MAZO";
			btnReintentar.Pressed += () =>
			{
				LimpiezaEfectos.LimpiarEfectosDeCampo();
				GetTree().Paused = false;
				if (EsOnline) GetTree().ChangeSceneToFile("res://escenas/menu/MenuConstructor.tscn");
				else          GetTree().ReloadCurrentScene();
			};
		}

		if (btnMenu != null)
			btnMenu.Pressed += () =>
			{
				LimpiezaEfectos.LimpiarEfectosDeCampo();
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
			};

		// Estilo del juego: botones, marcos (paneles) y título (en rojo) con nuestra paleta/fuente.
		EstiloUI.Boton(btnReintentar, accion: true); // boton principal: turquesa, no gris
		EstiloUI.Boton(btnMenu);
		EstiloUI.Texto(GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/Titulo"), 0, EstiloUI.Peligro);
		var pStats = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelStats");
		if (pStats != null) pStats.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());
		var pMvt = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelMVT");
		if (pMvt != null) pMvt.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());

		// Animación de aparición. El huevo roto y todo el contenido son HIJOS del Overlay, así que este
		// modulate los arrastra: si el fundido no terminaba (p. ej. al rendirse con el árbol pausado, o
		// si la pantalla se cerraba antes), el huevo quedaba a medias y se veía el campo por detrás —
		// ese era el "huevo transparente". Ahora la capa procesa SIEMPRE (aunque el juego esté en pausa)
		// y, al terminar, se fuerza el valor final por si el tween se interrumpió.
		ProcessMode = Node.ProcessModeEnum.Always;
		var overlay = GetNode<ColorRect>("Overlay");
		overlay.Modulate = new Color(1, 1, 1, 0);
		Tween tw = CreateTween();
		tw.TweenProperty(overlay, "modulate:a", 1.0f, 1.5f);
		tw.Finished += () => { if (IsInstanceValid(overlay)) overlay.Modulate = Colors.White; };

		CallDeferred(nameof(AjustarParticulasAnchoPantalla));
		CallDeferred(nameof(AjustarEscalaParaQueEntre));
		// Y se reevalúa cada vez que el contenido cambia de tamaño (al aparecer el MVT o el chip de
		// monedas), porque el primer cálculo puede correr antes de que el contenedor los acomode.
		var vboxAjuste = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		if (vboxAjuste != null) vboxAjuste.Resized += AjustarEscalaParaQueEntre;

		// Y se recalcula con el layout YA ASENTADO. El panel de MVT y el chip de monedas se muestran
		// por código, y el contenedor recién los acomoda en los frames siguientes: si el cálculo corría
		// antes, medía un contenido más corto del real, decidía que "ya entra" y por eso el último
		// botón terminaba fuera de pantalla justo en las partidas donde aparece el MVT.
		for (int i = 0; i < 3 && IsInstanceValid(this); i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this)) return;
			AjustarEscalaParaQueEntre();
		}
	}

	// Margen que se deja arriba y abajo para que nunca quede nada pegado al borde (el botón de abajo
	// se veía muy pegado al borde en el celular).
	private const float MARGEN_VERTICAL_PANTALLA = 48f;

	/// <summary>Achica el bloque entero (huevo + textos + paneles + botones) lo justo para que entre en
	/// la pantalla. Hace falta porque el alto del contenido NO es fijo: cuando hubo daño rival aparece
	/// el panel de MVT y el bloque pasa de ~1037 px a ~1186 px, más que los 1080 de alto útil. Sin esto,
	/// al estar centrado el sobrante se repartía arriba y abajo: el título se metía dentro del huevo y
	/// el botón de abajo quedaba cortado. Al ser un cálculo sobre el tamaño REAL medido tras el layout,
	/// funciona igual con o sin MVT y en cualquier pantalla.</summary>
	private void AjustarEscalaParaQueEntre()
	{
		var vbox  = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		var huevo = GetNodeOrNull<TextureRect>("Overlay/HuevoRoto");
		if (vbox == null || !IsInstanceValid(vbox) || huevo == null) return;

		// GetCombinedMinimumSize y NO Size: es el alto que el contenido REALMENTE necesita, calculado a
		// partir de los hijos. Size todavía puede venir desactualizado cuando esto corre, porque el chip
		// de monedas y el panel de MVT se agregan/muestran por código y el contenedor los acomoda
		// después — por eso el ajuste no se aplicaba y seguía cortándose arriba y abajo.
		float alto = Mathf.Max(vbox.GetCombinedMinimumSize().Y, vbox.Size.Y);
		if (alto <= 0f) return;

		// Las dos variantes (con y sin MVT) se escalan por el MISMO número, para que la pantalla se vea
		// del mismo tamaño en los dos casos. Si cada una usara su propio alto, la que tiene MVT saldría
		// más chica —que es justo lo que se notaba—. Se toma siempre como referencia la más alta.
		var panelMvt = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/PanelMVT");
		var mensaje  = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/Mensaje");
		if (panelMvt != null && mensaje != null && !panelMvt.Visible)
			alto += Mathf.Max(0f, panelMvt.GetCombinedMinimumSize().Y - mensaje.GetCombinedMinimumSize().Y);

		// Espacio REAL que queda por debajo del huevo (el huevo no está en el flujo justamente para que
		// su tamaño no empuje los botones). Se mide contra dónde termina el huevo, no contra la pantalla
		// entera: si no, el cálculo da de más y el último botón vuelve a quedar fuera.
		float pantalla   = GetViewport().GetVisibleRect().Size.Y;
		float tope       = BaseVisibleDelHuevo(huevo) + SEPARACION_HUEVO_TITULO;
		float disponible = pantalla - tope - MARGEN_VERTICAL_PANTALLA;
		if (disponible <= 0f) return;

		_escalaAjuste = alto > disponible ? disponible / alto : 1f;
		vbox.PivotOffset = new Vector2(vbox.Size.X / 2f, alto / 2f);
		vbox.Scale = new Vector2(_escalaAjuste, _escalaAjuste);

		// Con el alto YA escalado se coloca la banda, para que el título quede pegado bajo el huevo.
		AcomodarBandaDebajoDelHuevo(alto * _escalaAjuste);
	}

	private float _escalaAjuste = 1f;

	private const float SEPARACION_HUEVO_TITULO = 12f;

	/// <summary>Hace que el contenido arranque justo DEBAJO del huevo, sea cual sea la posición y el
	/// tamaño que tenga el huevo en la escena. Así se puede mover o redimensionar el huevo desde el
	/// editor sin tocar código y sin que el último botón se vaya de pantalla: la banda del contenido
	/// se recalcula sola y el ajuste de escala trabaja sobre ese alto real.</summary>
	/// <summary>Y donde termina el DIBUJO del huevo, no su caja. El nodo es cuadrado (377×377) pero la
	/// imagen es 396×279 y se dibuja con "mantener proporción centrado": queda centrada dentro de la
	/// caja y sobran ~56 px vacíos abajo. Midiendo la caja, el texto arrancaba después de ese vacío y
	/// por eso todo se veía empujado hacia abajo.</summary>
	private static float BaseVisibleDelHuevo(TextureRect huevo)
	{
		float abajoCaja = huevo.Position.Y + huevo.Size.Y;
		var tex = huevo.Texture;
		if (tex == null || huevo.Size.X <= 0f || huevo.Size.Y <= 0f) return abajoCaja;

		Vector2 t = tex.GetSize();
		if (t.X <= 0f || t.Y <= 0f) return abajoCaja;

		float escala = Mathf.Min(huevo.Size.X / t.X, huevo.Size.Y / t.Y); // KEEP_ASPECT_CENTERED
		float altoDibujo = t.Y * escala;
		return huevo.Position.Y + (huevo.Size.Y + altoDibujo) / 2f;
	}

	private void AcomodarBandaDebajoDelHuevo(float altoContenidoReal)
	{
		var banda = GetNodeOrNull<Control>("Overlay/CentroVBox");
		var huevo = GetNodeOrNull<TextureRect>("Overlay/HuevoRoto");
		if (banda == null || huevo == null || !IsInstanceValid(banda) || !IsInstanceValid(huevo)) return;

		float pantalla = GetViewport().GetVisibleRect().Size.Y;
		float tope = BaseVisibleDelHuevo(huevo) + SEPARACION_HUEVO_TITULO; // dónde debe empezar el título

		// CentroVBox es un CenterContainer: CENTRA el contenido en su banda, así que si la banda es más
		// alta que el contenido reparte el sobrante arriba y abajo — eso era el hueco entre el huevo y
		// "HAS PERDIDO". Despejando  tope = bandaTop + (alto_banda - alto_contenido)/2  queda esto, que
		// deja el título exactamente a SEPARACION_HUEVO_TITULO del huevo, sin aire de más.
		banda.OffsetTop = 2f * tope - pantalla + altoContenidoReal;
	}

	// La lluvia usaba ancho fijo (±700 px) centrada en x=640: en pantallas anchas ("keep_height")
	// solo cubría parte. Se recalcula al ancho REAL visible para que caiga en toda la pantalla.
	private void AjustarParticulasAnchoPantalla()
	{
		var p = GetNodeOrNull<CpuParticles2D>("Overlay/Lluvia");
		if (p == null) return;
		var overlay = GetNodeOrNull<Control>("Overlay");
		float w = (overlay != null && overlay.Size.X > 100f) ? overlay.Size.X : GetViewport().GetVisibleRect().Size.X;
		if (w < 100f) w = 1080f;
		p.Position = new Vector2(w / 2f, p.Position.Y);
		p.EmissionRectExtents = new Vector2(w / 2f + 140f, p.EmissionRectExtents.Y);
		p.Amount = Mathf.Max(p.Amount, (int)(w / 8f));
	}

	private void MostrarStats()
	{
		var lblD = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblDañoV");
		var lblE = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblElimV");
		if (lblD != null) lblD.Text = DañoInfligido.ToString();
		if (lblE != null) lblE.Text = BajasEnemigas.ToString();
	}

	private void MostrarMVT()
	{
		if (string.IsNullOrEmpty(MvtNombre)) return;
		var panel = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/PanelMVT");
		if (panel == null) return;
		panel.Visible = true;

		// Con MVT el bloque tenía un elemento más y había que achicarlo todo para que entrara, así que
		// se veía más chico que sin MVT. En vez de eso se saca el mensaje de "Modifica tu estrategia":
		// el chip de monedas sube a su lugar y el MVT ocupa el del chip. Queda el mismo alto que sin
		// MVT, o sea que las dos pantallas se ven del MISMO tamaño y no hace falta encoger nada.
		var mensaje = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/Mensaje");
		if (mensaje != null) mensaje.Visible = false;

		var lblNombre = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTInfo/MVTNombre");
		var lblStat   = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTInfo/MVTStat");
		var foto      = GetNodeOrNull<TextureRect>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTFoto");
		if (lblNombre != null) lblNombre.Text = MvtNombre;
		if (lblStat   != null) lblStat.Text   = $"{MvtDaño} de daño causado";
		if (foto != null && MvtIlustracion != null) foto.Texture = MvtIlustracion;
	}

	private void MostrarRecompensa()
	{
		if (MonedasGanadas <= 0) return;
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		var mensaje = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/Mensaje");
		if (vbox == null) return;

		var chip = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.12f, 0.10f, 0.03f, 0.9f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.85f, 0.72f, 0.3f, 0.7f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 34;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 16;
		chip.AddThemeStyleboxOverride("panel", sb);
		chip.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		var l = new Label();
		l.Text = $"+ {MonedasGanadas} monedas";
		l.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.45f));
		l.AddThemeFontSizeOverride("font_size", TAM_FUENTE_MONEDAS);
		if (EstiloUI.Fuente != null) l.AddThemeFontOverride("font", EstiloUI.Fuente);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		chip.AddChild(l);

		int idx = mensaje != null ? mensaje.GetIndex() + 1 : 1;
		vbox.AddChild(chip);
		vbox.MoveChild(chip, idx);
	}
}
