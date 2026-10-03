using Godot;

public partial class PantallaVictoria : CanvasLayer
{
	public int DañoInfligido    { get; set; }
	public int TropasEliminadas { get; set; }
	public int TurnosJugados    { get; set; }
	public int Racha            { get; set; }
	public int MonedasGanadas   { get; set; }

	// Chip de "+N monedas": se pidió bien grande, que se lea de una (mismo tamaño en Victoria y Derrota).
	private const int TAM_FUENTE_MONEDAS = 38;

	/// <summary>Lo fija Campo1 al abrir la pantalla: cambia qué hace el botón de "jugar de nuevo".</summary>
	public bool EsOnline { get; set; }
	// En línea: por qué terminó ("El rival se desconectó…"). Reemplaza el subtítulo genérico.
	public string MotivoFin { get; set; } = "";

	/// <summary>Victoria del TUTORIAL: sin monedas, sin auto-achicado, y el botón principal repite
	/// el tutorial en vez de empezar una partida normal.</summary>
	public bool EsTutorial { get; set; }

	public string    MvtNombre      { get; set; }
	public int       MvtDaño        { get; set; }
	public Texture2D MvtIlustracion { get; set; }

	// Premio por ganarle en línea a una cuenta dev: la respuesta del servidor puede llegar antes o
	// después de abrir esta pantalla; en los dos casos se muestra encima, tras la animación de entrada.
	private Economia _ecoSkin;

	private void MostrarSkinGanadaSiHay()
	{
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		if (Economia.Instancia()?.TomarSkinGanadaPendiente() is (string nombre, string ruta))
			PopupSkinDesbloqueada.Mostrar(this, nombre, ruta, subtitulo: "¡Le ganaste a un desarrollador!");
	}

	public override void _ExitTree()
	{
		if (_ecoSkin != null && IsInstanceValid(_ecoSkin)) _ecoSkin.SkinGanadaRecibida -= MostrarSkinGanadaSiHay;
	}

