using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	[Export] private int vidaJugador = 2000;
	[Export] private int vidaRival = 2000;
	private int tiempoTotalPartida = 300; 
	public bool juegoTerminado = false;

	// --- SISTEMA DE TURNOS ---
	public bool esTurnoJugador = true;
	public int movimientosRestantes = 3; 
	private int tiempoTurnoActual = 20; 
	private Timer timerReloj; 

	// --- LIMITADORES DE ACCIONES ---
	private int usosBarajar = 0;
	private int usosSacrificio = 0;
	private const int MAX_BARAJAR = 1;
	private const int MAX_SACRIFICIO = 2;

	// REFERENCIAS INTERFAZ
	private Control menuAcciones;
	private Node2D tropaSeleccionada;
	private Button btnBarajar;
	private Button btnSacrificio;

	[Export] private Texture2D iconoCursorSacrificio;
	[Export] private PackedScene escenaCartaBase; 
	[Export] private Control contenedorMano; 

	private List<int> mazoIndices = new List<int>();
	private int proximoIndiceMazo = 0;
	private bool modoSacrificioActivo = false;
	private Random random = new Random();

	[Export] private PackedScene escenaTronoRef = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private string[] imagenesCartas = {
		"res://imagenes/CartasPng/DragonCart.png", "res://imagenes/CartasPng/GolemCart.png",
		"res://imagenes/CartasPng/MaguinCart.png", "res://imagenes/CartasPng/SoldRealCart.png",
		"res://imagenes/CartasPng/TReXCart.png", "res://imagenes/CartasPng/TiburonCart.png",
		"res://imagenes/CartasPng/PeonCart.png", "res://imagenes/CartasPng/EncebolladoCart.png",
		"res://imagenes/CartasPng/CalamarGCart.png", "res://imagenes/CartasPng/CaballoCart.png",
		"res://imagenes/CartasPng/DamaCart.png", "res://imagenes/CartasPng/TorreCart.png"
	};

	private string[] escenasTropas = {
		"res://cartas prime/Dragon_prime.tscn", "res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn", "res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/TRex_prime.tscn", "res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/Peon_prime.tscn", "res://cartas prime/Encebollado_prime.tscn",
		"res://cartas prime/CalamarG_prime.tscn", "res://cartas prime/Caballo_prime.tscn",
		"res://cartas prime/Dama_prime.tscn", "res://cartas prime/Torre_prime.tscn"
	};

	private tronocampo tronoJugador, tronoRival; 

	public override void _Ready()
	{
		timerReloj = new Timer();
		timerReloj.WaitTime = 1.0f;
		timerReloj.Timeout += OnTickReloj;
		AddChild(timerReloj);
		timerReloj.Start();

		menuAcciones = GetNodeOrNull<Control>("InterfazMenu/MenuAcciones");
		btnBarajar = GetNodeOrNull<Button>("InterfazMenu/BtnBarajar");
		btnSacrificio = GetNodeOrNull<Button>("InterfazMenu/BtnSacrificio");

		if (menuAcciones != null) 
		{
			menuAcciones.Visible = false;
			menuAcciones.MouseFilter = Control.MouseFilterEnum.Stop; 
		}

		PrepararMazoSinRepetir();
		CrearEscenaDeBatalla();
		BarajarMazoInicial();
		ActualizarInterfaz();
	}

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
		esTurnoJugador = !esTurnoJugador;
		tiempoTurnoActual = 20; 
		movimientosRestantes = 3; 
		usosBarajar = 0;
		usosSacrificio = 0;

		if (modoSacrificioActivo) CancelarSacrificio(); 
		if (menuAcciones != null) menuAcciones.Visible = false;

		if (esTurnoJugador)
		{
			CompletarManoAlInicio();
			foreach (Node nodo in GetTree().GetNodesInGroup("tropas_jugador"))
				if (nodo.HasMethod("SetActivo")) nodo.Call("SetActivo", true);
		}
		else EjecutarTurnoIA();
		
		ActualizarInterfaz();
	}

	// --- LÓGICA DE IA INTELIGENTE ---
	private async void EjecutarTurnoIA()
	{
		if (juegoTerminado || esTurnoJugador) return;
		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		// 1. INVOCACIÓN PRIORITARIA: Buscar espacios vacíos
		string[] nombresPuntos = { "ModRival1", "ModRival2", "ModRival3" };
		foreach (string nombre in nombresPuntos)
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona == null) continue;
			Node marcador = zona.GetNodeOrNull("Ocupado");
			if (marcador == null)
			{
				int idx = random.Next(escenasTropas.Length);
				InvocacionRival(zona, GD.Load<PackedScene>(escenasTropas[idx]));
				await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
			}
		}

		// 2. TOMA DE DECISIONES POR TROPA
		var tropasBot = GetTree().GetNodesInGroup("tropas_rival");
		foreach (Node2D tropa in tropasBot)
		{
			if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;

			// Obtener datos del rival en su carril
			Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
			int miVida = (int)tropa.Get("vidaActual");
			int miEscudo = (int)tropa.Get("escudoActual");

			bool decidirDefender = false;

			if (objetivo != null)
			{
				int vidaEnemigo = (int)objetivo.Get("vidaActual");
				int miAtaque = (int)tropa.Get("puntosAtaque");
				bool enemigoDefendiendo = (bool)objetivo.Get("estaDefendiendo");

				// LÓGICA: ¿Debo defender en lugar de atacar?
				// Si tengo poca vida pero tengo escudo, o si el enemigo es muy fuerte y yo estoy herido
				if (miVida < 150 && miEscudo > 50) decidirDefender = true;
				// Si el enemigo está defendiendo y mi ataque es flojo, mejor me defiendo yo tmb
				if (enemigoDefendiendo && miAtaque < 150) decidirDefender = true;
			}
			else 
			{
				// Si no hay nadie enfrente, atacar al Rey es prioridad, pero si estoy casi muerto, defiendo
				if (miVida < 100 && miEscudo > 0) decidirDefender = true;
			}

			// Ejecutar Acción
			if (decidirDefender)
			{
				tropa.Call("EjecutarAccion", "preparar_defensa");
			}
			else
			{
				ProcesarCombateFrontal(tropa, "tropas_jugador");
			}

			movimientosRestantes--;
			ActualizarInterfaz();
			await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
		}

		if (!esTurnoJugador && !juegoTerminado) CambiarTurno();
	}

	private Node2D BuscarObjetivoEnCarril(Node2D atacante, string grupoEnemigo)
	{
		string carrilAtacante = (string)atacante.GetMeta("carril");
		string idNumero = carrilAtacante.ToLower().Replace("modrival", "").Replace("mod", "");

		foreach (Node2D ene in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (IsInstanceValid(ene))
			{
				string idEnemigo = ((string)ene.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "");
				if (idEnemigo == idNumero) return ene;
			}
		}
		return null;
	}

	private void InvocacionRival(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (escenaTropa == null) return;
		Node2D nuevaTropa = (Node2D)escenaTropa.Instantiate();
		AddChild(nuevaTropa);
		nuevaTropa.GlobalPosition = puntoMod.GlobalPosition;
		nuevaTropa.Scale = new Vector2(-1, 1); 
		nuevaTropa.AddToGroup("tropas_rival");
		nuevaTropa.SetMeta("carril", puntoMod.Name);
		Node marcador = new Node(); marcador.Name = "Ocupado";
		puntoMod.AddChild(marcador);
		marcador.SetMeta("tropa_instanciada", nuevaTropa);
	}

	private void ProcesarCombateFrontal(Node2D atacante, string grupoEnemigo)
	{
		if (!IsInstanceValid(atacante)) return;
		
		int daño = (int)atacante.Get("puntosAtaque");
		Node2D objetivo = BuscarObjetivoEnCarril(atacante, grupoEnemigo);

		atacante.Call("EjecutarAccion", "atacar");

		if (objetivo != null)
		{
			bool estaDefendiendo = (bool)objetivo.Get("estaDefendiendo"); 
			if (estaDefendiendo) objetivo.Call("RecibirDañoEscudo", daño);
			else objetivo.Call("RecibirDaño", daño);

			int vidaQueda = (int)objetivo.Get("vidaActual");
			if (vidaQueda <= 0) EjecutarMuerteTropaSacrificada(objetivo);
		}
		else
		{
			if (grupoEnemigo == "tropas_rival") vidaRival -= daño;
			else vidaJugador -= daño;
		}
		CheckEstadoJuego();
	}

	private void RegistrarGastoMovimiento() { movimientosRestantes--; ActualizarInterfaz(); if (movimientosRestantes <= 0 && !juegoTerminado) CambiarTurno(); }

	public void MostrarMenuTropa(Node2D tropa)
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || juegoTerminado || tropa.IsInGroup("tropas_rival")) return;
		tropaSeleccionada = tropa;
		
		Button btnDefensa = menuAcciones.GetNodeOrNull<Button>("BtnDefensa");
		if (btnDefensa != null)
		{
			int escudoDispo = (int)tropa.Get("escudoActual");
			btnDefensa.Disabled = (escudoDispo <= 0);
			btnDefensa.Modulate = (escudoDispo <= 0) ? new Color(1, 1, 1, 0.4f) : new Color(1, 1, 1, 1);
		}

		Vector2 pos = tropa.GetGlobalTransformWithCanvas().Origin;
		menuAcciones.GlobalPosition = pos + new Vector2(-50, -100); 
		menuAcciones.Visible = true;
	}

	public void _on_btn_ataque_pressed()
	{
		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada))
		{
			ProcesarCombateFrontal(tropaSeleccionada, "tropas_rival");
			if (tropaSeleccionada.HasMethod("SetActivo")) tropaSeleccionada.Call("SetActivo", false);
			menuAcciones.Visible = false;
			RegistrarGastoMovimiento();
		}
	}

	public void _on_btn_defensa_pressed()
	{
		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada))
		{
			if ((int)tropaSeleccionada.Get("escudoActual") > 0)
			{
				tropaSeleccionada.Call("EjecutarAccion", "preparar_defensa");
				if (tropaSeleccionada.HasMethod("SetActivo")) tropaSeleccionada.Call("SetActivo", false);
				menuAcciones.Visible = false;
				RegistrarGastoMovimiento();
			}
		}
	}

	// --- FUNCIONES DE SOPORTE ---
	public void _on_barajar_pressed() { if (!esTurnoJugador || movimientosRestantes <= 0 || usosBarajar >= MAX_BARAJAR) return; usosBarajar++; EjecutarBarajadoLogico(); RegistrarGastoMovimiento(); }
	private void EjecutarBarajadoLogico() { foreach (Node n in contenedorMano.GetChildren()) if (n is Carta c) { c.NombreSpot = "X"; c.QueueFree(); } GetTree().CreateTimer(0.1f).Timeout += () => { PrepararMazoSinRepetir(); BarajarMazoInicial(); }; }
	private void CompletarManoAlInicio() { string[] ss = { "Spot1", "Spot2", "Spot3" }; foreach (string s in ss) { bool o = false; foreach (Node n in contenedorMano.GetChildren()) if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion()) o = true; if (!o) CrearNuevaCartaEnSpot(s); } }

	public void _on_sacrificar_pressed() { if (!esTurnoJugador || movimientosRestantes <= 0 || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500) { if (modoSacrificioActivo) CancelarSacrificio(); return; } modoSacrificioActivo = !modoSacrificioActivo; if (modoSacrificioActivo && iconoCursorSacrificio != null) Input.SetCustomMouseCursor(iconoCursorSacrificio); else Input.SetCustomMouseCursor(null); }
	public override void _Input(InputEvent @event) { if (juegoTerminado || !esTurnoJugador) return; if (modoSacrificioActivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) VerificarSacrificioEnCampo(GetGlobalMousePosition()); }
	private void VerificarSacrificioEnCampo(Vector2 posClick) { foreach (Node2D punto in GetTree().GetNodesInGroup("zonas_invocacion")) { if (punto.GlobalPosition.DistanceTo(posClick) < 110f) { Node marcador = punto.GetNodeOrNull("Ocupado"); if (marcador != null && marcador.HasMeta("tropa_instanciada")) { Node2D tropa = (Node2D)marcador.GetMeta("tropa_instanciada"); if (IsInstanceValid(tropa)) { usosSacrificio++; EjecutarMuerteTropaSacrificada(tropa); CancelarSacrificio(); RegistrarGastoMovimiento(); break; } } } } }
	private void CancelarSacrificio() { modoSacrificioActivo = false; Input.SetCustomMouseCursor(null); }

	public void TropaInvocada(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (juegoTerminado || !esTurnoJugador || !puntoMod.IsInGroup("zonas_invocacion")) return; 
		if (puntoMod.GetNodeOrNull("Ocupado") != null) return; 
		if (escenaTropa != null)
		{
			Node2D nuevaTropa = (Node2D)escenaTropa.Instantiate();
			AddChild(nuevaTropa);
			nuevaTropa.GlobalPosition = puntoMod.GlobalPosition;
			nuevaTropa.AddToGroup("tropas_jugador");
			nuevaTropa.SetMeta("carril", puntoMod.Name);
			Node marcador = new Node(); marcador.Name = "Ocupado";
			puntoMod.AddChild(marcador);
			marcador.SetMeta("tropa_instanciada", nuevaTropa); 
			GetTree().CreateTimer(0.1f).Timeout += () => {
				int c = 0; foreach (Node n in contenedorMano.GetChildren()) if (n is Carta ct && !ct.IsQueuedForDeletion() && ct.NombreSpot != "X") c++;
				if (c == 0) GetTree().CreateTimer(1.0f).Timeout += () => { if (!juegoTerminado) EjecutarBarajadoLogico(); };
			};
		}
	}

	public void EjecutarMuerteTropaSacrificada(Node2D tropa)
	{
		if (!IsInstanceValid(tropa)) return;
		tropa.Call("ReproducirDerrota");
		int castigo = (int)tropa.Get("vidaMaxima");
		if (tropa.IsInGroup("tropas_rival")) vidaRival -= castigo; else vidaJugador -= castigo;
		string carril = (string)tropa.GetMeta("carril");
		Node2D zona = GetTree().Root.FindChild(carril, true, false) as Node2D;
		if (zona != null) { Node m = zona.GetNodeOrNull("Ocupado"); if (m != null) m.Free(); }
		Tween t = CreateTween();
		t.TweenInterval(0.8f); t.TweenProperty(tropa, "modulate:a", 0.0f, 0.6f);
		t.Finished += () => { if (IsInstanceValid(tropa)) tropa.QueueFree(); };
		CheckEstadoJuego(); ActualizarInterfaz();
	}

	private void ActualizarInterfaz() 
	{ 
		ActualizarEstadoBotones(); 
		if (HasNode("Vida1")) GetNode<Label>("Vida1").Text = $"Vida: {vidaJugador}"; 
		if (HasNode("Vida2")) GetNode<Label>("Vida2").Text = $"Vida: {vidaRival}"; 
		if (HasNode("Tiempo")) { int m = tiempoTotalPartida / 60; int s = tiempoTotalPartida % 60; GetNode<Label>("Tiempo").Text = $"Tiempo: {m}:{s:00}"; } 
		if (HasNode("LabelTurnoInfo")) 
		{ 
			var lbl = GetNode<Label>("LabelTurnoInfo"); 
			lbl.Text = $"{(esTurnoJugador ? "TU TURNO" : "TURNO RIVAL")}\nSiguiente en: {tiempoTurnoActual}s\nMovimientos: {movimientosRestantes}";
			lbl.Modulate = esTurnoJugador ? new Color(0, 0.5f, 0) : new Color(0.8f, 0, 0); 
		} 
	}

	private void ActualizarEstadoBotones() { if (btnBarajar != null) { bool b = !esTurnoJugador || usosBarajar >= MAX_BARAJAR || movimientosRestantes <= 0; btnBarajar.Modulate = b ? new Color(1, 1, 1, 0.4f) : new Color(1, 1, 1, 1); btnBarajar.Disabled = b; } if (btnSacrificio != null) { bool s = !esTurnoJugador || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500 || movimientosRestantes <= 0; btnSacrificio.Modulate = s ? new Color(1, 1, 1, 0.4f) : new Color(1, 1, 1, 1); btnSacrificio.Disabled = s; } }
	private void DeterminarGanadorPorTiempo() { if (vidaJugador > vidaRival) FinalizarPartida("¡VICTORIA!"); else if (vidaRival > vidaJugador) FinalizarPartida("¡DERROTA!"); else FinalizarPartida("¡EMPATE!"); }
	private void CheckEstadoJuego() { if (vidaJugador <= 0) FinalizarPartida("DERROTA"); else if (vidaRival <= 0) FinalizarPartida("VICTORIA"); }
	private void FinalizarPartida(string m) { if (juegoTerminado) return; juegoTerminado = true; timerReloj.Stop(); if (HasNode("PantallaFinal")) { GetNode<Control>("PantallaFinal").Visible = true; var lbl = GetNodeOrNull<Label>("PantallaFinal/LabelResultado"); if (lbl != null) lbl.Text = m; } }
	private void PrepararMazoSinRepetir() { mazoIndices.Clear(); for (int i = 0; i < imagenesCartas.Length; i++) mazoIndices.Add(i); for (int i = 0; i < mazoIndices.Count; i++) { int r = random.Next(i, mazoIndices.Count); int tmp = mazoIndices[i]; mazoIndices[i] = mazoIndices[r]; mazoIndices[r] = tmp; } proximoIndiceMazo = 0; }
	public void BarajarMazoInicial() { CrearNuevaCartaEnSpot("Spot1"); CrearNuevaCartaEnSpot("Spot2"); CrearNuevaCartaEnSpot("Spot3"); }
	private void CrearNuevaCartaEnSpot(string id) { if (juegoTerminado || escenaCartaBase == null || contenedorMano == null) return; Marker2D spot = contenedorMano.GetNodeOrNull<Marker2D>(id); if (spot != null) { Carta n = (Carta)escenaCartaBase.Instantiate(); n.NombreSpot = id; contenedorMano.AddChild(n); Vector2 esc = new Vector2(6.5f, 6.5f); n.Scale = esc; n.GlobalPosition = spot.GlobalPosition - (n.Size * esc / 2); n.GuardarEstadoOriginal(); if (proximoIndiceMazo >= mazoIndices.Count) PrepararMazoSinRepetir(); int idx = mazoIndices[proximoIndiceMazo++]; n.AsignarDatos(imagenesCartas[idx], escenasTropas[idx], idx); } }
	private void CrearEscenaDeBatalla() { Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1"); Marker2D m2 = GetNodeOrNull<Marker2D>("SpawnTrono2"); if (m1 != null && m2 != null) { tronoJugador = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoJugador); tronoJugador.GlobalPosition = m1.GlobalPosition; tronoJugador.CargarHuevo(escenaReyHuevoRef, false); tronoRival = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoRival); tronoRival.GlobalPosition = m2.GlobalPosition; tronoRival.CargarHuevo(escenaDinoHuevoRef, true); } }
}
