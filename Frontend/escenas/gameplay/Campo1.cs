using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── MÚSICA Y AUDIO ───────────────────────────────────────────────────
	[Export] private AudioStream _musicaPartida = GD.Load<AudioStream>("res://musica/DECISIVE BATTLE.mp3");
	[Export] private float _volumenMusicaDb = -17.0f;
	private AudioStreamPlayer _reproductorMusica;

	// ── VIDA ──────────────────────────────────────────────────────────────
	[Export] public int vidaJugador   = 2000;
	[Export] public int vidaRival     = 2000;
	public const   int vidaMaxJugador = 2000;
	private int tiempoTotalPartida    = 300;
	public  bool juegoTerminado       = false;

	// ── TURNOS ────────────────────────────────────────────────────────────
	public  const int ENERGIA_MAXIMA  = 3; // fija, sin escalado por turno
	public  bool esTurnoJugador       = true;
	public  int  movimientosRestantes = ENERGIA_MAXIMA;
	private int  tiempoTurnoActual    = 20;
	private Timer timerReloj;

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
	private bool   _hechizoUsadoEsteTurno = false; // 1 hechizo/trampa por turno (jugador e IA)

	// Pool visual de hechizos — mano de 3 cartas aleatorias de 5 posibles
	private static readonly string[] POOL_HECHIZO_NOMBRE = { "Curación", "Robar Carta", "Veneno", "Bloqueo", "Encebollado" };
	private static readonly string[] POOL_HECHIZO_RUTA = {
		"res://imagenes/HechizosPng/Cura_hechizo.png",
		"res://imagenes/HechizosPng/Robo_hechizo.png",
		"res://imagenes/HechizosPng/Veneno_hechizo.png",
		"res://imagenes/HechizosPng/Bloqueo_hechizo.png",
		"res://imagenes/HechizosPng/Encebo_hechizo.png"
	};
	private static readonly Color[] POOL_HECHIZO_COLOR = {
		new Color(0.25f,0.80f,0.35f),
		new Color(0.30f,0.65f,1f), new Color(0.60f,0.30f,0.75f), new Color(0.25f,0.55f,0.90f),
		new Color(1f,0.65f,0.15f)
	};
	private int[]   _hechizosMano     = new int[2];
	private Panel[] _tarjetasHechizo  = new Panel[2];
	private Panel[] _overlayHechizo   = new Panel[2];
	private Label[] _lblEstadoHechizo = new Label[2];
	private const int MAX_CAMBIO_HECHIZO = 3; // hasta 3 cambios de hechizo por partida (jugador e IA)
	private int     _usosCambioHechizo  = 0;
	private bool    _modoCambioHechizo  = false;
	private Button  _btnCambiarHechizo;
	private System.Collections.Generic.List<int> _poolHechizos = new();
	private int     _slotPendiente = -1;

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
		"res://imagenes/CartasPng/PaperReXCart.png",     "res://imagenes/CartasPng/TiburonCart.png",
		"res://imagenes/CartasPng/PeonCart.png",     "res://imagenes/CartasPng/TanqueCart.png",
		"res://imagenes/CartasPng/CalamarGCart.png", "res://imagenes/CartasPng/CaballoCart.png",
		"res://imagenes/CartasPng/DamaCart.png",     "res://imagenes/CartasPng/TorreCart.png",
		"res://imagenes/CartasPng/SoldCartoonCart.png", "res://imagenes/CartasPng/CamperoCart.png",
		"res://imagenes/CartasPng/ArfilCart.png" , "res://imagenes/CartasPng/FantasmaCart.png",
		"res://imagenes/CartasPng/GranaderoCart.png"
	};

	private string[] escenasTropas = {
		"res://cartas prime/Dragon_prime.tscn",   "res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn",   "res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/PAPEL/Paper_Rex.tscn",      "res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/AJEDREZ/Peon_prime.tscn",  "res://cartas prime/GUERRA CARTOONS/Tanque_cartoon_prime.tscn",  
		"res://cartas prime/CalamarG_prime.tscn", "res://cartas prime/AJEDREZ/Caballo_prime.tscn",
		"res://cartas prime/AJEDREZ/Dama_prime.tscn",      "res://cartas prime/AJEDREZ/Torre_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Soldado_cartoon_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Campero_cartoon_prime.tscn",
		"res://cartas prime/AJEDREZ/Arfil_prime.tscn", "res://cartas prime/GUERRA CARTOONS/Ka-Bar_cartoon_prime.tscn",
		"res://cartas prime/GUERRA CARTOONS/Granadero_cartoon_prime.tscn"
	};

	[Export] private PackedScene escenaTronoRef     = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef  = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private tronocampo tronoJugador, tronoRival;

	// ══════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		// Silencia el menú e inicia la canción de combate (de tu script)
		SilenciarOtrasMusicas();
		IniciarMusicaPartida();

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
				btnHabilidad         = new Button();
				btnHabilidad.Text     = "HABILIDAD";
				btnHabilidad.Visible  = false;
				btnHabilidad.Pressed += _on_btn_habilidad_pressed;
				hbox.AddChild(btnHabilidad);
			}
		}

		_contenedorHechizos = GetNodeOrNull<Control>("ManoHechizos");
		CrearPanelHechizos();

		if (contenedorMano == null)
			contenedorMano = GetNodeOrNull<Control>("ManoManual");
		if (contenedorMano == null)
			GD.PrintErr("[Campo1] ¡contenedorMano no encontrado! Asígnalo en el Inspector o crea un nodo ManoManual.");

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

		AplicarIdentidadEra();
		PrepararMazoSinRepetir();
		InicializarClasificacionMazo();
		InicializarMazoCPU();
		CrearEscenaDeBatalla();
		BarajarMazoInicial();
		_faseApertura  = true;
		faseInvocacion = true;
		CrearBarrasHPBase();
		CrearBotonAyudaTipos();
		CrearBotonHistorial();
		CrearBotonPausa();
		EstilizarLabelsHUD();
		BajarManoManual();
		ActualizarInterfaz();
		MostrarAvisoApertura();

		// Mejora estética integrada de zonas de invocación (del amigo)
		EstilizarIndicadoresInvocacion();

		if (SesionJuego.Instance != null)
			_rachaVictorias = SesionJuego.Instance.RachaActual;
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
			GD.PrintErr("⚠ No se encontró el archivo de música res://musica/DECISIVE BATTLE.mp3");
			return;
		}

		_reproductorMusica = new AudioStreamPlayer();
		_reproductorMusica.Stream = _musicaPartida;
		_reproductorMusica.Name = "MusicaDecisiveBattle";
		_reproductorMusica.VolumeDb = _volumenMusicaDb;
		
		AddChild(_reproductorMusica);
		_reproductorMusica.Play();
		GD.Print("🎵 Canción DECISIVE BATTLE.mp3 sonando en Campo1.");
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
					panel.CustomMinimumSize = new Vector2(40, 40);
					panel.Size = new Vector2(40, 40);
					panel.Position = new Vector2(-20, -20);

					var style = new StyleBoxFlat();
					style.BgColor = new Color(0, 0, 0, 0.25f);
					style.BorderWidthLeft = style.BorderWidthRight = style.BorderWidthTop = style.BorderWidthBottom = 2;
					style.BorderColor = esRival ? new Color(1f, 0.15f, 0.2f, 0.85f) : new Color(0.15f, 0.65f, 1f, 0.85f);
					style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 20;
					style.ShadowColor = esRival ? new Color(1f, 0.15f, 0.2f, 0.5f) : new Color(0.15f, 0.65f, 1f, 0.5f);
					style.ShadowSize = 8;

					panel.AddThemeStyleboxOverride("panel", style);
					zona.AddChild(panel);

					Tween tw = panel.CreateTween().SetLoops();
					tw.TweenProperty(panel, "modulate:a", 0.35f, 0.8f);
					tw.TweenProperty(panel, "modulate:a", 1.0f, 0.8f);
				}
			}
		}
	}
}
