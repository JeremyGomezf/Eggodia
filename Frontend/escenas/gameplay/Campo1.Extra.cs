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

	private void MostrarAviso(string texto, Color color)
	{
		// y=175: debajo del bloque de info de turno (que ocupa ~78-150)
		MostrarAvisoCentrado(ConstruirToast(texto, color, 16), 175f, 2.0f);
	}

	private void MostrarAvisoFase(string msg)
	{
		MostrarAvisoCentrado(ConstruirToast(msg, new Color(1f, 0.6f, 0.3f), 16), 175f, 2.0f);
	}

	// Muestra un aviso centrado en la parte superior. Solo uno a la vez (evita solapamiento).
	private void MostrarAvisoCentrado(PanelContainer panel, float top, float duracion)
	{
		if (_avisoActual != null && IsInstanceValid(_avisoActual)) _avisoActual.QueueFree();

		var host = new CenterContainer();
		host.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		host.OffsetTop = top; host.OffsetBottom = top + 60;
		host.MouseFilter = Control.MouseFilterEnum.Ignore;
		host.ZIndex = 200;
		panel.ZIndex = 200;
		host.AddChild(panel);
		CapaHUD().AddChild(host);
		_avisoActual = host;

		host.Modulate = new Color(1, 1, 1, 0);
		Tween tw = host.CreateTween();
		tw.TweenProperty(host, "modulate:a", 1.0f, 0.22f);
		tw.TweenInterval(duracion);
		tw.TweenProperty(host, "modulate:a", 0.0f, 0.4f);
		tw.Finished += () => { if (IsInstanceValid(host)) host.QueueFree(); };
	}

	private PanelContainer ConstruirToast(string texto, Color acento, int fontSize, string badge = null)
	{
		var panel = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.06f, 0.08f, 0.13f, 0.92f);
		sb.BorderWidthLeft = 3;
		sb.BorderColor = acento;
		sb.ContentMarginLeft = sb.ContentMarginRight = 16;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 8;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 6;
		sb.ShadowColor = new Color(0, 0, 0, 0.35f);
		sb.ShadowSize = 4;
		panel.AddThemeStyleboxOverride("panel", sb);

		var hb = new HBoxContainer();
		hb.AddThemeConstantOverride("separation", 8);
		panel.AddChild(hb);

		if (badge != null)
		{
			var b = new Label();
			b.Text = badge;
			b.AddThemeColorOverride("font_color", acento);
			b.AddThemeFontSizeOverride("font_size", fontSize + 4);
			b.VerticalAlignment = VerticalAlignment.Center;
			hb.AddChild(b);
		}

		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", new Color(0.95f, 0.96f, 1f));
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

	public void RegistrarGastoMovimiento()
	{
		movimientosRestantes--; ActualizarInterfaz();
		if (movimientosRestantes <= 0 && !juegoTerminado) CambiarTurno();
	}

	public void AplicarVenenoMeta(Node2D t, int daño, int turnos)
	{
		t.SetMeta("envenenado", true); t.SetMeta("dañoVeneno", daño); t.SetMeta("turnosVeneno", turnos);
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
	private void ScreenShake(float intensidad = 6f)
	{
		if (!PanelSettings.ScreenShakeEnabled) return;
		Vector2 orig = Position;
		Tween tw = CreateTween();
		for (int i = 0; i < 4; i++)
		{
			Vector2 off = new Vector2(
				(float)GD.RandRange(-intensidad, intensidad),
				(float)GD.RandRange(-intensidad * 0.5f, intensidad * 0.5f));
			tw.TweenProperty(this, "position", orig + off, 0.03f);
		}
		tw.TweenProperty(this, "position", orig, 0.03f);
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

	private string NombreCorto(Node2D tropa)
	{
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
		mano.OffsetTop    += 180;
		mano.OffsetBottom += 180;
	}

	// ── ESTILO GLOBAL PARA LABELS DEL HUD ────────────────────────────────
	private void EstilizarLabelsHUD()
	{
		_lblVida1     = GetNodeOrNull<Label>("Vida1");
		_lblVida2     = GetNodeOrNull<Label>("Vida2");
		_lblTiempo    = GetNodeOrNull<Label>("Tiempo");
		_lblTurnoInfo = GetNodeOrNull<Label>("LabelTurnoInfo");

		EstilizarLabel(_lblVida1,     new Color(0.5f, 1f, 0.6f), 14);
		EstilizarLabel(_lblVida2,     new Color(1f, 0.55f, 0.5f), 14);
		EstilizarLabel(_lblTiempo,    new Color(0.85f, 0.90f, 1f), 14);
		EstilizarLabel(_lblTurnoInfo, new Color(0.95f, 0.96f, 1f), 13);

		// Reparentar labels a CanvasLayer y anclar a bordes de pantalla (independiente de escala)
		var capa = CapaHUD();
		ReparentAnclado(_lblVida1,     capa, Control.LayoutPreset.TopLeft,   new Vector2(30, 20),   new Vector2(210, 20));
		ReparentAnclado(_lblVida2,     capa, Control.LayoutPreset.TopRight,  new Vector2(-210, 20), new Vector2(-30, 20));
		ReparentAnclado(_lblTiempo,    capa, Control.LayoutPreset.TopWide,   new Vector2(0, 20),    new Vector2(0, 55));
		ReparentAnclado(_lblTurnoInfo, capa, Control.LayoutPreset.TopWide,   new Vector2(0, 78),    new Vector2(0, 145));
		if (_lblTiempo != null)    { _lblTiempo.Scale = Vector2.One; _lblTiempo.HorizontalAlignment = HorizontalAlignment.Center; }
		if (_lblTurnoInfo != null) _lblTurnoInfo.HorizontalAlignment = HorizontalAlignment.Center;
	}

	private void ReparentAnclado(Control c, CanvasLayer capa, Control.LayoutPreset preset, Vector2 tl, Vector2 br)
	{
		if (c == null || capa == null) return;
		c.GetParent()?.RemoveChild(c);
		capa.AddChild(c);
		c.SetAnchorsPreset(preset);
		c.OffsetLeft = tl.X; c.OffsetTop = tl.Y;
		c.OffsetRight = br.X; c.OffsetBottom = br.Y;
	}

	private void EstilizarLabel(Label l, Color acento, int fontSize)
	{
		if (l == null) return;
		l.Modulate = Colors.White;    // el .tscn los tenía en negro
		l.AddThemeFontSizeOverride("font_size", fontSize);
		l.AddThemeColorOverride("font_color", new Color(0.98f, 0.99f, 1f));
		l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
		l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
		l.AddThemeConstantOverride("shadow_offset_x", 1);
		l.AddThemeConstantOverride("shadow_offset_y", 2);
		l.AddThemeConstantOverride("outline_size", 4);
	}

	// ── BARRAS DE VIDA BASE ──────────────────────────────────────────────
	private void CrearBarrasHPBase()
	{
		var lblV1 = GetNodeOrNull<Label>("Vida1");
		var lblV2 = GetNodeOrNull<Label>("Vida2");
		_barraHPJugador = CrearBarraHP(lblV1, new Color(0.2f, 0.75f, 0.25f));
		_barraHPRival   = CrearBarraHP(lblV2, new Color(0.8f, 0.2f, 0.2f));
	}

	private ProgressBar CrearBarraHP(Label lblRef, Color color)
	{
		var bar = new ProgressBar();
		bar.CustomMinimumSize = new Vector2(190, 12);
		bar.MinValue = 0; bar.MaxValue = 100; bar.Value = 100;
		bar.ShowPercentage = false;
		bar.ZIndex = 50;
		var bg = new StyleBoxFlat();
		bg.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
		bg.CornerRadiusTopLeft = bg.CornerRadiusTopRight =
		bg.CornerRadiusBottomLeft = bg.CornerRadiusBottomRight = 3;
		bar.AddThemeStyleboxOverride("background", bg);
		var fill = new StyleBoxFlat();
		fill.BgColor = color;
		fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight =
		fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 3;
		bar.AddThemeStyleboxOverride("fill", fill);
		if (lblRef != null)
		{
			bar.Position = new Vector2(0, 22);
			lblRef.AddChild(bar);
		}
		return bar;
	}

	private int  Gi(Node2D n, string p) { try { return (int)n.Get(p); } catch { return 0; } }

	private bool EstaBlockeada(Node2D t)
	{
		if (t.HasMeta("bloqueado")) try { return (bool)t.GetMeta("bloqueado"); } catch { }
		return false;
	}

	// ── PANEL DE AYUDA: TABLA DE TIPOS ───────────────────────────────────
	private Control _panelTipos;

	private void CrearBotonAyudaTipos()
	{
		var btn = new Button();
		btn.Text = "?";
		btn.TooltipText = "Ver tabla de tipos elementales";
		btn.CustomMinimumSize = new Vector2(48, 48);
		btn.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		btn.OffsetLeft = -68; btn.OffsetTop = -75;
		btn.OffsetRight = -20; btn.OffsetBottom = -27;
		btn.ZIndex = 90;
		btn.AddThemeFontSizeOverride("font_size", 22);
		btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));

		var sbN = new StyleBoxFlat();
		sbN.BgColor = new Color(0.10f, 0.15f, 0.25f, 0.92f);
		sbN.BorderWidthLeft = sbN.BorderWidthTop = sbN.BorderWidthRight = sbN.BorderWidthBottom = 2;
		sbN.BorderColor = new Color(0.4f, 0.6f, 0.9f, 0.75f);
		sbN.CornerRadiusTopLeft = sbN.CornerRadiusTopRight =
		sbN.CornerRadiusBottomLeft = sbN.CornerRadiusBottomRight = 26;
		btn.AddThemeStyleboxOverride("normal", sbN);

		var sbH = new StyleBoxFlat();
		sbH.BgColor = new Color(0.20f, 0.35f, 0.55f, 0.95f);
		sbH.BorderWidthLeft = sbH.BorderWidthTop = sbH.BorderWidthRight = sbH.BorderWidthBottom = 2;
		sbH.BorderColor = new Color(0.65f, 0.85f, 1f, 1f);
		sbH.CornerRadiusTopLeft = sbH.CornerRadiusTopRight =
		sbH.CornerRadiusBottomLeft = sbH.CornerRadiusBottomRight = 26;
		btn.AddThemeStyleboxOverride("hover", sbH);
		btn.AddThemeStyleboxOverride("pressed", sbH);

		btn.Pressed += ToggleAyudaTipos;
		CapaHUD().AddChild(btn);
	}

	private void ToggleAyudaTipos()
	{
		if (_panelTipos != null && IsInstanceValid(_panelTipos))
		{
			_panelTipos.QueueFree();
			_panelTipos = null;
			return;
		}
		_panelTipos = ConstruirPanelTipos();
		AddChild(_panelTipos);
	}

	private Control ConstruirPanelTipos()
	{
		var panel = new PanelContainer();
		panel.Position = new Vector2(780, 70);
		panel.CustomMinimumSize = new Vector2(440, 380);
		panel.ZIndex = 200;
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.08f, 0.10f, 0.15f, 0.96f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.4f, 0.6f, 0.9f, 0.7f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 14;
		sb.ContentMarginLeft = sb.ContentMarginRight = 18;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 14;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		panel.AddChild(vbox);

		var titulo = new Label();
		titulo.Text = "TABLA DE TIPOS";
		titulo.AddThemeFontSizeOverride("font_size", 20);
		titulo.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1f));
		titulo.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(titulo);

		var sub = new Label();
		sub.Text = "Ventaja: +25% daño   ·   Desventaja: -20% daño";
		sub.AddThemeFontSizeOverride("font_size", 12);
		sub.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(sub);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		string[] tipos    = { Tipos.FUEGO, Tipos.NATURALEZA, Tipos.METAL, Tipos.SOMBRA, Tipos.AGUA };
		string[] gana     = { Tipos.NATURALEZA, Tipos.METAL, Tipos.SOMBRA, Tipos.AGUA, Tipos.FUEGO };
		string[] pierde   = { Tipos.AGUA, Tipos.FUEGO, Tipos.NATURALEZA, Tipos.METAL, Tipos.SOMBRA };

		for (int i = 0; i < tipos.Length; i++)
		{
			var hb = new HBoxContainer();
			hb.AddThemeConstantOverride("separation", 10);
			hb.AddChild(ChipTipo(tipos[i]));
			hb.AddChild(TextoLbl("fuerte contra", new Color(0.7f, 0.75f, 0.8f), 13));
			hb.AddChild(ChipTipo(gana[i]));
			hb.AddChild(TextoLbl("·  débil contra", new Color(0.7f, 0.75f, 0.8f), 13));
			hb.AddChild(ChipTipo(pierde[i]));
			vbox.AddChild(hb);
		}

		var sep2 = new HSeparator();
		vbox.AddChild(sep2);

		var btnCerrar = new Button();
		btnCerrar.Text = "CERRAR";
		btnCerrar.CustomMinimumSize = new Vector2(0, 36);
		btnCerrar.Pressed += ToggleAyudaTipos;
		vbox.AddChild(btnCerrar);

		return panel;
	}

	private Control ChipTipo(string tipo)
	{
		var chip = new PanelContainer();
		chip.CustomMinimumSize = new Vector2(110, 28);
		var sb = new StyleBoxFlat();
		sb.BgColor = Tipos.Color(tipo);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 8;
		sb.ContentMarginLeft = sb.ContentMarginRight = 8;
		chip.AddThemeStyleboxOverride("panel", sb);
		var lbl = new Label();
		lbl.Text = Tipos.Etiqueta(tipo);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
		lbl.AddThemeConstantOverride("shadow_offset_x", 1);
		lbl.AddThemeConstantOverride("shadow_offset_y", 1);
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment   = VerticalAlignment.Center;
		chip.AddChild(lbl);
		return chip;
	}

	private Label TextoLbl(string t, Color c, int size)
	{
		var l = new Label();
		l.Text = t;
		l.AddThemeColorOverride("font_color", c);
		l.AddThemeFontSizeOverride("font_size", size);
		l.VerticalAlignment = VerticalAlignment.Center;
		return l;
	}
}
