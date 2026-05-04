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
	private Button   btnHabilidad;   // creado en código

	[Export] private Texture2D   iconoCursorSacrificio;
	[Export] private PackedScene escenaCartaBase;
	[Export] private Control     contenedorMano;

	// ── HECHIZOS: usos por partida ────────────────────────────────────────
	private bool usadoEncebollado = false;
	private bool usadoCuracion    = false;
	private bool usadoRobo        = false;
	private bool usadoVeneno      = false;
	private bool usadoBloqueo     = false;

	// Modo selección de objetivo para veneno/bloqueo
	private bool   _modoSeleccionObjetivo = false;
	private string _hechizoPendiente      = "";
	private Label  _lblInstruccion;

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

		// Rutas reales de la escena campo_1.tscn
		menuAcciones  = GetNodeOrNull<Control>("InterfazMenu/MenuAcciones");
		btnBarajar    = GetNodeOrNull<Button>("Barajar");
		btnSacrificio = GetNodeOrNull<Button>("Sacrificar");

		if (menuAcciones != null)
		{
			menuAcciones.Visible     = false;
			menuAcciones.MouseFilter = Control.MouseFilterEnum.Stop;

			// Agregar botón Habilidad al HBoxContainer del menú
			var hbox = menuAcciones.GetNodeOrNull<HBoxContainer>("HBoxContainer");
			if (hbox != null)
			{
				btnHabilidad           = new Button();
				btnHabilidad.Text      = "⚡ HABILIDAD";
				btnHabilidad.Visible   = false;
				btnHabilidad.Pressed  += _on_btn_habilidad_pressed;
				hbox.AddChild(btnHabilidad);
			}
		}

		CrearPanelHechizos();
		PrepararMazoSinRepetir();
		CrearEscenaDeBatalla();
		BarajarMazoInicial();
		ActualizarInterfaz();
	}

	// Señales de la escena que deben existir
	public void _on_timer_timeout() { }
	public void _on_pasar_turno_pressed() { if (!esTurnoJugador || juegoTerminado) return; CambiarTurno(); }

	// ── PANEL DE HECHIZOS ─────────────────────────────────────────────────
	private void CrearPanelHechizos()
	{
		var panel = new Control(); panel.Name = "PanelHechizos";
		var vbox  = new VBoxContainer(); vbox.Position = new Vector2(1095, 560);
		panel.AddChild(vbox);

		// Etiqueta de instrucción para selección de objetivo
		_lblInstruccion = new Label();
		_lblInstruccion.Text    = "Haz clic en una\ntropa enemiga";
		_lblInstruccion.Visible = false;
		_lblInstruccion.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
		vbox.AddChild(_lblInstruccion);

		AgregarBtnHechizo(vbox, "🧅 Encebollado", new Color(1f,0.7f,0.1f), () => UsarEncebollado());
		AgregarBtnHechizo(vbox, "💚 Curación",    new Color(0.2f,0.9f,0.3f), () => UsarCuracion());
		AgregarBtnHechizo(vbox, "🃏 Robar Carta", new Color(0.3f,0.7f,1f),  () => UsarRobo());
		AgregarBtnHechizo(vbox, "☠️ Veneno",      new Color(0.6f,0.2f,0.8f), () => IniciarSeleccionObjetivo("veneno"));
		AgregarBtnHechizo(vbox, "🔒 Bloqueo",     new Color(0.2f,0.5f,0.9f), () => IniciarSeleccionObjetivo("bloqueo"));

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

	// ── HECHIZOS ──────────────────────────────────────────────────────────
	// ENCEBOLLADO: +100 ATK y ESC a tropa seleccionada
	private void UsarEncebollado()
	{
		if (!ValidarHechizo()) return;
		if (usadoEncebollado) { GD.Print("Encebollado ya usado"); return; }
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada) || !tropaSeleccionada.IsInGroup("tropas_jugador"))
		{ GD.Print("Selecciona primero una tropa tuya"); return; }

		int ata = 0, esc = 0, escMax = 0;
		try { ata    = (int)tropaSeleccionada.Get("puntosAtaque"); } catch { }
		try { esc    = (int)tropaSeleccionada.Get("escudoActual"); } catch { }
		try { escMax = (int)tropaSeleccionada.Get("escudoMaximo"); } catch { }
		try { tropaSeleccionada.Set("puntosAtaque", ata + 100); }                      catch { }
		try { tropaSeleccionada.Set("escudoActual", esc + 100); }                      catch { }
		try { tropaSeleccionada.Set("escudoMaximo", Mathf.Max(escMax, esc+100)); }     catch { }

		Tween tw = tropaSeleccionada.CreateTween();
		tw.TweenProperty(tropaSeleccionada, "modulate", new Color(1.6f,1.3f,0.2f), 0.2f);
		tw.TweenProperty(tropaSeleccionada, "modulate", Colors.White, 0.5f);

		usadoEncebollado = true;
		RegistrarGastoMovimiento();
	}

	// CURACIÓN: +200 HP a tropa aliada o al jugador
	private void UsarCuracion()
	{
		if (!ValidarHechizo()) return;
		if (usadoCuracion) { GD.Print("Curación ya usada"); return; }

		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada) && tropaSeleccionada.IsInGroup("tropas_jugador"))
		{
			int vida = 0, vidaMax = 0;
			try { vida    = (int)tropaSeleccionada.Get("vidaActual"); } catch { }
			try { vidaMax = (int)tropaSeleccionada.Get("vidaMaxima"); } catch { }
			try { tropaSeleccionada.Set("vidaActual", Mathf.Min(vida + 200, vidaMax)); } catch { }
			Tween tw = tropaSeleccionada.CreateTween();
			tw.TweenProperty(tropaSeleccionada, "modulate", new Color(0.3f,1.6f,0.5f), 0.2f);
			tw.TweenProperty(tropaSeleccionada, "modulate", Colors.White, 0.5f);
		}
		else
		{
			vidaJugador = Mathf.Min(vidaJugador + 200, vidaMaxJugador);
		}

		usadoCuracion = true;
		RegistrarGastoMovimiento();
		ActualizarInterfaz();
	}

	// ROBO DE CARTAS
	private void UsarRobo()
	{
		if (!ValidarHechizo()) return;
		if (usadoRobo) { GD.Print("Robo ya usado"); return; }

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

	// VENENO y BLOQUEO: primero activar modo selección, luego clic en tropa rival
	private void IniciarSeleccionObjetivo(string hechizo)
	{
		if (!ValidarHechizo()) return;
		if (hechizo == "veneno"  && usadoVeneno)  { GD.Print("Veneno ya usado");  return; }
		if (hechizo == "bloqueo" && usadoBloqueo) { GD.Print("Bloqueo ya usado"); return; }

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
			objetivo.Modulate = new Color(0.6f, 1f, 0.4f);
			usadoVeneno = true;
		}
		else if (_hechizoPendiente == "bloqueo")
		{
			objetivo.SetMeta("bloqueado",     true);
			objetivo.SetMeta("turnosBloqueo", 2);
			objetivo.Modulate = new Color(0.4f, 0.6f, 1.4f);
			usadoBloqueo = true;
		}

		_modoSeleccionObjetivo = false;
		_hechizoPendiente      = "";
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		RegistrarGastoMovimiento();
	}

	private bool ValidarHechizo() =>
		esTurnoJugador && movimientosRestantes > 0 && !juegoTerminado;

	// ── INPUT: detectar clic en tropa rival para veneno/bloqueo ──────────
	public override void _Input(InputEvent @event)
	{
		if (juegoTerminado || !esTurnoJugador) return;

		if (_modoSeleccionObjetivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mouse = GetGlobalMousePosition();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			{
				if (n is Node2D t && IsInstanceValid(t) && t.GlobalPosition.DistanceTo(mouse) < 90f)
				{
					AplicarHechizoEnObjetivo(t);
					return;
				}
			}
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
		esTurnoJugador       = !esTurnoJugador;
		tiempoTurnoActual    = 20;
		movimientosRestantes = 3;
		usosBarajar          = 0;
		usosSacrificio       = 0;

		// Cancelar modo selección si estaba activo
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

		ActualizarInterfaz();
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

				// Notificar TickHabilidad a Torre (efecto temporal)
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
		if (t.HasMethod("RecibirDaño")) t.Call("RecibirDaño", daño);
		turnos--;
		if (turnos <= 0) { t.SetMeta("envenenado", false); t.Modulate = Colors.White; }
		else t.SetMeta("turnosVeneno", turnos);
	}

	private void TickBloqueo(Node2D t)
	{
		if (!t.HasMeta("bloqueado")) return;
		bool bl; try { bl = (bool)t.GetMeta("bloqueado"); } catch { return; }
		if (!bl) return;
		int turnos = t.HasMeta("turnosBloqueo") ? (int)t.GetMeta("turnosBloqueo") : 1;
		turnos--;
		if (turnos <= 0) { t.SetMeta("bloqueado", false); t.Modulate = Colors.White; }
		else t.SetMeta("turnosBloqueo", turnos);
	}

	// ── IA ────────────────────────────────────────────────────────────────
	private async void EjecutarTurnoIA()
	{
		if (juegoTerminado || esTurnoJugador) return;
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		string[] puntos = { "ModRival1", "ModRival2", "ModRival3" };
		foreach (string nombre in puntos)
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
			InvocacionRival(zona, GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length)]));
			await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
		}

		var bots = new List<Node2D>();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);

		foreach (Node2D tropa in bots)
		{
			if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
			if (EstaBlockeada(tropa)) continue;

			Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");

			// IA usa habilidad si la tiene y no la ha usado
			bool tieneH = HabilidadUsada(tropa) == false && (
				tropa.HasMethod("TickHabilidad") || // Torre
				tropa.GetType().Name == "CaballoPrime" ||
				tropa.GetType().Name == "CalamarGPrime" ||
				tropa.GetType().Name == "GolemPrime");

			if (tieneH && !HabilidadUsada(tropa) && random.Next(3) == 0)
				tropa.Call("EjecutarAccion", "usar_habilidad");
			else if (DebeDefender(tropa, objetivo))
				tropa.Call("EjecutarAccion", "preparar_defensa");
			else
				ProcesarCombateFrontal(tropa, "tropas_jugador");

			movimientosRestantes--;
			ActualizarInterfaz();
			await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		}

		if (!esTurnoJugador && !juegoTerminado) CambiarTurno();
	}

	private bool DebeDefender(Node2D t, Node2D obj)
	{
		int vida = Gi(t, "vidaActual"), esc = Gi(t, "escudoActual");
		if (obj != null) return (vida < 150 && esc > 50) || Gi(t, "puntosAtaque") < 100;
		return vida < 100 && esc > 0;
	}

	// ── COMBATE ───────────────────────────────────────────────────────────
	private void ProcesarCombateFrontal(Node2D atacante, string grupoEnemigo)
	{
		if (!IsInstanceValid(atacante) || EstaBlockeada(atacante)) return;
		int    daño = Gi(atacante, "puntosAtaque");
		Node2D obj  = BuscarObjetivoEnCarril(atacante, grupoEnemigo);
		atacante.Call("EjecutarAccion", "atacar");

		if (obj != null && IsInstanceValid(obj))
			obj.Call("RecibirDaño", daño);
		else
		{
			if (grupoEnemigo == "tropas_rival") { vidaRival   -= daño; if (vidaRival   < 0) vidaRival   = 0; }
			else                                { vidaJugador -= daño; if (vidaJugador < 0) vidaJugador = 0; }
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
			int v = Gi(e, "vidaActual"); if (v < min) { min = v; mejor = e; }
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
			bool sin = Gi(tropa, "escudoActual") <= 0;
			btnD.Disabled = sin; btnD.Modulate = sin ? new Color(1,1,1,0.4f) : Colors.White;
		}

		// Mostrar botón habilidad si la tropa tiene habilidad
		if (btnHabilidad != null)
		{
			bool tieneH = tropa.HasMethod("TickHabilidad") ||
						  tropa.GetType().Name == "CaballoPrime" ||
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
		if (EstaBlockeada(tropaSeleccionada) || Gi(tropaSeleccionada, "escudoActual") <= 0)
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

	private bool HabilidadUsada(Node2D t)
	{
		try { return (bool)t.Get("habilidadUsada"); } catch { return false; }
	}

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
		if (tropa.IsInGroup("tropas_rival")) { vidaRival   -= castigo; if (vidaRival   < 0) vidaRival   = 0; }
		else                                 { vidaJugador -= castigo; if (vidaJugador < 0) vidaJugador = 0; }

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

	public void BarajarMazoInicial() { CrearNuevaCartaEnSpot("Spot1"); CrearNuevaCartaEnSpot("Spot2"); CrearNuevaCartaEnSpot("Spot3"); }

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
			l.Text    = $"{(esTurnoJugador ? "TU TURNO" : "TURNO RIVAL")}\nSiguiente en: {tiempoTurnoActual}s\nMovimientos: {movimientosRestantes}";
			l.Modulate = esTurnoJugador ? new Color(0,0.5f,0) : new Color(0.8f,0,0);
		}
	}

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
		juegoTerminado = true; timerReloj.Stop();
		if (!HasNode("PantallaFinal")) return;
		GetNode<Control>("PantallaFinal").Visible = true;
		var lbl = GetNodeOrNull<Label>("PantallaFinal/LabelResultado")
			   ?? GetNodeOrNull<Label>("PantallaFinal/MensajeResultado");
		if (lbl != null) lbl.Text = msg;
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
	}

	public void AplicarBloqueoMeta(Node2D t, int turnos)
	{
		t.SetMeta("bloqueado", true); t.SetMeta("turnosBloqueo", turnos);
		t.Modulate = new Color(0.4f, 0.6f, 1.4f);
	}

	private int  Gi(Node2D n, string p) { try { return (int)n.Get(p);  } catch { return 0; } }

	private bool EstaBlockeada(Node2D t)
	{
		if (t.HasMeta("bloqueado")) try { return (bool)t.GetMeta("bloqueado"); } catch { }
		return false;
	}
}
