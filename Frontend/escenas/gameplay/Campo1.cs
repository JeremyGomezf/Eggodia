using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── VIDA ──────────────────────────────────────────────────────────────
	[Export] public int vidaJugador   = 2000;
	[Export] public int vidaRival     = 2000;
	public const   int vidaMaxJugador = 2000;
	private int tiempoTotalPartida    = 300;
	public  bool juegoTerminado       = false;

	// ── TURNOS ────────────────────────────────────────────────────────────
	public  bool esTurnoJugador       = true;
	public  int  movimientosRestantes = 3;
	private int  tiempoTurnoActual    = 20;
	private Timer timerReloj;

	// Fase de turno: primero invocar, luego atacar
	public  bool faseInvocacion       = true;
	private int  tropasInvocadasTurno = 0;

	// ── SISTEMA DE COMBOS Y RACHAS ────────────────────────────────────────
	private int  _comboTurno          = 0;   // tropas eliminadas en turno actual
	private int  _rachaVictorias      = 0;   // victorias consecutivas (persiste en SesionJuego)
	private int  _totalTropasElimIA   = 0;   // para logros

	// ── LOGROS ────────────────────────────────────────────────────────────
	private bool _logroPrimeraVictoria  = false;
	private bool _logro10Tropas         = false;
	private bool _logroHabilidadUsada   = false;
	private bool _logroHechizosUsados   = false;

	// ── FASE DE APERTURA (inicio de partida: obligatorio colocar 3 tropas) ─
	private bool _faseApertura         = true;

	// ── MODO DEMO ─────────────────────────────────────────────────────────
	private bool _modoDemo             = false;
	private Timer _timerDemo;

	// ── LIMITADORES ───────────────────────────────────────────────────────
	private int usosBarajar           = 0;
	private int usosSacrificio        = 0;
	private const int MAX_BARAJAR     = 1;
	private const int MAX_SACRIFICIO  = 2;

	// ── UI ────────────────────────────────────────────────────────────────
	private Control  menuAcciones;
	private Node2D   tropaSeleccionada;
	private Button   btnBarajar;
	private Button   btnSacrificio;
	private Button   btnHabilidad;
	private Control  panelPausa;

	[Export] private Texture2D   iconoCursorSacrificio;
	[Export] private PackedScene escenaCartaBase;
	[Export] private Control     contenedorMano;

	// ── HECHIZOS ──────────────────────────────────────────────────────────
	private bool usadoEncebollado = false;
	private bool usadoCuracion    = false;
	private bool usadoRobo        = false;
	private bool usadoVeneno      = false;
	private bool usadoBloqueo     = false;
	private bool _modoSeleccionObjetivo = false;
	private string _hechizoPendiente    = "";
	private Label  _lblInstruccion;

	// ── IA ADAPTATIVA ─────────────────────────────────────────────────────
	// Empieza en dificultad media (0=fácil, 1=medio, 2=difícil)
	private int  _dificultadIA        = 1;
	private int  _victoriasJugador    = 0;
	private int  _derrotasJugador     = 0;
	private int  _turnosJugados       = 0;

	// ── ESTADÍSTICAS DE PARTIDA ───────────────────────────────────────────
	private int _dañoTotalJugador     = 0;
	private int _dañoTotalRival       = 0;
	private int _tropasEliminadasJugador = 0;
	private int _tropasEliminadasRival   = 0;

	// ── BARRAS HP BASE ────────────────────────────────────────────────────
	private ProgressBar _barraHPJugador, _barraHPRival;
	private Label _lblVida1, _lblVida2, _lblTiempo, _lblTurnoInfo;

	// ── MAZO ──────────────────────────────────────────────────────────────
	private List<int> mazoIndices       = new List<int>();
	private int       proximoIndiceMazo = 0;
	private bool      modoSacrificioActivo = false;
	private Random    random            = new Random();

	private string[] imagenesCartas = {
		"res://imagenes/CartasPng/DragonCart.png",   "res://imagenes/CartasPng/GolemCart.png",
		"res://imagenes/CartasPng/MaguinCart.png",   "res://imagenes/CartasPng/SoldRealCart.png",
		"res://imagenes/CartasPng/TReXCart.png",     "res://imagenes/CartasPng/TiburonCart.png",
		"res://imagenes/CartasPng/PeonCart.png",     "res://imagenes/CartasPng/EncebolladoCart.png",
		"res://imagenes/CartasPng/CalamarGCart.png", "res://imagenes/CartasPng/CaballoCart.png",
		"res://imagenes/CartasPng/DamaCart.png",     "res://imagenes/CartasPng/TorreCart.png",
		"res://imagenes/CartasPng/SoldRealCart.png"   // placeholder: SoldadoCartoonCart.png aún no existe
	};

	private string[] escenasTropas = {
		"res://cartas prime/Dragon_prime.tscn",   "res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn",   "res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/TRex_prime.tscn",     "res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/Peon_prime.tscn",     "res://cartas prime/Encebollado_prime.tscn",
		"res://cartas prime/CalamarG_prime.tscn", "res://cartas prime/Caballo_prime.tscn",
		"res://cartas prime/Dama_prime.tscn",     "res://cartas prime/Torre_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Soldado_cartoon_prime.tscn"
	};

	[Export] private PackedScene escenaTronoRef     = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef  = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private tronocampo tronoJugador, tronoRival;

	// ══════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		timerReloj = new Timer();
		timerReloj.WaitTime = 1.0f;
		timerReloj.Timeout  += OnTickReloj;
		AddChild(timerReloj);
		timerReloj.Start();

		menuAcciones  = GetNodeOrNull<Control>("InterfazMenu/MenuAcciones");
		btnBarajar    = GetNodeOrNull<Button>("Barajar");
		btnSacrificio = GetNodeOrNull<Button>("Sacrificar");

		if (menuAcciones != null)
		{
			menuAcciones.Visible     = false;
			menuAcciones.MouseFilter = Control.MouseFilterEnum.Stop;
			var hbox = menuAcciones.GetNodeOrNull<HBoxContainer>("HBoxContainer");
			if (hbox != null)
			{
				btnHabilidad          = new Button();
				btnHabilidad.Text     = "HABILIDAD";
				btnHabilidad.Visible  = false;
				btnHabilidad.Pressed += _on_btn_habilidad_pressed;
				hbox.AddChild(btnHabilidad);
			}
		}

		CrearPanelHechizos();

		// Si contenedorMano no está asignado en el inspector, buscarlo por nombre
		if (contenedorMano == null)
			contenedorMano = GetNodeOrNull<Control>("ManoManual");
		if (contenedorMano == null)
			GD.PrintErr("[Campo1] ¡contenedorMano no encontrado! Asígnalo en el Inspector o crea un nodo ManoManual.");

		// Usar mazo del jugador si lo armó en el constructor
		if (SesionJuego.Instance != null && SesionJuego.Instance.TieneMazo)
		{
			imagenesCartas = SesionJuego.Instance.ImagenesMazo.ToArray();
			escenasTropas  = SesionJuego.Instance.MazoSeleccionado.ToArray();
			GD.Print($"[Campo1] Mazo personalizado: {escenasTropas.Length} cartas");
		}

		AplicarIdentidadEra();
		PrepararMazoSinRepetir();
		CrearEscenaDeBatalla();
		BarajarMazoInicial();
		_faseApertura  = true;
		faseInvocacion = true;
		CrearBarrasHPBase();
		CrearBotonAyudaTipos();
		CrearBotonHistorial();
		EstilizarLabelsHUD();
		BajarManoManual();
		ActualizarInterfaz();
		MostrarAvisoApertura();

		// Restaurar racha desde sesión
		if (SesionJuego.Instance != null)
			_rachaVictorias = SesionJuego.Instance.RachaActual;
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

		// En modo demo el jugador también es IA
		_timerDemo = new Timer();
		_timerDemo.WaitTime = 0.5f;
		_timerDemo.Timeout  += () => { if (_modoDemo && esTurnoJugador) EjecutarTurnoIADemo(); };
		AddChild(_timerDemo);
		_timerDemo.Start();

		// Clic en pantalla sale del demo
		SetProcessInput(true);
	}

	private async void EjecutarTurnoIADemo()
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

		var tropas = new System.Collections.Generic.List<Node2D>();
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
	public void _on_pasar_turno_pressed()
	{
		if (!esTurnoJugador || juegoTerminado) return;
		if (_faseApertura) { MostrarAviso("Coloca tus 3 tropas primero", Colors.Gold); return; }
		CambiarTurno();
	}
}
