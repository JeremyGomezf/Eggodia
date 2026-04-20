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
			// --- LO NUEVO: Completar cartas faltantes al iniciar turno ---
			CompletarManoAlInicio();

			foreach (Node nodo in GetTree().GetNodesInGroup("tropas_jugador"))
				if (nodo.HasMethod("SetActivo")) nodo.Call("SetActivo", true);
		}
		else
		{
			EjecutarTurnoIA(); 
		}
		ActualizarInterfaz();
	}

<<<<<<< HEAD
	// --- LÓGICA DE IA MEJORADA ---
	private async void EjecutarTurnoIA()
=======
	// Esta función revisa qué spots están vacíos y crea cartas solo ahí
	private void CompletarManoAlInicio()
	{
		string[] spots = { "Spot1", "Spot2", "Spot3" };
		foreach (string s in spots)
		{
			bool ocupado = false;
			foreach (Node n in contenedorMano.GetChildren())
			{
				if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion())
				{
					ocupado = true;
					break;
				}
			}

			if (!ocupado)
			{
				CrearNuevaCartaEnSpot(s);
			}
		}
	}

	private void RegistrarGastoMovimiento()
>>>>>>> 3b15d4b18a856a0faee6425c9cc8f137accdd03a
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

<<<<<<< HEAD
=======
	public void _on_barajar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || juegoTerminado || usosBarajar >= MAX_BARAJAR) return;
		
		usosBarajar++;
		EjecutarBarajadoLogico();
		RegistrarGastoMovimiento();
	}

	private void EjecutarBarajadoLogico()
	{
		foreach (Node n in contenedorMano.GetChildren()) 
		{
			if (n is Carta c) { c.NombreSpot = "X"; c.QueueFree(); }
		}
		GetTree().CreateTimer(0.1f).Timeout += () => { 
			PrepararMazoSinRepetir(); 
			BarajarMazoInicial(); 
		};
	}

	public void _on_sacrificar_pressed()
	{
		if (!esTurnoJugador || movimientosRestantes <= 0 || juegoTerminado || usosSacrificio >= MAX_SACRIFICIO || vidaJugador <= 500) 
		{
			if (modoSacrificioActivo) CancelarSacrificio();
			return;
		}

		modoSacrificioActivo = !modoSacrificioActivo;
		if (modoSacrificioActivo && iconoCursorSacrificio != null)
			Input.SetCustomMouseCursor(iconoCursorSacrificio, Input.CursorShape.Arrow, new Vector2(16, 16));
		else
			Input.SetCustomMouseCursor(null);
	}

	public override void _Input(InputEvent @event)
	{
		if (juegoTerminado || !esTurnoJugador) return;
		if (modoSacrificioActivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			VerificarSacrificioEnCampo(GetGlobalMousePosition());
	}

	private void VerificarSacrificioEnCampo(Vector2 posClick)
	{
		foreach (Node2D punto in GetTree().GetNodesInGroup("zonas_invocacion"))
		{
			if (punto.GlobalPosition.DistanceTo(posClick) < 110f)
			{
				Node marcador = punto.GetNodeOrNull("Ocupado");
				if (marcador != null)
				{
					vidaJugador -= 500;
					usosSacrificio++; 
					if (marcador.HasMeta("tropa_instanciada"))
					{
						Node2D tropa = (Node2D)marcador.GetMeta("tropa_instanciada");
						if (IsInstanceValid(tropa)) EjecutarMuerteTropaSacrificada(tropa);
					}
					marcador.QueueFree();
					CancelarSacrificio();
					RegistrarGastoMovimiento();
					CheckEstadoJuego();
					break;
				}
			}
		}
	}

	private void CancelarSacrificio() { modoSacrificioActivo = false; Input.SetCustomMouseCursor(null); }

	public void TropaInvocada(Node2D puntoMod, PackedScene escenaTropa)
	{
		if (juegoTerminado || !esTurnoJugador) return; 
		if (escenaTropa != null)
		{
			Node2D nuevaTropa = (Node2D)escenaTropa.Instantiate();
			AddChild(nuevaTropa);
			nuevaTropa.GlobalPosition = puntoMod.GlobalPosition;
			nuevaTropa.AddToGroup("tropas_jugador");
			Node marcador = new Node(); marcador.Name = "Ocupado";
			puntoMod.AddChild(marcador);
			marcador.SetMeta("tropa_instanciada", nuevaTropa); 
			
			// Si te quedas sin cartas, baraja automático tras 1 seg
			GetTree().CreateTimer(0.1f).Timeout += () => {
				int cartasEnMano = 0;
				foreach (Node n in contenedorMano.GetChildren())
					if (n is Carta c && !c.IsQueuedForDeletion() && c.NombreSpot != "X") cartasEnMano++;

				if (cartasEnMano == 0)
					GetTree().CreateTimer(1.0f).Timeout += () => { if (!juegoTerminado) EjecutarBarajadoLogico(); };
			};
		}
	}

	private void ActualizarInterfaz()
	{
		if (HasNode("Vida1")) GetNode<Label>("Vida1").Text = $"Vida: {vidaJugador}";
		if (HasNode("Vida2")) GetNode<Label>("Vida2").Text = $"Vida: {vidaRival}";
		
		if (HasNode("Tiempo")) 
		{
			int min = tiempoTotalPartida / 60;
			int seg = tiempoTotalPartida % 60;
			GetNode<Label>("Tiempo").Text = string.Format("Tiempo: {0}:{1:00}", min, seg);
		}

		var btnBarajar = GetNodeOrNull<Button>("Barajar"); 
		var btnSacrificar = GetNodeOrNull<Button>("Sacrificar");

		Color activo = new Color(1, 1, 1, 1);
		Color bloqueado = new Color(0.3f, 0.3f, 0.3f, 0.5f); 

		if (btnBarajar != null)
		{
			bool puede = esTurnoJugador && movimientosRestantes > 0 && usosBarajar < MAX_BARAJAR;
			btnBarajar.Disabled = !puede;
			btnBarajar.Modulate = puede ? activo : bloqueado;
			btnBarajar.MouseFilter = puede ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		}

		if (btnSacrificar != null)
		{
			bool puede = esTurnoJugador && movimientosRestantes > 0 && usosSacrificio < MAX_SACRIFICIO && vidaJugador > 500;
			btnSacrificar.Disabled = !puede;
			btnSacrificar.Modulate = puede ? activo : bloqueado;
			btnSacrificar.MouseFilter = puede ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		}

		if (HasNode("LabelTurnoInfo"))
		{
			var lbl = GetNode<Label>("LabelTurnoInfo");
			string txt = esTurnoJugador ? "TU TURNO" : "TURNO RIVAL";
			lbl.Text = $"{txt}\nSiguiente en: {tiempoTurnoActual}s\nMovimientos: {movimientosRestantes}";
			lbl.Modulate = esTurnoJugador ? Color.Color8(34, 139, 34) : Color.Color8(178, 34, 34);
		}
	}

	private void EjecutarMuerteTropaSacrificada(Node2D tropa)
	{
		if (tropa.HasMethod("ReproducirDerrota")) tropa.Call("ReproducirDerrota");
		var animSprite = tropa.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		float duracion = 1.0f; float t3f = 0.15f; 
		if (animSprite != null && animSprite.SpriteFrames.HasAnimation("derrota"))
		{
			animSprite.SpriteFrames.SetAnimationLoop("derrota", false);
			float spd = (float)animSprite.SpriteFrames.GetAnimationSpeed("derrota");
			duracion = (float)animSprite.SpriteFrames.GetFrameCount("derrota") / spd;
			t3f = 3.0f / spd; 
		}
		Tween t = CreateTween();
		t.TweenInterval(Mathf.Max(0f, duracion - t3f));
		t.TweenProperty(tropa, "modulate:a", 0.0f, t3f);
		t.Finished += () => { if (IsInstanceValid(tropa)) tropa.QueueFree(); };
	}

	private void DeterminarGanadorPorTiempo()
	{
		if (vidaJugador > vidaRival) FinalizarPartida("¡VICTORIA POR SALUD!");
		else if (vidaRival > vidaJugador) FinalizarPartida("¡DERROTA POR SALUD!");
		else FinalizarPartida("¡EMPATE!");
	}

>>>>>>> 3b15d4b18a856a0faee6425c9cc8f137accdd03a
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

<<<<<<< HEAD
	public void BarajarMazoInicial() {
		CrearNuevaCartaEnSpot("Spot1"); CrearNuevaCartaEnSpot("Spot2"); CrearNuevaCartaEnSpot("Spot3");
=======
	public void BarajarMazoInicial() 
	{ 
		CompletarManoAlInicio();
>>>>>>> 3b15d4b18a856a0faee6425c9cc8f137accdd03a
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
