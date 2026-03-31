using Godot;
using System;

public partial class Campo1 : Node2D
{
	// --- Variables de Estado ---
	private int vidaJugador = 2000;
	private int vidaRival = 2000;
	private bool juegoTerminado = false;
	private int tiempoRestante = 10; // el tiempo que quieran

	// --- Referencias a Nodos (UI) ---
	private Label labelVida1, labelVida2, labelTiempo;
	private HBoxContainer contenedorMano;
	private Timer timerTurno;

	// --- Variables Exportadas (Asignar en el Inspector) ---
	[Export] private Control panelFinal; // El ColorRect "PantallaFinal"
	[Export] private Label labelResultado; // El Label "MensajeResultado"
	[Export] private PackedScene escenaCartaBase; // Tu archivo carta_base.tscn
	
	[Export] private PackedScene escenaTronoRef = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private string[] imagenesCartas = {
		"res://imagenes/CartasPng/dragonfuego_carta.png",
		"res://imagenes/CartasPng/golem_carta.png",
		"res://imagenes/CartasPng/majin_carta.png",
		"res://imagenes/CartasPng/soldadoreal_carta.png",
		"res://imagenes/CartasPng/t-rex_carta.png",
        "res://imagenes/CartasPng/tiburon_carta.png"
	};

	private tronocampo tronoJugador, tronoRival; 
	private Random random = new Random();

	public override void _Ready()
	{
		// Vinculación de nodos por nombre
		labelVida1 = GetNodeOrNull<Label>("Vida1");
		labelVida2 = GetNodeOrNull<Label>("Vida2");
		labelTiempo = GetNodeOrNull<Label>("Tiempo");
		contenedorMano = GetNodeOrNull<HBoxContainer>("ManoJugador");
		timerTurno = GetNodeOrNull<Timer>("Timer");

		// Ocultar pantalla final al empezar
		if (panelFinal != null) panelFinal.Visible = false;

		CrearEscenaDeBatalla();
		BarajarMazoInicial(); 
		ActualizarInterfaz();
		
		// Configurar e iniciar el reloj si existe
		if (timerTurno != null)
		{
			timerTurno.WaitTime = 1.0f;
			timerTurno.OneShot = false;
			timerTurno.Start();
		}
	}

	// --- Lógica del Reloj ---
	public void _on_timer_timeout()
	{
		if (juegoTerminado) return;

		tiempoRestante--;
		ActualizarEtiquetaTiempo();

		if (tiempoRestante <= 0)
		{
			DeterminarGanadorPorTiempo();
		}
	}

	private void ActualizarEtiquetaTiempo()
	{
		if (labelTiempo != null)
		{
			int minutos = tiempoRestante / 60;
			int segundos = tiempoRestante % 60;
			labelTiempo.Text = string.Format("Tiempo: {0}:{1:00}", minutos, segundos);
		}
	}

	private void DeterminarGanadorPorTiempo()
	{
		if (vidaJugador > vidaRival) FinalizarPartida("¡GANASTE POR SALUD!");
		else if (vidaRival > vidaJugador) FinalizarPartida("¡PERDISTE POR SALUD!");
		else FinalizarPartida("¡EMPATE POR TIEMPO!");
	}

	// --- Combate y Reglas ---
	private void AplicarDaño(int daño)
	{
		if (juegoTerminado) return;

		vidaRival -= daño;
		if (vidaRival < 0) vidaRival = 0;

		ActualizarInterfaz();
		CheckEstadoJuego();
	}

	private void CheckEstadoJuego()
	{
		if (vidaRival <= 0 && vidaJugador <= 0) FinalizarPartida("¡EMPATE TOTAL!");
		else if (vidaRival <= 0) FinalizarPartida("¡VICTORIA!");
		else if (vidaJugador <= 0) FinalizarPartida("¡HAS PERDIDO!");
	}

