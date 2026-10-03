using Godot;
using System;
using System.Collections.Generic;

public partial class MenuPrincipal : Control
{
	[Export] public string RutaEscenaJuego     = "res://escenas/gameplay/campo_1.tscn";
	[Export] public string RutaConstructorMazo = "res://escenas/menu/MenuConstructor.tscn";
	[Export] public string RutaCampoPruebas    = "res://escenas/gameplay/campo_pruebas.tscn";
	[Export] public string RutaComoJugar       = "res://escenas/menu/PantallaComoJugar.tscn";
	[Export] public string RutaTutorialJugable = "res://escenas/gameplay/campo_tutorial.tscn";
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
	private ColorRect _bloqueadorAjustes; // backdrop modal: bloquea el menú mientras Opciones está abierto
	private PanelContainer _popupDialog;

	// Posiciones iniciales
	private Vector2 _posInicialIsla;
	private Vector2 _posInicialReyHuevo;
	// Escala "de reposo" del huevo equipado (la que fija MostrarHuevoEstatico) — la respiración de
	// _Process() multiplica sobre ESTA, no pisa directo a 1.0. Antes sí lo hacía, así que cualquier
	// escala puesta en MostrarHuevoEstatico se perdía en el primer frame sin que se notara por qué.
	private Vector2 _reyHuevoEscalaBase = Vector2.One;

	private float _tiempoAcumulado = 0f;

	// Veces que el tutorial de instalación nueva se abre solo como obligatorio (ver _Ready).
	private const int INTENTOS_TUTORIAL_OBLIGATORIO = 3;

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
		_panelSettings?.FijarEscala(0.82f);
		_popupDialog   = GetNodeOrNull<PanelContainer>("PopupDialog");

		// 3a. Botones del layout antiguo (VBoxContainer)
		var btnJugar = GetNodeOrNull<Button>("VBoxContainer/JUGAR");
		if (btnJugar != null)
		{
			btnJugar.Pressed += () => { if (!TieneMazoCompletoOAvisa()) return; ContextoOnline.Limpiar(); TransicionCarga.Ir(this, RutaEscenaJuego, SesionJuego.Instance?.MazoSeleccionado); }; // carga en 2do plano con pantalla de carga (antes congelaba 8-10 s)
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
			// Primero se revisa la cuenta: a un invitado no le sirve que le pidan completar el mazo si
			// igual no puede jugar en línea.
			btnOnline.Pressed += () =>
			{
				if (!EsCuentaRegistrada()) { MostrarAvisoCuentaRequerida(); return; }
				if (TieneMazoCompletoOAvisa()) MostrarPantallaOnline();
			};
			AgregarAnimacionHover(btnOnline);
		}

		DuckingMusica.Reiniciar(); // por si una voz quedó a medias en la escena anterior
		// Huevo Dorado ganado en línea que no alcanzó a mostrarse en la pantalla de victoria (se salió
		// antes de que respondiera el servidor): se muestra acá.
		Callable.From(MostrarSkinGanadaPendiente).CallDeferred();
		Callable.From(QuitarSkinDevAjena).CallDeferred(); // la skin de un dev solo para su cuenta (por Id)
		ConectarBtnTrofeo();

		// Clic de interfaz en todos los botones del menú (diferido: alcanza también los creados por código).
		Callable.From(() => SonidoUI.EngancharBotones(this)).CallDeferred();

