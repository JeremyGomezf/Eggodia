using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── MÚSICA Y AUDIO ───────────────────────────────────────────────────
	// Ya no hay una única canción fija: ElegirEscenarioBatalla() asigna la música según el
	// escenario sorteado para esta partida (ver más abajo).
	private AudioStream _musicaPartida;
	// -6dB respecto a la música del menú (que suena a 0dB propio): a 0dB quedaba muy fuerte en
	// batalla. Ambas siguen afectadas por igual por el volumen/mute del Bus Master.
	[Export] private float _volumenMusicaDb = -6.0f;
	private AudioStreamPlayer _reproductorMusica;

	// ── ESCENARIOS DE BATALLA (5 mapas visuales+musicales, sorteados por partida) ─────────
	// "digital" tiene 80% de probabilidad (mapa nuevo, para probarlo de una); los 4 mapas de
	// siempre se reparten el 20% restante, proporcional al peso que ya tenían entre ellos. Cada
	// escenario trae su propio FONDO/ESCENARIO (los dos Sprite2D ya existentes en la escena) y su
	// propia música — y, al perder, un segundo específico desde donde arrancar esa música (sin
	// loop, se apaga sola al terminar). El mapa "digital" es especial: no usa Fondo fijo, tiene su
	// propia secuencia de cambios de fondo sincronizada con la música (ver Campo1.SecuenciaDigital.cs).
	private struct EscenarioBatalla
	{
		public string Fondo, Escenario, Musica, Nombre;
		public float SegundoDerrota;
		public float Peso;
		public float VolumenExtraDb; // encima del volumen base de batalla (_volumenMusicaDb)
	}
	private static readonly EscenarioBatalla[] ESCENARIOS_BATALLA =
	{
		new EscenarioBatalla { Nombre = "digital", Peso = 3f,
			Fondo = "res://imagenes/Escenarios/digitalFONDO.png", Escenario = "res://imagenes/Escenarios/digital-escenario.png",
			Musica = "res://efectos/musica/MUSICA DIGITAL.mp3", SegundoDerrota = 2 * 60 + 46 },
		new EscenarioBatalla { Nombre = "medieval", Peso = 7f,
			Fondo = "res://imagenes/Escenarios/medievalFONDO.png", Escenario = "res://imagenes/Escenarios/medieval-escenario.png",
			Musica = "res://efectos/musica/MUSICA MEDIEVAL.mp3", SegundoDerrota = 2 * 60 + 53 },
		new EscenarioBatalla { Nombre = "ajedrez", Peso = 4f,
			Fondo = "res://imagenes/Escenarios/ajedrezlFONDO.png", Escenario = "res://imagenes/Escenarios/ajedrez-escenario.png",
			Musica = "res://efectos/musica/MUSICA AJEDREZ.mp3", SegundoDerrota = 3 * 60 + 26 },
		new EscenarioBatalla { Nombre = "toon", Peso = 5f,
			Fondo = "res://imagenes/Escenarios/toonslFONDO.png", Escenario = "res://imagenes/Escenarios/toons-escenario.png",
			Musica = "res://efectos/musica/MUSICA TOON.mp3", SegundoDerrota = 3 * 60 + 5 },
		new EscenarioBatalla { Nombre = "papeleo", Peso = 4f,
			Fondo = "res://imagenes/Escenarios/papeleolFONDO.png", Escenario = "res://imagenes/Escenarios/papeleo-escenario.png",
			Musica = "res://efectos/musica/MUSICA PAPELEO.mp3", SegundoDerrota = 4 * 60 + 42, VolumenExtraDb = 3f },
	};
	public bool EscenarioEsDigital => ESCENARIOS_BATALLA[_idxEscenarioActual].Nombre == "digital";
	private int   _idxEscenarioActual    = 0;
	private float _segundoDerrotaMusica  = 0f;
	private float _volumenExtraEscenario = 0f;
	private ColorRect _rectVintage;
	public  bool  EscenarioEsToon => ESCENARIOS_BATALLA[_idxEscenarioActual].Nombre == "toon";

	/// <summary>El azar normal (campo "random") NO está sincronizado entre los dos clientes de una
	/// partida online — cada uno tira su propio dado. Para el escenario de batalla específicamente
	/// necesitamos que ambos vean el mismo mapa/música, así que en modo online usamos la semilla que
	/// ya manda el servidor por partida (ContextoOnline.Semilla, ver MatchmakingOnline.cs / Backend
	/// GestorPartidas.Semilla) para que los dos clientes calculen exactamente la misma tirada sin
	/// necesitar ninguna acción de red nueva. Fuera de modo online, usa el random normal de siempre.</summary>
	private Random ObtenerRandomEscenario()
	{
		if (ContextoOnline.Activo && int.TryParse(ContextoOnline.Semilla, out int semilla))
			return new Random(semilla);
		return random;
	}

	private void ElegirEscenarioBatalla()
	{
		float total = 0f;
		foreach (var e in ESCENARIOS_BATALLA) total += e.Peso;
		float r = (float)ObtenerRandomEscenario().NextDouble() * total;
		float acumulado = 0f;
		int elegido = ESCENARIOS_BATALLA.Length - 1;
		for (int i = 0; i < ESCENARIOS_BATALLA.Length; i++)
		{
			acumulado += ESCENARIOS_BATALLA[i].Peso;
			if (r <= acumulado) { elegido = i; break; }
		}
		_idxEscenarioActual = elegido;
		var esc = ESCENARIOS_BATALLA[elegido];
		_segundoDerrotaMusica  = esc.SegundoDerrota;
		_volumenExtraEscenario = esc.VolumenExtraDb;

		var fondoNode     = GetNodeOrNull<Sprite2D>("FONDO");
		var escenarioNode = GetNodeOrNull<Sprite2D>("ESCENARIO");
		if (fondoNode != null && ResourceLoader.Exists(esc.Fondo))         fondoNode.Texture     = GD.Load<Texture2D>(esc.Fondo);
		if (escenarioNode != null && ResourceLoader.Exists(esc.Escenario)) escenarioNode.Texture = GD.Load<Texture2D>(esc.Escenario);
		if (ResourceLoader.Exists(esc.Musica)) _musicaPartida = GD.Load<AudioStream>(esc.Musica);

		GD.Print($"[Campo1] Escenario de batalla: {esc.Nombre}");
	}

	// ── VIDA ──────────────────────────────────────────────────────────────
	[Export] public int vidaJugador   = 2000;
	[Export] public int vidaRival     = 2000;
	public const   int vidaMaxJugador = 2000;
	private int tiempoTotalPartida    = 180; // 3:00 por partida (VS BOT y online)
	public  bool juegoTerminado       = false;

	// ── TURNOS ────────────────────────────────────────────────────────────
	public  const int ENERGIA_MAXIMA   = 3; // fija, sin escalado por turno
	public  bool esTurnoJugador        = true;
	public  bool EsOnline              = false; // partida en línea (rival humano en vez de CPU)
	// Modo "solo visual": true durante TODO el turno del rival en línea. Mientras está activo, este
	// cliente reproduce las jugadas del rival con sus animaciones/efectos reales pero NO muta el estado
	// (RecibirDaño, muerte de tropas y avisos de texto quedan en no-op); los números autoritativos los
	// pone el snapshot (ReconciliarLigero). Al ser por-turno (no solo durante la llamada síncrona)
	// también cubre el daño DIFERIDO por fotograma de los efectos. Se resetea en _Ready de cada partida.
	public static bool SoloVisualOnline = false;
	// Suprime los avisos de texto SOLO durante la reproducción síncrona de una acción del rival (para
	// que no salgan mensajes con la perspectiva del que actúa, "¡Envenenaste a…!"). Es transitorio, no
	// por-turno, para NO ocultar avisos legítimos (ej. "el rival se desconectó"). Ver EjecutarVisualOnline.
	public static bool SuprimiendoAvisosOnline = false;
	// Últimos 15s antes de que caiga una bomba Nuclear: una tropa en pre-defensa que recibe un golpe
	// NO pierde la guardia mientras le quede escudo (vuelve a "pre defensa" en vez de a "idle"). Solo
	// la pierde si le bajan el escudo a 0. Lo usa TropaBase.ReproducirDefensa. Se resetea en _Ready.
	public static bool GuardiaNuclearActiva = false;
	public const  int  SEGUNDOS_GUARDIA_NUCLEAR = 15;
	public  int  movimientosRestantes  = ENERGIA_MAXIMA;
	public  const int DURACION_TURNO_SEG = 20; // segundos por turno
	private int  tiempoTurnoActual     = DURACION_TURNO_SEG;
	private bool _turnoFinalizando     = false; // evita doble CambiarTurno (energía agotada + reloj a la vez)
	private Timer timerReloj;

	// EP mostrado por bando: se aísla del contador compartido `movimientosRestantes` (que en
	// realidad representa "energía del que tiene el turno ahora"), para que el panel del bando
	// inactivo conserve su último valor real en vez de reflejar el gasto del otro bando.
	private int _epMostradoJugador = ENERGIA_MAXIMA;
	private int _epMostradoRival   = ENERGIA_MAXIMA;

	// Fase de turno: primero invocar, luego atacar
	public  bool faseInvocacion       = true;
	private int  tropasInvocadasTurno = 0;

	// ── SISTEMA DE COMBOS Y RACHAS ────────────────────────────────────────
	private int  _comboTurno          = 0;   // tropas eliminadas en turno actual
	private int  _rachaVictorias      = 0;   // victorias consecutivas
	private int  _totalTropasElimIA   = 0;   // para logros

	// ── LOGROS ────────────────────────────────────────────────────────────
	private bool _logroPrimeraVictoria  = false;
	private bool _logro10Tropas         = false;
	private bool _logroHabilidadUsada   = false;
	private bool _logroHechizosUsados   = false;

	// ── FASE DE APERTURA ─────────────────────────────────────────────────
	private bool _faseApertura         = true;

	// La ronda de invocación (armar el campo al inicio de la partida) no debe contar como progreso
	// de ronda para el desbloqueo de habilidades — igual que invocar una tropa a mitad de partida no
	// cuenta su propio turno de invocación. Cada bando tiene su primer AvanzarTurnoTropa() salteado
	// una única vez, justo en la transición que cierra la fase de apertura (ver Campo1.Turnos.cs y
	// Campo1.CPU.cs) — después de esa vez, cuenta normal.
	private bool _primerAvanceJugadorPendiente = true;
	private bool _primerAvanceRivalPendiente   = true;

	// ── MODO DEMO ─────────────────────────────────────────────────────────
	private bool _modoDemo             = false;
	private Timer _timerDemo;

	// ── LIMITADORES ───────────────────────────────────────────────────────
	private int usosBarajar           = 0;
	private int usosSacrificio        = 0;
	private const int MAX_BARAJAR     = 1;
	private const int MAX_SACRIFICIO  = 2;

	// ── UI ────────────────────────────────────────────────────────────────
	private Control       menuAcciones;
	private Node2D        tropaSeleccionada;
	// Se prende al final de MostrarMenuTropa y se consume en Campo1.Nfc._UnhandledInput: evita que
	// el cierre-al-tocar-otra-cosa se coma el menú que ESTE MISMO clic recién abrió (ver ahí).
	private bool          _menuTropaRecienAbiertoEsteClic = false;
	private TextureButton btnBarajar;
	private TextureButton btnSacrificio;
	private Vector2        _escalaOriginalBtnSacrificio;
	private Button        btnHabilidad;
	private Control       panelPausa;

	[Export] private Texture2D   iconoCursorSacrificio;
	[Export] private PackedScene escenaCartaBase;
	[Export] private Control     contenedorMano;

	// ── HECHIZOS ──────────────────────────────────────────────────────────
	// Antes cada hechizo se gastaba UNA sola vez por partida entera (usadoVeneno=true para
	// siempre). Ahora vuelven a estar disponibles pasados 5 turnos "en general" (cuentan los del
	// jugador y los del rival). "Robar Carta" además exige que la carta robada anterior (Spot4)
	// ya se haya jugado.
	private struct HechizoDef
	{
		public string Id;      // clave estable (persistencia, switch de efectos)
		public string Nombre;
		public string Ruta;    // PNG en HechizosPng
		public bool   Aliado;  // true = objetivo tropas_jugador, false = tropas_rival
	}

	// Catálogo completo: 8 hechizos disponibles. El pool ACTIVO de una partida (_poolActivo) es
	// este completo por defecto, o los 6 elegidos en MenuConstructor si el jugador los guardó
	// (ver _Ready y SesionJuego.ArdidesSeleccionados).
	private static readonly HechizoDef[] POOL_HECHIZO_BASE = {
		new HechizoDef { Id = "curacion",     Nombre = "Curación",     Ruta = "res://imagenes/HechizosPng/Cura_hechizo.png",         Aliado = true  },
		new HechizoDef { Id = "robar_carta",  Nombre = "Robar Carta",  Ruta = "res://imagenes/HechizosPng/Robo_hechizo.png",         Aliado = false },
		new HechizoDef { Id = "veneno",       Nombre = "Veneno",       Ruta = "res://imagenes/HechizosPng/Veneno_hechizo.png",       Aliado = false },
		new HechizoDef { Id = "bloqueo",      Nombre = "Bloqueo",      Ruta = "res://imagenes/HechizosPng/Bloqueo_hechizo.png",      Aliado = false },
		new HechizoDef { Id = "encebollado",  Nombre = "Encebollado",  Ruta = "res://imagenes/HechizosPng/Encebo_hechizo.png",       Aliado = true  },
		new HechizoDef { Id = "desprotegido", Nombre = "Desprotegido", Ruta = "res://imagenes/HechizosPng/Desprotegido_hechizo.png", Aliado = false },
		new HechizoDef { Id = "escudo",       Nombre = "Escudo",       Ruta = "res://imagenes/HechizosPng/Escudo_hechizo.png",       Aliado = true  },
		new HechizoDef { Id = "fuerza",       Nombre = "Fuerza",       Ruta = "res://imagenes/HechizosPng/Fuerza_hechizo.png",       Aliado = true  },
		new HechizoDef { Id = "debil",        Nombre = "Débil",        Ruta = "res://imagenes/HechizosPng/Debil_hechizo.png",        Aliado = false },
		// Nuclear: se suelta sobre el HUEVO rival (como Robar), cuesta 2 de energía y tiene su propio
		// flujo completo en Campo1.Nuclear.cs.
		new HechizoDef { Id = "nuclear",      Nombre = "Nuclear",      Ruta = "res://imagenes/HechizosPng/Nuclear_hechizo.png",      Aliado = false },
	};
	private HechizoDef[] _poolActivo = POOL_HECHIZO_BASE;
	private int[] _cooldownHechizo;
	private bool  _cartaRobadaPendiente = false;
	private Carta _cartaRobada = null; // referencia a la carta robada al rival (antes se rastreaba por NombreSpot=="Spot4")
	private Label  _lblInstruccion; // usada por Campo1.Enroque.cs ("Elige el carril de destino")
	private bool   _hechizoUsadoEsteTurno = false; // 1 hechizo/trampa por turno (jugador e IA)
	private System.Collections.Generic.List<Node> _resaltadosHechizoActivos = new();

	private int[]   _hechizosMano        = new int[2];
	private Carta[] _tarjetasHechizoCarta = new Carta[2];
	private TextureButton _btnCambiarHechizo;
	// Cooldown del botón de Ardid (ArdidBarButton): 4 cambios de turno = 2 rondas completas
	// (mío→rival→mío→rival), reemplaza el viejo tope de "3 usos por partida".
	private const int COOLDOWN_BTN_ARDID = 4;
	private int _cooldownBtnArdid = 0;

	// ── CPU ADAPTATIVA ────────────────────────────────────────────────────
	private int  _dificultadCPU       = 1; // 0=fácil, 1=medio, 2=difícil
	private int  _victoriasJugador    = 0;
	private int  _derrotasJugador     = 0;
	private int  _turnosJugados       = 0;

	// ── ESTADÍSTICAS DE PARTIDA ───────────────────────────────────────────
	private int _dañoTotalJugador     = 0;
	private int _dañoTotalRival       = 0;
	private int _tropasEliminadasJugador = 0;
	private int _tropasEliminadasRival   = 0;

	// ── MANO DE HECHIZOS (contenedor en espacio de mundo) ────────────────────
	public Control _contenedorHechizos;

	// ── BARRAS HP Y HUD NUEVO (TextureProgressBar_User / _Rival dentro de InterfazMenu) ──
	private TextureProgressBar _barraHPJugador, _barraHPRival;
	private Label _lblTiempo, _lblTurnoAviso;
	private Vector2 _turnoPanelEscalaBase = Vector2.One;
	private Label _lblUsuario, _lblCPU;
	private Control _panelEnergiaUsuario, _panelEnergiaRival;
	private Label _lblEnergiaUsuario, _lblEnergiaRival;

	private static readonly string[] NOMBRES_CPU = {
		"Bot 67", "botcito", "Carlos", "Gonzalo", "Jeremy", "Mclovin", "Ec0tec_ec2",
		"CPU xd", "Hola k ase", "Guayaco", "Campo1", "Bot 1", "Bot 2", "Bot 3", "Maestro",
		"6 a 1", "Rival malo", "KanKox", "KromaNexus", "juegocards"
	};

	// El nombre del bot (VS BOT, offline) se sortea una sola vez y se guarda acá — tanto la
	// etiqueta sobre su barra de vida (Campo1.Extra.cs) como la skin que usa en batalla
	// (CrearEscenaDeBatalla, Campo1.Flujo.cs) leen ESTE mismo valor, para que si te toca "Jeremy"
	// de rival, se vea con la piel de Jeremy — no un nombre random con una skin random sin relación.
	private string _nombreCPUElegido = "";

	/// <summary>Cuando el nombre sorteado del bot coincide con uno de los devs (o su código/alias),
	/// devuelve la escena de esa skin exclusiva; si no matchea ninguno, null (skin aleatoria normal).</summary>
	private static readonly (string clave, string escena)[] SKIN_POR_NOMBRE_CPU = {
		("jeremy", "res://escenas/personajes/huevojeremy1.tscn"),
		("carlos", "res://escenas/personajes/huevocarlos1.tscn"),
		("kankox", "res://escenas/personajes/huevocarlos1.tscn"), // mismo dev que "Carlos"
		("gonzalo", "res://escenas/personajes/huevogonzalo1.tscn"),
		("ec0tec", "res://escenas/personajes/huevoecotec1.tscn"),
	};

	private PackedScene SkinPorNombreCPU()
	{
		string n = _nombreCPUElegido.ToLowerInvariant();
		foreach (var (clave, escena) in SKIN_POR_NOMBRE_CPU)
		{
			if (n.Contains(clave) && ResourceLoader.Exists(escena)) return GD.Load<PackedScene>(escena);
		}
		return null;
	}

	// ── SECUENCIA DE FIN DE PARTIDA ─────────────────────────────────────────
	private static readonly string[] FRASES_VICTORIA = { "GG BRO", "GANADOR", "BIEN HECHO", "VAMOOOOS SIII", "OSIOSIOSI" };
	private static readonly string[] FRASES_DERROTA   = { "VALISTE OE", "YA TE FUISTE XD", "GG EZ", "ÑIÑIÑIÑI", "XDDDDxdxd :V", "TE MURISTE ÑAÑO","BYE BYE", "HUEVO ROTO" };
	private static readonly string[] CARAS_VICTORIA   = { "B)", ":)", "🥚", ":D" };
	private static readonly string[] CARAS_DERROTA    = { "XD", ":v", ":(", "🍳" };

	// ── MAZO ──────────────────────────────────────────────────────────────
	private List<int> mazoIndices       = new List<int>();
	private int       proximoIndiceMazo = 0;
	private bool      modoSacrificioActivo = false;
	private Random    random            = new Random();

	private string[] imagenesCartas = {
		"res://imagenes/CartasPng/DragonFlama_Cart.png", "res://imagenes/CartasPng/GolemPedregal_Cart.png", "res://imagenes/CartasPng/Maguin_Cart.png",
		"res://imagenes/CartasPng/SoldReal_Cart.png", "res://imagenes/CartasPng/PapeRex_Cart.png", "res://imagenes/CartasPng/Tiburon_Cart.png",
		"res://imagenes/CartasPng/Peon_Cart.png", "res://imagenes/CartasPng/Tanque_Cart.png", "res://imagenes/CartasPng/Calamar_Cart.png",
		"res://imagenes/CartasPng/Caballo_Cart.png", "res://imagenes/CartasPng/Dama_Cart.png", "res://imagenes/CartasPng/Torre_Cart.png",
		"res://imagenes/CartasPng/SoldCartoon_Cart.png", "res://imagenes/CartasPng/Campero_Cart.png", "res://imagenes/CartasPng/Arfil_Cart.png",
		"res://imagenes/CartasPng/Kabar_Cart.png", "res://imagenes/CartasPng/Granadero_Cart.png",
		"res://imagenes/CartasPng/Machi_Cart.png"
	};

	private string[] escenasTropas = {
		"res://cartas prime/MEDIEVAL/Dragon_prime.tscn",   "res://cartas prime/MEDIEVAL/Golem_prime.tscn",
		"res://cartas prime/MEDIEVAL/Maguin_prime.tscn",   "res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn",
		"res://cartas prime/PAPEL/Paper_Rex.tscn",      "res://cartas prime/PACIFICO/Tiburon_prime.tscn",
		"res://cartas prime/AJEDREZ/Peon_prime.tscn",  "res://cartas prime/TOONS/Tanque_cartoon_prime.tscn",  
		"res://cartas prime/PACIFICO/CalamarG_prime.tscn", "res://cartas prime/AJEDREZ/Caballo_prime.tscn",
		"res://cartas prime/AJEDREZ/Dama_prime.tscn",      "res://cartas prime/AJEDREZ/Torre_prime.tscn",
		"res://cartas prime/TOONS/Soldado_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Campero_cartoon_prime.tscn",
		"res://cartas prime/AJEDREZ/Arfil_prime.tscn", "res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn",
		"res://cartas prime/TOONS/Granadero_cartoon_prime.tscn",
		"res://cartas prime/MEDIEVAL/Machi_prime.tscn"
	};

	[Export] private PackedScene escenaTronoRef     = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef  = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private TronoCampo tronoJugador, tronoRival;

	// ══════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		// Pizarra limpia para el flag estático de reproducción online: cada partida (bot u online)
		// arranca con el daño local ACTIVO. Sin esto, si una partida online quedó con el flag en true,
		// se filtraría a la siguiente partida y no se aplicaría daño (romperia el bot).
		SoloVisualOnline = false;
		SuprimiendoAvisosOnline = false;
		GuardiaNuclearActiva = false;

		// La cámara manual (posición/zoom/rotación fijados en el editor) debe quedar
		// activa siempre: el motor no la respeta si no se declara "current" en runtime.
		GetNodeOrNull<Camera2D>("Camera2D")?.MakeCurrent();

		// Sortea el escenario visual+musical de esta partida, silencia el menú e inicia su música
		ElegirEscenarioBatalla();
		SilenciarOtrasMusicas();
		IniciarMusicaPartida();

		// Escenario "toon": mismo filtro "1930s Cartoon Aesthetic" de MenuConstructor. Se queda
		// activo TODA la partida (incluida la frase y pantalla de Victoria/Derrota) y solo se
		// apaga al abandonar Campo1 (ver los botones de esas pantallas), nunca antes.
		_rectVintage = EfectoVintageToons.Instalar(this);
		if (EscenarioEsToon) EfectoVintageToons.AplicarIntensidad(_rectVintage, 0.7f, 0.6f);

		timerReloj = new Timer();
		timerReloj.WaitTime = 1.0f;
		timerReloj.Timeout  += OnTickReloj;
		AddChild(timerReloj);
		timerReloj.Start();

		menuAcciones  = GetNodeOrNull<Control>("InterfazMenu/MenuAcciones");

		if (menuAcciones != null)
		{
			menuAcciones.Visible     = false;
			menuAcciones.MouseFilter = Control.MouseFilterEnum.Stop;
			var hbox = menuAcciones.GetNodeOrNull<HBoxContainer>("HBoxContainer");
			if (hbox != null)
			{
				btnHabilidad         = new Button();
				btnHabilidad.Text     = "HABILIDAD";
				btnHabilidad.Visible  = false;
				btnHabilidad.CustomMinimumSize = new Vector2(225, 88);
				btnHabilidad.AddThemeFontSizeOverride("font_size", 34);
				var fuenteBotones = GD.Load<Font>("res://Almendra-Bold.ttf");
				if (fuenteBotones != null) btnHabilidad.AddThemeFontOverride("font", fuenteBotones);
				btnHabilidad.Pressed += _on_btn_habilidad_pressed;
				hbox.AddChild(btnHabilidad);
			}
		}

		// Ardides desde sesión del jugador (MenuConstructor): si equipó al menos
		// SesionJuego.MIN_ARDIDES_JUGAR (4), se usan esos en el orden elegido (sean 4, 5 o los 6
		// completos); si no, se mantiene el pool completo de 8. Debe resolverse ANTES de
		// CrearPanelHechizos() (más abajo), que ya depende de _poolActivo y _cooldownHechizo para
		// armar la mano inicial de hechizos.
		if (SesionJuego.Instance != null && SesionJuego.Instance.TieneArdides)
		{
			var elegidos = new List<HechizoDef>();
			foreach (string id in SesionJuego.Instance.ArdidesSeleccionados)
			{
				foreach (var def in POOL_HECHIZO_BASE)
					if (def.Id == id) { elegidos.Add(def); break; }
			}
			if (elegidos.Count >= SesionJuego.MIN_ARDIDES_JUGAR) _poolActivo = elegidos.ToArray();
		}
		// Sin ardides elegidos se usa el catálogo completo, pero Nuclear (600 monedas) solo entra si
		// el jugador ya lo compró — no se regala en el pool por defecto.
		if (_poolActivo == POOL_HECHIZO_BASE && !Preferencias.TieneHechizoDesbloqueado("nuclear"))
			_poolActivo = Array.FindAll(POOL_HECHIZO_BASE, d => d.Id != "nuclear");
		_cooldownHechizo = new int[_poolActivo.Length];

		_contenedorHechizos = GetNodeOrNull<Control>("ManoHechizos");
		// Las cartas de mano deben verse SIEMPRE por encima de las tropas invocadas (el mayor
		// ZIndex de una tropa es 100, en Mod3/ModRival3) — si no, en el carril 3 las tropas tapan
		// la mano. Como ZAsRelative es true por defecto, esto se suma al ZIndex propio de cada
		// Carta (hover/arrastre), quedando siempre por delante de cualquier tropa.
		if (_contenedorHechizos != null) _contenedorHechizos.ZIndex = 150;
		CrearPanelHechizos();

		if (contenedorMano == null)
			contenedorMano = GetNodeOrNull<Control>("ManoManual");
		if (contenedorMano == null)
			GD.PrintErr("[Campo1] ¡contenedorMano no encontrado! Asígnalo en el Inspector o crea un nodo ManoManual.");
		else
			contenedorMano.ZIndex = 150;

		// Mazo desde sesión del jugador
		if (SesionJuego.Instance != null && SesionJuego.Instance.TieneMazo)
		{
			escenasTropas  = SesionJuego.Instance.MazoSeleccionado.ToArray();
			// Imagen grande de batalla (CartasPng) derivada de la escena de cada tropa —
			// nunca los iconos pequeños del selector de mazo.
			var imgsSesion = SesionJuego.Instance.ImagenesMazo;
			imagenesCartas = new string[escenasTropas.Length];
			for (int i = 0; i < escenasTropas.Length; i++)
			{
				string png = ClasificacionCartas.ImagenBatalla(escenasTropas[i]);
				if (string.IsNullOrEmpty(png) && i < imgsSesion.Count) png = imgsSesion[i];
				imagenesCartas[i] = png ?? "";
			}
			GD.Print($"[Campo1] Mazo personalizado: {escenasTropas.Length} cartas");
		}

		PrepararMazoSinRepetir();
		InicializarClasificacionMazo();
		InicializarMazoCPU();
		InicializarManoVisualCPU();
		// Se sortea acá (antes de CrearEscenaDeBatalla) para que la skin del rival pueda usar este
		// mismo nombre — ver SkinPorNombreCPU(). En online no aplica: ahí se usa ContextoOnline.RivalNombre.
		_nombreCPUElegido = NOMBRES_CPU[random.Next(NOMBRES_CPU.Length)];
		CrearEscenaDeBatalla();
		BarajarMazoInicial();
		_faseApertura  = true;
		faseInvocacion = true;
		ConfigurarInterfazNueva();
		BajarManoManual();
		ActualizarInterfaz();
		AnunciarTurno(); // muestra "TU TURNO" desde el primer instante, no solo al cambiar de turno
		ConfigurarModoOnline();

		// Mejora estética integrada de zonas de invocación (del amigo)
		EstilizarIndicadoresInvocacion();

		if (SesionJuego.Instance != null)
			_rachaVictorias = SesionJuego.Instance.RachaActual;

		// Aviso inicial único: el bot ya está listo para pelear. Con un pequeño retraso para no
		// pisar el toast "TU TURNO" que AnunciarTurno() acaba de mostrar en el mismo instante.
		GetTree().CreateTimer(1.2f).Timeout += () =>
		{
			if (!juegoTerminado) MostrarAviso("¡El rival está listo para la batalla!", new Color(1f, 0.75f, 0.35f));
		};
	}

	// ── CORRECCIÓN DE ORIENTACIÓN PARA TROPAS RIVALES ────────────────────
	public void AsegurarOrientacionRival(Node2D tropa)
	{
		if (!IsInstanceValid(tropa)) return;

		// Mantiene la escala Y positiva para que nunca se ponga patas arriba
		Vector2 escalaActual = tropa.Scale;
		escalaActual.X = Mathf.Abs(escalaActual.X);
		escalaActual.Y = Mathf.Abs(escalaActual.Y);
		tropa.Scale = escalaActual;

		// Voltea únicamente la textura de la tropa con FlipH
		var animSprite = tropa.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		var sprite2D   = tropa.GetNodeOrNull<Sprite2D>("Sprite2D");

		// FlipH espeja la textura EN SU SITIO, sin mover el sprite. Pero el arte de cada tropa está
		// dibujado descentrado dentro de su frame (AnimatedSprite2D.Position.X), y en las piezas de
		// ajedrez ese offset es grande (Caballo=72, Peón=29). Sin espejar también esa posición, la
		// tropa rival queda ~2× ese offset hacia el lado equivocado del punto rojo. Espejamos la X
		// del sprite alrededor del ancla real (efecto_secundario_slot) para que quede simétrica a la
		// del jugador sobre el círculo del carril. Idempotente (meta) para no revertirse si se llama 2 veces.
		if (!tropa.HasMeta("orientacion_rival_aplicada"))
		{
			// Espejar la X del sprite tal cual (sin pivotear sobre "efecto_secundario_slot"): es la
			// misma regla que ya usan OffsetCentroColision/ObtenerSpotOrientado ("negar la X local")
			// y que Godot aplicaría solo con Scale.X=-1 (convención de campo_de_pruebas). Pivotear
			// sobre el ancla (fórmula vieja: 2*ejeX - X) solo coincidía con esto cuando ejeX=0 — para
			// el resto de tropas (p. ej. Gólem, ancla en X=-16 con sprite en X=20) el sprite quedaba
			// corrido 2*ejeX píxeles del centro real, desalineando el cuerpo visual de los efectos
			// (tentáculos del Calamar, etc.) que sí anclan correctamente sobre el origen del nodo.
			if (animSprite != null) animSprite.Position = new Vector2(-animSprite.Position.X, animSprite.Position.Y);
			if (sprite2D != null)   sprite2D.Position   = new Vector2(-sprite2D.Position.X,   sprite2D.Position.Y);
			// El área clicable ("ClickBody") ya no depende de esto: TropaBase.ActualizarAreaClicCuerpo
			// la centra sola en OffsetCentroColision (diferido, corre después de esto).
			tropa.SetMeta("orientacion_rival_aplicada", true);
		}

		if (animSprite != null) animSprite.FlipH = true;
		if (sprite2D != null)   sprite2D.FlipH = true;
	}

	// ── SISTEMA DE CONTROL DE MÚSICA ─────────────────────────────────────
	private void SilenciarOtrasMusicas()
	{
		Node musicaGlobal = GetTree().Root.GetNodeOrNull("MusicaGlobal");
		if (musicaGlobal != null)
		{
			if (musicaGlobal is AudioStreamPlayer player)
			{
				player.Stop();
			}
			else if (musicaGlobal.HasMethod("DetenerMusica"))
			{
				musicaGlobal.Call("DetenerMusica");
			}
		}

		foreach (Node nodo in GetTree().Root.GetChildren())
		{
			if (nodo is AudioStreamPlayer asp && asp != _reproductorMusica)
			{
				asp.Stop();
			}
		}
	}

	private void IniciarMusicaPartida()
	{
		if (_musicaPartida == null)
		{
			GD.PrintErr("⚠ No se pudo cargar la música del escenario de batalla sorteado.");
			return;
		}

		_reproductorMusica = new AudioStreamPlayer();
		_reproductorMusica.Stream = _musicaPartida;
		_reproductorMusica.Name = "MusicaBatalla";
		_reproductorMusica.VolumeDb = _volumenMusicaDb + _volumenExtraEscenario;

		AddChild(_reproductorMusica);
		_reproductorMusica.Play();
		GD.Print("🎵 Música del escenario sonando en Campo1.");

		if (EscenarioEsDigital)
		{
			IniciarSecuenciaDigital();
		}
		else
		{
			// Mismo criterio que el mapa Digital: si la pista se acaba mientras la partida sigue en
			// curso, se repite desde el principio en vez de quedarse en silencio (antes solo Digital
			// tenía esto). FinalizarPartida() ya reconfigura Loop/Seek de música al terminar, así que
			// esto solo importa mientras juegoTerminado siga en false.
			if (_musicaPartida is AudioStreamMP3 mp3NoDigital) mp3NoDigital.Loop = false;
			_reproductorMusica.Finished += () =>
			{
				if (!juegoTerminado && IsInstanceValid(_reproductorMusica)) _reproductorMusica.Play(0f);
			};
		}
	}

	// ── MODO DEMO: IA vs IA ───────────────────────────────────────────────
	public void ActivarModoDemo()
	{
		_modoDemo = true;
		var lbl = new Label();
		lbl.Text = "MODO DEMOSTRACIÓN — Toca para jugar";
		lbl.AddThemeColorOverride("font_color", Colors.Gold);
		lbl.AddThemeFontSizeOverride("font_size", 16);
		lbl.Position = new Vector2(350, 5);
		lbl.ZIndex   = 50;
		AddChild(lbl);

		_timerDemo = new Timer();
		_timerDemo.WaitTime = 0.5f;
		_timerDemo.Timeout  += () => { if (_modoDemo && esTurnoJugador) EjecutarTurnoCPUDemo(); };
		AddChild(_timerDemo);
		_timerDemo.Start();

		SetProcessInput(true);
	}

	private async void EjecutarTurnoCPUDemo()
	{
		if (juegoTerminado || !_modoDemo) return;
		await ToSignal(GetTree().CreateTimer(1.2f), "timeout");

		string[] puntos = { "Mod1", "Mod2", "Mod3" };
		foreach (string nombre in puntos)
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
			InvocacionRival(zona, GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length)]));
			await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
		}

		var tropas = new List<Node2D>();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			if (n is Node2D n2 && IsInstanceValid(n2)) tropas.Add(n2);

		foreach (Node2D tropa in tropas)
		{
			if (!IsInstanceValid(tropa)) continue;
			ProcesarCombateFrontal(tropa, "tropas_rival");
			await ToSignal(GetTree().CreateTimer(0.9f), "timeout");
		}

		if (_modoDemo) CambiarTurno();
	}

	public void _on_timer_timeout() { }

	// ── GESTIÓN DE PASAR TURNO Y CONTADOR DE VIDA EN CAMPO ───────────────
	public void IncrementarTurnosTropasJugador()
	{
		foreach (Node nodo in GetTree().GetNodesInGroup("tropas_jugador"))
		{
			if (nodo is Node2D tropa && IsInstanceValid(tropa))
			{
				int turnos = tropa.HasMeta("turnos_en_campo") ? (int)tropa.GetMeta("turnos_en_campo") : 1;
				tropa.SetMeta("turnos_en_campo", turnos + 1);
			}
		}
	}

	public void _on_pasar_turno_pressed()
	{
		if (!esTurnoJugador || juegoTerminado) return;
		if (_faseApertura) { MostrarAviso("Coloca tus 3 tropas primero", Colors.Gold); return; }
		
		IncrementarTurnosTropasJugador();
		CambiarTurno();
	}

	// ── ESTILIZADO MEJORADO DE INDICADORES (Aporte Visual Amigo) ──────────
	private void EstilizarIndicadoresInvocacion()
	{
		const float TAM = 72f; // antes 40 — más grande y visible que antes

		foreach (var grupo in new[] { "zonas_invocacion", "zonas_invocacion_rival" })
		{
			bool esRival = grupo.Contains("rival");
			foreach (Node2D zona in GetTree().GetNodesInGroup(grupo))
			{
				var ind = zona.GetNodeOrNull<ColorRect>("Indicador");
				if (ind != null)
				{
					ind.Visible = false;

					var panel = new Panel();
					panel.Name = "IndicadorMejorado";
					panel.CustomMinimumSize = new Vector2(TAM, TAM);
					panel.Size = new Vector2(TAM, TAM);
					panel.Position = new Vector2(-TAM / 2f, -TAM / 2f);

					var style = new StyleBoxFlat();
					style.BgColor = new Color(0, 0, 0, 0.25f);
					style.BorderWidthLeft = style.BorderWidthRight = style.BorderWidthTop = style.BorderWidthBottom = 3;
					style.BorderColor = esRival ? new Color(1f, 0.15f, 0.2f, 0.85f) : new Color(0.15f, 0.65f, 1f, 0.85f);
					style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = (int)(TAM / 2f);
					style.ShadowColor = esRival ? new Color(1f, 0.15f, 0.2f, 0.5f) : new Color(0.15f, 0.65f, 1f, 0.5f);
					style.ShadowSize = 8;

					panel.AddThemeStyleboxOverride("panel", style);
					zona.AddChild(panel);

					Tween tw = panel.CreateTween().SetLoops();
					tw.TweenProperty(panel, "modulate:a", 0.35f, 0.8f);
					tw.TweenProperty(panel, "modulate:a", 1.0f, 0.8f);

					// Oculto mientras el carril esté ocupado por una tropa — reaparece solo
					// cuando vuelve a quedar libre (muerte, Enroque, etc.), sin tener que tocar
					// cada lugar del código que agrega o libera el nodo "Ocupado".
					panel.Visible = zona.GetNodeOrNull("Ocupado") == null;
					zona.ChildEnteredTree += (Node hijo) => { if (hijo.Name == "Ocupado" && IsInstanceValid(panel)) panel.Visible = false; };
					zona.ChildExitingTree += (Node hijo) => { if (hijo.Name == "Ocupado" && IsInstanceValid(panel)) panel.Visible = true; };
				}
			}
		}
	}
}
