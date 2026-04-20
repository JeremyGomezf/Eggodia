using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	[Export] private int vidaJugador = 2000;
	[Export] private int vidaRival = 2000;
	private int tiempoTotalPartida = 300; 
	public bool juegoTerminado = false;

	public bool esTurnoJugador = true;
	public int movimientosRestantes = 3; 
	private int tiempoTurnoActual = 20; 
	private Timer timerReloj; 

	private int usosBarajar = 0;
	private int usosSacrificio = 0;
	private const int MAX_BARAJAR = 1;
	private const int MAX_SACRIFICIO = 2;

	private Control menuAcciones;
	private Node2D tropaSeleccionada;

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
		"res://imagenes/CartasPng/PeonCart.png", "res://imagenes/CartasPng/EncebolladoCart.png"
	};

	private string[] escenasTropas = {
		"res://cartas prime/Dragon_prime.tscn", "res://cartas prime/Golem_prime.tscn",
		"res://cartas prime/Maguin_prime.tscn", "res://cartas prime/SoldadoReal_prime.tscn",
		"res://cartas prime/TRex_prime.tscn", "res://cartas prime/Tiburon_prime.tscn",
		"res://cartas prime/Peon_prime.tscn", "res://cartas prime/Encebollado_prime.tscn"
	};

	private int[] ataqueTropas = { 500, 300, 400, 350, 600, 450, 200, 250 };
	private int[] vidaMaxTropas = { 1000, 1500, 800, 900, 1200, 1000, 500, 600 };

	private tronocampo tronoJugador, tronoRival; 

	public override void _Ready()
	{
		timerReloj = new Timer();
		timerReloj.WaitTime = 1.0f;
		timerReloj.Timeout += OnTickReloj;
		AddChild(timerReloj);
		timerReloj.Start();

		menuAcciones = GetNodeOrNull<Control>("InterfazMenu/MenuAcciones");
		if (menuAcciones != null) menuAcciones.Visible = false;

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
		if (tiempoTurnoActual > 0) tiempoTurnoActual--;
		else CambiarTurno();
		ActualizarInterfaz();
	}

	private void CambiarTurno()
	{
		if (juegoTerminado) return;
		esTurnoJugador = !esTurnoJugador;
		tiempoTurnoActual = 20; 
		movimientosRestantes = 3; 

		if (esTurnoJugador)
		{
			foreach (Node nodo in GetTree().GetNodesInGroup("tropas_jugador"))
				if (nodo.HasMethod("SetActivo")) nodo.Call("SetActivo", true);
		}
		else
		{
			EjecutarTurnoIA(); 
		}
		ActualizarInterfaz();
	}

	// --- LÓGICA DE IA MEJORADA ---
	private async void EjecutarTurnoIA()
	{
		if (juegoTerminado || esTurnoJugador) return;
		await ToSignal(GetTree().CreateTimer(1.2f), "timeout");

		// 1. INVOCAR (Busca zonas_invocacion_rival en el editor)
		var zonasRival = GetTree().GetNodesInGroup("zonas_invocacion_rival");
		foreach (Node2D zona in zonasRival)
		{
			if (movimientosRestantes > 0 && zona.GetNodeOrNull("Ocupado") == null)
			{
				int idx = random.Next(escenasTropas.Length);
				InvocacionRival(zona, idx);
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
			}
		}

		// 2. ATACAR
		var misTropas = GetTree().GetNodesInGroup("tropas_rival");
		foreach (Node2D tropa in misTropas)
		{
			if (juegoTerminado || esTurnoJugador) break;
			if (movimientosRestantes > 0 && IsInstanceValid(tropa))
			{
				LogicaAtaqueBot(tropa);
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
			}
		}

		if (!esTurnoJugador && !juegoTerminado) CambiarTurno();
	}

	private void InvocacionRival(Node2D punto, int idx)
	{
		PackedScene esc = GD.Load<PackedScene>(escenasTropas[idx]);
		Node2D t = (Node2D)esc.Instantiate();
		AddChild(t);
		t.GlobalPosition = punto.GlobalPosition;
		t.Scale = new Vector2(-1, 1); 
		t.AddToGroup("tropas_rival");
		t.SetMeta("ataque", ataqueTropas[idx]);
		t.SetMeta("vida", vidaMaxTropas[idx]);

		Node marcador = new Node(); marcador.Name = "Ocupado";
		punto.AddChild(marcador);
		marcador.SetMeta("tropa_instanciada", t);
	}

	private void LogicaAtaqueBot(Node2D miTropa)
	{
		int daño = miTropa.HasMeta("ataque") ? (int)miTropa.GetMeta("ataque") : 100;
		var objetivos = GetTree().GetNodesInGroup("tropas_jugador");

		if (objetivos.Count > 0)
		{
			Node2D target = (Node2D)objetivos[0];
			RecibirDañoTropa(target, daño);
		}
		else
		{
			vidaJugador = Math.Max(0, vidaJugador - daño); // Freno para no bajar de 0
		}
		CheckEstadoJuego();
	}

	private void RecibirDañoTropa(Node2D tropa, int daño)
	{
		if (!IsInstanceValid(tropa) || !tropa.HasMeta("vida")) return;
		int hp = (int)tropa.GetMeta("vida") - daño;
		tropa.SetMeta("vida", hp);
		if (hp <= 0) EjecutarMuerteTropaSacrificada(tropa);
	}

	// --- ACCIONES JUGADOR ---
	public void _on_btn_ataque_pressed()
	{
		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada))
		{
			int atk = (int)tropaSeleccionada.GetMeta("ataque");
			var rivales = GetTree().GetNodesInGroup("tropas_rival");

			if (rivales.Count > 0) RecibirDañoTropa((Node2D)rivales[0], atk);
			else vidaRival = Math.Max(0, vidaRival - atk); // Freno vida negativa

			tropaSeleccionada.Call("EjecutarAccion", "atacar");
			menuAcciones.Visible = false;
			CheckEstadoJuego();
			RegistrarGastoMovimiento();
		}
	}

	public void _on_btn_defensa_pressed()
	{
		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada))
		{
			tropaSeleccionada.SetMeta("vida", (int)tropaSeleccionada.GetMeta("vida") + 150);
			menuAcciones.Visible = false;
			RegistrarGastoMovimiento();
		}
	}

	private void CheckEstadoJuego() 
	{ 
		if (vidaJugador <= 0) { vidaJugador = 0; FinalizarPartida("DERROTA"); }
		else if (vidaRival <= 0) { vidaRival = 0; FinalizarPartida("VICTORIA"); }
	}

	private void FinalizarPartida(string m)
	{
		if (juegoTerminado) return;
		juegoTerminado = true;
		timerReloj.Stop();
		ActualizarInterfaz();
		if (HasNode("PantallaFinal"))
		{
			GetNode<Control>("PantallaFinal").Visible = true;
			var lbl = GetNodeOrNull<Label>("PantallaFinal/LabelResultado");
			if (lbl != null) lbl.Text = m;
		}
	}

	private void ActualizarInterfaz()
	{
		if (HasNode("Vida1")) GetNode<Label>("Vida1").Text = $"Vida: {vidaJugador}";
		if (HasNode("Vida2")) GetNode<Label>("Vida2").Text = $"Vida: {vidaRival}";
		if (HasNode("Tiempo")) {
			int min = tiempoTotalPartida / 60;
			int seg = tiempoTotalPartida % 60;
			GetNode<Label>("Tiempo").Text = string.Format("Tiempo: {0}:{1:00}", min, seg);
		}
		if (HasNode("LabelTurnoInfo")) {
			var lbl = GetNode<Label>("LabelTurnoInfo");
			lbl.Text = (esTurnoJugador ? "TU TURNO" : "TURNO RIVAL") + $"\nMovimientos: {movimientosRestantes}";
			lbl.Modulate = esTurnoJugador ? Colors.Green : Colors.Red;
		}
	}

	// --- MÉTODOS DE SOPORTE ---
	private void RegistrarGastoMovimiento() {
		movimientosRestantes--;
		ActualizarInterfaz();
		if (movimientosRestantes <= 0 && !juegoTerminado) CambiarTurno();
	}

	public void TropaInvocada(Node2D puntoMod, PackedScene escenaTropa) {
		if (juegoTerminado || !esTurnoJugador || escenaTropa == null) return;
		Node2D nuevaTropa = (Node2D)escenaTropa.Instantiate();
		AddChild(nuevaTropa);
		nuevaTropa.GlobalPosition = puntoMod.GlobalPosition;
		nuevaTropa.AddToGroup("tropas_jugador");
		int idx = Array.IndexOf(escenasTropas, escenaTropa.ResourcePath);
		if (idx == -1) idx = 6;
		nuevaTropa.SetMeta("ataque", ataqueTropas[idx]);
		nuevaTropa.SetMeta("vida", vidaMaxTropas[idx]);
		Node marcador = new Node(); marcador.Name = "Ocupado";
		puntoMod.AddChild(marcador);
		marcador.SetMeta("tropa_instanciada", nuevaTropa);
		ActualizarInterfaz();
	}

	public void MostrarMenuTropa(Node2D tropa) {
		if (!esTurnoJugador || movimientosRestantes <= 0 || juegoTerminado) return;
		tropaSeleccionada = tropa;
		menuAcciones.GlobalPosition = tropa.GlobalPosition + new Vector2(-50, -100);
		menuAcciones.Visible = true;
	}

	private void PrepararMazoSinRepetir() {
		mazoIndices.Clear();
		for (int i = 0; i < imagenesCartas.Length; i++) mazoIndices.Add(i);
		for (int i = 0; i < mazoIndices.Count; i++) {
			int r = random.Next(i, mazoIndices.Count);
			int temp = mazoIndices[i]; mazoIndices[i] = mazoIndices[r]; mazoIndices[r] = temp;
		}
		proximoIndiceMazo = 0;
	}

	public void BarajarMazoInicial() {
		CrearNuevaCartaEnSpot("Spot1"); CrearNuevaCartaEnSpot("Spot2"); CrearNuevaCartaEnSpot("Spot3");
	}

	private void CrearNuevaCartaEnSpot(string idSpot) {
		if (juegoTerminado || escenaCartaBase == null) return;
		Marker2D spot = contenedorMano.GetNodeOrNull<Marker2D>(idSpot);
		if (spot != null) {
			Carta nueva = (Carta)escenaCartaBase.Instantiate();
			nueva.NombreSpot = idSpot;
			contenedorMano.AddChild(nueva);
			nueva.GlobalPosition = spot.GlobalPosition;
			if (proximoIndiceMazo >= mazoIndices.Count) PrepararMazoSinRepetir();
			int idx = mazoIndices[proximoIndiceMazo++];
			nueva.AsignarDatos(imagenesCartas[idx], escenasTropas[idx], idx);
		}
	}

	private void CrearEscenaDeBatalla() {
		Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1");
		Marker2D m2 = GetNodeOrNull<Marker2D>("SpawnTrono2");
		if (m1 != null && m2 != null) {
			tronoJugador = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoJugador);
			tronoJugador.GlobalPosition = m1.GlobalPosition; tronoJugador.CargarHuevo(escenaReyHuevoRef, false);
			tronoRival = (tronocampo)escenaTronoRef.Instantiate(); AddChild(tronoRival);
			tronoRival.GlobalPosition = m2.GlobalPosition; tronoRival.CargarHuevo(escenaDinoHuevoRef, true);
		}
	}

	private void EjecutarMuerteTropaSacrificada(Node2D tropa) {
		if (IsInstanceValid(tropa)) tropa.QueueFree();
	}

	private void DeterminarGanadorPorTiempo() {
		if (vidaJugador > vidaRival) FinalizarPartida("VICTORIA");
		else FinalizarPartida("DERROTA");
	}

	private void CancelarSacrificio() { modoSacrificioActivo = false; Input.SetCustomMouseCursor(null); }
}