		var btnVsBot = GetNodeOrNull<BaseButton>("BottomButtons/BtnVsBot");
		if (btnVsBot != null)
		{
			btnVsBot.Pressed += () => { if (!TieneMazoCompletoOAvisa()) return; ContextoOnline.Limpiar(); TransicionCarga.Ir(this, RutaEscenaJuego, SesionJuego.Instance?.MazoSeleccionado); }; // carga en 2do plano con pantalla de carga (antes congelaba 8-10 s)
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

			// Antes decía "CÓMO JUGAR" y abría una pantalla de texto/imágenes. Ahora es "TUTORIAL" y
			// lanza el tutorial jugable (campo_tutorial.tscn). El botón de "Cómo jugar" de
			// Configuración NO cambia: sigue llevando a la pantalla de siempre.
			var btnComoJugar = secundarios.GetNodeOrNull<Button>("BtnComoJugar");
			if (btnComoJugar != null)
			{
				btnComoJugar.Text = "TUTORIAL";
				btnComoJugar.Pressed += () =>
				{
					ContextoOnline.Limpiar(); // el tutorial es local: nunca arrastra un contexto online previo
					GetTree().ChangeSceneToFile(RutaTutorialJugable);
				};
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
			// Antes acá se creaba por código un botón redondo con "?" que abría el tutorial. Se quitó:
			// la única entrada al tutorial desde el menú es el botón TUTORIAL (el que antes decía
			// CÓMO JUGAR), más el arranque automático para cuentas nuevas.
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
			// El inventario de la cuenta llega del servidor DESPUÉS de armar el menú: al llegar, se
			// vuelve a pintar el huevo con la skin equipada de verdad.
			eco.InventarioAplicado += ActualizarHuevoMenu;
		}

		// 7. Juego RECIÉN INSTALADO: se abre solo el tutorial jugable, una única vez POR APARATO (como
		// los juegos que te lo muestran apenas los instalás), después de elegir invitado, iniciar sesión
		// o registrarse. Crear otra cuenta o cambiar de cuenta NO lo vuelve a abrir: es del celular, no
		// de la cuenta. Solo vuelve a salir si se borra y se vuelve a instalar el juego. Quien solo
		// actualizó el juego entra directo al menú: no se le abre nada.
		// Es OBLIGATORIO: "pendiente" recién se borra al GANAR el tutorial
		// (ver Campo1.FinPartida). Si se sale antes (cerrando la app o perdiendo), al volver al menú se
		// abre otra vez. RENDIRSE desde la pausa sí lo da por terminado (quien se rinde ya sabe jugar).
		// Red de seguridad: si ya se abrió 2 veces sin terminarlo, la 3.ª se abre igual pero deja de
		// ser obligatorio. Así, si algo fallara en el tutorial en algún celular, nadie queda atrapado
		// para siempre (reinstalar no lo arreglaría: una instalación nueva vuelve a dejarlo pendiente).
		if (Preferencias.TutorialPendiente)
		{
			if (++Preferencias.IntentosTutorial >= INTENTOS_TUTORIAL_OBLIGATORIO)
				Preferencias.TutorialPendiente = false;
			Preferencias.TutorialVisto = true;
			Callable.From(AbrirTutorialJugable).CallDeferred();
		}
		else
		{
			// Primera vez en el menú (después del tutorial, o al actualizar): guía de sus botones.
			ProgramarGuiaMenu();
		}

		// El chequeo de versión nueva del APK corre AL INICIO, en la PantallaCarga (antes del login),
		// así un update obligatorio bloquea desde el arranque. Pero eso solo pasa al ABRIR la app: en
		// Android, salir con "inicio" y volver no reinicia el juego (solo lo reanuda, ver
		// SesionJuego._Notification), y quien ya estaba jugando cuando se publicó una versión nueva
		// nunca veía el aviso. Por eso también se revisa cada vez que se entra al menú (al volver de
		// una partida) y al volver a la app estando en él (AlVolverDeSegundoPlano). No se revisa en
		// medio de un combate para no cortarlo.
		RevisarVersionNueva();

		// Saldo y nivel de la cuenta al día (p. ej. monedas que regaló el admin) y premios que hayan
		// quedado pendientes sin internet. El nivel se repinta cuando llega la respuesta.
		if (eco != null)
		{
			eco.ProgresoCambiado += ActualizarNivel;
			eco.ConexionCambiada += MostrarAvisoConexion;
			eco.RefrescarCuenta();
			if (!eco.HayConexion) MostrarAvisoConexion(false);
		}

		Callable.From(LiberarMemoriaPartidaAnterior).CallDeferred();
	}

	private void RevisarVersionNueva()
	{
		if (!ChequeoActualizacion.RevisadoHaceMenosDe(30))
			AddChild(new ChequeoActualizacion());
	}

	/// <summary>Volvió a la app estando en el menú (ver SesionJuego._Notification).</summary>
	public void AlVolverDeSegundoPlano()
	{
		RevisarVersionNueva();
		Economia.Instancia()?.RefrescarCuenta(forzar: true);
	}

	private void ActualizarNivel()
	{
		var lblNivel = GetNodeOrNull<Label>("TopHUD/LevelPanel/Label");
		if (lblNivel != null) lblNivel.Text = $"Nv. {Preferencias.Nivel}";
	}

