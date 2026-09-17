using Godot;
using System;
using System.Collections.Generic;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://escenas/menu/MenuConstructor.tscn";
	[Export] public string RutaCampoPruebas    = "res://escenas/gameplay/campo_pruebas.tscn";
	[Export] public string RutaComoJugar       = "res://escenas/menu/PantallaComoJugar.tscn";
	[Export] public string RutaBestiario       = "res://escenas/menu/PantallaBestiario.tscn";
	[Export] public string RutaTienda          = "res://escenas/menu/Tienda.tscn";
	[Export] public string RutaInvocacion      = "res://escenas/SummonTerminal.tscn";

	// Refuerzo de escala por skin en el selector (mismo orden que Preferencias.SKIN_ESCENAS:
	// Rey, Capitán, Dino, Majestad, Paper Dino, Coronel, Huevo Rosa, Majestad II). Ya no hace
	// falta compensar nada: todos los renders de "Huevo render/" vienen al mismo tamaño real.
	// Paper Dino (idx 4) no usa este arreglo — se muestra animado (ver AbrirSelectorSkin).
	// idx 0 (Rey Huevo): el problema real NO era la escala — ReyHuevo_Render.png es mucho más ancho
	// que el PNG viejo (600×661 vs 361×661), así que con KeepAspectCentered dentro de la misma
	// cajita angosta de siempre le sobraba muchísimo espacio vacío arriba/abajo. Se arregló
	// agrandando la cajita en menu_principal.tscn (nodo ReyHuevoCrowned) para que coincida con la
	// proporción real de la imagen nueva — ya no hace falta ningún refuerzo de escala acá.
	private static readonly float[] SKIN_ESCALA_EXTRA = { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };

	// Corrección de centrado horizontal solo dentro del selector de skins (AbrirSelectorSkin) —
	// ya no hace falta con los renders unificados.
	private static readonly float[] SKIN_OFFSET_X_SELECTOR = { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

	// Nodos de animación y UI
	private Control _islaContainer;
	private TextureRect _portalNode;
	private TextureRect _reyHuevoNode;
	private AnimatedSprite2D _reyHuevoAnimado;

	private Label _lblCoins;
	private PanelSettings _panelSettings;
	private PanelContainer _popupDialog;

	// Posiciones iniciales
	private Vector2 _posInicialIsla;
	private Vector2 _posInicialReyHuevo;
	// Escala "de reposo" del huevo equipado (la que fija MostrarHuevoEstatico) — la respiración de
	// _Process() multiplica sobre ESTA, no pisa directo a 1.0. Antes sí lo hacía, así que cualquier
	// escala puesta en MostrarHuevoEstatico se perdía en el primer frame sin que se notara por qué.
	private Vector2 _reyHuevoEscalaBase = Vector2.One;

	private float _tiempoAcumulado = 0f;

	public override void _Ready()
	{
		// Si venimos de una partida en Campo1 (victoria/derrota/pausa), la música global quedó
		// detenida a propósito durante la batalla — se reanuda acá, sea cual sea el camino de vuelta.
		GlobalAudioManager.Instance?.AsegurarReproduccion();

		// 1. Obtener referencias del escenario, portal y huevo coronado
		_islaContainer     = GetNodeOrNull<Control>("IslaContainer");
		_portalNode        = GetNodeOrNull<TextureRect>("IslaContainer/Portal");
		_reyHuevoNode      = GetNodeOrNull<TextureRect>("IslaContainer/ReyHuevoCrowned");
		_reyHuevoAnimado   = GetNodeOrNull<AnimatedSprite2D>("IslaContainer/ReyHuevoAnimado");

		// Guardar posiciones iniciales si los nodos existen
		if (_islaContainer   != null) _posInicialIsla = _islaContainer.Position;
		if (_reyHuevoNode    != null)
		{
			_posInicialReyHuevo = _reyHuevoNode.Position;
			// Aplica la skin activa (y su compensación de escala) ANTES de armar el hover, para
			// que este capture la escala ya correcta como base — si no, al sacar el mouse
			// siempre volvería a la escala de cuando arrancó la escena, no a la de la skin actual.
			ActualizarHuevoMenu();
			_reyHuevoNode.PivotOffset = new Vector2(_reyHuevoNode.Size.X / 2, _reyHuevoNode.Size.Y * 0.8f);
			AgregarAnimacionHover(_reyHuevoNode);
			_reyHuevoNode.MouseFilter = Control.MouseFilterEnum.Stop;
			_reyHuevoNode.GuiInput += (ev) => {
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					AbrirSelectorSkin();
			};
		}

		// 2. Obtener UI de ajustes y diálogos
		_panelSettings = GetNodeOrNull<PanelSettings>("PanelSettings");
		// En el menú principal el panel de ajustes va un poco más chico que en la partida (VS BOT).
		_panelSettings?.FijarEscala(0.9f);
		_popupDialog   = GetNodeOrNull<PanelContainer>("PopupDialog");

		// 3a. Botones del layout antiguo (VBoxContainer)
		var btnJugar = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
		if (btnJugar != null)
		{
			btnJugar.Pressed += () => { if (!TieneMazoCompletoOAvisa()) return; ContextoOnline.Limpiar(); GetTree().ChangeSceneToFile(RutaEscenaJuego); };
			AgregarAnimacionHover(btnJugar);
		}

		var btnOpciones = GetNodeOrNull<Button>("VBoxContainer/OPCIONES");
		if (btnOpciones != null)
		{
			btnOpciones.Pressed += MostrarSettings;
			AgregarAnimacionHover(btnOpciones);
		}

		var btnSalir = GetNodeOrNull<Button>("VBoxContainer/SALIR");
		if (btnSalir != null)
		{
			btnSalir.Pressed += () => GetTree().Quit();
			AgregarAnimacionHover(btnSalir);
		}

		// 3b. Vincular botones principales (Cartas, Tienda, VS Bot, Online)
		var btnCartas = GetNodeOrNull<TextureButton>("IslaContainer/CARTAS") ?? GetNodeOrNull<TextureButton>("CARTAS");
		if (btnCartas != null)
		{
			btnCartas.Pressed += () => GetTree().ChangeSceneToFile(RutaConstructorMazo);
			AgregarAnimacionHover(btnCartas);
		}

		var btnTienda = GetNodeOrNull<TextureButton>("IslaContainer/TIENDA") ?? GetNodeOrNull<TextureButton>("TIENDA");
		if (btnTienda != null)
		{
			btnTienda.Pressed += () => GetTree().ChangeSceneToFile(RutaTienda);
			AgregarAnimacionHover(btnTienda);
		}

		var btnOnline = GetNodeOrNull<BaseButton>("BottomButtons/BtnOnline");
		if (btnOnline != null)
		{
			btnOnline.Pressed += () => { if (TieneMazoCompletoOAvisa()) MostrarPantallaOnline(); };
			AgregarAnimacionHover(btnOnline);
		}

		ConectarBtnTrofeo();

		var btnVsBot = GetNodeOrNull<BaseButton>("BottomButtons/BtnVsBot");
		if (btnVsBot != null)
		{
			btnVsBot.Pressed += () => { if (!TieneMazoCompletoOAvisa()) return; ContextoOnline.Limpiar(); GetTree().ChangeSceneToFile(RutaEscenaJuego); };
			AgregarAnimacionHover(btnVsBot);
		}

		// 4. Panel con los botones secundarios (Bestiario, Cómo Jugar, Pruebas) — visible de forma
		// permanente en su posición de siempre; ya no depende de BtnDev (ver más abajo).
		var secundarios = GetNodeOrNull<Control>("SecondaryButtons");
		if (secundarios != null)
		{
			var btnBestiario = secundarios.GetNodeOrNull<Button>("BtnBestiario");
			if (btnBestiario != null)
			{
				btnBestiario.Pressed += () => GetTree().ChangeSceneToFile(RutaBestiario);
				AgregarAnimacionHover(btnBestiario);
			}

			var btnComoJugar = secundarios.GetNodeOrNull<Button>("BtnComoJugar");
			if (btnComoJugar != null)
			{
				btnComoJugar.Pressed += () => GetTree().ChangeSceneToFile(RutaComoJugar);
				AgregarAnimacionHover(btnComoJugar);
			}

			var btnPruebas = secundarios.GetNodeOrNull<Button>("BtnPruebas");
			if (btnPruebas != null)
			{
				btnPruebas.Pressed += () => GetTree().ChangeSceneToFile(RutaCampoPruebas);
				AgregarAnimacionHover(btnPruebas);
			}

			var btnInvocar = secundarios.GetNodeOrNull<Button>("BtnInvocarCarta");
			if (btnInvocar != null)
			{
				btnInvocar.Pressed += () => GetTree().ChangeSceneToFile(RutaInvocacion);
				AgregarAnimacionHover(btnInvocar);
			}
		}

		// 4b. BtnDev: ahora abre "Ingresar Código" (ver MenuPrincipal.Codigos.cs) — ya no lleva al
		// Campo de Pruebas (ese acceso sigue disponible solo desde BtnPruebas, en SecondaryButtons).
		var btnDev = GetNodeOrNull<BaseButton>("BtnDev");
		if (btnDev != null)
		{
			btnDev.Pressed += MostrarPantallaCodigos;
			AgregarAnimacionHover(btnDev);
		}

		// 5. Vincular Ajustes y Cierre de Popups
		var btnSettings = GetNodeOrNull<TextureButton>("BtnSettings");
		if (btnSettings != null)
		{
			btnSettings.Pressed += MostrarSettings;
			AgregarAnimacionHover(btnSettings);
		}

		// 5b. HUD superior: nombre/nivel del jugador + perfil al hacer clic en UserPanel.
		var userPanel = GetNodeOrNull<Control>("TopHUD/UserPanel");
		if (userPanel != null)
		{
			var lblUser = userPanel.GetNodeOrNull<Label>("Label");
			if (lblUser != null) lblUser.Text = SesionJuego.Instance?.NombreJugador ?? "Invitado";
			userPanel.MouseFilter = Control.MouseFilterEnum.Stop;
			userPanel.GuiInput += (ev) => {
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					AbrirPerfil();
			};
			AgregarAnimacionHover(userPanel);
		}

		var lblNivel = GetNodeOrNull<Label>("TopHUD/LevelPanel/Label");
		if (lblNivel != null) lblNivel.Text = $"Nv. {Preferencias.Nivel}";

		var btnClosePopup = GetNodeOrNull<Button>("PopupDialog/VBox/BtnClosePopup");
		if (btnClosePopup != null)
		{
			btnClosePopup.Pressed += OcultarPopup;
			AgregarAnimacionHover(btnClosePopup);
		}

		// 6. Conectar dinámicamente el HUD de Monedas superior
		_lblCoins = GetNodeOrNull<Label>("TopHUD/CoinsPanel/HBox/LabelVal");
		var eco = Economia.Instancia();
		if (eco != null)
		{
			eco.MonedasCambiaron += OnMonedasCambiaron;
			OnMonedasCambiaron(eco.Monedas);
		}

		// 7. Primer inicio del juego: abrir el tutorial automáticamente (una sola vez)
		if (!Preferencias.TutorialVisto)
		{
			Preferencias.TutorialVisto = true;
			Callable.From(AbrirComoJugar).CallDeferred();
		}

		// El chequeo de versión nueva del APK ahora corre AL INICIO, en la PantallaCarga (antes del
		// login), no aquí — así un update obligatorio bloquea desde el arranque. Ver PantallaCarga.cs.
	}

	public override void _ExitTree()
	{
		if (Economia.Instance != null)
		{
			Economia.Instance.MonedasCambiaron -= OnMonedasCambiaron;
		}
	}

	public override void _Process(double delta)
	{
		_tiempoAcumulado += (float)delta;

		// A. Portal girando continuamente
		if (_portalNode != null)
		{
			_portalNode.Rotation += 0.35f * (float)delta;
		}

		// B. Isla flotando en el cielo (bobbing suave)
		if (_islaContainer != null)
		{
			_islaContainer.Position = new Vector2(
				_posInicialIsla.X,
				_posInicialIsla.Y + MathF.Sin(_tiempoAcumulado * 1.5f) * 8f
			);
		}

		// C. Huevo Coronado (Rey Huevo) visible y respirando suavemente en el centro del nido —
		// multiplica sobre _reyHuevoEscalaBase (la escala real de la skin equipada), no pisa a 1.0.
		// Antes era rápida y marcada (2.2 de frecuencia, hasta 2% de escala); ahora mucho más lenta
		// y sutil, como se pidió ("suave suave suave").
		if (_reyHuevoNode != null)
		{
			float wobbleY = 1.0f + MathF.Sin(_tiempoAcumulado * 0.9f) * 0.008f;
			float wobbleX = 1.0f - MathF.Sin(_tiempoAcumulado * 0.9f) * 0.005f;
			_reyHuevoNode.Scale = new Vector2(_reyHuevoEscalaBase.X * wobbleX, _reyHuevoEscalaBase.Y * wobbleY);
			_reyHuevoNode.Position = new Vector2(
				_posInicialReyHuevo.X,
				_posInicialReyHuevo.Y + MathF.Sin(_tiempoAcumulado * 2.2f) * 3f
			);
		}
	}

	private void OnMonedasCambiaron(int total)
	{
		if (_lblCoins != null)
		{
			_lblCoins.Text = total.ToString();
		}
	}

	private void AbrirComoJugar()
	{
		Preferencias.TutorialVisto = true;
		GetTree().ChangeSceneToFile(RutaComoJugar);
	}

	private void AgregarAnimacionHover(Control btn)
	{
		Vector2 escalaBase = btn.Scale;
		btn.PivotOffset = btn.Size / 2;
		btn.Resized += () => btn.PivotOffset = btn.Size / 2;

		btn.MouseEntered += () =>
		{
			// Recaptura la escala base por si cambió desde afuera (ej. el huevo de la isla
			// cambia de escala al equipar otra skin) — si no, el mouse-exit volvería siempre
			// a la escala de cuando se armó el hover, no a la actual.
			escalaBase = btn.Scale;
			var tween = btn.CreateTween();
			tween.TweenProperty(btn, "scale", escalaBase * 1.08f, 0.15f)
				 .SetTrans(Tween.TransitionType.Back)
				 .SetEase(Tween.EaseType.Out);
		};
		btn.MouseExited += () => 
		{
			var tween = btn.CreateTween();
			tween.TweenProperty(btn, "scale", escalaBase, 0.15f)
				 .SetTrans(Tween.TransitionType.Sine)
				 .SetEase(Tween.EaseType.Out);
		};

		if (btn is BaseButton baseBtn)
		{
			baseBtn.ButtonDown += () =>
			{
				if (btn.Name == "CARTAS" || btn.Name == "TIENDA")
				{
					btn.SelfModulate = new Color(0.5f, 0.5f, 0.5f);
				}
			};

			baseBtn.ButtonUp += () =>
			{
				if (btn.Name == "CARTAS" || btn.Name == "TIENDA")
				{
					btn.SelfModulate = Colors.White;
				}
			};
		}
	}

	private void AbrirSelectorSkin()
	{
		// Evitar doble apertura
		if (GetNodeOrNull("SelectorSkin") != null) return;

		var overlay = new ColorRect();
		overlay.Name = "SelectorSkin";
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0, 0, 0, 0.78f);
		overlay.ZIndex = 200;
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.CustomMinimumSize = new Vector2(820, 420);
		panel.OffsetLeft = -410; panel.OffsetRight = 410;
		panel.OffsetTop  = -210; panel.OffsetBottom = 210;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.06f, 0.08f, 0.16f, 0.98f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(1f, 0.80f, 0.25f);
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 14;
		sb.ContentMarginLeft = sb.ContentMarginRight =
		sb.ContentMarginTop  = sb.ContentMarginBottom = 16;
		sb.ShadowColor = new Color(0,0,0,0.6f); sb.ShadowSize = 10;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		// Cabecera
		var header = new HBoxContainer();
		var lblTitulo = new Label();
		lblTitulo.Text = "SELECCIONA TU HUEVO";
		lblTitulo.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
		lblTitulo.AddThemeFontSizeOverride("font_size", 22);
		lblTitulo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(lblTitulo);
		var btnX = new Button();
		btnX.Text = "✕";
		btnX.CustomMinimumSize = new Vector2(40, 40);
		btnX.AddThemeFontSizeOverride("font_size", 18);
		btnX.Pressed += () => overlay.QueueFree();
		header.AddChild(btnX);
		vbox.AddChild(header);

		// Grid de skins en una sola fila, envuelto en un ScrollTactil horizontal — con 8 skins ya no
		// entran todas a la vez en el panel; se arrastra/desliza para ver el resto (con el dedo o el
		// mouse en cualquier parte del contenido, además de la barra nativa, que sigue disponible).
		var scrollSkins = new ScrollTactil();
		scrollSkins.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scrollSkins.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
		scrollSkins.CustomMinimumSize = new Vector2(0, 260);
		vbox.AddChild(scrollSkins);

		var grid = new GridContainer();
		grid.Columns = Preferencias.SKIN_NOMBRES.Length + 4;
		grid.AddThemeConstantOverride("h_separation", 14);
		grid.AddThemeConstantOverride("v_separation", 12);
		scrollSkins.AddChild(grid);

		string exclusivaActiva = Preferencias.SkinExclusivaActiva;

		for (int i = 0; i < Preferencias.SKIN_NOMBRES.Length; i++)
		{
			int capI = i;
			bool poseida = Preferencias.TieneSkin(i);
			// El selector solo lista lo que ya tienes — una cuenta nueva solo ve Rey Huevo (idx 0,
			// siempre poseída) hasta que compre/canjee más. La Tienda sigue mostrando el catálogo
			// completo para comprar lo que falta.
			if (!poseida) continue;
			bool activa  = string.IsNullOrEmpty(exclusivaActiva) && Preferencias.SkinActivaIdx == i;

			var skinPanel = new PanelContainer();
			skinPanel.CustomMinimumSize = new Vector2(190, 230);

			var sbSkin = new StyleBoxFlat();
			sbSkin.BgColor = activa ? new Color(0.12f, 0.22f, 0.10f) : new Color(0.08f, 0.10f, 0.20f, 0.95f);
			sbSkin.BorderWidthLeft = sbSkin.BorderWidthTop = sbSkin.BorderWidthRight = sbSkin.BorderWidthBottom = 2;
			sbSkin.BorderColor = activa ? new Color(0.4f, 1f, 0.4f)
							  : poseida ? new Color(0.85f, 0.65f, 0.2f)
							  :           new Color(0.35f, 0.35f, 0.55f);
			sbSkin.CornerRadiusTopLeft = sbSkin.CornerRadiusTopRight =
			sbSkin.CornerRadiusBottomLeft = sbSkin.CornerRadiusBottomRight = 10;
			sbSkin.ContentMarginLeft = sbSkin.ContentMarginRight =
			sbSkin.ContentMarginTop  = sbSkin.ContentMarginBottom = 8;
			skinPanel.AddThemeStyleboxOverride("panel", sbSkin);

			var svbox = new VBoxContainer();
			svbox.AddThemeConstantOverride("separation", 6);

			var tex = new TextureRect();
			tex.CustomMinimumSize = new Vector2(130, 140); // caja más cuadrada — antes 130x195 (alargada)
			tex.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			var txImg = GD.Load<Texture2D>(Preferencias.SKIN_IMAGENES[capI]);
			if (txImg != null) tex.Texture = txImg;
			tex.PivotOffset = tex.CustomMinimumSize / 2f;
			// Grayscale para skins no poseídas
			if (!poseida) tex.Modulate = new Color(0.4f, 0.4f, 0.4f);
			svbox.AddChild(tex);

			var lblN = new Label();
			lblN.Text = Preferencias.SKIN_NOMBRES[capI];
			lblN.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.80f));
			lblN.AddThemeFontSizeOverride("font_size", 12);
			lblN.HorizontalAlignment = HorizontalAlignment.Center;
			lblN.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			svbox.AddChild(lblN);

			if (activa)
			{
				var lbl = new Label();
				lbl.Text = "✓ EQUIPADA";
				lbl.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
				lbl.AddThemeFontSizeOverride("font_size", 11);
				lbl.HorizontalAlignment = HorizontalAlignment.Center;
				svbox.AddChild(lbl);
			}
			else
			{
				var btnEquip = new Button();
				btnEquip.Text = "EQUIPAR";
				btnEquip.CustomMinimumSize = new Vector2(0, 32);
				btnEquip.AddThemeFontSizeOverride("font_size", 12);
				btnEquip.Pressed += () => {
					Preferencias.SkinExclusivaActiva = "";
					Preferencias.SkinActivaIdx = capI;
					overlay.QueueFree();
					// Actualizar la imagen del huevo visible en el menú
					ActualizarHuevoMenu();
				};
				svbox.AddChild(btnEquip);
			}

			skinPanel.AddChild(svbox);
			grid.AddChild(skinPanel);
		}

		// ── SKINS EXCLUSIVAS (Huevo Ecotec + Skins Fijas de Devs) ──────────
		int userIdActual = SesionJuego.Instance != null ? SesionJuego.Instance.UsuarioId : 0;
		string nombreUsuario = (SesionJuego.Instance != null ? SesionJuego.Instance.NombreJugador : "").ToLowerInvariant();

		var skinsExclusivas = new (string nombre, string ruta, int devId, string claveDev)[]
		{
			("Huevo Dorado", "res://imagenes/RendersTropa/Huevo render/HuevoDorado_Render.png", -1, ""),
			("Huevo Ecotec", "res://imagenes/RendersTropa/Huevo render/HuevoEcotec_Render.png",  -1, ""),
			("Jeremi Huevo", "res://imagenes/RendersTropa/Huevo render/JeremyHuevo_Render.png",  1, "jeremy"),
			("Carlos Huevo", "res://imagenes/RendersTropa/Huevo render/CarlosHuevo_Render.png",  4, "kankox"),
			("Gonza Huevo",  "res://imagenes/RendersTropa/Huevo render/GonzaHuevo_Render.png",   2, "gonza"),
		};

		foreach (var (nombreExc, rutaExc, devId, claveDev) in skinsExclusivas)
		{
			bool esDev = (devId > 0 && userIdActual == devId) || (!string.IsNullOrEmpty(claveDev) && nombreUsuario.Contains(claveDev));
			bool poseida = esDev || Preferencias.TieneSkinExclusiva(rutaExc);
			bool activa = exclusivaActiva == rutaExc;

			// El selector solo lista lo que ya tienes: skins de dev ocultas para cualquier otra
			// cuenta (mantiene la exclusividad), y skins por código ocultas hasta canjearlas.
			if (!poseida) continue;

			var skinPanel = new PanelContainer();
			skinPanel.CustomMinimumSize = new Vector2(190, 230);

			var sbSkin = new StyleBoxFlat();
			sbSkin.BgColor = activa ? new Color(0.15f, 0.25f, 0.12f) : new Color(0.12f, 0.08f, 0.22f, 0.95f);
			sbSkin.BorderWidthLeft = sbSkin.BorderWidthTop = sbSkin.BorderWidthRight = sbSkin.BorderWidthBottom = 2;
			sbSkin.BorderColor = activa ? new Color(0.4f, 1f, 0.4f)
							  : poseida ? new Color(1f, 0.85f, 0.25f)
							  :           new Color(0.35f, 0.35f, 0.55f);
			sbSkin.CornerRadiusTopLeft = sbSkin.CornerRadiusTopRight =
			sbSkin.CornerRadiusBottomLeft = sbSkin.CornerRadiusBottomRight = 10;
			sbSkin.ContentMarginLeft = sbSkin.ContentMarginRight =
			sbSkin.ContentMarginTop  = sbSkin.ContentMarginBottom = 8;
			skinPanel.AddThemeStyleboxOverride("panel", sbSkin);

			var svbox = new VBoxContainer();
			svbox.AddThemeConstantOverride("separation", 6);

			var tex = new TextureRect();
			tex.CustomMinimumSize = new Vector2(130, 140);
			tex.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			if (ResourceLoader.Exists(rutaExc)) tex.Texture = GD.Load<Texture2D>(rutaExc);
			tex.PivotOffset = tex.CustomMinimumSize / 2f;
			if (!poseida) tex.Modulate = new Color(0.4f, 0.4f, 0.4f);
			svbox.AddChild(tex);

			var lblN = new Label();
			lblN.Text = nombreExc + "\n★ EXCLUSIVO ★";
			lblN.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
			lblN.AddThemeFontSizeOverride("font_size", 11);
			lblN.HorizontalAlignment = HorizontalAlignment.Center;
			lblN.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			svbox.AddChild(lblN);

			if (activa)
			{
				var lbl = new Label();
				lbl.Text = "✓ EQUIPADA";
				lbl.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.45f));
				lbl.AddThemeFontSizeOverride("font_size", 11);
				lbl.HorizontalAlignment = HorizontalAlignment.Center;
				svbox.AddChild(lbl);
			}
			else
			{
				var btnEquip = new Button();
				btnEquip.Text = "EQUIPAR";
				btnEquip.CustomMinimumSize = new Vector2(0, 32);
				btnEquip.AddThemeFontSizeOverride("font_size", 12);
				string r = rutaExc;
				btnEquip.Pressed += () => {
					Preferencias.SkinExclusivaActiva = r;
					overlay.QueueFree();
					ActualizarHuevoMenu();
				};
				svbox.AddChild(btnEquip);
			}

			skinPanel.AddChild(svbox);
			grid.AddChild(skinPanel);
		}

		overlay.AddChild(panel);

		// Animación de entrada
		panel.Scale = new Vector2(0.7f, 0.7f);
		panel.PivotOffset = panel.CustomMinimumSize / 2;
		var tw = panel.CreateTween();
		tw.TweenProperty(panel, "scale", Vector2.One, 0.22f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private const string RUTA_REY_HUEVO_CORONADO = "res://imagenes/RendersTropa/Huevo render/ReyHuevo_Render.png";
	// Paper Dino Huevo: en el selector y la Tienda usa el render estático como todos (ya arreglado),
	// pero en el MENÚ PRINCIPAL, cuando es la skin equipada, se pidió mostrarla animada — mismos
	// sprite frames que su ficha de personaje — pero calculada para verse al mismo tamaño que el
	// resto. La caja de referencia (ReyHuevoCrowned, ver menu_principal.tscn) mide 317.7×350 tras el
	// arreglo de proporción; el frame de la animación mide 942×1057, así que escala =
	// min(317.7/942, 350/1057) ≈ 0.331 reproduce el mismo criterio "cabe completo, centrado" que
	// usa KeepAspectCentered para las demás.
	private const int IDX_PAPER_DINO = 4; // Preferencias.SKIN_ESCENAS[4]

	/// <summary>Refleja en el menú principal la skin de huevo equipada (soporta tanto catálogo estándar como exclusivas).</summary>
	private void ActualizarHuevoMenu()
	{
		if (_reyHuevoNode == null) return;

		string exclusiva = Preferencias.SkinExclusivaActiva;
		if (!string.IsNullOrEmpty(exclusiva) && ResourceLoader.Exists(exclusiva))
		{
			MostrarHuevoEstatico(exclusiva, Vector2.One);
			return;
		}

		int idx = Preferencias.SkinActivaIdx;

		if (idx == IDX_PAPER_DINO)
		{
			MostrarHuevoAnimado();
			return;
		}

		string ruta = idx == 0 ? RUTA_REY_HUEVO_CORONADO : Preferencias.SKIN_IMAGENES[idx];
		float escalaExtra = idx < SKIN_ESCALA_EXTRA.Length ? SKIN_ESCALA_EXTRA[idx] : 1.0f;
		MostrarHuevoEstatico(ruta, new Vector2(escalaExtra, escalaExtra));
	}

	/// <summary>Muestra el huevo equipado como TextureRect estático y oculta la versión animada.
	/// _reyHuevoNode se mantiene siempre Visible=true (nunca se apaga) porque es el único nodo con
	/// el GuiInput que abre el selector de skin — para ocultarlo visualmente se usa alpha 0, nunca
	/// Visible=false.</summary>
	private void MostrarHuevoEstatico(string ruta, Vector2 escala)
	{
		var tex = GD.Load<Texture2D>(ruta);
		if (tex != null) _reyHuevoNode.Texture = tex;
		_reyHuevoEscalaBase = escala;
		_reyHuevoNode.Scale = escala;
		_reyHuevoNode.Modulate = Colors.White;
		if (_reyHuevoAnimado != null) _reyHuevoAnimado.Visible = false;
	}

	/// <summary>Paper Dino Huevo equipado en el menú principal: se muestra animada (idle real),
	/// tapando al TextureRect normal (que sigue detrás, transparente, para conservar el clic que
	/// abre el selector).</summary>
	private void MostrarHuevoAnimado()
	{
		if (_reyHuevoAnimado == null) { MostrarHuevoEstatico(RUTA_REY_HUEVO_CORONADO, Vector2.One); return; }
		_reyHuevoNode.Modulate = new Color(1, 1, 1, 0);
		_reyHuevoAnimado.Visible = true;
		if (_reyHuevoAnimado.SpriteFrames != null && _reyHuevoAnimado.SpriteFrames.HasAnimation("idle"))
			_reyHuevoAnimado.Play("idle");
	}

	// ── PERFIL DEL JUGADOR ────────────────────────────────────────────────────
	// Tamaño de referencia del huevo del menú principal (IslaContainer/ReyHuevoCrowned):
	// 195×350 — todas las imágenes de huevo en overlays deben verse a esa misma escala.
	private static readonly Vector2 TAMAÑO_HUEVO_REFERENCIA = new Vector2(195, 350);

	private void AbrirPerfil()
	{
		if (GetNodeOrNull("PerfilJugador") != null) return;

		// Capa casi transparente: solo atrapa el clic para poder cerrar tocando afuera,
		// sin oscurecer el fondo (mismo criterio visual que PanelSettings, que no tiene backdrop).
		var overlay = new ColorRect();
		overlay.Name = "PerfilJugador";
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0, 0, 0, 0.12f);
		overlay.ZIndex = 200;
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		var panel = new PanelContainer();
		// Anclado y centrado con offsets fijos (no solo AnchorsPreset) — así queda centrado de
		// verdad sin importar el tamaño final del contenido, igual que el selector de skins.
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.OffsetLeft = -300; panel.OffsetRight = 300;
		panel.OffsetTop  = -310; panel.OffsetBottom = 310;
		panel.CustomMinimumSize = new Vector2(600, 0);
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		panel.GuiInput += (ev) => { if (ev is InputEventMouseButton) AcceptEvent(); };

		// Cierra AMBOS nodos (antes solo se liberaba "overlay" y el panel se quedaba pegado).
		void Cerrar()
		{
			if (IsInstanceValid(overlay)) overlay.QueueFree();
			if (IsInstanceValid(panel))   panel.QueueFree();
		}
		overlay.GuiInput += (ev) => { if (ev is InputEventMouseButton mb && mb.Pressed) Cerrar(); };

		// Vidrio semitransparente tipo PanelSettings, con borde neón — se ve el fondo del
		// menú a través, en vez del recuadro casi opaco de antes.
		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.10f, 0.13f, 0.20f, 0.82f);
		sb.BorderWidthLeft = sb.BorderWidthTop = sb.BorderWidthRight = sb.BorderWidthBottom = 2;
		sb.BorderColor = new Color(0.3f, 0.9f, 1.0f, 0.85f); // borde neón cian
		sb.ShadowColor = new Color(0.2f, 0.9f, 1.0f, 0.35f);
		sb.ShadowSize = 14;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 18;
		sb.ContentMarginLeft = sb.ContentMarginRight = 26;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 20;
		panel.AddThemeStyleboxOverride("panel", sb);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		// Cabecera: nombre + X
		var header = new HBoxContainer();
		var lblNombre = new Label();
		lblNombre.Text = SesionJuego.Instance?.NombreJugador ?? "Invitado";
		lblNombre.AddThemeColorOverride("font_color", new Color(0.4f, 0.95f, 1.0f));
		lblNombre.AddThemeFontSizeOverride("font_size", 26);
		lblNombre.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(lblNombre);
		var btnX = new Button();
		btnX.Text = "✕";
		btnX.CustomMinimumSize = new Vector2(40, 40);
		btnX.Pressed += Cerrar;
		header.AddChild(btnX);
		vbox.AddChild(header);

		// Fila superior: huevo equipado (a tamaño real) + carta más usada (en su propio cuadro).
		var filaTop = new HBoxContainer();
		filaTop.AddThemeConstantOverride("separation", 24);
		filaTop.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(filaTop);

		var colSkin = new VBoxContainer();
		colSkin.AddThemeConstantOverride("separation", 6);
		var tex = new TextureRect();
		tex.CustomMinimumSize = TAMAÑO_HUEVO_REFERENCIA;
		tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
		tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		int idxSkin = Preferencias.SkinActivaIdx;
		string rutaSkin = idxSkin == 0 ? RUTA_REY_HUEVO_CORONADO : Preferencias.SKIN_IMAGENES[idxSkin];
		var txSkin = GD.Load<Texture2D>(rutaSkin);
		if (txSkin != null) tex.Texture = txSkin;
		tex.PivotOffset = tex.CustomMinimumSize / 2f;
		float escalaExtraPerfil = idxSkin < SKIN_ESCALA_EXTRA.Length ? SKIN_ESCALA_EXTRA[idxSkin] : 1.0f;
		tex.Scale = new Vector2(escalaExtraPerfil, escalaExtraPerfil);
		colSkin.AddChild(tex);
		var lblSkin = new Label();
		lblSkin.Text = Preferencias.SKIN_NOMBRES[idxSkin];
		lblSkin.HorizontalAlignment = HorizontalAlignment.Center;
		lblSkin.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 1f));
		colSkin.AddChild(lblSkin);
		filaTop.AddChild(colSkin);

		// Cuadro de la carta más usada (mismo tratamiento visual que un cuadro de hechizo/trampa).
		var (nombreCarta, imgCarta) = ResolverCartaPorRuta(Preferencias.CartaMasUsada());
		var colCarta = new VBoxContainer();
		colCarta.AddThemeConstantOverride("separation", 6);
		var cartaFrame = new PanelContainer();
		cartaFrame.CustomMinimumSize = new Vector2(150, 210);
		var sbCarta = new StyleBoxFlat();
		sbCarta.BgColor = new Color(0.05f, 0.06f, 0.10f, 0.9f);
		sbCarta.BorderWidthLeft = sbCarta.BorderWidthTop = sbCarta.BorderWidthRight = sbCarta.BorderWidthBottom = 2;
		sbCarta.BorderColor = new Color(0.95f, 0.78f, 0.25f, 0.9f); // borde dorado, como una carta
		sbCarta.CornerRadiusTopLeft = sbCarta.CornerRadiusTopRight =
		sbCarta.CornerRadiusBottomLeft = sbCarta.CornerRadiusBottomRight = 12;
		cartaFrame.AddThemeStyleboxOverride("panel", sbCarta);
		if (imgCarta != null)
		{
			var texCarta = new TextureRect();
			texCarta.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			texCarta.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			texCarta.Texture = imgCarta;
			cartaFrame.AddChild(texCarta);
		}
		else
		{
			var lblVacio = new Label();
			lblVacio.Text = "—";
			lblVacio.HorizontalAlignment = HorizontalAlignment.Center;
			lblVacio.VerticalAlignment   = VerticalAlignment.Center;
			lblVacio.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
			cartaFrame.AddChild(lblVacio);
		}
		colCarta.AddChild(cartaFrame);
		var lblCartaNombre = new Label();
		lblCartaNombre.Text = nombreCarta ?? "Carta más usada";
		lblCartaNombre.HorizontalAlignment = HorizontalAlignment.Center;
		lblCartaNombre.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 1f));
		colCarta.AddChild(lblCartaNombre);
		filaTop.AddChild(colCarta);

		vbox.AddChild(new HSeparator());

		void Fila(string etiqueta, string valor)
		{
			var fila = new HBoxContainer();
			var k = new Label(); k.Text = etiqueta; k.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			k.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
			var v = new Label(); v.Text = valor;
			v.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.5f));
			fila.AddChild(k); fila.AddChild(v);
			vbox.AddChild(fila);
		}

		Fila("Nivel",              Preferencias.Nivel.ToString());
		Fila("Experiencia",        $"{Preferencias.ExperienciaTotal} XP");
		Fila("Monedas",            (Economia.Instancia()?.Monedas ?? 0).ToString());
		Fila("Partidas ganadas",   Preferencias.PartidasGanadas.ToString());
		Fila("Partidas perdidas",  Preferencias.PartidasPerdidas.ToString());
		Fila("Hechizo más usado",  Preferencias.HechizoMasUsado() ?? "—");

		AddChild(panel);
		panel.Scale = new Vector2(0.7f, 0.7f);
		panel.PivotOffset = panel.Size / 2;
		var tw = panel.CreateTween();
		tw.TweenProperty(panel, "scale", Vector2.One, 0.22f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private Dictionary<string, CartaData> _cacheCartaData;

	/// <summary>Resuelve (nombre, ilustración) de la CartaData cuya RutaEscena coincide con
	/// <paramref name="rutaEscena"/> (la clave que guarda Preferencias.CartaMasUsada()). Mismo
	/// criterio de cruce que usa Campo1 para el MVT de fin de partida.</summary>
	private (string nombre, Texture2D imagen) ResolverCartaPorRuta(string rutaEscena)
	{
		if (string.IsNullOrEmpty(rutaEscena)) return (null, null);

		if (_cacheCartaData == null)
		{
			_cacheCartaData = new Dictionary<string, CartaData>();
			using var dir = DirAccess.Open("res://DatosCartas");
			if (dir != null)
			{
				dir.ListDirBegin();
				string archivo = dir.GetNext();
				while (archivo != "")
				{
					if (archivo.EndsWith(".tres"))
					{
						var datos = GD.Load<CartaData>($"res://DatosCartas/{archivo}");
						if (datos != null && !string.IsNullOrEmpty(datos.RutaEscena))
							_cacheCartaData[datos.RutaEscena] = datos;
					}
					archivo = dir.GetNext();
				}
			}
		}

		return _cacheCartaData.TryGetValue(rutaEscena, out var carta)
			? (carta.Nombre, carta.Imagen)
			: (null, null);
	}

	private void MostrarSettings()
	{
		if (_panelSettings != null) _panelSettings.Visible = true;
	}

	/// <summary>Antes esta validación pasaba al confirmar el mazo en el Constructor (botón
	/// SELECCIONAR, ya reemplazado por LIMPIAR); ahora el mazo se autoguarda ahí sin bloquear nada,
	/// así que el aviso de "completa tu mazo" se movió acá — justo antes de arrancar una partida
	/// (VS BOT, ONLINE o el botón JUGAR viejo del VBoxContainer). Revisa TROPAS (8 obligatorias) Y
	/// ARDIDES (mínimo SesionJuego.MIN_ARDIDES_JUGAR) — antes solo se podía jugar sin ningún ardid
	/// equipado, cosa que ya no se permite.</summary>
	private bool TieneMazoCompletoOAvisa()
	{
		bool tieneMazo   = SesionJuego.Instance != null && SesionJuego.Instance.TieneMazo;
		int  nArdides    = SesionJuego.Instance?.ArdidesSeleccionados?.Count ?? 0;
		bool tieneArdides = nArdides >= SesionJuego.MIN_ARDIDES_JUGAR;

		if (tieneMazo && tieneArdides) return true;

		string mensaje = (!tieneMazo && !tieneArdides)
			? $"Equípate 8 tropas y al menos {SesionJuego.MIN_ARDIDES_JUGAR} ardides (hechizos) para jugar."
			: !tieneMazo
				? "Completa tu mazo de 8 cartas de tropa en el menú de Cartas antes de jugar."
				: $"Equípate al menos {SesionJuego.MIN_ARDIDES_JUGAR} ardides (hechizos) en el menú de Cartas antes de jugar.";

		MostrarPopup("Mazo incompleto", mensaje);
		return false;
	}

	private void MostrarPopup(string titulo, string mensaje)
	{
		if (_popupDialog != null)
		{
			// Único uso hoy es el aviso de mazo incompleto (VS BOT/ONLINE/JUGAR) — se pidió que
			// esta interfaz se vea más grande, así que se agranda acá directo (no hay otro caso
			// que dependa del tamaño chico anterior).
			var lblTitulo  = _popupDialog.GetNode<Label>("VBox/Title");
			var lblMensaje = _popupDialog.GetNode<Label>("VBox/Message");
			lblTitulo.Text = titulo;
			lblTitulo.AddThemeFontSizeOverride("font_size", 32);
			lblMensaje.Text = mensaje;
			lblMensaje.AddThemeFontSizeOverride("font_size", 22);
			lblMensaje.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			lblMensaje.CustomMinimumSize = new Vector2(520, 0);
			_popupDialog.CustomMinimumSize = new Vector2(600, 0);
			_popupDialog.Visible = true;

			_popupDialog.Scale = new Vector2(0.6f, 0.6f);
			_popupDialog.PivotOffset = _popupDialog.Size / 2;
			var tw = _popupDialog.CreateTween();
			tw.TweenProperty(_popupDialog, "scale", Vector2.One, 0.25f)
			  .SetTrans(Tween.TransitionType.Back)
			  .SetEase(Tween.EaseType.Out);
		}
	}

	private void OcultarPopup()
	{
		if (_popupDialog != null)
		{
			_popupDialog.Visible = false;
		}
	}
}
