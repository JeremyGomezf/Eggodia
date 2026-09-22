using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── SISTEMA DE LOGROS ─────────────────────────────────────────────────
	private void VerificarLogros(string resultado)
	{
		if (resultado.Contains("VICTORIA"))
		{
			// Primera victoria
			if (!_logroPrimeraVictoria)
			{
				_logroPrimeraVictoria = true;
				MostrarLogroEnPantalla("LOGRO: Primera Victoria");
			}
			// Racha
			_rachaVictorias++;
			if (SesionJuego.Instance != null)
				SesionJuego.Instance.RachaActual = _rachaVictorias;
			if (_rachaVictorias >= 3)
				MostrarLogroEnPantalla($"RACHA DE {_rachaVictorias} VICTORIAS");
		}
		else
		{
			_rachaVictorias = 0;
			if (SesionJuego.Instance != null)
				SesionJuego.Instance.RachaActual = 0;
		}

		// Logro habilidades
		if (!_logroHabilidadUsada)
		{
			foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			{
				if (n is Node2D t && HabilidadUsada(t))
				{
					_logroHabilidadUsada = true;
					MostrarLogroEnPantalla("LOGRO: Habilidad especial activada");
					break;
				}
			}
		}
	}

	private void MostrarLogroEnPantalla(string texto)
	{
		var panel = ConstruirToast(texto, new Color(1f, 0.82f, 0.30f), 18, badge: "★");
		panel.Position = new Vector2(430, 55);
		panel.ZIndex = 300;
		AddChild(panel);
		AnimarToast(panel, subida: 40f, duracion: 2.2f);
	}

	private Control _avisoActual;
	private const float DURACION_TOAST = 2.5f; // exigido: exactamente 2.5s visible antes de desaparecer

	// Público: algunas cartas (p. ej. MaguinPrime, con su modo de selección "decide a cuál
	// transformas") lo llaman desde afuera vía campo.Call("MostrarAviso", ...).
	public void MostrarAviso(string texto, Color color)
	{
		// Durante la reproducción visual de una jugada del rival (online) NO se muestran avisos: el texto
		// está escrito desde la perspectiva del que actúa ("¡Envenenaste a…!") y saldría al revés.
		if (SuprimiendoAvisosOnline) return;
		// y=230: un poco más arriba que antes (300), pidiendo seguir debajo del bloque superior
		// (barras HP, Tiempo, Turno) pero sin quedar tan abajo en el tablero.
		MostrarAvisoCentrado(ConstruirToast(texto, color, 26), 230f, DURACION_TOAST);
	}

	private void MostrarAvisoFase(string msg)
	{
		MostrarAvisoCentrado(ConstruirToast(msg, new Color(1f, 0.6f, 0.3f), 26), 230f, DURACION_TOAST);
	}

	// Frase burlona/celebratoria de fin de partida — SIN panel/caja de fondo, solo la letra:
	// blanca, grande, con borde negro grueso y sombra oscura difuminada detrás. Mismo efecto
	// "gelatina" al aparecer. La secuencia completa de cierre dura 4s (ver FinalizarPartida),
	// pero el texto debe alcanzar a desvanecerse ANTES de la pantalla de Victoria/Derrota: con
	// ~0.45s de pop + 3.0s de espera + 0.35s de fade queda completamente invisible a los 3.8s.
	private void MostrarFraseFinPartida(string frase, Color acento)
	{
		// Centrado real en X e Y (null = FullRect), no un "top" fijo — así queda bien centrado
		// respecto a la Camera2D y su zoom sin importar la resolución/aspecto del dispositivo.
		MostrarAvisoCentrado(ConstruirTextoGrande(frase, 90), null, 3.0f);
	}

	// Solo texto, sin panel de fondo: blanco, borde negro grueso, sombra difuminada detrás.
	private Label ConstruirTextoGrande(string texto, int fontSize)
	{
		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1f));
		lbl.AddThemeConstantOverride("outline_size", 14);
		lbl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.65f));
		lbl.AddThemeConstantOverride("shadow_offset_x", 4);
		lbl.AddThemeConstantOverride("shadow_offset_y", 6);
		lbl.AddThemeConstantOverride("shadow_outline_size", 12);
		lbl.AddThemeFontSizeOverride("font_size", fontSize);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		return lbl;
	}

	// Muestra un aviso centrado. Con "top" fijo queda pegado bajo el HUD superior (toasts); con
	// top=null queda centrado en TODA la pantalla (X e Y) — usado por la frase de fin de partida,
	// que debe verse centrada respecto a la Camera2D/zoom sin importar la resolución del
	// dispositivo. Efecto "gelatina" (pop elástico) al aparecer. Solo uno a la vez.
	private void MostrarAvisoCentrado(Control panel, float? top, float duracion)
	{
		if (_avisoActual != null && IsInstanceValid(_avisoActual)) _avisoActual.QueueFree();

		var host = new CenterContainer();
		if (top.HasValue)
		{
			host.SetAnchorsPreset(Control.LayoutPreset.TopWide);
			host.OffsetTop = top.Value; host.OffsetBottom = top.Value + 130;
		}
		else
		{
			host.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		}
		host.MouseFilter = Control.MouseFilterEnum.Ignore;
		host.ZIndex = 200;
		panel.ZIndex = 200;
		host.AddChild(panel);
		CapaHUD().AddChild(host);
		_avisoActual = host;

		host.PivotOffset = host.Size / 2f;
		host.Modulate = new Color(1, 1, 1, 0);
		host.Scale = new Vector2(0.55f, 0.55f);

		Tween tw = host.CreateTween().SetParallel(true);
		tw.TweenProperty(host, "modulate:a", 1.0f, 0.15f);
		tw.TweenProperty(host, "scale", Vector2.One, 0.45f)
		  .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
		tw.Chain().TweenInterval(duracion);
		tw.Chain().TweenProperty(host, "modulate:a", 0.0f, 0.35f);
		tw.Finished += () => { if (IsInstanceValid(host)) host.QueueFree(); };
	}

	// Panel/texto agrandados para lectura cómoda en móvil.
	private PanelContainer ConstruirToast(string texto, Color acento, int fontSize, string badge = null)
	{
		var panel = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.06f, 0.08f, 0.13f, 0.94f);
		sb.BorderWidthLeft = 5;
		sb.BorderColor = acento;
		sb.ContentMarginLeft = sb.ContentMarginRight = 26;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 14;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ShadowColor = new Color(0, 0, 0, 0.4f);
		sb.ShadowSize = 6;
		panel.AddThemeStyleboxOverride("panel", sb);

		var hb = new HBoxContainer();
		hb.AddThemeConstantOverride("separation", 12);
		panel.AddChild(hb);

		if (badge != null)
		{
			var b = new Label();
			b.Text = badge;
			b.AddThemeColorOverride("font_color", acento);
			b.AddThemeFontSizeOverride("font_size", fontSize + 6);
			b.VerticalAlignment = VerticalAlignment.Center;
			hb.AddChild(b);
		}

		var lbl = new Label();
		lbl.Text = texto;
		// Texto blanco + borde negro grueso + sombra oscura difuminada por detrás (legibilidad
		// total sobre cualquier fondo del campo de batalla).
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
		lbl.AddThemeConstantOverride("outline_size", 8);
		lbl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
		lbl.AddThemeConstantOverride("shadow_offset_x", 3);
		lbl.AddThemeConstantOverride("shadow_offset_y", 4);
		lbl.AddThemeConstantOverride("shadow_outline_size", 6);
		lbl.AddThemeFontSizeOverride("font_size", fontSize);
		lbl.VerticalAlignment = VerticalAlignment.Center;
		hb.AddChild(lbl);
		return panel;
	}

	private void AnimarToast(Control panel, float subida, float duracion)
	{
		panel.Modulate = new Color(1, 1, 1, 0);
		Vector2 destino = panel.Position + new Vector2(0, -subida);
		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(panel, "modulate:a", 1.0f, 0.25f);
		tw.TweenProperty(panel, "position", destino, duracion).SetTrans(Tween.TransitionType.Sine);
		tw.Chain().TweenInterval(duracion * 0.5f);
		tw.Chain().TweenProperty(panel, "modulate:a", 0.0f, 0.4f);
		tw.Finished += () => { if (IsInstanceValid(panel)) panel.QueueFree(); };
	}

	public void RegistrarGastoMovimiento(int cantidad = 1)
	{
		movimientosRestantes = Mathf.Max(0, movimientosRestantes - cantidad); ActualizarInterfaz();
		// Al llegar a EP:0/3 no se cambia de turno de inmediato: el panel de energía se queda
		// en blanco (ya "gastado") 2s antes de que el turno realmente pase al rival.
		if (movimientosRestantes <= 0 && !juegoTerminado && !_turnoFinalizando)
		{
			_turnoFinalizando = true;
			GetTree().CreateTimer(2.0).Timeout += () => { if (!juegoTerminado) CambiarTurno(); };
		}
	}

	public void AplicarVenenoMeta(Node2D t, int daño, int turnos)
	{
		t.SetMeta("envenenado", true); t.SetMeta("danoVeneno", daño); t.SetMeta("turnosVeneno", turnos);
		t.Modulate = new Color(0.6f, 1f, 0.4f);
		ActualizarIconosEstado(t);
	}

	public void AplicarBloqueoMeta(Node2D t, int turnos)
	{
		t.SetMeta("bloqueado", true); t.SetMeta("turnosBloqueo", turnos);
		t.Modulate = new Color(0.4f, 0.6f, 1.4f);
		ActualizarIconosEstado(t);
	}

	// ── SCREEN SHAKE ──────────────────────────────────────────────────────
	public void ScreenShake(float intensidad = 6f)
	{
		// Mismo sistema que los sacudones de la Nuclear y de los misiles (Campo1.Nuclear.cs): así dos
		// sacudidas seguidas nunca pelean por la posición del campo ni lo dejan corrido. Este es el
		// golpecito corto de siempre (0.15s) y solo mueve el campo, no los botones.
		Sacudir(0.15f, intensidad, 0f);
	}

	// ── GOLPE CRÍTICO (números dorados grandes) ──────────────────────────
	private void MostrarDañoFlotanteCritico(Vector2 posGlobal, int cantidad)
	{
		var lbl = new Label();
		lbl.Text = $"CRIT {cantidad}";
		lbl.AddThemeColorOverride("font_color", Colors.Gold);
		lbl.AddThemeFontSizeOverride("font_size", 30);
		lbl.ZIndex = 310;
		lbl.GlobalPosition = posGlobal + new Vector2(-30, -70);
		AddChild(lbl);

		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "position:y", lbl.Position.Y - 80f, 1.1f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 1.1f);
		tw.TweenProperty(lbl, "scale", new Vector2(1.3f, 1.3f), 0.15f);
		tw.Chain().TweenProperty(lbl, "scale", Vector2.One, 0.2f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	// ── ERA: VENTAJA DE TIPO ─────────────────────────────────────────────
	private string ObtenerTipoTropa(Node2D tropa)
	{
		if (tropa is TropaBase tb) return tb.Tipo;
		return Tipos.NEUTRO;
	}

	// Nombre tal como aparece en el Menú Constructor (CartaData.Nombre) — ej. "Dragón de Flama"
	// en vez de "Dragon". Si la tropa no tiene CartaData asociada (caso raro), cae al nombre
	// corto derivado de su clase, como antes.
	private string NombreCorto(Node2D tropa)
	{
		string nombreCompleto = ObtenerNombreCompleto(tropa);
		if (!string.IsNullOrEmpty(nombreCompleto)) return nombreCompleto;

		string n = tropa.GetType().Name.Replace("Prime", "");
		return n switch
		{
			"SoldadoCartoon" => "Soldado Mod.",
			"SoldadoReal"    => "Soldado Real",
			"CalamarG"       => "Calamar",
			"TRex"           => "T-Rex",
			_ => n
		};
	}

	private string ObtenerEraTropa(Node2D tropa)
	{
		string tipo = tropa.GetType().Name;
		return tipo switch
		{
			"TRexPrime" or "TiburonPrime" or "CalamarGPrime" => "primordial",
			"DragonPrime" or "GolemPrime" or "MaguinPrime"   => "mistica",
			"SoldadoCartoonPrime"                             => "moderna",
			_ => "medieval"
		};
	}

	private float ObtenerMultiplicadorEra(string eraAtk, string eraDef)
	{
		if (eraAtk == eraDef) return 1.0f;
		if (eraAtk == "primordial" && eraDef == "medieval")  return 1.2f;
		if (eraAtk == "medieval"   && eraDef == "moderna")   return 1.2f;
		if (eraAtk == "moderna"    && eraDef == "mistica")   return 1.2f;
		if (eraAtk == "mistica"    && eraDef == "primordial") return 1.2f;
		if (eraAtk == "medieval"   && eraDef == "primordial") return 0.85f;
		if (eraAtk == "moderna"    && eraDef == "medieval")   return 0.85f;
		if (eraAtk == "mistica"    && eraDef == "moderna")    return 0.85f;
		if (eraAtk == "primordial" && eraDef == "mistica")    return 0.85f;
		return 1.0f;
	}

	private void MostrarVentajaEra(Vector2 pos, float mult)
	{
		if (mult == 1.0f) return;
		var lbl = new Label();
		lbl.Text = mult > 1f ? "▲ VENTAJA" : "▼ DESVENTAJA";
		lbl.AddThemeColorOverride("font_color", mult > 1f ? Colors.LightGreen : Colors.OrangeRed);
		lbl.AddThemeFontSizeOverride("font_size", 14);
		lbl.ZIndex = 280;
		lbl.GlobalPosition = pos + new Vector2(-35, -45);
		AddChild(lbl);
		Tween tw = CreateTween();
		tw.TweenInterval(0.8f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.3f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	private void BajarManoManual()
	{
		var mano = GetNodeOrNull<Control>("ManoManual");
		if (mano == null) return;

		float nuevoBottom = mano.OffsetBottom + 220f; // fallback si no hay cámara
		var cam = GetNodeOrNull<Camera2D>("Camera2D");
		if (cam != null)
		{
			float mitadAlturaMundo  = (GetViewport().GetVisibleRect().Size.Y / 2f) / cam.Zoom.Y;
			float bordeInferiorMundo = cam.Position.Y + mitadAlturaMundo;
			// Un poco más abajo que el borde visible: despeja la vista central del campo
			// sin solaparse con los botones de acción.
			nuevoBottom = bordeInferiorMundo + 30f;
		}

		float delta = nuevoBottom - mano.OffsetBottom;
		mano.OffsetTop    += delta;
		mano.OffsetBottom += delta;

	}

	// ── NUEVA INTERFAZ (TextureProgressBar_User/_Rival, TiempoPanel, TurnoPanel, botones) ──
	// Reemplaza por completo el HUD anterior de labels planos + ProgressBar genérico.
	//
	// Estos 8 nodos siguen ubicados a mano en el editor exactamente donde los dejaste (hijos
	// de la raíz, junto a la Camera2D) — así se editan/arrastran con normalidad en la vista 2D.
	// Al entrar en juego, MoverACanvasInmune() los traslada al CanvasLayer capturando su
	// posición/escala YA RESUELTAS por el motor (Control.GetGlobalTransformWithCanvas, que
	// incluye el efecto de la cámara activa), no recalculadas a mano por mí. El resultado
	// visual queda idéntico al de la cámara, pero desde ese momento son inmunes a cualquier
	// cambio futuro de Position/Zoom en la Camera2D.
	private void MoverACanvasInmune(Control c, CanvasLayer capa)
	{
		if (c == null || capa == null) return;
		Transform2D t = c.GetGlobalTransformWithCanvas();

		c.GetParent()?.RemoveChild(c);
		capa.AddChild(c);

		c.AnchorLeft = c.AnchorTop = c.AnchorRight = c.AnchorBottom = 0f;
		c.PivotOffset = Vector2.Zero;
		c.Position = t.Origin;
		c.Rotation = t.Rotation;
		c.Scale    = t.Scale;
	}

	// ── AVISOS DE BOMBA (uno por bando) ───────────────────────────────────
	// "AvisoArdidPanel" (el de la escena, al lado de MI barra de vida) muestra la cuenta de MI bomba,
	// usando las DOS labels que ya están puestas a mano en el editor, directamente bajo el panel (ya
	// no adentro de un HBox): "avisos de ardid" es el título/aviso de arriba, "numero" es la cuenta de
	// abajo. Para la del rival se clona ese mismo panel (con esas dos labels ya adentro) y se ubica
	// espejado, junto a SU barra de vida.
	private const float ESCALA_EXTRA_PANEL_BOMBA = 1.3f;  // "un poco más grande" (letras chicas, pedido)
	private const int   FUENTE_TITULO_BOMBA      = 42;    // antes 50 en la escena, pero a esta escala se
	private const int   FUENTE_NUMERO_BOMBA      = 66;    // veía chico — se agranda por código
	private Control _avisoBombaJugador, _avisoBombaRival;
	private Label   _lblTituloBombaJugador, _lblTituloBombaRival;
	private Label   _lblBombaJugador,       _lblBombaRival;

	private void PrepararAvisosDeBomba(CanvasLayer capa)
	{
		_avisoBombaJugador = capa.GetNodeOrNull<Control>("AvisoArdidPanel");
		if (_avisoBombaJugador == null) return;
		_avisoBombaJugador.Scale *= ESCALA_EXTRA_PANEL_BOMBA;

		_lblTituloBombaJugador = _avisoBombaJugador.GetNodeOrNull<Label>("avisos de ardid");
		_lblBombaJugador       = _avisoBombaJugador.GetNodeOrNull<Label>("numero");
		AgrandarLetraBomba(_lblTituloBombaJugador, FUENTE_TITULO_BOMBA);
		AgrandarLetraBomba(_lblBombaJugador, FUENTE_NUMERO_BOMBA);

		_avisoBombaRival = (Control)_avisoBombaJugador.Duplicate();
		_avisoBombaRival.Name = "AvisoArdidPanelRival";
		capa.AddChild(_avisoBombaRival);
		_lblTituloBombaRival = _avisoBombaRival.GetNodeOrNull<Label>("avisos de ardid");
		_lblBombaRival        = _avisoBombaRival.GetNodeOrNull<Label>("numero");

		// Espejado horizontal respecto del centro de la pantalla, a la altura de la barra del rival.
		float ancho = _avisoBombaJugador.Size.X * _avisoBombaJugador.Scale.X;
		float x = GetViewport().GetVisibleRect().Size.X - _avisoBombaJugador.Position.X - ancho;
		float y = _avisoBombaJugador.Position.Y;
		if (_barraHPJugador != null && _barraHPRival != null)
			y = _barraHPRival.Position.Y + (_avisoBombaJugador.Position.Y - _barraHPJugador.Position.Y);
		_avisoBombaRival.Position = new Vector2(x, y);

		_avisoBombaJugador.Visible = false; // solo se ven mientras hay una bomba en cuenta regresiva
		_avisoBombaRival.Visible   = false;
	}

	private static void AgrandarLetraBomba(Label lbl, int tamaño)
	{
		if (lbl == null || !IsInstanceValid(lbl)) return;
		lbl.AddThemeFontSizeOverride("font_size", tamaño);
	}

	/// <summary>Muestra/oculta la cuenta de una bomba en el panel del bando que la lanzó: título arriba
	/// ("avisos de ardid"), número grande abajo ("numero") — las dos labels reales de la escena.</summary>
	public void MostrarCuentaBombaEnPanel(bool esMia, int segundos)
	{
		Control panel  = esMia ? _avisoBombaJugador       : _avisoBombaRival;
		Label   titulo = esMia ? _lblTituloBombaJugador   : _lblTituloBombaRival;
		Label   numero = esMia ? _lblBombaJugador         : _lblBombaRival;
		if (panel == null || !IsInstanceValid(panel)) return;

		if (segundos <= 0) { panel.Visible = false; return; }
		panel.Visible = true;
		if (titulo != null && IsInstanceValid(titulo))
			titulo.Text = esMia ? "¡TU BOMBA NUCLEAR!" : "¡BOMBA NUCLEAR DEL RIVAL!";
		if (numero == null || !IsInstanceValid(numero)) return;
		numero.Text = segundos.ToString();
		numero.AddThemeColorOverride("font_color",
			segundos <= 15 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.75f, 0.2f));
	}

	private void ConfigurarInterfazNueva()
	{
		var capa = CapaHUD();

		MoverACanvasInmune(GetNodeOrNull<Control>("SacrificioButton"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("BarajarButton"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("ArdidBarButton"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("PausaButton"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("TiempoPanel"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("TurnoPanel"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("TextureProgressBar_User"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("TextureProgressBar_Rival"), capa);
		MoverACanvasInmune(GetNodeOrNull<Control>("AvisoArdidPanel"), capa);

		_barraHPJugador = capa.GetNodeOrNull<TextureProgressBar>("TextureProgressBar_User");
		_barraHPRival   = capa.GetNodeOrNull<TextureProgressBar>("TextureProgressBar_Rival");
		PrepararAvisosDeBomba(capa); // usa la posición de ambas barras, por eso va después

		_lblUsuario = _barraHPJugador?.GetNodeOrNull<Label>("usuariolabel");
		_lblCPU     = _barraHPRival?.GetNodeOrNull<Label>("CPUlabel");
		if (_lblUsuario != null) _lblUsuario.Text = SesionJuego.Instance?.NombreJugador ?? "Invitado";
		if (_lblCPU     != null) _lblCPU.Text     = ContextoOnline.Activo ? ContextoOnline.RivalNombre : _nombreCPUElegido;

		_panelEnergiaUsuario = _barraHPJugador?.GetNodeOrNull<Control>("EnergiaPanel");
		_lblEnergiaUsuario   = _panelEnergiaUsuario?.GetNodeOrNull<Label>("HBox/energialabel");
		_panelEnergiaRival   = _barraHPRival?.GetNodeOrNull<Control>("EnergiaPanel");
		_lblEnergiaRival     = _panelEnergiaRival?.GetNodeOrNull<Label>("HBox/energialabel");

		_lblTiempo     = capa.GetNodeOrNull<Label>("TiempoPanel/HBox/tiempolabel");
		_lblTurnoAviso = capa.GetNodeOrNull<Label>("TurnoPanel/HBox/turno o avisos");
		// Escala real ya calculada por MoverACanvasInmune (incluye el factor de la cámara):
		// el "rebote" de AnunciarTurno debe volver a ESTA escala, no a la del .tscn original.
		if (_lblTurnoAviso?.GetParent()?.GetParent() is Control turnoPanel)
		{
			turnoPanel.Scale *= 1.22f; // "un poco más grande" (pedido)
			_turnoPanelEscalaBase = turnoPanel.Scale;
		}

		// Botones con textura nueva: se conectan por código, igual que el resto del HUD dinámico.
		btnBarajar    = capa.GetNodeOrNull<TextureButton>("BarajarButton");
		btnSacrificio = capa.GetNodeOrNull<TextureButton>("SacrificioButton");
		if (btnBarajar    != null) btnBarajar.Pressed    += _on_barajar_pressed;
		if (btnSacrificio != null) btnSacrificio.Pressed += _on_sacrificar_pressed;

		_btnCambiarHechizo = capa.GetNodeOrNull<TextureButton>("ArdidBarButton");
		if (_btnCambiarHechizo != null) _btnCambiarHechizo.Pressed += ActivarModoCambio;

		var btnPausa = capa.GetNodeOrNull<TextureButton>("PausaButton");
		if (btnPausa != null)
		{
			if (ContextoOnline.Activo) btnPausa.Visible = false; // sin pausa en partidas en línea
			else btnPausa.Pressed += () => GetNodeOrNull<MenuPausa>("MenuPausa")?.Pausar();
		}

		// Juice de botones (hover: agranda + aura blanca / press: encoge y oscurece). Se agrega
		// DESPUÉS de MoverACanvasInmune para partir de la escala final ya calculada con la cámara.
		AgregarJuiceBoton(btnSacrificio);
		AgregarJuiceBoton(btnBarajar);
		AgregarJuiceBoton(_btnCambiarHechizo);
		AgregarJuiceBoton(btnPausa);

		if (btnSacrificio != null) _escalaOriginalBtnSacrificio = btnSacrificio.Scale;
	}

	// Escala "de reposo" de cada botón con juice — normalmente es su escala original del .tscn,
	// pero código externo puede pisarla (ver ActualizarEscalaBotonSacrificio) para que el botón se
	// quede agrandado de forma persistente mientras dure un estado (p. ej. modo sacrificio activo),
	// sin que el hover/press de abajo lo hagan volver a la escala original de golpe.
	private readonly Dictionary<TextureButton, Vector2> _escalaReposoBoton = new();

	// ── JUICE DE BOTONES (hover / press) ──────────────────────────────────
	private void AgregarJuiceBoton(TextureButton btn)
	{
		if (btn == null) return;
		Vector2 escalaBase = btn.Scale;
		_escalaReposoBoton[btn] = escalaBase;
		btn.PivotOffset = btn.Size / 2f;

		// Aura blanca sutil detrás del botón: mismo sprite en blanco con mezcla aditiva.
		var aura = new TextureRect();
		aura.Texture = btn.TextureNormal;
		aura.Size = btn.Size;
		aura.Position = btn.Position;
		aura.Rotation = btn.Rotation;
		aura.Scale = escalaBase;
		aura.PivotOffset = btn.Size / 2f;
		aura.SelfModulate = new Color(1f, 1f, 1f, 0f);
		aura.MouseFilter = Control.MouseFilterEnum.Ignore;
		var mat = new CanvasItemMaterial();
		mat.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
		aura.Material = mat;
		btn.GetParent().AddChild(aura);
		aura.GetParent().MoveChild(aura, btn.GetIndex()); // justo detrás del botón

		btn.MouseEntered += () =>
		{
			// Bloqueado/desactivado: no reacciona al mouse ni crece.
			if (btn.Disabled) return;
			Vector2 reposo = _escalaReposoBoton[btn];
			btn.CreateTween().TweenProperty(btn, "scale", reposo * 1.08f, 0.15f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			aura.Scale = reposo * 1.1f;
			aura.CreateTween().TweenProperty(aura, "self_modulate:a", 0.55f, 0.18f);
		};
		btn.MouseExited += () =>
		{
			// Siempre revierte a la escala de reposo ACTUAL (por si quedó agrandado/con aura justo
			// antes de bloquearse) — normalmente la original, salvo que algo la haya pisado.
			Vector2 reposo = _escalaReposoBoton[btn];
			btn.CreateTween().TweenProperty(btn, "scale", reposo, 0.15f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			aura.CreateTween().TweenProperty(aura, "self_modulate:a", 0.0f, 0.2f);
		};
		btn.ButtonDown += () =>
		{
			if (btn.Disabled) return;
			Vector2 reposo = _escalaReposoBoton[btn];
			Tween tw = btn.CreateTween().SetParallel(true);
			tw.TweenProperty(btn, "scale", reposo * 0.92f, 0.06f);
			tw.TweenProperty(btn, "modulate", new Color(0.7f, 0.7f, 0.7f), 0.06f);
		};
		btn.ButtonUp += () =>
		{
			Vector2 reposo = _escalaReposoBoton[btn];
			Tween tw = btn.CreateTween().SetParallel(true);
			tw.TweenProperty(btn, "scale", reposo, 0.12f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(btn, "modulate", Colors.White, 0.12f);
			// Si este mismo clic dejó el botón bloqueado (p. ej. Ardid Barajar entra en cooldown al
			// soltarlo), el tween de arriba lo pisaría de vuelta a blanco opaco — se corrige al terminar,
			// así el semitransparente de "bloqueado" no desaparece un instante después de aparecer.
			tw.Chain().TweenCallback(Callable.From(() =>
			{
				if (IsInstanceValid(btn) && btn.Disabled) btn.Modulate = new Color(1f, 1f, 1f, 0.4f);
			}));
		};
	}

	// Se llama cada vez que cambia modoSacrificioActivo: el botón de sacrificio queda agrandado de
	// forma persistente mientras el modo está activo (no solo mientras el mouse está encima), para
	// que sea obvio en todo momento "estoy a punto de matar una tropa, no me confíe".
	// ── MODO SACRIFICIO: todo atenuado y bloqueado, menos Pausa ───────────
	// Mientras está activo, el resto del HUD y las dos manos quedan semitransparentes y sin responder
	// (así se ve clarísimo que estás eligiendo a quién sacrificar). El botón de Pausa NUNCA se bloquea.
	private readonly List<(CanvasItem nodo, Color modulate, bool eraDisabled, Control.MouseFilterEnum filtro)> _atenuadosSacrificio = new();
	private Tween _tweenBaileSacrificio;

	private void AplicarModoSacrificioVisual(bool activo)
	{
		if (!activo) { RestaurarAtenuadosSacrificio(); BailarBotonSacrificio(false); return; }

		RestaurarAtenuadosSacrificio();
		var capa = CapaHUD();
		foreach (Node n in capa.GetChildren())
		{
			if (n is not CanvasItem ci || !IsInstanceValid(ci)) continue;
			if (n.Name == "PausaButton" || ci == btnSacrificio) continue; // Pausa y Sacrificio siguen vivos
			AtenuarParaSacrificio(ci);
		}
		foreach (Control mano in new[] { contenedorMano, _contenedorHechizos })
		{
			if (mano == null || !IsInstanceValid(mano)) continue;
			AtenuarParaSacrificio(mano);
			foreach (Node n in mano.GetChildren())
				if (n is Carta c && IsInstanceValid(c)) c.BloquearPorModo(true);
		}

		BailarBotonSacrificio(true);
		MostrarAviso("Escoge a cuál sacrificarás", Colors.OrangeRed);
	}

	private void AtenuarParaSacrificio(CanvasItem ci)
	{
		bool eraDisabled = ci is BaseButton bb && bb.Disabled;
		var filtroPrevio = ci is Control ctrl ? ctrl.MouseFilter : Control.MouseFilterEnum.Ignore;
		_atenuadosSacrificio.Add((ci, ci.Modulate, eraDisabled, filtroPrevio));
		ci.Modulate = new Color(ci.Modulate.R, ci.Modulate.G, ci.Modulate.B, 0.35f);
		if (ci is BaseButton btn) btn.Disabled = true;
		if (ci is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
	}

	private void RestaurarAtenuadosSacrificio()
	{
		foreach (var (ci, modulate, eraDisabled, filtroPrevio) in _atenuadosSacrificio)
		{
			if (!IsInstanceValid(ci)) continue;
			ci.Modulate = modulate;
			if (ci is BaseButton btn) btn.Disabled = eraDisabled;
			if (ci is Control c) c.MouseFilter = filtroPrevio;
		}
		_atenuadosSacrificio.Clear();
		foreach (Control mano in new[] { contenedorMano, _contenedorHechizos })
		{
			if (mano == null || !IsInstanceValid(mano)) continue;
			foreach (Node n in mano.GetChildren())
				if (n is Carta c && IsInstanceValid(c)) c.BloquearPorModo(false);
		}
	}

	// Balanceo del botón mientras el modo está activo: se inclina de un lado al otro sin moverse de su
	// lugar, para que se note que está "encendido".
	private void BailarBotonSacrificio(bool bailar)
	{
		_tweenBaileSacrificio?.Kill();
		_tweenBaileSacrificio = null;
		if (btnSacrificio == null || !IsInstanceValid(btnSacrificio)) return;
		if (!bailar) { btnSacrificio.Rotation = 0f; btnSacrificio.ZIndex = 0; return; }

		btnSacrificio.PivotOffset = btnSacrificio.Size / 2f;
		btnSacrificio.ZIndex = 50; // por delante de Barajar (y del resto del HUD atenuado)
		_tweenBaileSacrificio = btnSacrificio.CreateTween().SetLoops();
		_tweenBaileSacrificio.TweenProperty(btnSacrificio, "rotation", Mathf.DegToRad(2.5f), 0.18f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_tweenBaileSacrificio.TweenProperty(btnSacrificio, "rotation", Mathf.DegToRad(-2.5f), 0.36f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_tweenBaileSacrificio.TweenProperty(btnSacrificio, "rotation", 0f, 0.18f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void ActualizarEscalaBotonSacrificio()
	{
		if (btnSacrificio == null || !_escalaReposoBoton.ContainsKey(btnSacrificio)) return;
		Vector2 escalaOriginal = _escalaOriginalBtnSacrificio;
		Vector2 nuevoReposo = modoSacrificioActivo ? escalaOriginal * 1.18f : escalaOriginal;
		_escalaReposoBoton[btnSacrificio] = nuevoReposo;
		btnSacrificio.CreateTween().TweenProperty(btnSacrificio, "scale", nuevoReposo, 0.15f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private int  Gi(Node2D n, string p) { try { return (int)n.Get(p); } catch { return 0; } }

	private bool EstaBlockeada(Node2D t)
	{
		if (t.HasMeta("bloqueado")) try { if ((bool)t.GetMeta("bloqueado")) return true; } catch { }
		// "atrapado_tentaculo": bloqueo persistente del Calamar Gigante, independiente del
		// contador de turnos de "bloqueado" (dura hasta que se rompe el escudo del Calamar,
		// no un número fijo de turnos) — ver TentaculoHabilidadPrime.cs.
		if (t.HasMeta("atrapado_tentaculo")) try { if ((bool)t.GetMeta("atrapado_tentaculo")) return true; } catch { }
		// "en_postura_permanente": el propio Calamar, mientras mantiene atrapados a los
		// enemigos, pierde su propio menú de acciones (ATACAR/DEFENSA/HABILIDAD) — ver
		// CalamarGPrime.cs.
		if (t.HasMeta("en_postura_permanente")) try { return (bool)t.GetMeta("en_postura_permanente"); } catch { }
		return false;
	}

}