	// ── AVISO "SIN CONEXIÓN" ──────────────────────────────────────────────
	// Si el servidor no responde (sin internet o servidor caído), un cartel arriba lo dice y deja
	// reintentar. Antes no se avisaba nada: el login, la tienda o el online simplemente fallaban.
	private CanvasLayer _capaSinConexion;

	private void MostrarAvisoConexion(bool hayConexion)
	{
		if (hayConexion)
		{
			if (_capaSinConexion != null && IsInstanceValid(_capaSinConexion)) _capaSinConexion.Visible = false;
			return;
		}
		if (_capaSinConexion == null || !IsInstanceValid(_capaSinConexion))
		{
			_capaSinConexion = new CanvasLayer { Layer = 90 };
			AddChild(_capaSinConexion);
			var ancla = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			ancla.SetAnchorsPreset(Control.LayoutPreset.TopWide);
			ancla.OffsetTop = 205; // debajo de la barra de arriba (monedas / nombre / nivel)
			ancla.OffsetBottom = 305;
			_capaSinConexion.AddChild(ancla);
			var panel = new PanelContainer();
			EstiloUI.Panel(panel); // vidrio de código: el marco de textura se aplasta en una sola línea
			ancla.AddChild(panel);
			var fila = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			fila.AddThemeConstantOverride("separation", 28);
			panel.AddChild(fila);
			var lbl = new Label { Text = "Sin conexión con el servidor", VerticalAlignment = VerticalAlignment.Center };
			EstiloUI.Texto(lbl, 32, EstiloUI.Peligro);
			fila.AddChild(lbl);
			var btn = new Button { Text = "REINTENTAR", CustomMinimumSize = new Vector2(230, 64) };
			EstiloUI.Boton(btn, 28, accion: true);
			btn.Pressed += () =>
			{
				var e = Economia.Instancia();
				int cuenta = SesionJuego.Instance?.UsuarioId ?? -1;
				// Si al abrir la app no había internet, el inventario de la cuenta nunca llegó: se trae.
				if (cuenta > 0) e?.CargarInventarioCuenta(cuenta);
				e?.RefrescarCuenta(forzar: true);
			};
			fila.AddChild(btn);
			SonidoUI.EngancharBotones(_capaSinConexion);
		}
		_capaSinConexion.Visible = true;
	}

	/// <summary>Las texturas y escenas de la partida anterior quedan retenidas por los objetos C# que
	/// las envuelven hasta que pasa el recolector de .NET, y en el celular casi nunca pasa solo: la
	/// memoria crecía en cada partida (412 → 800 MB en 6 partidas, medido en un Redmi de 6 GB) hasta
	/// que Android mataba apps y el juego se congelaba segundos al cargar tropas. Se fuerza aquí, en
	/// el menú, donde el tirón de unos ms no se nota (en PC: 677 → 192 MB de texturas).</summary>
	private static void LiberarMemoriaPartidaAnterior()
	{
		System.GC.Collect();
		System.GC.WaitForPendingFinalizers();
		System.GC.Collect();
	}

