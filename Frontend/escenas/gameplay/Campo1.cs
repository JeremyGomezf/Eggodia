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
		"res://imagenes/CartasPng/DamaCart.png",     "res://imagenes/CartasPng/TorreCart.png"
	};

	private string[] escenasTropas = {
		"res://cartas prime/Dragon_prime.tscn",   "res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn",   "res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/TRex_prime.tscn",     "res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/Peon_prime.tscn",     "res://cartas prime/Encebollado_prime.tscn",
		"res://cartas prime/CalamarG_prime.tscn", "res://cartas prime/Caballo_prime.tscn",
		"res://cartas prime/Dama_prime.tscn",     "res://cartas prime/Torre_prime.tscn"
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
				btnHabilidad.Text     = "⚡ HABILIDAD";
				btnHabilidad.Visible  = false;
				btnHabilidad.Pressed += _on_btn_habilidad_pressed;
				hbox.AddChild(btnHabilidad);
			}
		}

		CrearPanelHechizos();
		CrearPanelPausa();

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
		ColocarTropasIniciales();   // ambos lados empiezan con 1 tropa en el carril central
		BarajarMazoInicial();
		faseInvocacion = false;     // no se requiere invocar antes de atacar desde el turno 1
		ActualizarInterfaz();

		// Restaurar racha desde sesión
		if (SesionJuego.Instance != null)
			_rachaVictorias = SesionJuego.Instance.RachaActual;
	}

	// ── MODO DEMO: IA vs IA ───────────────────────────────────────────────
	public void ActivarModoDemo()
	{
		_modoDemo = true;
		var lbl = new Label();
		lbl.Text = "👁 MODO DEMOSTRACIÓN — Toca para jugar";
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
	public void _on_pasar_turno_pressed() { if (!esTurnoJugador || juegoTerminado) return; CambiarTurno(); }

	// ── PAUSA ─────────────────────────────────────────────────────────────
	private void CrearPanelPausa()
	{
		panelPausa = new Control();
		panelPausa.Name        = "PanelPausa";
		panelPausa.Visible     = false;
		panelPausa.ProcessMode = ProcessModeEnum.Always; // <- sigue activo aunque el árbol esté pausado
		panelPausa.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		panelPausa.ZIndex = 200;

		// Fondo semi-transparente (Ignore para que los clics lleguen a los botones)
		var fondo = new ColorRect();
		fondo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		fondo.Color       = new Color(0, 0, 0, 0.7f);
		fondo.MouseFilter = Control.MouseFilterEnum.Ignore;
		panelPausa.AddChild(fondo);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.Position = new Vector2(-120, -100);
		panelPausa.AddChild(vbox);

		var titulo = new Label();
		titulo.Text = "⏸ PAUSA";
		titulo.AddThemeColorOverride("font_color", Colors.White);
		titulo.AddThemeFontSizeOverride("font_size", 32);
		vbox.AddChild(titulo);

		AgregarBtnPausa(vbox, "▶ Continuar",  Colors.LightGreen, () => TogglePausa());
		AgregarBtnPausa(vbox, "🏳 Rendirse",   new Color(1f,0.4f,0.4f), () => { TogglePausa(); FinalizarPartida("DERROTA"); });
		AgregarBtnPausa(vbox, "🔄 Reiniciar",  Colors.LightBlue, () => GetTree().ReloadCurrentScene());

		AddChild(panelPausa);

		// Botón de pausa en esquina superior
		var btnP = new Button();
		btnP.Text     = "⏸";
		btnP.Position = new Vector2(610, 8);
		btnP.CustomMinimumSize = new Vector2(50, 35);
		btnP.Pressed += () => TogglePausa();
		AddChild(btnP);
	}

	private void AgregarBtnPausa(VBoxContainer parent, string texto, Color color, Action onPress)
	{
		var btn = new Button();
		btn.Text              = texto;
		btn.CustomMinimumSize = new Vector2(240, 50);
		btn.SelfModulate      = color;
		btn.Pressed          += () => onPress();
		parent.AddChild(btn);
	}

	private void TogglePausa()
	{
		if (juegoTerminado) return;
		bool pausado = !panelPausa.Visible;
		panelPausa.Visible = pausado;
		GetTree().Paused   = pausado;
	}

	// ── PANEL DE HECHIZOS ─────────────────────────────────────────────────
	private void CrearPanelHechizos()
	{
		var panel = new Control(); panel.Name = "PanelHechizos";
		var vbox  = new VBoxContainer(); vbox.Position = new Vector2(1095, 560);
		panel.AddChild(vbox);

		_lblInstruccion = new Label();
		_lblInstruccion.Text    = "Haz clic en una\ntropa enemiga";
		_lblInstruccion.Visible = false;
		_lblInstruccion.AddThemeColorOverride("font_color", new Color(1f,0.3f,0.3f));
		vbox.AddChild(_lblInstruccion);

		AgregarBtnHechizo(vbox, "🧅 Encebollado", new Color(1f,0.7f,0.1f),  () => UsarEncebollado());
		AgregarBtnHechizo(vbox, "💚 Curación",    new Color(0.2f,0.9f,0.3f), () => UsarCuracion());
		AgregarBtnHechizo(vbox, "🃏 Robar Carta", new Color(0.3f,0.7f,1f),   () => UsarRobo());
		AgregarBtnHechizo(vbox, "☠️ Veneno",      new Color(0.6f,0.2f,0.8f), () => IniciarSeleccion("veneno"));
		AgregarBtnHechizo(vbox, "🔒 Bloqueo",     new Color(0.2f,0.5f,0.9f), () => IniciarSeleccion("bloqueo"));

		AddChild(panel);
	}

	private void AgregarBtnHechizo(BoxContainer parent, string texto, Color color, Action onPress)
	{
		var btn = new Button();
		btn.Text              = texto;
		btn.CustomMinimumSize = new Vector2(155, 40);
		btn.SelfModulate      = color;
		btn.Pressed          += () => onPress();
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

	// ── HECHIZOS ──────────────────────────────────────────────────────────
	private void UsarEncebollado()
	{
		if (!ValidarHechizo() || usadoEncebollado) return;
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada) || !tropaSeleccionada.IsInGroup("tropas_jugador"))
		{ GD.Print("Selecciona una tropa tuya primero"); return; }

		int ata = 0, esc = 0, escMax = 0;
		try { ata    = (int)tropaSeleccionada.Get("puntosAtaque"); } catch { }
		try { esc    = (int)tropaSeleccionada.Get("escudoActual"); } catch { }
		try { escMax = (int)tropaSeleccionada.Get("escudoMaximo"); } catch { }
		try { tropaSeleccionada.Set("puntosAtaque", ata + 100); }                  catch { }
		try { tropaSeleccionada.Set("escudoActual", esc + 100); }                  catch { }
		try { tropaSeleccionada.Set("escudoMaximo", Mathf.Max(escMax,esc+100)); }  catch { }

		MostrarDañoFlotante(tropaSeleccionada.GlobalPosition, 100, true);
		Tween tw = tropaSeleccionada.CreateTween();
		tw.TweenProperty(tropaSeleccionada,"modulate", new Color(1.6f,1.3f,0.2f), 0.2f);
		tw.TweenProperty(tropaSeleccionada,"modulate", Colors.White, 0.5f);

		usadoEncebollado = true;
		RegistrarGastoMovimiento();
	}

	private void UsarCuracion()
	{
		if (!ValidarHechizo() || usadoCuracion) return;
		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada) && tropaSeleccionada.IsInGroup("tropas_jugador"))
		{
			int vida = 0, vidaMax = 0;
			try { vida    = (int)tropaSeleccionada.Get("vidaActual"); } catch { }
			try { vidaMax = (int)tropaSeleccionada.Get("vidaMaxima"); } catch { }
			int curado = Mathf.Min(200, vidaMax - vida);
			try { tropaSeleccionada.Set("vidaActual", vida + curado); } catch { }
			MostrarDañoFlotante(tropaSeleccionada.GlobalPosition, curado, true);
			Tween tw = tropaSeleccionada.CreateTween();
			tw.TweenProperty(tropaSeleccionada,"modulate", new Color(0.3f,1.6f,0.5f), 0.2f);
			tw.TweenProperty(tropaSeleccionada,"modulate", Colors.White, 0.5f);
		}
		else
		{
			int curado = Mathf.Min(200, vidaMaxJugador - vidaJugador);
			vidaJugador += curado;
			MostrarDañoFlotante(new Vector2(200, 300), curado, true);
		}
		usadoCuracion = true;
		RegistrarGastoMovimiento();
		ActualizarInterfaz();
	}

	private void UsarRobo()
	{
		if (!ValidarHechizo() || usadoRobo) return;
		foreach (string s in new[]{"Spot1","Spot2","Spot3"})
		{
			bool ok = false;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion()) ok = true;
			if (!ok) { CrearNuevaCartaEnSpot(s); break; }
		}
		usadoRobo = true;
		RegistrarGastoMovimiento();
	}

	private void IniciarSeleccion(string hechizo)
	{
		if (!ValidarHechizo()) return;
		if (hechizo == "veneno"  && usadoVeneno)  return;
		if (hechizo == "bloqueo" && usadoBloqueo) return;
		_modoSeleccionObjetivo = true;
		_hechizoPendiente      = hechizo;
		if (_lblInstruccion != null) _lblInstruccion.Visible = true;
	}

	private void AplicarHechizoEnObjetivo(Node2D objetivo)
	{
		if (!IsInstanceValid(objetivo) || !objetivo.IsInGroup("tropas_rival")) return;
		if (_hechizoPendiente == "veneno")
		{
			objetivo.SetMeta("envenenado",   true);
			objetivo.SetMeta("dañoVeneno",   50);
			objetivo.SetMeta("turnosVeneno", 3);
			objetivo.Modulate = new Color(0.6f,1f,0.4f);
			usadoVeneno = true;
		}
		else if (_hechizoPendiente == "bloqueo")
		{
			objetivo.SetMeta("bloqueado",     true);
			objetivo.SetMeta("turnosBloqueo", 2);
			objetivo.Modulate = new Color(0.4f,0.6f,1.4f);
			usadoBloqueo = true;
		}
		ActualizarIconosEstado(objetivo);
		_modoSeleccionObjetivo = false;
		_hechizoPendiente      = "";
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		RegistrarGastoMovimiento();
	}

	private bool ValidarHechizo() => esTurnoJugador && movimientosRestantes > 0 && !juegoTerminado;

	// ── INPUT ─────────────────────────────────────────────────────────────
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape && !juegoTerminado)
		{
			TogglePausa(); return;
		}

		if (juegoTerminado || !esTurnoJugador) return;

		if (_modoSeleccionObjetivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mouse = GetGlobalMousePosition();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D t && IsInstanceValid(t) && t.GlobalPosition.DistanceTo(mouse) < 90f)
				{ AplicarHechizoEnObjetivo(t); return; }
		}

		if (modoSacrificioActivo && @event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			VerificarSacrificioEnCampo(GetGlobalMousePosition());
	}

	// ── RELOJ ─────────────────────────────────────────────────────────────
	private void OnTickReloj()
	{
		if (juegoTerminado) return;
		tiempoTotalPartida--;
		if (tiempoTotalPartida <= 0) { DeterminarGanadorPorTiempo(); return; }
		tiempoTurnoActual--;
		if (tiempoTurnoActual <= 0) CambiarTurno();
		ActualizarInterfaz();
	}

	private void CambiarTurno()
	{
		_turnosJugados++;
		esTurnoJugador    = !esTurnoJugador;
		tiempoTurnoActual = 28;

		// Energía escala progresivamente: 3 → 4 → 5 (cap), cada 3 turnos completos
		int turnoGlobal      = _turnosJugados / 2;
		movimientosRestantes = Mathf.Min(5, 3 + turnoGlobal / 3);

		// Comeback: si tienes <30% HP ganas +1 energía ese turno
		int hpActivo = esTurnoJugador ? vidaJugador : vidaRival;
		if (hpActivo < (int)(vidaMaxJugador * 0.3f) && movimientosRestantes < 5)
		{
			movimientosRestantes++;
			if (esTurnoJugador)
				MostrarAviso("💪 ¡Desesperación! +1 Energía", Colors.OrangeRed);
		}

		usosBarajar    = 0;
		usosSacrificio = 0;
		faseInvocacion = false; // ya no se exige invocar antes de atacar
		_comboTurno    = 0;
		_modoSeleccionObjetivo = false;
		_hechizoPendiente      = "";
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		if (modoSacrificioActivo) CancelarSacrificio();
		if (menuAcciones != null) menuAcciones.Visible = false;

		ProcesarStatusEfectos();

		if (esTurnoJugador)
		{
			CompletarManoAlInicio();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
				if (n.HasMethod("SetActivo")) n.Call("SetActivo", true);
		}
		else EjecutarTurnoIA();

		AnunciarTurno();
		ActualizarInterfaz();
	}

	private void AnunciarTurno()
	{
		if (juegoTerminado) return;
		int turnoNum  = _turnosJugados / 2 + 1;
		int maxEnergy = Mathf.Min(5, 3 + (_turnosJugados / 2) / 3);
		string energyBar = new string('⚡', movimientosRestantes)
		                 + new string('·', Mathf.Max(0, maxEnergy - movimientosRestantes));
		string texto = esTurnoJugador
			? $"⚡ TU TURNO  [{energyBar}]  Turno {turnoNum}"
			: $"🤖 TURNO RIVAL  —  Turno {turnoNum}";
		Color color = esTurnoJugador ? Colors.LightGreen : Colors.OrangeRed;

		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 28);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		lbl.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		lbl.OffsetTop   = 160;
		lbl.OffsetLeft  = -340;
		lbl.OffsetRight = 340;
		lbl.ZIndex      = 150;
		AddChild(lbl);

		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.7f).SetDelay(0.6f);
		tw.TweenProperty(lbl, "scale", new Vector2(1.08f, 1.08f), 0.3f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	// ── STATUS EFFECTS ────────────────────────────────────────────────────
	private void ProcesarStatusEfectos()
	{
		foreach (string grupo in new[] { "tropas_jugador", "tropas_rival" })
		{
			var lista = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup(grupo))
				if (n is Node2D n2 && IsInstanceValid(n2)) lista.Add(n2);

			foreach (Node2D tropa in lista)
			{
				TickVeneno(tropa);
				TickBloqueo(tropa);
				if (tropa.HasMethod("TickHabilidad")) tropa.Call("TickHabilidad");
				if (Gi(tropa, "vidaActual") <= 0) EjecutarMuerteTropaSacrificada(tropa);
			}
		}
		CheckEstadoJuego();
	}

	private void TickVeneno(Node2D t)
	{
		if (!t.HasMeta("envenenado")) return;
		bool env; try { env = (bool)t.GetMeta("envenenado"); } catch { return; }
		if (!env) return;
		int daño   = t.HasMeta("dañoVeneno")  ? (int)t.GetMeta("dañoVeneno")  : 30;
		int turnos = t.HasMeta("turnosVeneno") ? (int)t.GetMeta("turnosVeneno"): 1;
		MostrarDañoFlotante(t.GlobalPosition, daño);
		if (t.HasMethod("RecibirDaño")) t.Call("RecibirDaño", daño);
		turnos--;
		if (turnos <= 0) { t.SetMeta("envenenado", false); t.Modulate = Colors.White; }
		else             t.SetMeta("turnosVeneno", turnos);
		ActualizarIconosEstado(t);
	}

	private void TickBloqueo(Node2D t)
	{
		if (!t.HasMeta("bloqueado")) return;
		bool bl; try { bl = (bool)t.GetMeta("bloqueado"); } catch { return; }
		if (!bl) return;
		int turnos = t.HasMeta("turnosBloqueo") ? (int)t.GetMeta("turnosBloqueo") : 1;
		turnos--;
		if (turnos <= 0) { t.SetMeta("bloqueado", false); t.Modulate = Colors.White; }
		else             t.SetMeta("turnosBloqueo", turnos);
		ActualizarIconosEstado(t);
	}

	// ── IA ADAPTATIVA ─────────────────────────────────────────────────────
	// Ajusta la dificultad según el rendimiento del jugador
	private void AjustarDificultad()
	{
		// Si el jugador tiene mucha vida y el rival poca → subir dificultad
		float pctJugador = (float)vidaJugador / vidaMaxJugador;
		float pctRival   = (float)vidaRival   / vidaMaxJugador;

		if (pctJugador > 0.7f && pctRival < 0.4f && _dificultadIA < 2)
			_dificultadIA++;
		else if (pctJugador < 0.3f && pctRival > 0.6f && _dificultadIA > 0)
			_dificultadIA--;
	}

	private async void EjecutarTurnoIA()
	{
		if (juegoTerminado || esTurnoJugador) return;
		AjustarDificultad();

		float delay = _dificultadIA == 0 ? 1.8f : _dificultadIA == 1 ? 1.2f : 0.85f;
		await ToSignal(GetTree().CreateTimer(delay * 0.4f), "timeout");
		if (juegoTerminado) return;

		// Evaluar estado del campo
		int tropasEnCampo = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (IsInstanceValid(n as Node2D)) tropasEnCampo++;

		float ratioHP      = (float)vidaRival / Mathf.Max(1, vidaMaxJugador);
		bool atacarPrimero = tropasEnCampo >= 2 && ratioHP > 0.35f;
		string[] puntos    = { "ModRival1", "ModRival2", "ModRival3" };

		if (atacarPrimero)
		{
			// Con tropas en campo: atacar primero (gasta energía)
			var bots = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);
			if (_dificultadIA == 2)
				bots.Sort((a, b) => Gi(b, "puntosAtaque").CompareTo(Gi(a, "puntosAtaque")));

			foreach (Node2D tropa in bots)
			{
				if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
				if (EstaBlockeada(tropa)) continue;
				Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
				if (!HabilidadUsada(tropa) && _dificultadIA >= 1 && random.Next(3) == 0)
					tropa.Call("EjecutarAccion", "usar_habilidad");
				else if (DebeDefender(tropa, objetivo))
					tropa.Call("EjecutarAccion", "preparar_defensa");
				else
					ProcesarCombateFrontal(tropa, "tropas_jugador");
				if (_dificultadIA == 2 && random.Next(5) == 0) IAUsarHechizo();
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}
			// Reforzar zonas vacías GRATIS (invocar no gasta energía)
			int maxNuevos = _dificultadIA + 1;
			int reforzadas = 0;
			foreach (string nombre in puntos)
			{
				if (reforzadas >= maxNuevos || juegoTerminado) break;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaIA());
				reforzadas++;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay * 0.7f), "timeout");
				if (juegoTerminado) return;
			}
		}
		else
		{
			// Sin tropas suficientes: invocar primero GRATIS, luego atacar (gasta energía)
			int aInvocar = _dificultadIA == 0 ? 1 : _dificultadIA == 1 ? 2 : 3;
			int invocadas = 0;
			foreach (string nombre in puntos)
			{
				if (invocadas >= aInvocar || juegoTerminado) break;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaIA());
				invocadas++;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}

			// Atacar con todas las tropas (incluyendo las recién invocadas)
			var bots = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);
			if (_dificultadIA == 2)
				bots.Sort((a, b) => Gi(b, "puntosAtaque").CompareTo(Gi(a, "puntosAtaque")));

			foreach (Node2D tropa in bots)
			{
				if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
				if (EstaBlockeada(tropa)) continue;
				Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
				if (!HabilidadUsada(tropa) && _dificultadIA >= 1 && random.Next(3) == 0)
					tropa.Call("EjecutarAccion", "usar_habilidad");
				else if (DebeDefender(tropa, objetivo))
					tropa.Call("EjecutarAccion", "preparar_defensa");
				else
					ProcesarCombateFrontal(tropa, "tropas_jugador");
				if (_dificultadIA == 2 && random.Next(5) == 0) IAUsarHechizo();
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}
		}

		if (!esTurnoJugador && !juegoTerminado) CambiarTurno();
	}

	private PackedScene ElegirTropaIA()
	{
		if (_dificultadIA == 2) return GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length / 2, escenasTropas.Length)]);
		if (_dificultadIA == 0) return GD.Load<PackedScene>(escenasTropas[random.Next(0, escenasTropas.Length / 2)]);
		return GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length)]);
	}

	private bool DebeDefender(Node2D t, Node2D obj)
	{
		int vida = Gi(t,"vidaActual"), esc = Gi(t,"escudoActual");
		// Difícil defiende más inteligentemente
		float umbralVida = _dificultadIA == 2 ? 0.4f : 0.25f;
		if (obj != null) return ((float)vida / Mathf.Max(Gi(t,"vidaMaxima"),1) < umbralVida && esc > 50);
		return vida < 100 && esc > 0;
	}

	// ── COMBATE CON NÚMEROS FLOTANTES ─────────────────────────────────────
	private void ProcesarCombateFrontal(Node2D atacante, string grupoEnemigo)
	{
		if (!IsInstanceValid(atacante) || EstaBlockeada(atacante)) return;
		int    daño = Gi(atacante, "puntosAtaque");
		Node2D obj  = BuscarObjetivoEnCarril(atacante, grupoEnemigo);
		atacante.Call("EjecutarAccion", "atacar");

		if (obj != null && IsInstanceValid(obj))
		{
			int vidaAntes = Gi(obj, "vidaActual");
			obj.Call("RecibirDaño", daño);
			int vidaDespues = Gi(obj, "vidaActual");
			int dañoReal = vidaAntes - vidaDespues;
			if (dañoReal > 0) MostrarDañoFlotante(obj.GlobalPosition, dañoReal);

			// Estadísticas
			if (grupoEnemigo == "tropas_rival") _dañoTotalJugador += dañoReal;
			else                                _dañoTotalRival   += dañoReal;
		}
		else
		{
			if (grupoEnemigo == "tropas_rival")
			{
				vidaRival -= daño; if (vidaRival < 0) vidaRival = 0;
				MostrarDañoFlotante(new Vector2(900, 200), daño);
				_dañoTotalJugador += daño;
			}
			else
			{
				vidaJugador -= daño; if (vidaJugador < 0) vidaJugador = 0;
				MostrarDañoFlotante(new Vector2(200, 200), daño);
				_dañoTotalRival += daño;
			}
			CheckEstadoJuego();
		}
	}

	private Node2D BuscarObjetivoEnCarril(Node2D atacante, string grupo)
	{
		string carril = ((string)atacante.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");
		Node2D mejor  = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			if (((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","") != carril) continue;
			int v = Gi(e,"vidaActual"); if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}

	// ── MENÚ TROPA ────────────────────────────────────────────────────────
	public void MostrarMenuTropa(Node2D tropa)
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || juegoTerminado || tropa.IsInGroup("tropas_rival")) return;
		tropaSeleccionada = tropa;

		Button btnD = menuAcciones?.GetNodeOrNull<Button>("HBoxContainer/BtnDefensa");
		if (btnD != null)
		{
			bool sin = Gi(tropa,"escudoActual") <= 0;
			btnD.Disabled = sin; btnD.Modulate = sin ? new Color(1,1,1,0.4f) : Colors.White;
		}
		if (btnHabilidad != null)
		{
			bool tieneH = tropa.HasMethod("TickHabilidad") ||
						  tropa.GetType().Name == "CaballoPrime"  ||
						  tropa.GetType().Name == "CalamarGPrime" ||
						  tropa.GetType().Name == "GolemPrime";
			bool usada  = HabilidadUsada(tropa);
			btnHabilidad.Visible  = tieneH;
			btnHabilidad.Disabled = usada;
			btnHabilidad.Modulate = usada ? new Color(1,1,1,0.4f) : Colors.White;
		}
		menuAcciones.GlobalPosition = tropa.GetGlobalTransformWithCanvas().Origin + new Vector2(-50,-110);
		menuAcciones.Visible = true;
	}

	public void _on_btn_ataque_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (EstaBlockeada(tropaSeleccionada)) { menuAcciones.Visible = false; return; }

		ProcesarCombateFrontal(tropaSeleccionada, "tropas_rival");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
	}

	public void _on_btn_defensa_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (EstaBlockeada(tropaSeleccionada) || Gi(tropaSeleccionada,"escudoActual") <= 0)
		{ menuAcciones.Visible = false; return; }
		tropaSeleccionada.Call("EjecutarAccion", "preparar_defensa");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
	}

	public void _on_btn_habilidad_pressed()
	{
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada)) return;
		if (HabilidadUsada(tropaSeleccionada)) { menuAcciones.Visible = false; return; }
		tropaSeleccionada.Call("EjecutarAccion", "usar_habilidad");
		tropaSeleccionada.Call("SetActivo", false);
		menuAcciones.Visible = false;
		RegistrarGastoMovimiento();
	}

	private bool HabilidadUsada(Node2D t) { try { return (bool)t.Get("habilidadUsada"); } catch { return false; } }

	// ── BARAJAR / SACRIFICIO ──────────────────────────────────────────────
	public void _on_barajar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || usosBarajar >= MAX_BARAJAR) return;
		usosBarajar++; EjecutarBarajadoLogico(); RegistrarGastoMovimiento();
	}

	private void EjecutarBarajadoLogico()
	{
		foreach (Node n in contenedorMano.GetChildren()) if (n is Carta c) { c.NombreSpot = "X"; c.QueueFree(); }
		GetTree().CreateTimer(0.1f).Timeout += () => { PrepararMazoSinRepetir(); BarajarMazoInicial(); };
	}

	private void CompletarManoAlInicio()
	{
		foreach (string s in new[]{"Spot1","Spot2","Spot3"})
		{
			bool o = false;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion()) o = true;
			if (!o) CrearNuevaCartaEnSpot(s);
		}
	}

	public void _on_sacrificar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500)
		{ if (modoSacrificioActivo) CancelarSacrificio(); return; }
		modoSacrificioActivo = !modoSacrificioActivo;
		Input.SetCustomMouseCursor(modoSacrificioActivo ? iconoCursorSacrificio : null);
	}

	private void VerificarSacrificioEnCampo(Vector2 p)
	{
		foreach (Node2D punto in GetTree().GetNodesInGroup("zonas_invocacion"))
		{
			if (punto.GlobalPosition.DistanceTo(p) >= 110f) continue;
			Node m = punto.GetNodeOrNull("Ocupado");
			if (m == null || !m.HasMeta("tropa_instanciada")) continue;
			Node2D t = (Node2D)m.GetMeta("tropa_instanciada");
			if (!IsInstanceValid(t)) continue;
			usosSacrificio++; EjecutarMuerteTropaSacrificada(t); CancelarSacrificio(); RegistrarGastoMovimiento(); break;
		}
	}

	private void CancelarSacrificio() { modoSacrificioActivo = false; Input.SetCustomMouseCursor(null); }

	// ── INVOCACIÓN ────────────────────────────────────────────────────────
	public void TropaInvocada(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (juegoTerminado || !esTurnoJugador || !puntoMod.IsInGroup("zonas_invocacion")) return;
		if (puntoMod.GetNodeOrNull("Ocupado") != null || escenaTropa == null) return;
		Node2D t = (Node2D)escenaTropa.Instantiate();
		AddChild(t); t.GlobalPosition = puntoMod.GlobalPosition;
		t.AddToGroup("tropas_jugador"); t.SetMeta("carril", puntoMod.Name);
		Node marc = new Node(); marc.Name = "Ocupado"; puntoMod.AddChild(marc); marc.SetMeta("tropa_instanciada", t);

		// Al invocar, salir de fase de invocación → ya puede atacar
		tropasInvocadasTurno++;
		faseInvocacion = false;
		GetTree().CreateTimer(0.1f).Timeout += () =>
		{
			int c = 0;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta ct && !ct.IsQueuedForDeletion() && ct.NombreSpot != "X") c++;
			if (c == 0) GetTree().CreateTimer(1.0f).Timeout += () => { if (!juegoTerminado) EjecutarBarajadoLogico(); };
		};
	}

	private void InvocacionRival(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (escenaTropa == null) return;
		Node2D t = (Node2D)escenaTropa.Instantiate();
		AddChild(t); t.GlobalPosition = puntoMod.GlobalPosition; t.Scale = new Vector2(-1,1);
		t.AddToGroup("tropas_rival"); t.SetMeta("carril", puntoMod.Name);
		Node m = new Node(); m.Name = "Ocupado"; puntoMod.AddChild(m); m.SetMeta("tropa_instanciada", t);
	}

	// ── MUERTE ────────────────────────────────────────────────────────────
	public void EjecutarMuerteTropaSacrificada(Node2D tropa)
	{
		if (!IsInstanceValid(tropa)) return;
		tropa.Call("ReproducirDerrota");
		int castigo = Gi(tropa, "vidaMaxima");

		if (tropa.IsInGroup("tropas_rival"))
		{
			vidaRival -= castigo; if (vidaRival < 0) vidaRival = 0;
			_tropasEliminadasRival++;
		}
		else
		{
			vidaJugador -= castigo; if (vidaJugador < 0) vidaJugador = 0;
			_tropasEliminadasJugador++;
		}

		string carril = (string)tropa.GetMeta("carril");
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		zona?.GetNodeOrNull("Ocupado")?.Free();
		Tween tw = CreateTween(); tw.TweenInterval(0.8f); tw.TweenProperty(tropa,"modulate:a",0.0f,0.6f);
		tw.Finished += () => { if (IsInstanceValid(tropa)) tropa.QueueFree(); };
		CheckEstadoJuego(); ActualizarInterfaz();
	}

	// ── MAZO ──────────────────────────────────────────────────────────────
	private void PrepararMazoSinRepetir()
	{
		mazoIndices.Clear();
		for (int i = 0; i < imagenesCartas.Length; i++) mazoIndices.Add(i);
		for (int i = 0; i < mazoIndices.Count; i++)
		{ int r = random.Next(i, mazoIndices.Count); (mazoIndices[i], mazoIndices[r]) = (mazoIndices[r], mazoIndices[i]); }
		proximoIndiceMazo = 0;
	}

	public void BarajarMazoInicial()
	{
		CrearNuevaCartaEnSpot("Spot1");
		CrearNuevaCartaEnSpot("Spot2");
		CrearNuevaCartaEnSpot("Spot3");
		MostrarTutorialInicio();
	}

	private async void MostrarTutorialInicio()
	{
		if (_turnosJugados > 0) return; // solo en el primer turno

		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		string[] pasos = {
			"👋 ¡Bienvenido a Age of Cards!",
			"🃏 Arrastra una carta al campo para invocar tu tropa",
			"⚔️ Luego haz clic en tu tropa para atacar",
			"🧅 Usa hechizos para potenciar tus tropas o dañar al rival"
		};

		for (int i = 0; i < pasos.Length; i++)
		{
			MostrarAviso(pasos[i], Colors.White);
			await ToSignal(GetTree().CreateTimer(2.5f), "timeout");
		}
	}

	private void CrearNuevaCartaEnSpot(string id)
	{
		if (juegoTerminado || escenaCartaBase == null || contenedorMano == null) return;
		Marker2D spot = contenedorMano.GetNodeOrNull<Marker2D>(id); if (spot == null) return;
		Carta n = (Carta)escenaCartaBase.Instantiate(); n.NombreSpot = id; contenedorMano.AddChild(n);
		Vector2 esc = new Vector2(6.5f,6.5f); n.Scale = esc;
		n.GlobalPosition = spot.GlobalPosition - (n.Size * esc / 2);
		n.GuardarEstadoOriginal();
		if (proximoIndiceMazo >= mazoIndices.Count) PrepararMazoSinRepetir();
		int idx = mazoIndices[proximoIndiceMazo++];
		n.AsignarDatos(imagenesCartas[idx], escenasTropas[idx], idx);
	}

	private void CrearEscenaDeBatalla()
	{
		Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1"), m2 = GetNodeOrNull<Marker2D>("SpawnTrono2");
		if (m1 == null || m2 == null) return;
		tronoJugador = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoJugador);
		tronoJugador.GlobalPosition = m1.GlobalPosition; tronoJugador.CargarHuevo(escenaReyHuevoRef, false);
		tronoRival = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoRival);
		tronoRival.GlobalPosition = m2.GlobalPosition; tronoRival.CargarHuevo(escenaDinoHuevoRef, true);
	}

	// Coloca 1 tropa inicial por lado en el carril central para que turno 1 sea accionable
	private void ColocarTropasIniciales()
	{
		// Tropa del jugador — carril central (Mod2)
		Node2D zonaJugador = GetTree().Root.FindChild("Mod2", true, false) as Node2D;
		if (zonaJugador != null && zonaJugador.GetNodeOrNull("Ocupado") == null)
		{
			int idx = mazoIndices[proximoIndiceMazo % mazoIndices.Count];
			proximoIndiceMazo++;
			var escena = GD.Load<PackedScene>(escenasTropas[idx]);
			if (escena != null)
			{
				Node2D t = (Node2D)escena.Instantiate();
				AddChild(t); t.GlobalPosition = zonaJugador.GlobalPosition;
				t.AddToGroup("tropas_jugador"); t.SetMeta("carril", "Mod2");
				Node m = new Node(); m.Name = "Ocupado"; zonaJugador.AddChild(m); m.SetMeta("tropa_instanciada", t);
			}
		}

		// Tropa del rival — carril central (ModRival2)
		Node2D zonaRival = GetTree().Root.FindChild("ModRival2", true, false) as Node2D;
		if (zonaRival != null && zonaRival.GetNodeOrNull("Ocupado") == null)
		{
			var escena = ElegirTropaIA();
			if (escena != null)
			{
				Node2D t = (Node2D)escena.Instantiate();
				AddChild(t); t.GlobalPosition = zonaRival.GlobalPosition; t.Scale = new Vector2(-1, 1);
				t.AddToGroup("tropas_rival"); t.SetMeta("carril", "ModRival2");
				Node m = new Node(); m.Name = "Ocupado"; zonaRival.AddChild(m); m.SetMeta("tropa_instanciada", t);
			}
		}
	}

	// ── INTERFAZ ──────────────────────────────────────────────────────────
	private void ActualizarInterfaz()
	{
		if (btnBarajar != null)    { bool b = !esTurnoJugador||usosBarajar>=MAX_BARAJAR||movimientosRestantes<=0; btnBarajar.Disabled=b; btnBarajar.Modulate=b?new Color(1,1,1,0.4f):Colors.White; }
		if (btnSacrificio != null) { bool s = !esTurnoJugador||usosSacrificio>=MAX_SACRIFICIO||vidaJugador<=500||movimientosRestantes<=0; btnSacrificio.Disabled=s; btnSacrificio.Modulate=s?new Color(1,1,1,0.4f):Colors.White; }
		if (HasNode("Vida1"))   GetNode<Label>("Vida1").Text = $"Vida: {vidaJugador}";
		if (HasNode("Vida2"))   GetNode<Label>("Vida2").Text = $"Vida: {vidaRival}";
		if (HasNode("Tiempo"))  { int m=tiempoTotalPartida/60,s=tiempoTotalPartida%60; GetNode<Label>("Tiempo").Text=$"Tiempo: {m}:{s:00}"; }
		if (HasNode("LabelTurnoInfo"))
		{
			var l = GetNode<Label>("LabelTurnoInfo");
			string dif     = _dificultadIA == 0 ? "🟢 Fácil" : _dificultadIA == 1 ? "🟡 Medio" : "🔴 Difícil";
			bool urgente   = esTurnoJugador && tiempoTurnoActual <= 8;
			string timer   = urgente ? $"⚠️ {tiempoTurnoActual}s!" : $"⏱ {tiempoTurnoActual}s";
			int maxEnergy  = Mathf.Min(5, 3 + (_turnosJugados / 2) / 3);
			string eBar    = new string('⚡', movimientosRestantes)
			               + new string('·', Mathf.Max(0, maxEnergy - movimientosRestantes));
			int turnoNum   = _turnosJugados / 2 + 1;
			l.Text = $"Turno {turnoNum}  {dif}\n{eBar} {movimientosRestantes}/{maxEnergy} Energía\n{(esTurnoJugador ? "TU TURNO" : "TURNO RIVAL")}  {timer}";
			l.Modulate = urgente      ? Colors.Red
					   : esTurnoJugador ? new Color(0, 0.55f, 0)
					   : new Color(0.85f, 0, 0);
		}
	}

	// ── FIN DE PARTIDA ────────────────────────────────────────────────────
	private void DeterminarGanadorPorTiempo()
	{
		if (vidaJugador > vidaRival) FinalizarPartida("¡VICTORIA!");
		else if (vidaRival > vidaJugador) FinalizarPartida("¡DERROTA!");
		else FinalizarPartida("¡EMPATE!");
	}

	private void CheckEstadoJuego()
	{
		if (vidaJugador <= 0) FinalizarPartida("DERROTA");
		else if (vidaRival <= 0) FinalizarPartida("VICTORIA");
	}

	private void FinalizarPartida(string msg)
	{
		if (juegoTerminado) return;
		juegoTerminado = true;
		timerReloj.Stop();
		GetTree().Paused = false;

		// Actualizar historial de dificultad
		if (msg.Contains("VICTORIA")) _victoriasJugador++;
		else if (msg.Contains("DERROTA")) _derrotasJugador++;

		if (!HasNode("PantallaFinal")) return;
		var pantalla = GetNode<Control>("PantallaFinal");
		pantalla.Visible = true;

		// Resultado principal
		var lbl = GetNodeOrNull<Label>("PantallaFinal/MensajeResultado")
			   ?? GetNodeOrNull<Label>("PantallaFinal/LabelResultado");
		if (lbl != null)
		{
			lbl.Text     = msg;
			lbl.Modulate = msg.Contains("VICTORIA") ? Colors.Gold : msg.Contains("EMPATE") ? Colors.White : Colors.Red;
		}

		// ── Nombre del jugador y racha ────────────────────────────────────────
		string nombreJ = SesionJuego.Instance?.NombreJugador ?? "Jugador";
		var lblNombre = new Label();
		lblNombre.Text = $"👤 {nombreJ}";
		lblNombre.AddThemeColorOverride("font_color", Colors.LightBlue);
		lblNombre.AddThemeFontSizeOverride("font_size", 17);
		lblNombre.Position = new Vector2(50, 55);
		pantalla.AddChild(lblNombre);

		if (_rachaVictorias > 1)
		{
			var lblRacha = new Label();
			lblRacha.Text = $"🔥 Racha: {_rachaVictorias} victorias seguidas";
			lblRacha.AddThemeColorOverride("font_color", Colors.OrangeRed);
			lblRacha.AddThemeFontSizeOverride("font_size", 16);
			lblRacha.Position = new Vector2(50, 80);
			pantalla.AddChild(lblRacha);
		}

		// ── Estadísticas ──────────────────────────────────────────────────────
		var lblStats = new Label();
		lblStats.Text = $"📊 ESTADÍSTICAS\n" +
						$"Daño infligido:     {_dañoTotalJugador}\n" +
						$"Daño recibido:      {_dañoTotalRival}\n" +
						$"Tropas eliminadas:  {_tropasEliminadasRival}\n" +
						$"Tropas perdidas:    {_tropasEliminadasJugador}\n" +
						$"Turnos jugados:     {_turnosJugados}";
		lblStats.AddThemeColorOverride("font_color", Colors.White);
		lblStats.AddThemeFontSizeOverride("font_size", 15);
		lblStats.Position = new Vector2(50, 110);
		lblStats.AutowrapMode = TextServer.AutowrapMode.Word;
		pantalla.AddChild(lblStats);

		// ── Botones ───────────────────────────────────────────────────────────
		var btnReinicio = new Button();
		btnReinicio.Text              = "🔄 Jugar de nuevo";
		btnReinicio.Position          = new Vector2(50, 300);
		btnReinicio.CustomMinimumSize = new Vector2(190, 48);
		btnReinicio.Pressed += () => GetTree().ReloadCurrentScene();
		pantalla.AddChild(btnReinicio);

		var btnMenu = new Button();
		btnMenu.Text              = "🏠 Menú Principal";
		btnMenu.Position          = new Vector2(255, 300);
		btnMenu.CustomMinimumSize = new Vector2(190, 48);
		btnMenu.Pressed += () => GetTree().ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
		pantalla.AddChild(btnMenu);

		if (SesionJuego.Instance?.EstaLogueado == true)
		{
			var btnRanking = new Button();
			btnRanking.Text              = "🏆 Ver Ranking";
			btnRanking.Position          = new Vector2(50, 358);
			btnRanking.CustomMinimumSize = new Vector2(395, 44);
			btnRanking.SelfModulate      = Colors.Gold;
			btnRanking.Pressed += () =>
			{
				var panel = new PanelRanking();
				pantalla.AddChild(panel);
				panel.Mostrar();
			};
			pantalla.AddChild(btnRanking);
		}

		// Guardar resultado en sesión y enviar al backend
		string resultadoStr = msg.Contains("VICTORIA") ? "victoria"
							: msg.Contains("EMPATE")   ? "empate" : "derrota";
		VerificarLogros(msg);
		if (SesionJuego.Instance != null)
		{
			SesionJuego.Instance.UltimoResultado    = resultadoStr;
			SesionJuego.Instance.DañoUltimaPartida  = _dañoTotalJugador;
			if (SesionJuego.Instance.EstaLogueado)
				EnviarResultadoBackend(resultadoStr, _dañoTotalJugador);
		}
	}

	private void EnviarResultadoBackend(string resultado, int daño)
	{
		var http = new Godot.HttpRequest();
		AddChild(http);
		string json = System.Text.Json.JsonSerializer.Serialize(new {
			UsuarioId = SesionJuego.Instance!.UsuarioId,
			Resultado = resultado,
			DañoHecho = daño
		});
		string[] h = { "Content-Type: application/json" };
		http.Request("http://localhost:5289/api/usuarios/resultado", h, HttpClient.Method.Post, json);
		GD.Print($"[Campo1] Resultado → backend: {resultado}");
	}

	// ── IDENTIDAD VISUAL POR ERA ─────────────────────────────────────────
	private void AplicarIdentidadEra()
	{
		if (SesionJuego.Instance == null || !SesionJuego.Instance.TieneMazo) return;

		var escenas = SesionJuego.Instance.MazoSeleccionado;
		int era1 = 0, era2 = 0, era3 = 0;
		foreach (string e in escenas)
		{
			if (e.Contains("TRex") || e.Contains("Tiburon") || e.Contains("CalamarG")) era1++;
			else if (e.Contains("Torre") || e.Contains("Caballo") || e.Contains("Dama") ||
					 e.Contains("SoldadoReal") || e.Contains("Peon") || e.Contains("Encebollado")) era2++;
			else era3++;
		}

		string nombreEra = era1 >= era2 && era1 >= era3 ? "🦕 Era Primordial"
						 : era2 >= era1 && era2 >= era3 ? "⚔️ Era Medieval"
														: "🔮 Era Mística";

		// Mostrar nombre de la era al inicio
		var aviso = new Label();
		aviso.Text = nombreEra;
		aviso.AddThemeFontSizeOverride("font_size", 28);
		aviso.AddThemeColorOverride("font_color", Colors.Gold);
		aviso.Position = new Vector2(450, 250);
		aviso.ZIndex   = 100;
		AddChild(aviso);
		Tween tw = CreateTween();
		tw.TweenInterval(1.5f);
		tw.TweenProperty(aviso, "modulate:a", 0.0f, 0.8f);
		tw.Finished += () => { if (IsInstanceValid(aviso)) aviso.QueueFree(); };
	}

	// ── IA HECHIZOS ──────────────────────────────────────────────────────
	private void IAUsarHechizo()
	{
		// Buscar tropa del jugador con más vida para envenenaría
		Node2D objetivo = null;
		int maxVida = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
		{
			if (!(n is Node2D t) || !IsInstanceValid(t)) continue;
			int v = Gi(t, "vidaActual");
			if (v > maxVida) { maxVida = v; objetivo = t; }
		}
		if (objetivo == null) return;

		// Alternar entre veneno y bloqueo
		if (random.Next(2) == 0)
		{
			objetivo.SetMeta("envenenado",   true);
			objetivo.SetMeta("dañoVeneno",   40);
			objetivo.SetMeta("turnosVeneno", 2);
			objetivo.Modulate = new Color(0.6f, 1f, 0.4f);
			ActualizarIconosEstado(objetivo);
		}
		else
		{
			objetivo.SetMeta("bloqueado",     true);
			objetivo.SetMeta("turnosBloqueo", 1);
			objetivo.Modulate = new Color(0.4f, 0.6f, 1.4f);
			ActualizarIconosEstado(objetivo);
		}
	}

	// ── IA PRIORIZA TROPAS DÉBILES ────────────────────────────────────────
	private Node2D BuscarObjetivoDebilEnCarril(Node2D atacante, string grupo)
	{
		string carril = ((string)atacante.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");
		Node2D mejor = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			if (((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","") != carril) continue;
			int v = Gi(e, "vidaActual");
			if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}

	// ── SISTEMA DE LOGROS ─────────────────────────────────────────────────
	private void VerificarLogros(string resultado)
	{
		if (resultado.Contains("VICTORIA"))
		{
			// Primera victoria
			if (!_logroPrimeraVictoria)
			{
				_logroPrimeraVictoria = true;
				MostrarLogroEnPantalla("🏆 LOGRO: ¡Primera Victoria!");
			}
			// Racha
			_rachaVictorias++;
			if (SesionJuego.Instance != null)
				SesionJuego.Instance.RachaActual = _rachaVictorias;
			if (_rachaVictorias >= 3)
				MostrarLogroEnPantalla($"🔥 ¡RACHA DE {_rachaVictorias} VICTORIAS!");
		}
		else
		{
			_rachaVictorias = 0;
			if (SesionJuego.Instance != null)
				SesionJuego.Instance.RachaActual = 0;
		}

		// Logro habilidades
		if (!_logroHabilidadUsada)
		{
			foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			{
				if (n is Node2D t && HabilidadUsada(t))
				{
					_logroHabilidadUsada = true;
					MostrarLogroEnPantalla("⚡ LOGRO: ¡Usaste una habilidad especial!");
					break;
				}
			}
		}
	}

	private void MostrarLogroEnPantalla(string texto)
	{
		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", Colors.Gold);
		lbl.AddThemeFontSizeOverride("font_size", 20);
		lbl.Position = new Vector2(300, 60);
		lbl.ZIndex   = 300;
		AddChild(lbl);
		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(lbl, "position:y", lbl.Position.Y - 50f, 1.5f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 1.5f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	private void MostrarAviso(string texto, Color color)
	{
		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 19);
		lbl.Position = new Vector2(300, 90);
		lbl.ZIndex   = 200;
		AddChild(lbl);
		Tween tw = CreateTween();
		tw.TweenInterval(2.0f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.5f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	private void MostrarAvisoFase(string msg)
	{
		var lbl = new Label();
		lbl.Text = msg;
		lbl.AddThemeColorOverride("font_color", Colors.OrangeRed);
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.Position  = new Vector2(350, 20);
		lbl.ZIndex    = 200;
		AddChild(lbl);
		Tween tw = CreateTween();
		tw.TweenInterval(2.0f);
		tw.TweenProperty(lbl, "modulate:a", 0.0f, 0.5f);
		tw.Finished += () => { if (IsInstanceValid(lbl)) lbl.QueueFree(); };
	}

	public void RegistrarGastoMovimiento()
	{
		movimientosRestantes--; ActualizarInterfaz();
		if (movimientosRestantes <= 0 && !juegoTerminado) CambiarTurno();
	}

	public void AplicarVenenoMeta(Node2D t, int daño, int turnos)
	{
		t.SetMeta("envenenado", true); t.SetMeta("dañoVeneno", daño); t.SetMeta("turnosVeneno", turnos);
		t.Modulate = new Color(0.6f, 1f, 0.4f);
		ActualizarIconosEstado(t);
	}

	public void AplicarBloqueoMeta(Node2D t, int turnos)
	{
		t.SetMeta("bloqueado", true); t.SetMeta("turnosBloqueo", turnos);
		t.Modulate = new Color(0.4f, 0.6f, 1.4f);
		ActualizarIconosEstado(t);
	}

	private int  Gi(Node2D n, string p) { try { return (int)n.Get(p); } catch { return 0; } }

	private bool EstaBlockeada(Node2D t)
	{
		if (t.HasMeta("bloqueado")) try { return (bool)t.GetMeta("bloqueado"); } catch { }
		return false;
	}
}
