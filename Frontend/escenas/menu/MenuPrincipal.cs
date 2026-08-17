using Godot;
using System;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://DatosCartas/MenuConstructor.tscn";
	[Export] public string RutaCampoPruebas    = "res://escenas/gameplay/campo_pruebas.tscn";
	[Export] public string RutaComoJugar       = "res://escenas/menu/PantallaComoJugar.tscn";
	[Export] public string RutaBestiario       = "res://escenas/menu/PantallaBestiario.tscn";

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
		}

		// 2. Obtener UI de ajustes y diálogos
		_panelSettings = GetNodeOrNull<PanelContainer>("PanelSettings");
		_popupDialog   = GetNodeOrNull<PanelContainer>("PopupDialog");

		// 3. Vincular botones principales (Cartas, Tienda, VS Bot, Online)
		var btnCartas = GetNodeOrNull<TextureButton>("CARTAS");
		if (btnCartas != null)
		{
			btnCartas.Pressed += () => GetTree().ChangeSceneToFile(RutaConstructorMazo);
			AgregarAnimacionHover(btnCartas);
		}

		var btnTienda = GetNodeOrNull<TextureButton>("TIENDA");
		if (btnTienda != null)
		{
			btnTienda.Pressed += () => MostrarPopup("TIENDA", "La tienda de cartas estará disponible en una próxima actualización.");
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