	public override void _Ready()
	{
		_ecoSkin = Economia.Instancia();
		if (_ecoSkin != null) _ecoSkin.SkinGanadaRecibida += MostrarSkinGanadaSiHay;
		GetTree().CreateTimer(1.6).Timeout += MostrarSkinGanadaSiHay;

		var lblD  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblDañoV");
		var lblE  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblElimV");
		var lblT  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblTurnosV");
		var lblR  = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelStats/StatsGrid/LblRachaV");
		if (lblD != null) lblD.Text = DañoInfligido.ToString();
		if (lblE != null) lblE.Text = TropasEliminadas.ToString();
		if (lblT != null) lblT.Text = TurnosJugados.ToString();
		if (lblR != null) lblR.Text = Racha > 1 ? $"{Racha} victorias seguidas" : $"{Racha}";

		// 4 columnas: las 4 estadísticas quedan de a dos por fila (más letra sin sumar altura).
		EstiloUI.AgrandarResumenFinPartida(GetNodeOrNull<Control>("Overlay/CentroVBox/VBox"), columnasStats: 4);
		MostrarMVT();
		if (!string.IsNullOrEmpty(MotivoFin) && GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/Subtitulo") is Label lblMotivo)
			lblMotivo.Text = MotivoFin;

		var btnJugar = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnJugarDeNuevo");
		var btnMenu  = GetNodeOrNull<Button>("Overlay/CentroVBox/VBox/BtnMenu");
		// VS BOT: repite la partida al toque. ONLINE: no se puede "repetir" (hace falta otro rival), así
		// que el botón pasa a ser RE-ARMAR MAZO y lleva al constructor.
		if (btnJugar != null)
		{
			if (EsOnline)        btnJugar.Text = "RE-ARMAR MAZO";
			else if (EsTutorial) btnJugar.Text = "REPETIR TUTORIAL"; // recarga la escena del tutorial
			btnJugar.Pressed += () =>
			{
				LimpiezaEfectos.LimpiarEfectosDeCampo();
				GetTree().Paused = false;
				if (EsOnline) GetTree().ChangeSceneToFile("res://escenas/menu/MenuConstructor.tscn");
				else          GetTree().ReloadCurrentScene();
			};
		}
		if (btnMenu  != null) btnMenu.Pressed  += () => { LimpiezaEfectos.LimpiarEfectosDeCampo(); GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn"); };

		// Estilo del juego: botones, marcos (paneles) y título con nuestra paleta/fuente.
		EstiloUI.Boton(btnJugar, accion: true); // boton principal: turquesa, no gris
		EstiloUI.Boton(btnMenu);
		EstiloUI.Titulo(GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/Titulo"), 0);
		var pStats = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelStats");
		if (pStats != null) pStats.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());
		var pMvt = GetNodeOrNull<PanelContainer>("Overlay/CentroVBox/VBox/PanelMVT");
		if (pMvt != null) pMvt.AddThemeStyleboxOverride("panel", EstiloUI.CuadroDorado());

		MostrarRecompensa();
		// Diferido: necesita el Size REAL ya calculado por el layout para saber si el bloque entra.
		CallDeferred(nameof(AnimarEntrada));
		// Y además se REVISA cada vez que el bloque cambia de tamaño (igual que hace la pantalla de
		// Derrota). Sin esto, si la primera medición salía inflada, la escala chica se quedaba fija
		// para siempre — era por lo que la victoria se veía en miniatura.
		var vboxReajuste = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		if (vboxReajuste != null) vboxReajuste.Resized += ReajustarEscalaBloque;
		CallDeferred(nameof(AjustarParticulasAnchoPantalla));
	}

	// El confeti/lluvia usaba un ancho fijo (±700 px) centrado en x=640, así que en pantallas anchas
	// (el juego usa "keep_height", el ancho visible cambia por dispositivo) solo cubría una parte.
	// Aquí se recalcula al ancho REAL visible para que caiga en toda la pantalla, en cualquier celular.
	private void AjustarParticulasAnchoPantalla()
	{
		var p = GetNodeOrNull<CpuParticles2D>("Overlay/Confetti");
		if (p == null) return;
		var overlay = GetNodeOrNull<Control>("Overlay");
		float w = (overlay != null && overlay.Size.X > 100f) ? overlay.Size.X : GetViewport().GetVisibleRect().Size.X;
		if (w < 100f) w = 1080f;
		p.Position = new Vector2(w / 2f, p.Position.Y);
		p.EmissionRectExtents = new Vector2(w / 2f + 140f, p.EmissionRectExtents.Y);
		p.Amount = Mathf.Max(p.Amount, (int)(w / 10f)); // densidad acorde al ancho
	}

	private void MostrarRecompensa()
	{
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		var panelStats = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/PanelStats");
		if (vbox == null || MonedasGanadas <= 0) return;

		var chip = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.12f, 0.10f, 0.03f, 0.9f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.95f, 0.78f, 0.25f, 0.85f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 34;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 16;
		chip.AddThemeStyleboxOverride("panel", sb);
		chip.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		var l = new Label();
		l.Text = $"+ {MonedasGanadas} monedas";
		l.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.4f));
		l.AddThemeFontSizeOverride("font_size", TAM_FUENTE_MONEDAS);
		if (EstiloUI.Fuente != null) l.AddThemeFontOverride("font", EstiloUI.Fuente);
		l.HorizontalAlignment = HorizontalAlignment.Center;
		chip.AddChild(l);

		int idx = panelStats != null ? panelStats.GetIndex() + 1 : 1;
		vbox.AddChild(chip);
		vbox.MoveChild(chip, idx);
	}

	private void MostrarMVT()
	{
		if (string.IsNullOrEmpty(MvtNombre)) return;
		var panel = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox/PanelMVT");
		if (panel == null) return;
		panel.Visible = true;

		var lblNombre = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTInfo/MVTNombre");
		var lblStat   = GetNodeOrNull<Label>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTInfo/MVTStat");
		var foto      = GetNodeOrNull<TextureRect>("Overlay/CentroVBox/VBox/PanelMVT/MVTBox/MVTFoto");
		if (lblNombre != null) lblNombre.Text = MvtNombre;
		if (lblStat   != null) lblStat.Text   = $"{MvtDaño} de daño causado";
		if (foto != null && MvtIlustracion != null) foto.Texture = MvtIlustracion;
	}

	// Margen que se deja arriba y abajo para que nada quede pegado al borde.
	private const float MARGEN_VERTICAL_PANTALLA = 30f;