	public override void _ExitTree()
	{
		if (Economia.Instance != null)
		{
			Economia.Instance.MonedasCambiaron -= OnMonedasCambiaron;
			Economia.Instance.InventarioAplicado -= ActualizarHuevoMenu;
			Economia.Instance.ProgresoCambiado -= ActualizarNivel;
			Economia.Instance.ConexionCambiada -= MostrarAvisoConexion;
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

	/// <summary>Abre el tutorial jugable (campo_tutorial.tscn). Lo usan el botón TUTORIAL y el
	/// arranque automático de las cuentas nuevas.</summary>
	private void AbrirTutorialJugable()
	{
		Preferencias.TutorialVisto = true;
		ContextoOnline.Limpiar();
		GetTree().ChangeSceneToFile(RutaTutorialJugable);
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

	// Tarjetas del selector "SELECCIONA TU HUEVO" (antes 190×230 con huevo de 130×140).
	private static readonly Vector2 TAM_TARJETA_SKIN = new(240, 300);
	private static readonly Vector2 TAM_HUEVO_SKIN   = new(170, 190);

	private const string RUTA_REY_HUEVO_CORONADO = "res://imagenes/RendersTropa/Huevo render/ReyHuevo_Render.png";
	// Paper Dino Huevo: en el selector y la Tienda usa el render estático como todos (ya arreglado),
	// pero en el MENÚ PRINCIPAL, cuando es la skin equipada, se pidió mostrarla animada — mismos
	// sprite frames que su ficha de personaje — pero calculada para verse al mismo tamaño que el
	// resto. La caja de referencia (ReyHuevoCrowned, ver menu_principal.tscn) mide 317.7×350 tras el
	// arreglo de proporción; el frame de la animación mide 942×1057, así que escala =
	// min(317.7/942, 350/1057) ≈ 0.331 reproduce el mismo criterio "cabe completo, centrado" que
	// usa KeepAspectCentered para las demás.
	private const int IDX_PAPER_DINO = 4; // Preferencias.SKIN_ESCENAS[4]

	// Qué huevo está dibujado ahora. Si al llegar el inventario de la cuenta el equipado es el MISMO, no
	// se vuelve a dibujar (re-aplicar textura/escala/color en medio de su animación se notaba como un
	// salto).
	private string _huevoDibujado = "";

	/// <summary>Refleja en el menú principal la skin de huevo equipada (soporta tanto catálogo estándar como exclusivas).</summary>
	private void ActualizarHuevoMenu()
	{
		if (_reyHuevoNode == null) return;

		string clave = Preferencias.SkinExclusivaActiva + "|" + Preferencias.SkinActivaIdx;
		if (clave == _huevoDibujado) return;
		_huevoDibujado = clave;

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
					// En el APK exportado los .tres aparecen como "x.tres.remap": sin quitar ese sufijo no se
					// encontraba NINGUNA carta en el celular y el perfil se quedaba sin la imagen de la tropa.
					if (archivo.EndsWith(".remap")) archivo = archivo[..^".remap".Length];
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
		if (_panelSettings == null) return;
		// Modal: mientras Opciones esté abierto NO se puede tocar nada del menú detrás (hay que CERRAR).
		MostrarBloqueadorAjustes();
		_panelSettings.AlCerrar = OcultarBloqueadorAjustes; // al cerrar el panel, quitar el bloqueo
		_panelSettings.Visible = true;
		ProgramarGuiaAjustes(); // la primera vez que se abren, se explica cada opción
	}

	// Coloca (o reutiliza) un backdrop a pantalla completa JUSTO debajo del panel de Opciones: come
	// todos los clics/toques (MouseFilter=Stop) → los botones del menú detrás quedan intocables hasta
	// cerrar. También oscurece un poco para señalar que es modal.
	private void MostrarBloqueadorAjustes()
	{
		Node padre = _panelSettings.GetParent();
		if (padre == null) return;

		if (_bloqueadorAjustes == null || !GodotObject.IsInstanceValid(_bloqueadorAjustes))
		{
			_bloqueadorAjustes = new ColorRect
			{
				Name        = "BloqueadorAjustes",
				Color       = new Color(0.03f, 0.04f, 0.07f, 0.6f),
				MouseFilter = Control.MouseFilterEnum.Stop
			};
			_bloqueadorAjustes.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			padre.AddChild(_bloqueadorAjustes);
		}

		// Orden de dibujo (robusto en cada reapertura): el backdrop al tope y el panel justo encima de
		// él. Así el backdrop tapa TODOS los botones del menú (hijos anteriores) y el panel queda visible
		// por encima del backdrop.
		padre.MoveChild(_bloqueadorAjustes, -1);
		padre.MoveChild(_panelSettings, -1);
		_bloqueadorAjustes.Visible = true;
	}

	private void OcultarBloqueadorAjustes()
	{
		if (_bloqueadorAjustes != null && GodotObject.IsInstanceValid(_bloqueadorAjustes))
			_bloqueadorAjustes.Visible = false;
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

	/// <summary>Aviso del menú (hoy: mazo incompleto). Usa el MISMO aviso grande que el de "Necesitas
	/// una cuenta" (ver MostrarAvisoGrande), así los dos se ven igual de grandes y legibles.</summary>
	private void MostrarPopup(string titulo, string mensaje)
	{
		MostrarAvisoGrande(titulo, mensaje, ("ENTENDIDO", COLOR_BOTON_AVISO_PRINCIPAL, COLOR_TEXTO_BOTON_PRINCIPAL, null));
	}

	private void OcultarPopup()
	{
		if (_popupDialog != null)
		{
			_popupDialog.Visible = false;
		}
	}
}