	private void FinalizarPartida(string mensaje)
	{
		juegoTerminado = true;
		if (timerTurno != null) timerTurno.Stop();
		
		if (panelFinal != null)
		{
			panelFinal.Visible = true; // Muestra el ColorRect
			if (labelResultado != null) labelResultado.Text = mensaje;
		}

		if (vidaRival <= 0 && tronoRival != null) MatarHuevo(tronoRival);
		if (vidaJugador <= 0 && tronoJugador != null) MatarHuevo(tronoJugador);
	}

	// --- Botones de Interfaz ---
	public void _on_barajar_pressed()
	{
		if (juegoTerminado || contenedorMano == null) return;
		
		foreach (Node hijo in contenedorMano.GetChildren()) hijo.QueueFree();
		
		GetTree().Connect("process_frame", Callable.From(() => {
			BarajarMazoInicial();
		}), (uint)ConnectFlags.OneShot);
	}

	public void _on_sacrificar_pressed()
	{
		if (juegoTerminado) return;
		vidaJugador -= 500; 
		if (vidaJugador < 0) vidaJugador = 0;
		ActualizarInterfaz();
		_on_barajar_pressed();
		CheckEstadoJuego();
	}

	public void _on_boton_reiniciar_pressed()
	{
		GetTree().ReloadCurrentScene();
	}

	// --- Gestión de Cartas ---
	private void BarajarMazoInicial()
	{
		for (int i = 0; i < 3; i++) { CrearNuevaCarta(); }
	}

	private void CrearNuevaCarta()
	{
		if (escenaCartaBase == null || contenedorMano == null)
		{
			GD.PrintErr("ERROR: No hay CartaBase asignada en el Inspector");
			return;
		}

		Control nuevaCarta = (Control)escenaCartaBase.Instantiate();
		contenedorMano.AddChild(nuevaCarta);

		string rutaAleatoria = imagenesCartas[random.Next(imagenesCartas.Length)];
		if (FileAccess.FileExists(rutaAleatoria))
		{
			TextureRect display = nuevaCarta.GetNodeOrNull<TextureRect>("foto");
			if (display != null) display.Texture = GD.Load<Texture2D>(rutaAleatoria);
		}

		int dañoAleatorio = random.Next(300, 701);
		nuevaCarta.GuiInput += (ev) => {
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				AnimarYAtacar(nuevaCarta, dañoAleatorio);
			}
		};
	}

	private void AnimarYAtacar(Control nodoCarta, int daño)
	{
		if (juegoTerminado) return;
		Tween tween = CreateTween();
		Vector2 centro = GetViewportRect().Size / 2;

		tween.TweenProperty(nodoCarta, "global_position", centro - (nodoCarta.Size / 2), 0.4f);
		tween.TweenProperty(nodoCarta, "modulate:a", 0.0f, 0.2f);
		
		tween.Finished += () => {
			AplicarDaño(daño);
			nodoCarta.QueueFree();
			if (!juegoTerminado) CrearNuevaCarta();
		};
	}

	private void ActualizarInterfaz()
	{
		if (labelVida1 != null) labelVida1.Text = $"Vida: {vidaJugador}";
		if (labelVida2 != null) labelVida2.Text = $"Vida: {vidaRival}";
	}

	private void CrearEscenaDeBatalla()
	{
		Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1");
		Marker2D m2 = GetNodeOrNull<Marker2D>("SpawnTrono2");

		if (m1 != null && m2 != null) 
		{
			tronoJugador = (tronocampo)escenaTronoRef.Instantiate();
			AddChild(tronoJugador);
			tronoJugador.GlobalPosition = m1.GlobalPosition;
			tronoJugador.CargarHuevo(escenaReyHuevoRef, false);

			tronoRival = (tronocampo)escenaTronoRef.Instantiate();
			AddChild(tronoRival);
			tronoRival.GlobalPosition = m2.GlobalPosition;
			tronoRival.CargarHuevo(escenaDinoHuevoRef, true);
		}
	}

	private void MatarHuevo(tronocampo trono)
	{
		if (trono != null) trono.EfectoMuerteMinecraft();
	}
}