	/// <summary>Escala a la que el bloque entra completo en pantalla (1 si ya entra). El alto NO es
	/// fijo: con el panel de MVT y la racha el contenido crece, y si se pasa del alto útil, al estar
	/// centrado el sobrante se reparte y se corta arriba y abajo. Se mide el tamaño REAL tras el
	/// layout, así vale con o sin MVT y en cualquier pantalla.</summary>
	// Por debajo de esto no se achica nunca: si una medición sale rara (p. ej. el bloque todavía sin
	// layout, o una ilustración de MVT que no cargó y reserva de más), antes la pantalla entera
	// terminaba diminuta. Es preferible recortar un pelo que ver la victoria en miniatura.
	private const float ESCALA_MINIMA_BLOQUE = 0.82f;

	// Se dispara cuando el bloque cambia de tamaño (terminó de acomodarse el texto/los paneles):
	// recalcula la escala con la medida buena. No toca nada mientras la animación de entrada sigue
	// corriendo, para no pelearse con su tween.
	private bool _entradaVictoriaEnCurso = true;

	private void ReajustarEscalaBloque()
	{
		if (_entradaVictoriaEnCurso) return;
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		if (vbox == null || !IsInstanceValid(vbox)) return;
		float e = EscalaParaQueEntre(vbox);
		vbox.PivotOffset = vbox.Size / 2;
		vbox.Scale = new Vector2(e, e);
	}

	private float EscalaParaQueEntre(Control vbox)
	{
		// Tutorial: SIN auto-achicado. Solo pasaba ahí (en la partida normal se ve bien), y como no
		// hay forma de reproducirlo leyendo el código, se corta por lo sano: en el tutorial la
		// pantalla se muestra siempre a tamaño natural, igual que la de campo_1.
		if (EsTutorial) return 1f;

		float disponible = GetViewport().GetVisibleRect().Size.Y - MARGEN_VERTICAL_PANTALLA * 2f;
		float alto = Mathf.Max(vbox.GetCombinedMinimumSize().Y, vbox.Size.Y);
		if (alto <= 0f || disponible <= 0f || alto <= disponible) return 1f;
		return Mathf.Max(disponible / alto, ESCALA_MINIMA_BLOQUE);
	}

	private async void AnimarEntrada()
	{
		var vbox = GetNodeOrNull<Control>("Overlay/CentroVBox/VBox");
		if (vbox == null) return;
		// Misma red de seguridad que en Derrota: la capa procesa aunque el juego esté pausado y el
		// estado final se fuerza al terminar, para que nunca quede contenido a medio desvanecer.
		ProcessMode = Node.ProcessModeEnum.Always;

		// Se espera a que el layout se ASIENTE antes de medir. Con un solo CallDeferred no alcanza:
		// los contenedores de Godot terminan de resolver tamaños en el frame siguiente, y hasta
		// entonces GetCombinedMinimumSize() puede devolver un alto mucho mayor que el real. Con ese
		// alto inflado, el "achicar para que entre" de abajo escalaba el bloque muchísimo y la
		// pantalla de victoria terminaba en miniatura.
		for (int i = 0; i < 2; i++)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this) || !IsInstanceValid(vbox)) return;
		}

		// La animación termina en la escala que HACE QUE ENTRE, no en 1: si terminara en 1 volvería a
		// desbordar justo al final y se vería el recorte.
		float escalaFinal = EscalaParaQueEntre(vbox);
		Vector2 destino = new Vector2(escalaFinal, escalaFinal);

		vbox.Modulate = new Color(1, 1, 1, 0);
		vbox.Scale    = destino * 0.85f;
		vbox.PivotOffset = vbox.Size / 2;
		var tw = CreateTween().SetParallel(true);
		tw.Finished += () =>
		{
			if (!IsInstanceValid(vbox)) return;
			vbox.Modulate = Colors.White;
			vbox.Scale = destino;
			_entradaVictoriaEnCurso = false; // de acá en más manda ReajustarEscalaBloque
		};
		tw.TweenProperty(vbox, "modulate:a", 1.0f, 0.5f);
		tw.TweenProperty(vbox, "scale", destino, 0.55f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}
}
