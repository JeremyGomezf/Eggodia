using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── HISTORIAL DE BATALLA ─────────────────────────────────────────────
	private VBoxContainer _historialContenido;
	private PanelContainer _cardHistorial;
	private Button _btnToggleHistorial;
	private bool _historialAbierto = false;
	private readonly System.Collections.Generic.Queue<string> _eventos = new(9);

	private void CrearBotonHistorial()
	{
		_btnToggleHistorial = new Button();
		_btnToggleHistorial.Text = "☰  HISTORIAL";
		_btnToggleHistorial.CustomMinimumSize = new Vector2(155, 48);
		_btnToggleHistorial.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_btnToggleHistorial.OffsetLeft = -240; _btnToggleHistorial.OffsetTop = -75;
		_btnToggleHistorial.OffsetRight = -85; _btnToggleHistorial.OffsetBottom = -27;
		_btnToggleHistorial.AddThemeFontSizeOverride("font_size", 15);
		_btnToggleHistorial.AddThemeColorOverride("font_color", new Color(0.95f, 0.97f, 1f));

		var n = new StyleBoxFlat();
		n.BgColor = new Color(0.10f, 0.15f, 0.25f, 0.95f);
		n.BorderWidthLeft = 4;
		n.BorderColor = new Color(0.35f, 0.75f, 0.55f);
		n.CornerRadiusTopLeft = n.CornerRadiusTopRight =
		n.CornerRadiusBottomLeft = n.CornerRadiusBottomRight = 10;
		n.ShadowColor = new Color(0, 0, 0, 0.4f); n.ShadowSize = 4;
		_btnToggleHistorial.AddThemeStyleboxOverride("normal", n);

		var h = new StyleBoxFlat();
		h.BgColor = new Color(0.20f, 0.32f, 0.28f, 1f);
		h.BorderWidthLeft = 4;
		h.BorderColor = new Color(0.55f, 1f, 0.75f);
		h.CornerRadiusTopLeft = h.CornerRadiusTopRight =
		h.CornerRadiusBottomLeft = h.CornerRadiusBottomRight = 10;
		_btnToggleHistorial.AddThemeStyleboxOverride("hover", h);
		_btnToggleHistorial.AddThemeStyleboxOverride("pressed", h);

		_btnToggleHistorial.Pressed += ToggleHistorial;
		CapaHUD().AddChild(_btnToggleHistorial);

		// Panel del historial — oculto por defecto
		_cardHistorial = new PanelContainer();
		_cardHistorial.CustomMinimumSize = new Vector2(320, 360);
		_cardHistorial.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_cardHistorial.OffsetLeft = -340; _cardHistorial.OffsetTop = -450;
		_cardHistorial.OffsetRight = -20; _cardHistorial.OffsetBottom = -90;
		_cardHistorial.Visible = false;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.06f, 0.09f, 0.14f, 0.94f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.30f, 0.75f, 0.55f, 0.6f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 12;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 12;
		sb.ShadowColor = new Color(0, 0, 0, 0.4f); sb.ShadowSize = 6;
		_cardHistorial.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		_cardHistorial.AddChild(vbox);

		var head = new HBoxContainer();
		var titulo = new Label();
		titulo.Text = "HISTORIAL";
		titulo.AddThemeColorOverride("font_color", new Color(0.85f, 0.95f, 0.88f));
		titulo.AddThemeFontSizeOverride("font_size", 15);
		titulo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		head.AddChild(titulo);
		var btnX = new Button();
		btnX.Text = "×";
		btnX.CustomMinimumSize = new Vector2(30, 30);
		btnX.AddThemeFontSizeOverride("font_size", 20);
		btnX.Pressed += ToggleHistorial;
		head.AddChild(btnX);
		vbox.AddChild(head);
		vbox.AddChild(new HSeparator());

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.CustomMinimumSize = new Vector2(0, 260);
		vbox.AddChild(scroll);

		_historialContenido = new VBoxContainer();
		_historialContenido.AddThemeConstantOverride("separation", 4);
		_historialContenido.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(_historialContenido);

		CapaHUD().AddChild(_cardHistorial);
		RegistrarEvento("Partida iniciada", new Color(0.6f, 0.8f, 1f));
	}

	public void RegistrarEvento(string texto, Color acento)
	{
		if (_historialContenido == null) return;
		var linea = new PanelContainer();
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.10f, 0.13f, 0.19f, 0.75f);
		sb.BorderWidthLeft = 3;
		sb.BorderColor = acento;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 4;
		sb.ContentMarginLeft = sb.ContentMarginRight = 8;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 5;
		linea.AddThemeStyleboxOverride("panel", sb);
		var l = new Label();
		l.Text = texto;
		l.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 1f));
		l.AddThemeFontSizeOverride("font_size", 12);
		l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		linea.AddChild(l);
		_historialContenido.AddChild(linea);
		_historialContenido.MoveChild(linea, 0);   // más reciente arriba
		while (_historialContenido.GetChildCount() > 12)
		{
			var last = _historialContenido.GetChild(_historialContenido.GetChildCount() - 1);
			last.QueueFree();
		}
	}

	private void ToggleHistorial()
	{
		if (_cardHistorial == null) return;
		_historialAbierto = !_historialAbierto;
		_cardHistorial.Visible = _historialAbierto;
		_btnToggleHistorial.Text = _historialAbierto ? "☰  CERRAR" : "☰  HISTORIAL";
		if (_historialAbierto)
		{
			_cardHistorial.Modulate = new Color(1, 1, 1, 0);
			var tw = _cardHistorial.CreateTween();
			tw.TweenProperty(_cardHistorial, "modulate:a", 1f, 0.18f);
		}
	}

	// ── PANEL DE HECHIZOS (desplegable, mobile-friendly) ─────────────────
	private PanelContainer _cardHechizos;
	private Button         _btnToggleHechizos;
	private bool           _hechizosAbiertos = false;

	private void CrearPanelHechizos()
	{
		// Botón toggle compacto — siempre visible
		_btnToggleHechizos = new Button();
		_btnToggleHechizos.Name = "BtnToggleHechizos";
		_btnToggleHechizos.Text = "✦  HECHIZOS";
		_btnToggleHechizos.CustomMinimumSize = new Vector2(155, 48);
		_btnToggleHechizos.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		_btnToggleHechizos.OffsetLeft   = 20;
		_btnToggleHechizos.OffsetTop    = -75;
		_btnToggleHechizos.OffsetRight  = 175;
		_btnToggleHechizos.OffsetBottom = -27;
		_btnToggleHechizos.ZIndex = 50;
		_btnToggleHechizos.AddThemeFontSizeOverride("font_size", 17);
		_btnToggleHechizos.AddThemeColorOverride("font_color", new Color(0.95f, 0.97f, 1f));

		var tglN = new StyleBoxFlat();
		tglN.BgColor = new Color(0.10f, 0.15f, 0.25f, 0.95f);
		tglN.BorderWidthLeft = 4;
		tglN.BorderColor = new Color(0.55f, 0.35f, 0.85f);
		tglN.CornerRadiusTopLeft = tglN.CornerRadiusTopRight =
		tglN.CornerRadiusBottomLeft = tglN.CornerRadiusBottomRight = 10;
		tglN.ShadowColor = new Color(0, 0, 0, 0.4f); tglN.ShadowSize = 4;
		_btnToggleHechizos.AddThemeStyleboxOverride("normal", tglN);

		var tglH = new StyleBoxFlat();
		tglH.BgColor = new Color(0.20f, 0.28f, 0.45f, 1f);
		tglH.BorderWidthLeft = 4;
		tglH.BorderColor = new Color(0.75f, 0.55f, 1f);
		tglH.CornerRadiusTopLeft = tglH.CornerRadiusTopRight =
		tglH.CornerRadiusBottomLeft = tglH.CornerRadiusBottomRight = 10;
		_btnToggleHechizos.AddThemeStyleboxOverride("hover", tglH);
		_btnToggleHechizos.AddThemeStyleboxOverride("pressed", tglH);

		_btnToggleHechizos.Pressed += ToggleHechizos;
		CapaHUD().AddChild(_btnToggleHechizos);

		// Panel de hechizos — oculto por defecto
		var card = new PanelContainer();
		card.Name = "PanelHechizos";
		card.CustomMinimumSize = new Vector2(200, 320);
		card.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		card.OffsetLeft   = 20;
		card.OffsetTop    = -410;
		card.OffsetRight  = 220;
		card.OffsetBottom = -90;
		card.ZIndex = 60;
		card.Visible = false;
		_cardHechizos = card;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.06f, 0.09f, 0.14f, 0.92f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.30f, 0.50f, 0.85f, 0.55f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
		sb.ContentMarginLeft = sb.ContentMarginRight = 10;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 10;
		sb.ShadowColor = new Color(0, 0, 0, 0.4f);
		sb.ShadowSize  = 6;
		card.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		card.AddChild(vbox);

		var header = new HBoxContainer();
		var titulo = new Label();
		titulo.Text = "HECHIZOS";
		titulo.AddThemeColorOverride("font_color", new Color(0.85f, 0.90f, 1f));
		titulo.AddThemeFontSizeOverride("font_size", 15);
		titulo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titulo.VerticalAlignment = VerticalAlignment.Center;
		header.AddChild(titulo);
		var btnX = new Button();
		btnX.Text = "×";
		btnX.CustomMinimumSize = new Vector2(30, 30);
		btnX.AddThemeFontSizeOverride("font_size", 20);
		btnX.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1f));
		btnX.Pressed += ToggleHechizos;
		header.AddChild(btnX);
		vbox.AddChild(header);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		_lblInstruccion = new Label();
		_lblInstruccion.Text    = "Haz clic en una\ntropa enemiga";
		_lblInstruccion.Visible = false;
		_lblInstruccion.HorizontalAlignment = HorizontalAlignment.Center;
		_lblInstruccion.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.4f));
		_lblInstruccion.AddThemeFontSizeOverride("font_size", 12);
		vbox.AddChild(_lblInstruccion);

		AgregarBtnHechizo(vbox, "Encebollado", "Buff aliado",   new Color(1f,0.65f,0.15f),  () => UsarEncebollado());
		AgregarBtnHechizo(vbox, "Curación",    "+200 HP",       new Color(0.25f,0.80f,0.35f), () => UsarCuracion());
		AgregarBtnHechizo(vbox, "Robar Carta", "Reponer mano",  new Color(0.30f,0.65f,1f),   () => UsarRobo());
		AgregarBtnHechizo(vbox, "Veneno",      "50 dmg / turno",new Color(0.60f,0.30f,0.75f), () => IniciarSeleccion("veneno"));
		AgregarBtnHechizo(vbox, "Bloqueo",     "Anular 2 turnos",new Color(0.25f,0.55f,0.90f), () => IniciarSeleccion("bloqueo"));

		CapaHUD().AddChild(card);
	}

	private CanvasLayer _capaHUD;
	private CanvasLayer CapaHUD()
	{
		if (_capaHUD != null && IsInstanceValid(_capaHUD)) return _capaHUD;
		_capaHUD = GetNodeOrNull<CanvasLayer>("InterfazMenu");
		if (_capaHUD == null)
		{
			_capaHUD = new CanvasLayer();
			_capaHUD.Name = "HUDLayer";
			_capaHUD.Layer = 10;
			AddChild(_capaHUD);
		}
		return _capaHUD;
	}

	private void ToggleHechizos()
	{
		if (_cardHechizos == null) return;
		_hechizosAbiertos = !_hechizosAbiertos;
		if (_hechizosAbiertos)
		{
			_cardHechizos.Visible = true;
			_cardHechizos.Modulate = new Color(1, 1, 1, 0);
			_cardHechizos.Scale = new Vector2(0.9f, 0.9f);
			_cardHechizos.PivotOffset = new Vector2(0, _cardHechizos.CustomMinimumSize.Y);
			var tw = _cardHechizos.CreateTween().SetParallel(true);
			tw.TweenProperty(_cardHechizos, "modulate:a", 1f, 0.18f);
			tw.TweenProperty(_cardHechizos, "scale", Vector2.One, 0.22f)
			  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			_btnToggleHechizos.Text = "✦  CERRAR";
		}
		else
		{
			var tw = _cardHechizos.CreateTween().SetParallel(true);
			tw.TweenProperty(_cardHechizos, "modulate:a", 0f, 0.14f);
			tw.TweenProperty(_cardHechizos, "scale", new Vector2(0.9f, 0.9f), 0.14f);
			tw.Chain().TweenCallback(Callable.From(() =>
			{ if (IsInstanceValid(_cardHechizos)) _cardHechizos.Visible = false; }));
			_btnToggleHechizos.Text = "✦  HECHIZOS";
		}
	}

	private void AgregarBtnHechizo(BoxContainer parent, string texto, string subtexto, Color color, Action onPress)
	{
		var btn = new Button();
		btn.CustomMinimumSize = new Vector2(0, 44);
		btn.Alignment = HorizontalAlignment.Left;
		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.AddThemeFontSizeOverride("font_size", 13);
		btn.Text = $"  {texto}\n  {subtexto}";
		btn.AutowrapMode = TextServer.AutowrapMode.Off;
		btn.Pressed += () => onPress();

		var sbN = new StyleBoxFlat();
		sbN.BgColor = color * new Color(1f, 1f, 1f, 0.85f);
		sbN.BorderWidthLeft = 3;
		sbN.BorderColor = color;
		sbN.CornerRadiusTopLeft = sbN.CornerRadiusTopRight =
		sbN.CornerRadiusBottomLeft = sbN.CornerRadiusBottomRight = 6;
		sbN.ContentMarginLeft = 4; sbN.ContentMarginRight = 8;
		btn.AddThemeStyleboxOverride("normal", sbN);

		var sbH = new StyleBoxFlat();
		sbH.BgColor = color;
		sbH.BorderWidthLeft = 4;
		sbH.BorderColor = new Color(1f, 1f, 1f, 0.9f);
		sbH.CornerRadiusTopLeft = sbH.CornerRadiusTopRight =
		sbH.CornerRadiusBottomLeft = sbH.CornerRadiusBottomRight = 6;
		sbH.ContentMarginLeft = 4; sbH.ContentMarginRight = 8;
		btn.AddThemeStyleboxOverride("hover", sbH);
		btn.AddThemeStyleboxOverride("pressed", sbH);

		parent.AddChild(btn);
	}

	// ── NÚMEROS FLOTANTES DE DAÑO ─────────────────────────────────────────
	private void MostrarDañoFlotante(Vector2 posGlobal, int cantidad, bool esCuracion = false)
	{
		var lbl = new Label();
		lbl.Text = esCuracion ? $"+{cantidad}" : $"-{cantidad}";
		lbl.AddThemeColorOverride("font_color", esCuracion ? Colors.LightGreen : Colors.Red);
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.ZIndex       = 300;
		lbl.GlobalPosition = posGlobal + new Vector2(-20, -60);
		AddChild(lbl);

		// Flotar hacia arriba y desvanecerse
		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "position:y", lbl.Position.Y - 60f, 0.9f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.9f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	// ── ÍCONOS DE ESTADO (veneno / bloqueo) ───────────────────────────────
	private void ActualizarIconosEstado(Node2D tropa)
	{
		// Limpiar íconos anteriores
		Node iconosViejos = tropa.GetNodeOrNull("IconosEstado");
		iconosViejos?.QueueFree();

		bool envenenado = tropa.HasMeta("envenenado") && ((bool)tropa.GetMeta("envenenado") == true);
		bool bloqueado  = tropa.HasMeta("bloqueado")  && ((bool)tropa.GetMeta("bloqueado")  == true);

		if (!envenenado && !bloqueado) return;

		var contenedor = new HBoxContainer();
		contenedor.Name     = "IconosEstado";
		contenedor.Position = new Vector2(-20, -85);
		tropa.AddChild(contenedor);

		if (envenenado)
		{
			var ico = new Label();
			ico.Text = "☠";
			ico.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.2f));
			ico.AddThemeFontSizeOverride("font_size", 20);
			contenedor.AddChild(ico);
		}
		if (bloqueado)
		{
			var ico = new Label();
			ico.Text = "🔒";
			ico.AddThemeFontSizeOverride("font_size", 20);
			contenedor.AddChild(ico);
		}
	}
}
