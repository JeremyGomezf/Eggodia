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

	// ── PANEL DE HECHIZOS — 2 tarjetas en HUD, ancladas a la esquina inferior derecha
	private const float HECHIZO_W = 100f;
	private const float HECHIZO_H = 140f;
	private const float HECHIZO_GAP = 112f;
	private const float HECHIZO_MARGEN = 20f;
	private float HECHIZO_X;
	private float HECHIZO_Y;

	private void CrearPanelHechizos()
	{
		Vector2 vp = GetViewport().GetVisibleRect().Size;
		float anchoBloque = HECHIZO_W * 2 + HECHIZO_GAP;
		float altoBloque  = HECHIZO_H + 6f + 40f + 44f; // cartas + instrucción + botón CAMBIAR
		HECHIZO_X = vp.X - anchoBloque - HECHIZO_MARGEN;
		HECHIZO_Y = vp.Y - altoBloque - HECHIZO_MARGEN;

		// Barajar pool [0..4] y sacar 2 para la mano inicial
		_poolHechizos = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4 };
		for (int i = 0; i < _poolHechizos.Count; i++)
		{
			int r = random.Next(i, _poolHechizos.Count);
			(_poolHechizos[i], _poolHechizos[r]) = (_poolHechizos[r], _poolHechizos[i]);
		}
		for (int i = 0; i < 2; i++) { _hechizosMano[i] = _poolHechizos[0]; _poolHechizos.RemoveAt(0); }

		// Colocar 2 cartas en HUD
		for (int i = 0; i < 2; i++)
		{
			var card = CrearTarjetaHechizo(i);
			card.Position = new Vector2(HECHIZO_X + i * HECHIZO_GAP, HECHIZO_Y);
			card.Size     = new Vector2(HECHIZO_W, HECHIZO_H);
			CapaHUD().AddChild(card);
		}

		// Instrucción (oculta por defecto)
		_lblInstruccion = new Label();
		_lblInstruccion.Text    = "Toca una tropa\nenemiga";
		_lblInstruccion.Visible = false;
		_lblInstruccion.Position = new Vector2(HECHIZO_X, HECHIZO_Y + HECHIZO_H + 6);
		_lblInstruccion.Size     = new Vector2(HECHIZO_W * 2 + HECHIZO_GAP, 40);
		_lblInstruccion.HorizontalAlignment = HorizontalAlignment.Center;
		_lblInstruccion.AddThemeColorOverride("font_color", new Color(1f, 0.55f, 0.4f));
		_lblInstruccion.AddThemeFontSizeOverride("font_size", 12);
		CapaHUD().AddChild(_lblInstruccion);

		// Botón CAMBIAR
		_btnCambiarHechizo = new Button();
		_btnCambiarHechizo.Text = $"↺ CAMBIAR ({MAX_CAMBIO_HECHIZO})";
		_btnCambiarHechizo.Position = new Vector2(HECHIZO_X, HECHIZO_Y + HECHIZO_H + 50);
		_btnCambiarHechizo.Size     = new Vector2(HECHIZO_W * 2 + HECHIZO_GAP, 40);
		_btnCambiarHechizo.AddThemeFontSizeOverride("font_size", 12);
		_btnCambiarHechizo.AddThemeColorOverride("font_color", Colors.White);
		_btnCambiarHechizo.AddThemeStyleboxOverride("normal",  HudEstilo(new Color(0.22f,0.15f,0.40f,0.95f), new Color(0.65f,0.45f,1f)));
		_btnCambiarHechizo.AddThemeStyleboxOverride("hover",   HudEstilo(new Color(0.38f,0.25f,0.65f),      new Color(0.88f,0.68f,1f)));
		_btnCambiarHechizo.AddThemeStyleboxOverride("pressed", HudEstilo(new Color(0.38f,0.25f,0.65f),      new Color(0.88f,0.68f,1f)));
		_btnCambiarHechizo.Pressed += ActivarModoCambio;
		CapaHUD().AddChild(_btnCambiarHechizo);
	}

	// Reemplaza automáticamente la carta usada con la siguiente del pool
	private void AutoReemplazarHechizo(int slotIdx)
	{
		if (slotIdx < 0 || slotIdx >= 2) return;
		if (_poolHechizos.Count == 0)
		{
			if (_tarjetasHechizo[slotIdx] != null && IsInstanceValid(_tarjetasHechizo[slotIdx]))
				_tarjetasHechizo[slotIdx].QueueFree();
			_tarjetasHechizo[slotIdx] = null;
			return;
		}
		_hechizosMano[slotIdx] = _poolHechizos[0];
		_poolHechizos.RemoveAt(0);

		var padre = _tarjetasHechizo[slotIdx]?.GetParent();
		if (padre != null && IsInstanceValid(_tarjetasHechizo[slotIdx]))
		{
			int   pos     = _tarjetasHechizo[slotIdx].GetIndex();
			Vector2 cardPos = _tarjetasHechizo[slotIdx].Position;
			_tarjetasHechizo[slotIdx].QueueFree();
			var nueva = CrearTarjetaHechizo(slotIdx);
			nueva.Position = cardPos;
			nueva.Size     = new Vector2(HECHIZO_W, HECHIZO_H);
			padre.AddChild(nueva);
			padre.MoveChild(nueva, pos);
		}
	}

	private Panel CrearTarjetaHechizo(int slotIdx)
	{
		int   pi    = _hechizosMano[slotIdx];
		Color color = POOL_HECHIZO_COLOR[pi];

		var panel = new Panel();
		panel.CustomMinimumSize = new Vector2(100, 140);
		panel.Size              = new Vector2(100, 140);
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.07f, 0.05f, 0.14f, 0.96f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = color;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 8;
		sb.ShadowColor = color * new Color(1,1,1,0.4f); sb.ShadowSize = 5;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 4;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.OffsetLeft = 4; vbox.OffsetRight = -4; vbox.OffsetTop = 4; vbox.OffsetBottom = -4;
		vbox.AddThemeConstantOverride("separation", 2);

		var tex = new TextureRect();
		tex.CustomMinimumSize = new Vector2(90, 88);
		tex.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		tex.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
		tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
		tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		var t = GD.Load<Texture2D>(POOL_HECHIZO_RUTA[pi]);
		if (t != null) tex.Texture = t;
		vbox.AddChild(tex);

		var lblN = new Label();
		lblN.Text = POOL_HECHIZO_NOMBRE[pi];
		lblN.AddThemeColorOverride("font_color", color);
		lblN.AddThemeFontSizeOverride("font_size", 10);
		lblN.HorizontalAlignment = HorizontalAlignment.Center;
		lblN.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(lblN);

		var lblE = new Label();
		lblE.Text = "DISPONIBLE";
		lblE.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.55f));
		lblE.AddThemeFontSizeOverride("font_size", 9);
		lblE.HorizontalAlignment = HorizontalAlignment.Center;
		_lblEstadoHechizo[slotIdx] = lblE;
		vbox.AddChild(lblE);

		panel.AddChild(vbox);

		// Overlay "USADO"
		var ov = new Panel();
		ov.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		ov.Visible = false;
		var sbOv = new StyleBoxFlat();
		sbOv.BgColor = new Color(0,0,0,0.68f);
		sbOv.CornerRadiusTopLeft = sbOv.CornerRadiusTopRight =
		sbOv.CornerRadiusBottomLeft = sbOv.CornerRadiusBottomRight = 8;
		ov.AddThemeStyleboxOverride("panel", sbOv);
		var lblUs = new Label();
		lblUs.Text = "USADO";
		lblUs.AddThemeColorOverride("font_color", new Color(1f,0.38f,0.38f));
		lblUs.AddThemeFontSizeOverride("font_size", 15);
		lblUs.SetAnchorsPreset(Control.LayoutPreset.Center);
		lblUs.OffsetLeft = -32; lblUs.OffsetRight = 32;
		lblUs.OffsetTop  = -13; lblUs.OffsetBottom = 13;
		ov.AddChild(lblUs);
		panel.AddChild(ov);
		_overlayHechizo[slotIdx]   = ov;
		_tarjetasHechizo[slotIdx]  = panel;

		int captured = slotIdx;
		panel.GuiInput += (@event) => {
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				EjecutarHechizo(captured);
		};
		return panel;
	}

	// Helper de estilo compartido
	private static StyleBoxFlat HudEstilo(Color bg, Color border, int cornerR = 8, int borderW = 2, Color shadowColor = default)
	{
		var sb = new StyleBoxFlat();
		sb.BgColor = bg;
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = borderW;
		sb.BorderColor = border;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = cornerR;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 5;
		if (shadowColor != default) { sb.ShadowColor = shadowColor; sb.ShadowSize = 4; }
		return sb;
	}

	private void EjecutarHechizo(int slotIdx)
	{
		if (_modoCambioHechizo) { EjecutarCambioHechizo(slotIdx); return; }
		if (!ValidarHechizo() || EsHechizoUsado(slotIdx)) return;
		if (_hechizoUsadoEsteTurno) { MostrarAvisoHechizoLimite(); return; }
		_hechizoUsadoEsteTurno = true; // se compromete al usar este hechizo, aunque falte elegir objetivo
		int pi = _hechizosMano[slotIdx];
		string[] nombresHechizo = { "Curación", "Robar Carta", "Veneno", "Bloqueo", "Encebollado" };
		if (pi >= 0 && pi < nombresHechizo.Length) Preferencias.RegistrarUsoHechizo(nombresHechizo[pi]);
		switch (pi)
		{
			case 0: IniciarSeleccion("curacion",    slotIdx); break;
			case 1: UsarRobo(); AutoReemplazarHechizo(slotIdx); break;
			case 2: IniciarSeleccion("veneno",      slotIdx); break;
			case 3: IniciarSeleccion("bloqueo",     slotIdx); break;
			case 4: IniciarSeleccion("encebollado", slotIdx); break;
		}
		if (pi == 1) ActualizarVisualesHechizos();
	}

	public bool EsHechizoUsado(int slotIdx)
	{
		if (slotIdx < 0 || slotIdx >= 2 || _tarjetasHechizo[slotIdx] == null) return false;
		return _hechizosMano[slotIdx] switch
		{
			0 => usadoCuracion, 1 => usadoRobo,
			2 => usadoVeneno,   3 => usadoBloqueo,  4 => usadoEncebollado,  _ => false
		};
	}

	public void ActualizarVisualesHechizos()
	{
		for (int i = 0; i < 2; i++)
		{
			if (_overlayHechizo[i] == null || !IsInstanceValid(_overlayHechizo[i])) continue;
			bool usado = EsHechizoUsado(i);
			_overlayHechizo[i].Visible = usado;
			if (_lblEstadoHechizo[i] != null && IsInstanceValid(_lblEstadoHechizo[i]))
			{
				_lblEstadoHechizo[i].Text = usado ? "USADO" : "DISPONIBLE";
				_lblEstadoHechizo[i].AddThemeColorOverride("font_color",
					usado ? new Color(1f,0.4f,0.4f) : new Color(0.4f,1f,0.55f));
			}
		}
	}

	private void ActivarModoCambio()
	{
		if (_usosCambioHechizo >= MAX_CAMBIO_HECHIZO) return;
		_modoCambioHechizo = !_modoCambioHechizo;
		_btnCambiarHechizo.Text = _modoCambioHechizo ? "Elige un hechizo..." : $"↺  CAMBIAR ({MAX_CAMBIO_HECHIZO - _usosCambioHechizo})";
		for (int i = 0; i < 2; i++)
			if (_tarjetasHechizo[i] != null && IsInstanceValid(_tarjetasHechizo[i]))
				_tarjetasHechizo[i].Modulate = _modoCambioHechizo && !EsHechizoUsado(i)
					? new Color(1.25f, 1.25f, 0.45f) : Colors.White;
	}

	private void EjecutarCambioHechizo(int slotIdx)
	{
		if (slotIdx < 0 || slotIdx >= 2) { _modoCambioHechizo = false; return; }
		_usosCambioHechizo++;
		_modoCambioHechizo = false;
		AutoReemplazarHechizo(slotIdx);

		bool agotado = _usosCambioHechizo >= MAX_CAMBIO_HECHIZO;
		_btnCambiarHechizo.Text     = agotado ? "↺  CAMBIAR (usado)" : $"↺  CAMBIAR ({MAX_CAMBIO_HECHIZO - _usosCambioHechizo})";
		_btnCambiarHechizo.Disabled = agotado;
		_btnCambiarHechizo.Modulate = agotado ? new Color(0.55f, 0.55f, 0.55f) : Colors.White;
		for (int i = 0; i < 2; i++)
			if (_tarjetasHechizo[i] != null && IsInstanceValid(_tarjetasHechizo[i]))
				_tarjetasHechizo[i].Modulate = Colors.White;
	}

	/// <summary>Texto flotante sobre el panel de hechizos: solo se permite 1 por turno.</summary>
	private void MostrarAvisoHechizoLimite()
	{
		var lbl = new Label();
		lbl.Text = "Solo 1 Hechizo\npor turno";
		lbl.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.35f));
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.Position = new Vector2(HECHIZO_X, HECHIZO_Y - 34);
		lbl.Size     = new Vector2(HECHIZO_W * 2 + HECHIZO_GAP, 30);
		lbl.ZIndex   = 200;
		CapaHUD().AddChild(lbl);

		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "position:y", lbl.Position.Y - 25f, 1.0f);
		tw.TweenProperty(lbl, "modulate:a", 0f, 1.0f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	private void CrearBotonPausa()
	{
		var btn = new Button();
		btn.Text = "⚙";
		btn.CustomMinimumSize = new Vector2(58, 58);
		btn.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		btn.OffsetLeft = 8; btn.OffsetTop = 8;
		btn.OffsetRight = 66; btn.OffsetBottom = 66;
		btn.AddThemeFontSizeOverride("font_size", 26);
		btn.AddThemeColorOverride("font_color", Colors.White);
		btn.ZIndex = 100;

		var n = new StyleBoxFlat();
		n.BgColor = new Color(0.08f, 0.08f, 0.14f, 0.88f);
		n.BorderWidthLeft = n.BorderWidthTop = n.BorderWidthRight = n.BorderWidthBottom = 2;
		n.BorderColor = new Color(0.55f, 0.55f, 0.80f, 0.8f);
		n.CornerRadiusTopLeft = n.CornerRadiusTopRight =
		n.CornerRadiusBottomLeft = n.CornerRadiusBottomRight = 12;
		n.ShadowColor = new Color(0, 0, 0, 0.5f); n.ShadowSize = 5;
		btn.AddThemeStyleboxOverride("normal", n);

		var h = new StyleBoxFlat();
		h.BgColor = new Color(0.20f, 0.20f, 0.35f, 0.96f);
		h.BorderWidthLeft = h.BorderWidthTop = h.BorderWidthRight = h.BorderWidthBottom = 2;
		h.BorderColor = new Color(0.75f, 0.75f, 1f);
		h.CornerRadiusTopLeft = h.CornerRadiusTopRight =
		h.CornerRadiusBottomLeft = h.CornerRadiusBottomRight = 12;
		btn.AddThemeStyleboxOverride("hover", h);
		btn.AddThemeStyleboxOverride("pressed", h);

		btn.Pressed += () => {
			var pausa = GetNodeOrNull<MenuPausa>("MenuPausa");
			pausa?.Pausar();
		};
		CapaHUD().AddChild(btn);
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

			// El ícono es solo un aviso visual pasajero; el veneno en sí sigue activo el resto de sus turnos.
			GetTree().CreateTimer(3.0).Timeout += () => { if (IsInstanceValid(ico)) ico.QueueFree(); };
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
