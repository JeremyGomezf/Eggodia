using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ═══════════════════════════════════════════════════════════════════════
	// MODO TUTORIAL — ETAPA 2 de la construcción por partes:
	//   · Splash "BIENVENIDO AL TUTORIAL" (2s) antes de mostrar nada del campo.
	//   · Cuadro "Guia" (efectos/guiatexto.tscn, hecho por el usuario) con 3 mensajes de
	//     introducción (turnos/energía, botones/rendirse/tiempo límite, 20s por turno) — cada uno
	//     avanza con un click/touch en cualquier lado ("EsperarClickParaAvanzar").
	//   · Recién ahí aparece la mano (3 cartas fijas) y el aviso "Coloca tus 3 tropas...", que se
	//     resuelve solo cuando las 3 quedan puestas (reusa _faseApertura, que YA bloquea barajar/
	//     sacrificar/hechizos/ataque hasta ese momento en el juego normal — no hizo falta duplicar
	//     ese bloqueo acá).
	//   · Un cuarto mensaje de Guia explica el menú de tropa (ataque/defensa/habilidad); se oculta
	//     solo cuando el jugador abre ese menú por primera vez (ver MostrarMenuTropa).
	//   · Reloj congelado en 0:00, sin música, sin intro cinemática (ver Campo1.cs / Campo1.Turnos.cs
	//     / Campo1.Flujo.cs / Campo1.Nfc.cs para el gancho de click-para-avanzar).
	//
	// LO QUE FALTA (próxima etapa, no armado todavía): la secuencia de combate guionada turno por
	// turno (qué tropa ataca a cuál, el rival apareciendo con sus 3 tropas sin el aviso normal de
	// "rival listo"), la curación guiada arrastrando la carta de Curación, el desbloqueo/uso guiado
	// de la habilidad del Maguín forzando el objetivo (tanque rival → pez, permanente), el
	// sacrificio guiado del MAGUÍN + elección de reemplazo, el remate final (atacar con todo)
	// y la pantalla especial "TUTORIAL APROBADO" (sin música/reloj/monedas, botón "Repetir tutorial"
	// que reinicia toda esta secuencia desde cero). También: escenario/música fijos en Medieval con
	// el trono del Sargento Huevo de rival, y que entre los ardides mostrados solo Curación pueda
	// usarse de verdad (los demás visibles pero sin efecto) durante esa etapa.
	// ═══════════════════════════════════════════════════════════════════════

	private const string RUTA_GOLEM_TUTORIAL       = "res://cartas prime/MEDIEVAL/Golem_prime.tscn";
	private const string RUTA_MAGUIN_TUTORIAL      = "res://cartas prime/MEDIEVAL/Maguin_prime.tscn";
	private const string RUTA_SOLDADOREAL_TUTORIAL = "res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn";

	/// <summary>Reemplaza el mazo de 8 cartas de la sesión por las 3 fijas del tutorial (1 táctico,
	/// 1 asesino, 1 coloso — Maguín, Soldado Real, Golem). Se llama ANTES de PrepararMazoSinRepetir/
	/// InicializarClasificacionMazo, así que el resto del juego (tipos, fan de la mano, invocación)
	/// funciona sin ningún cambio, como si el jugador hubiera armado ese mazo de verdad.</summary>
	// Cartas de reemplazo: las 3 que aparecen al usar BARAJAR (el Dragón va al medio).
	private const string RUTA_TIBURON_TUTORIAL = "res://cartas prime/PACIFICO/Tiburon_prime.tscn";
	private const string RUTA_DRAGON_TUTORIAL  = "res://cartas prime/MEDIEVAL/Dragon_prime.tscn";
	private const string RUTA_CALAMAR_TUTORIAL = "res://cartas prime/PACIFICO/CalamarG_prime.tscn";
	private const int IDX_TIBURON_TUTORIAL = 3, IDX_DRAGON_TUTORIAL = 4, IDX_CALAMAR_TUTORIAL = 5;

	private void ForzarMazoTutorial()
	{
		// Los 3 primeros son la mano inicial; los 3 últimos, el reemplazo tras barajar.
		escenasTropas = new[] { RUTA_GOLEM_TUTORIAL, RUTA_MAGUIN_TUTORIAL, RUTA_SOLDADOREAL_TUTORIAL,
			RUTA_TIBURON_TUTORIAL, RUTA_DRAGON_TUTORIAL, RUTA_CALAMAR_TUTORIAL };
		imagenesCartas = new string[escenasTropas.Length];
		for (int i = 0; i < escenasTropas.Length; i++)
			imagenesCartas[i] = ClasificacionCartas.ImagenBatalla(escenasTropas[i]) ?? "";
		GD.Print("[Tutorial] Mazo fijo: Golem, Maguín, Soldado Real");
	}

	/// <summary>Pone las 3 cartas fijas DIRECTO en Spot1/2/3 — sin el sorteo por tipo de la partida
	/// normal. Ya NO se llama desde _Ready: la llama la propia secuencia del tutorial recién
	/// después de los 3 mensajes de introducción (ver PasoMostrarManoTutorial).</summary>
	/// <summary>BARAJAR del tutorial: en vez del barajado normal, reparte las 3 cartas de reemplazo
	/// fijas — Tiburón, Dragón (en el medio) y Calamar.</summary>
	private void BarajarTutorialReemplazo()
	{
		if (contenedorMano != null)
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c) { c.NombreSpot = "X"; c.QueueFree(); }

		_tutorialManoBloqueada = false;
		CrearCartaConIndice("Spot1", IDX_TIBURON_TUTORIAL);
		CrearCartaConIndice("Spot2", IDX_DRAGON_TUTORIAL);  // el Dragón, al medio
		CrearCartaConIndice("Spot3", IDX_CALAMAR_TUTORIAL);
		ReacomodarManoTropas();
	}

	private void IniciarManoTutorial()
	{
		for (int i = 0; i < SPOTS_MANO.Length; i++)
			CrearCartaConIndice(SPOTS_MANO[i], i);
		// Igual que RellenarManoObjetivo en la partida normal: sin esto las cartas quedaban sueltas
		// en la posición cruda de cada spot, sin el abanico/escala de siempre (se veían mal puestas).
		ReacomodarManoTropas();
	}

	// ── CARRIL FIJO POR CARTA ─────────────────────────────────────────────────────────────────
	// En el tutorial cada una de las 3 cartas iniciales tiene SU carril: Soldado Real en Mod1,
	// Maguín en Mod2, Gólem en Mod3. Al agarrar una carta, los otros dos carriles se ponen grises
	// (y si igual se suelta ahí, se rechaza con un aviso). Además de guiar, deja el tablero siempre
	// en el mismo orden, así el resto del guion (quién ataca a quién) es predecible.
	private static readonly Dictionary<string, string> CARRIL_FIJO_TUTORIAL = new()
	{
		{ RUTA_SOLDADOREAL_TUTORIAL, "Mod1" },
		{ RUTA_MAGUIN_TUTORIAL,      "Mod2" },
		{ RUTA_GOLEM_TUTORIAL,       "Mod3" },
	};

	/// <summary>Carril obligatorio de esa carta, o null si puede ir a cualquiera (las cartas de
	/// reemplazo posteriores al sacrificio no tienen carril fijo).</summary>
	public string CarrilFijoTutorial(string rutaEscena)
		=> (!string.IsNullOrEmpty(rutaEscena) && CARRIL_FIJO_TUTORIAL.TryGetValue(rutaEscena, out string c)) ? c : null;

	/// <summary>La llama Carta.cs al empezar a arrastrar: apaga los carriles que no corresponden.</summary>
	public void MarcarCarrilesTutorial(string rutaEscena)
	{
		string permitido = CarrilFijoTutorial(rutaEscena);
		foreach (Node n in GetTree().GetNodesInGroup("zonas_invocacion"))
		{
			if (n is not Node2D zona || !IsInstanceValid(zona)) continue;
			bool habilitado = permitido == null || (string)zona.Name == permitido;
			zona.Modulate = habilitado ? Colors.White : new Color(0.4f, 0.4f, 0.45f, 0.45f);
		}
	}

	/// <summary>La llama Carta.cs al soltar/cancelar: devuelve los 3 carriles a su color normal.</summary>
	public void RestaurarCarrilesTutorial()
	{
		foreach (Node n in GetTree().GetNodesInGroup("zonas_invocacion"))
			if (n is Node2D zona && IsInstanceValid(zona)) zona.Modulate = Colors.White;
	}

	/// <summary>Vida del huevo rival en el tutorial. Es poca a propósito (al caer sus 3 tropas la
	/// partida se cierra), pero su barra se mide contra este mismo número, así que se ve LLENA al
	/// empezar — ver PorcentajeBarraVida en Campo1.FinPartida.cs.</summary>
	public const int VIDA_RIVAL_TUTORIAL = 400;

	/// <summary>Cuánto MÁS BAJO suena la música del tutorial respecto a la de los escenarios. Apenas
	/// un escalón por debajo: se pidió "casi igual que los escenarios, pero un poquito menos".</summary>
	private const float VOLUMEN_EXTRA_TUTORIAL_DB = -2f;

	/// <summary>Mientras es true, RellenarManoObjetivo (Campo1.MazoRobo.cs) no repone NADA: una vez
	/// jugadas las 3 cartas iniciales la mano queda vacía a propósito, para que el jugador no pueda
	/// invocar fuera del guion. Se libera recién al sacrificar al Maguín, que es el único momento en
	/// que el tutorial ofrece invocar una tropa nueva (Dragón / Tiburón / Calamar).</summary>
	private bool _tutorialManoBloqueada = true;

	private void PermitirManoTutorial()
	{
		_tutorialManoBloqueada = false;
		RellenarManoObjetivo();
	}

	// ── CAPA DEL TUTORIAL (splash + cuadro Guia) ──────────────────────────────────────────────
	private CanvasLayer _capaTutorial;

	private void IniciarInterfazTutorial()
	{
		_capaTutorial = new CanvasLayer { Name = "CapaTutorial", Layer = 500 }; // por encima de TODO
		AddChild(_capaTutorial);

		CrearGuiaTutorial();
		MostrarSplashBienvenida();
	}

	// ── SPLASH "BIENVENIDO AL TUTORIAL" (~4s, semioscuro, luego se revela el campo) ───────────
	private void MostrarSplashBienvenida()
	{
		var capa = new CanvasLayer { Name = "SplashBienvenidaTutorial", Layer = 600 };
		AddChild(capa);

		var fondo = new ColorRect { Color = new Color(0f, 0f, 0f, 0f) };
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.MouseFilter = Control.MouseFilterEnum.Stop; // no deja pasar clicks mientras dura
		capa.AddChild(fondo);

		// FullRect + alineación centrada (no el preset "Center", que depende del auto-tamaño del
		// Label) para que el texto quede siempre en el centro exacto de la pantalla, en cualquier
		// resolución — clave para que se vea bien también en celular.
		var lbl = new Label { Text = "BIENVENIDO AL TUTORIAL" };
		lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.VerticalAlignment = VerticalAlignment.Center;
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lbl.Modulate = new Color(1, 1, 1, 0);
		EstiloUI.Texto(lbl, 110, EstiloUI.Dorado);
		capa.AddChild(lbl);

		var tw = CreateTween();
		tw.TweenProperty(fondo, "color", new Color(0.03f, 0.03f, 0.05f, 0.88f), 0.4f);
		tw.Parallel().TweenProperty(lbl, "modulate", new Color(1, 1, 1, 1), 0.4f);
		tw.TweenInterval(3.2f); // total con los fades: ~4s
		tw.TweenProperty(fondo, "color", new Color(0f, 0f, 0f, 0f), 0.4f);
		tw.Parallel().TweenProperty(lbl, "modulate", new Color(1, 1, 1, 0), 0.4f);
		tw.TweenCallback(Callable.From(() =>
		{
			capa.QueueFree();
			PasoGuiaIntro1();
		}));
	}

	// ── CUADRO "Guia" (efectos/guiatexto.tscn) ────────────────────────────────────────────────
	// Vive junto a la mano manual (ManoManual): es "el cuadro que nos indica", ya no el panel
	// armado por código de la etapa 1. Aparece con un efecto de chico a grande, y se corre solo
	// cuando el jugador arrastra una carta para invocar (OcultarGuiaTutorial/RestaurarGuiaTutorial,
	// llamados desde Carta.cs) o cuando abre el menú de acciones de una tropa (MostrarMenuTropa).
	private const string RUTA_GUIA_TUTORIAL = "res://efectos/guiatexto.tscn";
	private Node2D _guiaTutorial;
	private Label  _lblGuiaTutorial;
	private Tween  _tweenGuiaTutorial;
	private string _textoGuiaTutorialActual = "";

	// Escala final del cuadro cuando lo instancia el código (plan B). Si el nodo "Guia" ya está
	// puesto a mano en la escena, se respeta la escala que tenga ahí y esta constante no se usa.
	private static readonly Vector2 ESCALA_GUIA_TUTORIAL = new Vector2(1.45f, 1.45f);
	private Vector2 _escalaFinalGuiaTutorial = Vector2.One;

	private void CrearGuiaTutorial()
	{
		// PLAN A: el nodo "Guia" ya colocado a mano en campo_tutorial.tscn. Es el que manda: se
		// respeta TAL CUAL la posición y la escala que tenga puestas en el editor (así el cuadro
		// queda exactamente donde se lo acomodó, sin que el código lo mueva por su cuenta).
		_guiaTutorial = FindChild("Guia", true, false) as Node2D;

		// PLAN B: si la escena todavía no lo tiene guardado, se instancia por código y se lo ubica
		// cerca de la mano, como aproximación.
		if (_guiaTutorial == null)
		{
			var escena = GD.Load<PackedScene>(RUTA_GUIA_TUTORIAL);
			if (escena == null) return;
			_guiaTutorial = escena.Instantiate<Node2D>();
			_capaTutorial.AddChild(_guiaTutorial);

			var mano = GetNodeOrNull<Control>("ManoManual");
			_guiaTutorial.Position = mano != null ? mano.GlobalPosition + new Vector2(20f, -90f) : new Vector2(640f, 620f);
			_guiaTutorial.Scale = ESCALA_GUIA_TUTORIAL;
			GD.Print("[Tutorial] Nodo 'Guia' no encontrado en la escena: se instanció por código.");
		}

		// Por delante de todo lo del tablero (tropas llegan a ZIndex 100, la mano a 150).
		_guiaTutorial.ZIndex = 4000;
		_escalaFinalGuiaTutorial = _guiaTutorial.Scale;
		_lblGuiaTutorial = _guiaTutorial.FindChild("LblHabilidad", true, false) as Label;
		// El .tscn trae un texto de ejemplo para poder acomodarlo en el editor: se limpia acá para
		// que no llegue a verse ni un fotograma antes del primer mensaje real.
		if (_lblGuiaTutorial != null) _lblGuiaTutorial.Text = "";
		_guiaTutorial.Visible = false;
	}

	/// <summary>Muestra el cuadro Guia con un texto nuevo, con la animación de chico a grande.</summary>
	private void MostrarGuiaConTexto(string texto)
	{
		_textoGuiaTutorialActual = texto;
		if (_guiaTutorial == null) return;
		if (_lblGuiaTutorial != null) _lblGuiaTutorial.Text = texto;
		_guiaTutorial.Visible = true;
		_guiaTutorial.Scale = _escalaFinalGuiaTutorial * 0.15f;
		_tweenGuiaTutorial?.Kill();
		_tweenGuiaTutorial = CreateTween();
		_tweenGuiaTutorial.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_tweenGuiaTutorial.TweenProperty(_guiaTutorial, "scale", _escalaFinalGuiaTutorial, 0.35f);
	}

	/// <summary>Se llama desde Carta.cs apenas el jugador empieza a arrastrar una carta, y desde
	/// MostrarMenuTropa al abrir el menú de una tropa: el cuadro se corre para no tapar la vista.</summary>
	public void OcultarGuiaTutorial()
	{
		if (_guiaTutorial != null) _guiaTutorial.Visible = false;
	}

	/// <summary>Cierra el cuadro DE VERDAD: además de ocultarlo, olvida el texto, para que no vuelva
	/// a aparecer solo. (OcultarGuiaTutorial es un escondite temporal — al terminar de arrastrar una
	/// carta, RestaurarGuiaTutorial lo devolvía con el mensaje viejo: por eso el texto de "tienes 20
	/// segundos" reaparecía cada vez que se agarraba una carta.)</summary>
	private void CerrarGuiaTutorial()
	{
		_textoGuiaTutorialActual = "";
		if (_lblGuiaTutorial != null) _lblGuiaTutorial.Text = "";
		if (_guiaTutorial != null) _guiaTutorial.Visible = false;
	}

	/// <summary>Se llama desde Carta.cs cuando termina un arrastre (soltó o canceló) — vuelve a
	/// mostrar el cuadro con el texto vigente, si había uno.</summary>
	public void RestaurarGuiaTutorial()
	{
		if (_guiaTutorial != null) _guiaTutorial.Visible = !string.IsNullOrEmpty(_textoGuiaTutorialActual);
	}

	/// <summary>Oculta/muestra TODA la capa del tutorial (cuadro de guía "info") mientras el menú de
	/// pausa está abierto. La capa está en Layer 500 (por encima de todo), así que sin esto el "info"
	/// quedaba flotando ENCIMA del panel de pausa. Lo llama MenuPausa al pausar/reanudar. Fuera del
	/// tutorial _capaTutorial es null → no hace nada.</summary>
	public void OcultarCapaTutorialEnPausa(bool pausado)
	{
		if (_capaTutorial != null && IsInstanceValid(_capaTutorial))
			_capaTutorial.Visible = !pausado;

		// El cuadro "Guia" está puesto a mano en campo_tutorial.tscn (no dentro de _capaTutorial),
		// así que hay que taparlo aparte: con ZIndex 4000 se vería encima del panel de pausa.
		if (_guiaTutorial != null && IsInstanceValid(_guiaTutorial))
			_guiaTutorial.Visible = pausado ? false : !string.IsNullOrEmpty(_textoGuiaTutorialActual);
	}

	// ── "Dale click a cualquier lado para seguir" — mecanismo real de avance ─────────────────
	// (Antes el texto lo prometía pero nada estaba conectado; ahora si ModoTutorial y hay un paso
	// esperando, el primer click/touch en cualquier lado dispara el siguiente paso. Enganchado
	// desde el _UnhandledInput que ya existe en Campo1.Nfc.cs para no duplicar el override.)
	private bool   _esperandoClickTutorial = false;
	private Action _alAvanzarClickTutorial;

	private Timer _timerPistaClickTutorial;

	private void EsperarClickParaAvanzar(Action alAvanzar)
	{
		_esperandoClickTutorial = true;
		_alAvanzarClickTutorial = alAvanzar;

		// Si el cuadro se queda puesto un rato sin que nadie lo toque, se recuerda cada tanto cómo
		// seguir — antes no había forma de enterarse de que había que tocar la pantalla.
		_timerPistaClickTutorial?.Stop();
		_timerPistaClickTutorial?.QueueFree();
		_timerPistaClickTutorial = new Timer { WaitTime = 10.0, Autostart = true, OneShot = false };
		AddChild(_timerPistaClickTutorial);
		_timerPistaClickTutorial.Timeout += () =>
		{
			// Si la partida ya terminó, la pista se apaga: si no, seguía saltando cada 10s y le
			// borraba la frase de cierre de la pantalla.
			if (juegoTerminado) { DetenerPistaClickTutorial(); return; }
			if (!_esperandoClickTutorial) return;
			MostrarAviso("Toca cualquier lado para continuar", Colors.Gold);
		};
	}

	private void DetenerPistaClickTutorial()
	{
		if (_timerPistaClickTutorial == null) return;
		_timerPistaClickTutorial.Stop();
		_timerPistaClickTutorial.QueueFree();
		_timerPistaClickTutorial = null;
	}

	/// <returns>true si el evento fue consumido acá (el llamador debe cortar su propio manejo).</returns>
	private bool ManejarClickAvanceTutorial(InputEvent @event)
	{
		if (!_esperandoClickTutorial) return false;
		bool esClick = (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			|| (@event is InputEventScreenTouch st && st.Pressed);
		if (!esClick) return false;

		_esperandoClickTutorial = false;
		DetenerPistaClickTutorial();
		var accion = _alAvanzarClickTutorial;
		_alAvanzarClickTutorial = null;
		accion?.Invoke();
		GetViewport().SetInputAsHandled();
		return true;
	}

	// ── SECUENCIA DE INTRODUCCIÓN (3 mensajes de Guia, luego la mano) ────────────────────────
	private void PasoGuiaIntro1()
	{
		MostrarGuiaConTexto("¡Hola! Vamos a guiarte en cómo funciona EGGODIA. Primero: este juego es " +
			"por turnos, y solo tienes 3 energías cada turno. Invocar cartas NO consume energía.");
		EsperarClickParaAvanzar(PasoGuiaIntro2);
	}

	private void PasoGuiaIntro2()
	{
		MostrarGuiaConTexto("Cada botón que veas consume energía, menos el de la esquina derecha (el " +
			"rojo), que sirve para cambiar tus ardides. Cada partida dura 3 minutos: si se acaba el " +
			"tiempo, gana quien tenga más vida.");
		EsperarClickParaAvanzar(PasoGuiaIntro3);
	}

	private void PasoGuiaIntro3()
	{
		MostrarGuiaConTexto("En cada turno tienes 20 segundos para armar tu estrategia. Ahora te vamos " +
			"a presentar nuestras 3 cartas.");
		EsperarClickParaAvanzar(PasoMostrarManoTutorial);
	}

	private void PasoMostrarManoTutorial()
	{
		IniciarManoTutorial();
		// Antes de mandar a colocarlas, se explica que hay 3 TIPOS de carta y que estas 3 son una
		// de cada uno (coinciden con ClasificacionCartas: Maguín táctico, Soldado Real asesino,
		// Gólem coloso).
		MostrarGuiaConTexto("Hay 3 tipos de carta, y tienes una de cada uno: el TÁCTICO (Maguín) " +
			"apoya con su habilidad, el ASESINO (Soldado Real) golpea fuerte, y el COLOSO (Gólem) " +
			"es el que más aguanta.");
		EsperarClickParaAvanzar(PasoColocarTropasTutorial);
	}

	private void PasoColocarTropasTutorial()
	{
		CerrarGuiaTutorial();
		MostrarAviso("Coloca tus 3 tropas en los círculos azules", Colors.Gold);
		EsperarColocarTresTropasTutorial();
	}

	/// <summary>Sondea (con un Timer, no _Process — Campo1.SecuenciaDigital.cs ya usa ese override)
	/// hasta que las 3 tropas quedan puestas, y recién ahí explica el menú de acciones.</summary>
	private void EsperarColocarTresTropasTutorial()
	{
		var t = new Timer { WaitTime = 0.3, Autostart = true, OneShot = false };
		AddChild(t);
		t.Timeout += () =>
		{
			if (!TodosSpotsOcupados()) return;
			t.Stop();
			t.QueueFree();
			PasoExplicarMenuTropaTutorial();
		};
	}

	private void PasoExplicarMenuTropaTutorial()
	{
		MostrarGuiaConTexto("¡Bien hecho! Ahora presiona al personaje que quieras y vas a ver que tiene " +
			"3 funciones: ATAQUE, DEFENSA y la mejor, ¡HABILIDAD!");
		EsperarClickParaAvanzar(PasoTuTurnoCombateTutorial);
	}

	// ── RIVAL FIJO DEL TUTORIAL (Sargento Huevo) ──────────────────────────────────────────────
	// Aparece espejado por carril según qué tropa propia haya en cada uno (no según el orden en
	// que se armó la mano, porque el jugador puede arrastrar sus 3 cartas a cualquier carril):
	// donde esté el Soldado Real aparece el Soldado Cartoon, donde esté el Maguín aparece el
	// Campero, y donde esté el Gólem aparece el Tanque — así el resto de la secuencia guionada
	// (quién ataca a quién) siempre calza sin importar el orden en que el jugador invocó.
	private static readonly Dictionary<System.Type, string> RIVAL_FORZADO_TUTORIAL = new()
	{
		{ typeof(SoldadoRealPrime), "res://cartas prime/TOONS/Soldado_cartoon_prime.tscn" },
		{ typeof(MaguinPrime),      "res://cartas prime/TOONS/Campero_cartoon_prime.tscn" },
		{ typeof(GolemPrime),       "res://cartas prime/TOONS/Tanque_cartoon_prime.tscn" },
	};

	// Mientras es true, MostrarMenuTropa (Campo1.Flujo.cs) no abre nada y no muestra ningún aviso —
	// ni siquiera el de "coloca tus tropas". Se usa durante transiciones del tutorial (el rival
	// apareciendo, el mensaje "¡Bien hecho!...") donde tocar una tropa antes no debería hacer NADA,
	// ni interrumpir con un cartel — antes, tocar una tropa en esa ventana repetía el aviso de
	// "coloca tus tropas restantes", que ya no aplicaba (bug reportado).
	private bool _tutorialBloqueoTotal = false;

	private async void InvocarRivalTutorial()
	{
		// Se llama apenas el jugador termina de colocar su 3ª tropa (ver TropaInvocada en
		// Campo1.Flujo.cs). _faseApertura se apaga YA (no solo cuando arranca el combate guiado) —
		// _tutorialBloqueoTotal es quien de verdad impide tocar algo hasta que corresponda.
		_faseApertura = false;
		_tutorialBloqueoTotal = true;

		foreach (string nombre in new[] { "Mod1", "Mod2", "Mod3" })
		{
			Node2D zonaJugador = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			Node ocupado = zonaJugador?.GetNodeOrNull("Ocupado");
			Node2D tropaJugador = (ocupado != null && ocupado.HasMeta("tropa_instanciada"))
				? (Node2D)ocupado.GetMeta("tropa_instanciada") : null;
			if (tropaJugador == null) continue;

			string ruta = null;
			foreach (var par in RIVAL_FORZADO_TUTORIAL)
			{
				if (par.Key.IsInstanceOfType(tropaJugador)) { ruta = par.Value; break; }
			}
			if (ruta == null) continue;

			// Aparecen en orden — ModRival1, después ModRival2, después ModRival3 — no las 3 juntas.
			Node2D zonaRival = GetTree().Root.FindChild("ModRival" + nombre.Substring(3), true, false) as Node2D;
			var escena = GD.Load<PackedScene>(ruta);
			if (zonaRival != null && escena != null)
			{
				InvocacionRival(zonaRival, escena);
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(0.55f), "timeout");
				if (juegoTerminado) return;
			}
		}
	}

	// ── PRIMER TURNO DE COMBATE GUIADO (3 acciones forzadas, una por tropa) ───────────────────
	private enum AccionForzadaTutorial { Ninguna, Atacar, Defender, Habilidad }
	private System.Type            _tropaForzadaTutorial;
	private AccionForzadaTutorial  _accionForzadaTutorial = AccionForzadaTutorial.Ninguna;
	private Action                 _alCompletarPasoForzadoTutorial;

	/// <summary>Deja habilitado en el menú de acciones SOLO el botón indicado, y solo para la tropa
	/// indicada (ver los ganchos en MostrarMenuTropa/_on_btn_*_pressed, Campo1.Flujo.cs).</summary>
	private void ForzarPasoTutorial(System.Type tipoTropa, AccionForzadaTutorial accion, Action alCompletar)
	{
		_tropaForzadaTutorial = tipoTropa;
		_accionForzadaTutorial = accion;
		_alCompletarPasoForzadoTutorial = alCompletar;
	}

	/// <summary>La llaman los handlers de ataque/defensa/habilidad al completar la acción. Si no
	/// coincide con el paso forzado vigente (o no hay ninguno), no hace nada.</summary>
	private void AvanzarPasoForzadoTutorial(AccionForzadaTutorial completada)
	{
		if (_accionForzadaTutorial == AccionForzadaTutorial.Ninguna) return;
		// En el remate final vale tanto ATAQUE como la HABILIDAD del Dragón: las dos matan enemigos.
		bool coincide = _accionForzadaTutorial == completada
			|| (_remateFinalTutorial && completada == AccionForzadaTutorial.Habilidad);
		if (!coincide) return;
		_tropaForzadaTutorial = null;
		_accionForzadaTutorial = AccionForzadaTutorial.Ninguna;
		var siguiente = _alCompletarPasoForzadoTutorial;
		_alCompletarPasoForzadoTutorial = null;
		siguiente?.Invoke();
	}

	private void PasoTuTurnoCombateTutorial()
	{
		_tutorialBloqueoTotal = false; // recién ahora se puede abrir el menú de una tropa
		MostrarGuiaConTexto("Ahora es tu turno. Tú vas a poder decidir, pero por ahora te damos las " +
			"indicaciones: haz clic en el Soldado Real y elige ATAQUE.");
		ForzarPasoTutorial(typeof(SoldadoRealPrime), AccionForzadaTutorial.Atacar, PasoDefenderMaguinTutorial);
	}

	private void PasoDefenderMaguinTutorial()
	{
		MostrarGuiaConTexto("Ahora haz clic en el Maguín y elige DEFENSA.");
		ForzarPasoTutorial(typeof(MaguinPrime), AccionForzadaTutorial.Defender, PasoAtacarGolemTutorial);
	}

	private void PasoAtacarGolemTutorial()
	{
		MostrarGuiaConTexto("Por último, haz clic en el Gólem y elige ATAQUE.");
		ForzarPasoTutorial(typeof(GolemPrime), AccionForzadaTutorial.Atacar, PasoFinPrimerTurnoTutorial);
	}

	private void PasoFinPrimerTurnoTutorial()
	{
		// Gastaste las 3 energías del turno (una por acción) — el cambio de turno hacia el rival lo
		// maneja el flujo normal del juego (RegistrarGastoMovimiento -> CambiarTurno al llegar a 0),
		// que termina llamando a EjecutarTurnoCPU() → EjecutarTurnoCPUTutorial() acá abajo.
		CerrarGuiaTutorial();
	}

	// ── TURNO DE ATAQUE GUIONADO DEL RIVAL ────────────────────────────────────────────────────
	// Nada de IA real: ataca en este orden fijo, sin habilidad ni ardides ni invocar de más.
	// Soldado Cartoon → el Soldado Real (recién atacó, sin escudo). Campero → el Maguín (tiene
	// escudo por la DEFENSA del paso anterior, así que este golpe no le hace daño). Tanque → el
	// Gólem. El orden de tipos, no de carril, porque InvocarRivalTutorial ya garantizó que cada
	// tropa rival está en el MISMO carril que su contraparte fijada.
	private static readonly System.Type[] ORDEN_ATAQUE_RIVAL_TUTORIAL =
		{ typeof(SoldadoCartoonPrime), typeof(CamperoCartoonPrime), typeof(TanqueCartoonPrime) };

	private async Task EjecutarTurnoCPUTutorial()
	{
		foreach (var tipo in ORDEN_ATAQUE_RIVAL_TUTORIAL)
		{
			if (juegoTerminado) return;
			Node2D tropa = null;
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			{
				if (n is Node2D n2 && IsInstanceValid(n2) && tipo.IsInstanceOfType(n2)) { tropa = n2; break; }
			}
			if (tropa == null) continue; // no debería faltar en este punto de la secuencia

			await EsperarTableroLibre();
			if (juegoTerminado || !IsInstanceValid(tropa)) return;
			if (BuscarObjetivoEnCarril(tropa, "tropas_jugador") != null)
				ProcesarCombateFrontal(tropa, "tropas_jugador");
			ActualizarInterfaz();
			await ToSignal(GetTree().CreateTimer(0.8f, false), "timeout");
			if (juegoTerminado) return;
		}

		if (juegoTerminado) return;

		_rondaRivalTutorial++;
		CambiarTurno(); // vuelve el turno al jugador
		// Deja ver el cartel "TU TURNO" y arranca el paso guionado que toque según la ronda:
		//   ronda 1 → Curación + habilidad del Maguín + Barajar
		//   ronda 2 → remate final (atacar con todo)
		//   ronda 2 → sacrificio guiado del Maguín + invocar reemplazo, y de ahí al remate
		Action siguiente = _rondaRivalTutorial <= 1 ? PasoCurarSoldadoTutorial : PasoSacrificarMaguinTutorial;
		GetTree().CreateTimer(1.6f).Timeout += () => { if (!juegoTerminado) siguiente(); };
	}

	private int _rondaRivalTutorial = 0;

	// ═══════════════════════════════════════════════════════════════════════════
	// SEGUNDA MITAD GUIONADA: Curación → Habilidad Maguín → (sacrificio/remate/aprobado, próximas)
	// ═══════════════════════════════════════════════════════════════════════════

	// ── PIEZA 1: CURACIÓN GUIADA AL SOLDADO REAL ──────────────────────────────────────────────
	private bool _esperandoCuracionTutorial = false;

	private void PasoCurarSoldadoTutorial()
	{
		// Bloqueo de menús de tropa mientras se cura (el arrastre de la carta de Curación NO pasa por
		// MostrarMenuTropa, así que sigue permitido; esto solo evita que ataque una tropa por error).
		_tutorialBloqueoTotal = true;
		_esperandoCuracionTutorial = true;
		// Todo el HUD y la mano quedan semitransparentes y sin responder MENOS la carta de Curación,
		// que es lo único que se puede tocar en este paso.
		AtenuarTodoMenosCuracionTutorial();
		MostrarGuiaConTexto("El Soldado Real quedó herido por el ataque enemigo. Arrastra tu carta de " +
			"CURACIÓN sobre él (el círculo verde te marca dónde soltarla) para devolverle vida.");
	}

	/// <summary>La llama AplicarCuracion (Campo1.Hechizos.cs) cuando se cura una tropa en modo tutorial.
	/// Solo avanza si estábamos esperando la curación y fue sobre el Soldado Real.</summary>
	private void NotificarCuracionTutorial(Node2D objetivo)
	{
		if (!_esperandoCuracionTutorial || !(objetivo is SoldadoRealPrime)) return;
		_esperandoCuracionTutorial = false;
		_cambioArdidPermitidoTutorial = true; // ya se usó la Curación: el botón pasa a funcionar
		LimpiarAvisoActual();
		_tutorialBloqueoTotal = false;
		RestaurarResaltadoBotonTutorial(); // devuelve el HUD y la mano a la normalidad
		CallDeferred(nameof(PasoMaguinTutorial));
	}

	// ── PIEZA 2: HABILIDAD DEL MAGUÍN SOBRE EL TANQUE (transformación permanente) ──────────────
	private void PasoMaguinTutorial()
	{
		MostrarGuiaConTexto("¡Bien hecho! Ahora usa la HABILIDAD del Maguín: transformará al Tanque " +
			"enemigo en un pez indefenso… ¡y en el tutorial es para siempre! Toca al Maguín y elige HABILIDAD.");
		// Solo se puede tocar el Maguín, y solo su botón HABILIDAD queda habilitado. Al usarla,
		// AvanzarPasoForzadoTutorial(Habilidad) dispara el siguiente paso.
		ForzarPasoTutorial(typeof(MaguinPrime), AccionForzadaTutorial.Habilidad, PasoTrasMaguinTutorial);
	}

	// ── PIEZA 3: BARAJAR GUIADO (es lo que trae cartas nuevas a la mano) ──────────────────────
	// Tras transformar al Tanque, el cuadro pide usar BARAJAR. Ese botón queda agrandado y
	// balanceándose (mismo tratamiento visual que el de Sacrificio cuando está activo) y TODO lo
	// demás del HUD queda atenuado y bloqueado, para que no haya forma de equivocarse.
	private void PasoTrasMaguinTutorial()
	{
		MostrarGuiaConTexto("¡Excelente! El Tanque quedó convertido en pez para siempre. Ahora usa el " +
			"botón BARAJAR: con él cambias tu mano y te llegan cartas nuevas para invocar.");
		_esperandoBarajarTutorial = true;
		_barajarPermitidoTutorial = true; // recién ahora el botón funciona
		// Se desbloquea la reposición ANTES de barajar: así el barajado ya reparte cartas nuevas.
		_tutorialManoBloqueada = false;
		ResaltarSoloBotonTutorial(btnBarajar);
	}

	private bool _esperandoBarajarTutorial = false;

	/// <summary>La llama _on_barajar_pressed (Campo1.Flujo.cs) después de barajar, en modo tutorial.</summary>
	private void NotificarBarajarTutorial()
	{
		if (!_esperandoBarajarTutorial) return;
		_esperandoBarajarTutorial = false;
		_barajarPermitidoTutorial = false; // una sola vez en todo el tutorial
		RestaurarResaltadoBotonTutorial();
		CerrarGuiaTutorial();
		// El barajado gasta la última energía del turno, así que el juego pasa solo al turno del
		// rival → EjecutarTurnoCPUTutorial (segunda ronda de ataques, sin matar a nadie).
	}

	// ── PERMISOS DE BOTONES DEL TUTORIAL ──────────────────────────────────────────────────────
	// Los botones se ven SIEMPRE normales (nunca atenuados), pero solo funcionan en el momento que
	// el guion los pide. Fuera de ese momento avisan "No disponible por el momento" y no hacen nada.
	// Barajar: una sola vez, en su paso. Sacrificio: una sola vez, para el Maguín.
	private bool _barajarPermitidoTutorial    = false;
	private bool _sacrificioPermitidoTutorial = false;
	// Cambiar de ardid: bloqueado hasta que se use la Curación (antes de eso, cambiarla rompía el
	// paso guiado). Después ya da igual, porque ningún otro ardid se puede usar — se deja funcionar
	// solo para que se vea que el botón responde.
	private bool _cambioArdidPermitidoTutorial = false;

	/// <returns>true si se puede cambiar de ardid ahora; si no, avisa y devuelve false.</returns>
	private bool PuedeCambiarArdidTutorial()
	{
		if (_cambioArdidPermitidoTutorial) return true;
		MostrarAviso("No disponible por el momento", Colors.Gold);
		return false;
	}

	/// <returns>true si el barajado puede ejecutarse ahora; si no, avisa y devuelve false.</returns>
	private bool PuedeUsarBarajarTutorial()
	{
		if (_barajarPermitidoTutorial) return true;
		MostrarAviso("No disponible por el momento", Colors.Gold);
		return false;
	}

	/// <returns>true si el sacrificio puede activarse ahora; si no, avisa y devuelve false.</returns>
	private bool PuedeUsarSacrificioTutorial()
	{
		if (_sacrificioPermitidoTutorial) return true;
		MostrarAviso("No disponible por el momento", Colors.Gold);
		return false;
	}

	/// <summary>Paso de Curación: bloquea las CARTAS menos la de Curación (los botones del HUD se
	/// siguen viendo normales y avisan al pulsarlos).</summary>
	private void AtenuarTodoMenosCuracionTutorial()
	{
		RestaurarResaltadoBotonTutorial();

		// Los botones del HUD NO se atenúan (se ven normales); si se pulsan fuera de su momento
		// avisan "No disponible por el momento". Acá solo se bloquean las CARTAS.

		// Mano de tropas: entera bloqueada (en este paso no se invoca nada).
		if (contenedorMano != null && IsInstanceValid(contenedorMano))
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && IsInstanceValid(c)) c.BloquearPorModo(true);

		// Mano de ardides: se bloquea carta por carta, salvando la de Curación.
		if (_contenedorHechizos != null && IsInstanceValid(_contenedorHechizos))
		{
			foreach (Node n in _contenedorHechizos.GetChildren())
			{
				if (n is not Carta c || !IsInstanceValid(c)) continue;
				bool esCuracion = c.HechizoPi >= 0 && c.HechizoPi < _poolActivo.Length
					&& _poolActivo[c.HechizoPi].Id == "curacion";
				if (!esCuracion) c.BloquearPorModo(true);
			}
		}
	}

	// ── PIEZA 3.5: SACRIFICIO GUIADO DEL MAGUÍN ───────────────────────────────────────────────
	// Turno 3: el Maguín quedó muy golpeado. El único botón vivo es SACRIFICIO; el aviso explica el
	// doble clic (la confirmación de dos toques ya existe en el juego normal). Al morir, se libera
	// su carril y toca invocar una de las 3 cartas nuevas que trajo el barajado.
	private bool _esperandoSacrificioTutorial = false;
	private bool _esperandoInvocarReemplazoTutorial = false;

	private void PasoSacrificarMaguinTutorial()
	{
		_esperandoSacrificioTutorial = true;
		_sacrificioPermitidoTutorial = true; // recién ahora el botón funciona
		MostrarGuiaConTexto("El Maguín quedó muy herido y ya no aguanta otro golpe. Usa el botón de " +
			"SACRIFICIO para retirarlo y poder invocar una tropa nueva en su lugar.");
		MostrarAviso("Sacrifica al Maguín para seguir la batalla", Colors.OrangeRed);
		ResaltarSoloBotonTutorial(btnSacrificio);
	}

	/// <summary>La llama VerificarSacrificioEnCampo (Campo1.Flujo.cs) al confirmar un sacrificio.</summary>
	private void NotificarSacrificioTutorial(Node2D tropa)
	{
		if (!_esperandoSacrificioTutorial) return;
		_esperandoSacrificioTutorial = false;
		_sacrificioPermitidoTutorial = false; // una sola vez en todo el tutorial
		LimpiarAvisoActual();
		RestaurarResaltadoBotonTutorial();
		_esperandoInvocarReemplazoTutorial = true;

		MostrarGuiaConTexto("Sé que duele, pero hay que seguir. Ahora invoca una de tus cartas nuevas " +
			"en el carril que quedó libre.");
		// El cuadro se cierra con un toque; recién ahí sale el aviso de la acción (antes se quedaba
		// puesto tapando la mano mientras había que elegir la carta nueva).
		EsperarClickParaAvanzar(() =>
		{
			CerrarGuiaTutorial();
			MostrarAviso("Elige una carta nueva e invócala", Colors.Gold);
		});
	}

	/// <summary>La llama TropaInvocada (Campo1.Flujo.cs) cuando el jugador pone la tropa de reemplazo.</summary>
	private void NotificarInvocacionReemplazoTutorial()
	{
		if (!_esperandoInvocarReemplazoTutorial) return;
		_esperandoInvocarReemplazoTutorial = false;
		LimpiarAvisoActual();
		// Barajar y sacrificio ya cumplieron su función: quedan muertos el resto del tutorial.
		_tutorialManoBloqueada = true;
		CerrarGuiaTutorial();
		GetTree().CreateTimer(0.8f).Timeout += () => { if (!juegoTerminado) PasoRemateFinalTutorial(); };
	}

	// ── PIEZA 4: REMATE FINAL (atacar con todo) ───────────────────────────────────────────────
	// Último turno del jugador: solo está habilitado ATAQUE, en cualquiera de las 3 tropas. El
	// cuadro da la indicación una vez y desaparece; el aviso recuerda que hay que atacar con las 3.
	private bool _remateFinalTutorial = false;

	private void PasoRemateFinalTutorial()
	{
		_remateFinalTutorial = true;
		_tutorialBloqueoTotal = false;
		// Energía al máximo: el remate necesita 3 ataques y, si el turno venía gastado, el jugador se
		// quedaba sin energía a mitad y el tutorial se trababa sin salida.
		movimientosRestantes = ENERGIA_MAXIMA;
		ActualizarInterfaz();
		MostrarGuiaConTexto("¡Última indicación! Ataca con todo: tienes que eliminar a las 3 tropas " +
			"enemigas para que el huevo rival caiga y ganes el tutorial.");
		// Cualquier tropa propia sirve, pero SOLO el botón de ataque (tropa = null → no se filtra por
		// tipo; la acción forzada sigue limitando los botones en MostrarMenuTropa).
		ForzarPasoTutorial(null, AccionForzadaTutorial.Atacar, ProgramarContinuarRemateTutorial);
		EsperarClickParaAvanzar(() =>
		{
			CerrarGuiaTutorial();
			MostrarAviso($"Elimina a las {ContarTropasRivalesTutorial()} tropas rivales", Colors.Gold);
		});
	}

	/// <summary>Espera a que termine la animación de muerte antes de revisar el tablero: si se
	/// consultara en el mismo instante del golpe, la tropa recién matada todavía contaría como viva.</summary>
	private void ProgramarContinuarRemateTutorial()
	{
		// Se re-arma YA (no después del timer) para que en el rato que dura la animación de muerte
		// no quede ninguna ventana con los botones de defensa/habilidad sueltos. De paso se rellena
		// la energía: en el remate nunca puede faltar para terminar de matar a las 3 tropas.
		if (_remateFinalTutorial)
		{
			movimientosRestantes = ENERGIA_MAXIMA;
			ActualizarInterfaz();
			ForzarPasoTutorial(null, AccionForzadaTutorial.Atacar, ProgramarContinuarRemateTutorial);
		}
		GetTree().CreateTimer(1.2f).Timeout += () => { if (!juegoTerminado) ContinuarRemateFinalTutorial(); };
	}

	/// <summary>Tras cada ataque del remate, se vuelve a dejar solo ATAQUE habilitado (para las tropas
	/// que todavía no actuaron) y se revisa si ya no queda ninguna tropa rival viva.</summary>
	private void ContinuarRemateFinalTutorial()
	{
		if (!_remateFinalTutorial || juegoTerminado) return;

		// Cada tropa rival que cae le arranca un tercio de la vida a su huevo: así la barra baja de a
		// poco, a medida que se van eliminando, en vez de desplomarse de golpe al final.
		int vivas = ContarTropasRivalesTutorial();
		int objetivo = Mathf.RoundToInt(VIDA_RIVAL_TUTORIAL * (vivas / 3f));
		if (vivas > 0 && vidaRival > objetivo)
		{
			vidaRival = objetivo;
			ActualizarInterfaz();
		}

		if (vivas > 0)
		{
			// Sigue el remate: se re-arma el paso forzado para el próximo ataque.
			ForzarPasoTutorial(null, AccionForzadaTutorial.Atacar, ProgramarContinuarRemateTutorial);
			return;
		}

		// Ya no queda ninguna tropa rival: el huevo rival (que en el tutorial arranca con muy poca
		// vida) cae a 0 y la partida se cierra en victoria. Se fuerza acá en vez de esperar al
		// castigo por carriles vacíos, para que pase justo al morir la última tropa.
		_remateFinalTutorial = false;
		_accionForzadaTutorial = AccionForzadaTutorial.Ninguna;
		_tropaForzadaTutorial  = null;

		PasoCierreTutorial();
		GetTree().CreateTimer(2.4f).Timeout += () =>
		{
			if (juegoTerminado) return;
			CerrarGuiaTutorial();
			vidaRival = 0;
			ActualizarInterfaz();
			CheckEstadoJuego(); // → FinalizarPartida("VICTORIA")
		};
	}

	/// <summary>Cuántas tropas rivales siguen vivas. Cuenta también al PEZ en el que se transformó el
	/// Tanque: sigue siendo una tropa rival y hay que matarlo igual para terminar.</summary>
	private int ContarTropasRivalesTutorial()
	{
		int vivas = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
		{
			if (n is not Node2D t || !IsInstanceValid(t) || t.IsQueuedForDeletion()) continue;
			if (t is TropaBase tb && tb.EstaMuerta) continue;
			vivas++;
		}
		return vivas;
	}

	// ── PIEZA 5: CIERRE ───────────────────────────────────────────────────────────────────────
	private void PasoCierreTutorial()
	{
		MostrarGuiaConTexto("¡BIEN HECHO! Ya sabes lo básico de EGGODIA. Espero que ganes muchas " +
			"batallas más y armes una estrategia todavía mejor.");
		// PRÓXIMA PIEZA (aún no armada): la frase "TUTORIAL COMPLETADO" y la pantalla de victoria
		// especial (sin monedas, con el botón "Repetir tutorial" en vez de "Reintentar"). Mientras
		// tanto, al morir las 3 tropas rivales el flujo normal de fin de partida hace su trabajo.
	}

	// ── RESALTAR UN SOLO BOTÓN DEL HUD (y bloquear el resto) ─────────────────────────────────
	private readonly List<(CanvasItem nodo, Color modulate, bool eraDisabled, Control.MouseFilterEnum filtro)> _atenuadosTutorial = new();
	private Tween         _tweenBaileBotonTutorial;
	private TextureButton _botonResaltadoTutorial;
	private Vector2       _escalaPrevioBotonTutorial = Vector2.One;

	private void ResaltarSoloBotonTutorial(TextureButton objetivo)
	{
		RestaurarResaltadoBotonTutorial();
		if (objetivo == null || !IsInstanceValid(objetivo)) return;
		_botonResaltadoTutorial = objetivo;

		// Ya NO se atenúa nada del HUD: todos los botones se siguen viendo normales y disponibles.
		// Lo que impide usarlos fuera de su momento son los permisos del tutorial (ver
		// PuedeUsarBarajarTutorial / PuedeUsarSacrificioTutorial), que muestran "No disponible por el
		// momento" al pulsarlos. El único indicador visual es que el botón que TOCA usar crece y se
		// balancea.
		objetivo.Disabled    = false;
		objetivo.Modulate    = Colors.White;
		objetivo.MouseFilter = Control.MouseFilterEnum.Stop;
		objetivo.ZIndex      = 60;
		objetivo.PivotOffset = objetivo.Size / 2f;
		_escalaPrevioBotonTutorial = objetivo.Scale;
		Vector2 agrandado = _escalaPrevioBotonTutorial * 1.18f;
		if (_escalaReposoBoton.ContainsKey(objetivo)) _escalaReposoBoton[objetivo] = agrandado;
		objetivo.CreateTween().TweenProperty(objetivo, "scale", agrandado, 0.15f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		_tweenBaileBotonTutorial = objetivo.CreateTween().SetLoops();
		_tweenBaileBotonTutorial.TweenProperty(objetivo, "rotation", Mathf.DegToRad(2.5f), 0.18f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_tweenBaileBotonTutorial.TweenProperty(objetivo, "rotation", Mathf.DegToRad(-2.5f), 0.36f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_tweenBaileBotonTutorial.TweenProperty(objetivo, "rotation", 0f, 0.18f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void AtenuarNodoTutorial(CanvasItem ci)
	{
		bool eraDisabled = ci is BaseButton bb && bb.Disabled;
		var filtroPrevio = ci is Control ctrl ? ctrl.MouseFilter : Control.MouseFilterEnum.Ignore;
		_atenuadosTutorial.Add((ci, ci.Modulate, eraDisabled, filtroPrevio));
		ci.Modulate = new Color(ci.Modulate.R, ci.Modulate.G, ci.Modulate.B, 0.35f);
		if (ci is BaseButton btn) btn.Disabled = true;
		if (ci is Control c) c.MouseFilter = Control.MouseFilterEnum.Ignore;
	}

	private void RestaurarResaltadoBotonTutorial()
	{
		_tweenBaileBotonTutorial?.Kill();
		_tweenBaileBotonTutorial = null;

		if (_botonResaltadoTutorial != null && IsInstanceValid(_botonResaltadoTutorial))
		{
			_botonResaltadoTutorial.Rotation = 0f;
			_botonResaltadoTutorial.ZIndex   = 0;
			if (_escalaReposoBoton.ContainsKey(_botonResaltadoTutorial))
				_escalaReposoBoton[_botonResaltadoTutorial] = _escalaPrevioBotonTutorial;
			_botonResaltadoTutorial.Scale = _escalaPrevioBotonTutorial;
		}
		_botonResaltadoTutorial = null;

		foreach (var (ci, modulate, eraDisabled, filtroPrevio) in _atenuadosTutorial)
		{
			if (!IsInstanceValid(ci)) continue;
			ci.Modulate = modulate;
			if (ci is BaseButton btn) btn.Disabled = eraDisabled;
			if (ci is Control c) c.MouseFilter = filtroPrevio;
		}
		_atenuadosTutorial.Clear();
		foreach (Control mano in new[] { contenedorMano, _contenedorHechizos })
		{
			if (mano == null || !IsInstanceValid(mano)) continue;
			foreach (Node n in mano.GetChildren())
				if (n is Carta c && IsInstanceValid(c)) c.BloquearPorModo(false);
		}
	}

}
