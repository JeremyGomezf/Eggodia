using Godot;
using System;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://DatosCartas/MenuConstructor.tscn";
	[Export] public string RutaCampoPruebas    = "res://escenas/gameplay/campo_pruebas.tscn";
	[Export] public string RutaComoJugar       = "res://escenas/menu/PantallaComoJugar.tscn";
	[Export] public string RutaBestiario       = "res://escenas/menu/PantallaBestiario.tscn";
	[Export] public string RutaTienda          = "res://escenas/menu/Tienda.tscn";

	// Nodos de animación y UI
	private Control _islaContainer;
	private TextureRect _portalNode;
	private TextureRect _reyHuevoNode;
	
	private Label _lblCoins;
	private PanelContainer _panelSettings;
	private PanelContainer _popupDialog;

	// Posiciones iniciales
	private Vector2 _posInicialIsla;
	private Vector2 _posInicialReyHuevo;

	private float _tiempoAcumulado = 0f;

	public override void _Ready()
	{
		// 1. Obtener referencias del escenario, portal y huevo coronado
		_islaContainer     = GetNodeOrNull<Control>("IslaContainer");
		_portalNode        = GetNodeOrNull<TextureRect>("IslaContainer/Portal");
		_reyHuevoNode      = GetNodeOrNull<TextureRect>("IslaContainer/ReyHuevoCrowned");

		// Guardar posiciones iniciales si los nodos existen
		if (_islaContainer   != null) _posInicialIsla = _islaContainer.Position;
		if (_reyHuevoNode    != null)
		{
			_posInicialReyHuevo = _reyHuevoNode.Position;
			_reyHuevoNode.PivotOffset = new Vector2(_reyHuevoNode.Size.X / 2, _reyHuevoNode.Size.Y * 0.8f);
			AgregarAnimacionHover(_reyHuevoNode);
			_reyHuevoNode.MouseFilter = Control.MouseFilterEnum.Stop;
			_reyHuevoNode.GuiInput += (ev) => {
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					AbrirSelectorSkin();
			};
		}

		// 2. Obtener UI de ajustes y diálogos
		_panelSettings = GetNodeOrNull<PanelContainer>("PanelSettings");
		_popupDialog   = GetNodeOrNull<PanelContainer>("PopupDialog");

		// 3a. Botones del layout antiguo (VBoxContainer)
		var btnJugar = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
		if (btnJugar != null)
		{
			btnJugar.Pressed += () => GetTree().ChangeSceneToFile(RutaEscenaJuego);
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
		var btnCartas = GetNodeOrNull<TextureButton>("CARTAS");
		if (btnCartas != null)
		{
			btnCartas.Pressed += () => GetTree().ChangeSceneToFile(RutaConstructorMazo);
			AgregarAnimacionHover(btnCartas);
		}

		var btnTienda = GetNodeOrNull<TextureButton>("TIENDA");
		if (btnTienda != null)
		{
			btnTienda.Pressed += () => GetTree().ChangeSceneToFile(RutaTienda);
			AgregarAnimacionHover(btnTienda);
		}

		var btnOnline = GetNodeOrNull<Button>("BottomButtons/BtnOnline");
		if (btnOnline != null)
		{
			btnOnline.Pressed += () => MostrarPopup("MODO ONLINE", "El modo multijugador online estará disponible próximamente.");
			AgregarAnimacionHover(btnOnline);
		}

		var btnVsBot = GetNodeOrNull<Button>("BottomButtons/BtnVsBot");
		if (btnVsBot != null)
		{
			btnVsBot.Pressed += () => GetTree().ChangeSceneToFile(RutaEscenaJuego);
			AgregarAnimacionHover(btnVsBot);
		}

		// 4. Vincular botones secundarios (Bestiario, Cómo Jugar, Pruebas)
		var btnBestiario = GetNodeOrNull<Button>("SecondaryButtons/BtnBestiario");
		if (btnBestiario != null)
		{
			btnBestiario.Pressed += () => GetTree().ChangeSceneToFile(RutaBestiario);
			AgregarAnimacionHover(btnBestiario);
		}

		var btnComoJugar = GetNodeOrNull<Button>("SecondaryButtons/BtnComoJugar");
		if (btnComoJugar != null)
		{
			btnComoJugar.Pressed += AbrirComoJugar;
			AgregarAnimacionHover(btnComoJugar);
		}

		var btnPruebas = GetNodeOrNull<Button>("SecondaryButtons/BtnPruebas");
		if (btnPruebas != null)
		{
			btnPruebas.Pressed += () => GetTree().ChangeSceneToFile(RutaCampoPruebas);
			AgregarAnimacionHover(btnPruebas);
		}

		// 5. Vincular Ajustes y Cierre de Popups
		var btnSettings = GetNodeOrNull<TextureButton>("BtnSettings");
		if (btnSettings != null)
		{
			btnSettings.Pressed += MostrarSettings;
			AgregarAnimacionHover(btnSettings);
		}

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

		// C. Huevo Coronado (Rey Huevo) visible y respirando suavemente en el centro del nido
		if (_reyHuevoNode != null)
		{
			float escalaY = 1.0f + MathF.Sin(_tiempoAcumulado * 2.2f) * 0.02f;
			float escalaX = 1.0f - MathF.Sin(_tiempoAcumulado * 2.2f) * 0.012f;
			_reyHuevoNode.Scale = new Vector2(escalaX, escalaY);
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
		btn.PivotOffset = btn.Size / 2;
		btn.Resized += () => btn.PivotOffset = btn.Size / 2;

		btn.MouseEntered += () => 
		{
			var tween = btn.CreateTween();
			tween.TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.15f)
				 .SetTrans(Tween.TransitionType.Back)
				 .SetEase(Tween.EaseType.Out);
		};
		btn.MouseExited += () => 
		{
			var tween = btn.CreateTween();
			tween.TweenProperty(btn, "scale", Vector2.One, 0.15f)
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
		panel.CustomMinimumSize = new Vector2(680, 440);
		panel.OffsetLeft = -340; panel.OffsetRight = 340;
		panel.OffsetTop  = -220; panel.OffsetBottom = 220;

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

		// Grid de skins
		var grid = new GridContainer();
		grid.Columns = 4;
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		vbox.AddChild(grid);

		for (int i = 0; i < Preferencias.SKIN_NOMBRES.Length; i++)
		{
			int capI = i;
			bool poseida = Preferencias.TieneSkin(i);
			bool activa  = Preferencias.SkinActivaIdx == i;

			var skinPanel = new PanelContainer();
			skinPanel.CustomMinimumSize = new Vector2(148, 200);

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
			tex.CustomMinimumSize = new Vector2(110, 110);
			tex.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			tex.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			var txImg = GD.Load<Texture2D>(Preferencias.SKIN_IMAGENES[capI]);
			if (txImg != null) tex.Texture = txImg;
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
			else if (poseida)
			{
				var btnEquip = new Button();
				btnEquip.Text = "EQUIPAR";
				btnEquip.CustomMinimumSize = new Vector2(0, 32);
				btnEquip.AddThemeFontSizeOverride("font_size", 12);
				btnEquip.Pressed += () => {
					Preferencias.SkinActivaIdx = capI;
					overlay.QueueFree();
					// Actualizar la imagen del huevo visible en el menú
					ActualizarHuevoMenu();
				};
				svbox.AddChild(btnEquip);
			}
			else
			{
				var lblLocked = new Label();
				lblLocked.Text = "🔒 No poseída";
				lblLocked.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
				lblLocked.AddThemeFontSizeOverride("font_size", 11);
				lblLocked.HorizontalAlignment = HorizontalAlignment.Center;
				svbox.AddChild(lblLocked);
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

	private void ActualizarHuevoMenu()
	{
		// El huevo visualmente en el menú siempre muestra ReyHuevoCrowned.png (el nodo del .tscn)
		// Solo el personaje en batalla cambia — no hay que cambiar la textura aquí.
	}

	private void MostrarSettings()
	{
		if (_panelSettings != null) _panelSettings.Visible = true;
	}

	private void MostrarPopup(string titulo, string mensaje)
	{
		if (_popupDialog != null)
		{
			_popupDialog.GetNode<Label>("VBox/Title").Text = titulo;
			_popupDialog.GetNode<Label>("VBox/Message").Text = mensaje;
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
