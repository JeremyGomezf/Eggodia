using Godot;
using System;

public partial class Campo1 : Node2D
{
	private int vidaJugador = 2000;
	private int vidaRival = 2000;
	private bool juegoTerminado = false;
	private int tiempoRestante = 90; 

	private Label labelVida1, labelVida2, labelTiempo;
	private Control contenedorMano; 
	private Timer timerTurno;

	[Export] private Control panelFinal; 
	[Export] private Label labelResultado; 
	[Export] private PackedScene escenaCartaBase; 
	
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

	private string[] escenasTropas = {
		"res://escenas/personajes/dragonfuego1.tscn",
		"res://escenas/personajes/golem1.tscn",
		"res://escenas/personajes/majin1.tscn",
		"res://escenas/personajes/soldadoreal1.tscn",
		"res://escenas/personajes/trex1.tscn", 
		"res://escenas/personajes/tiburon1.tscn" 
	};

	private tronocampo tronoJugador, tronoRival; 
	private Random random = new Random();

	public override void _Ready()
	{
		labelVida1 = GetNodeOrNull<Label>("Vida1");
		labelVida2 = GetNodeOrNull<Label>("Vida2");
		labelTiempo = GetNodeOrNull<Label>("Tiempo");
		contenedorMano = GetNodeOrNull<Control>("ManoManual"); 
		timerTurno = GetNodeOrNull<Timer>("Timer");

		if (panelFinal != null) panelFinal.Visible = false;

		CrearEscenaDeBatalla();
		BarajarMazoInicial(); 
		ActualizarInterfaz();
		
		if (timerTurno != null)
		{
			timerTurno.WaitTime = 1.0f;
			timerTurno.Start();
		}
	}

	public void _on_timer_timeout()
	{
		if (juegoTerminado) return;
		tiempoRestante--;
		ActualizarEtiquetaTiempo();
		if (tiempoRestante <= 0) DeterminarGanadorPorTiempo();
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

	public void AplicarDaño(int daño)
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
			panelFinal.Visible = true; 
			if (labelResultado != null) labelResultado.Text = mensaje;
		}
		if (vidaRival <= 0 && tronoRival != null) MatarHuevo(tronoRival);
		if (vidaJugador <= 0 && tronoJugador != null) MatarHuevo(tronoJugador);
	}

	public void _on_barajar_pressed()
	{
		if (juegoTerminado || contenedorMano == null) return;
		foreach (Node hijo in contenedorMano.GetChildren()) 
		{
			if (hijo is Carta) hijo.QueueFree();
		}
		GetTree().Connect("process_frame", Callable.From(() => { BarajarMazoInicial(); }), (uint)ConnectFlags.OneShot);
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

	private void BarajarMazoInicial()
	{
		for (int i = 0; i < 3; i++) { CrearNuevaCarta(); }
	}

	// --- LÓGICA ANTI-BLOQUEO DE ASIENTOS ---
	private void CrearNuevaCarta()
	{
		if (escenaCartaBase == null || contenedorMano == null) return;

		Marker2D spotDestino = null;
		string nombreSpotLibre = "";

		// Buscamos cuál de los 3 spots está realmente vacío
		for (int i = 1; i <= 3; i++)
		{
			string nombreBusqueda = "Spot" + i;
			bool ocupado = false;

			foreach (Node hijo in contenedorMano.GetChildren())
			{
				if (hijo is Carta c && !c.IsQueuedForDeletion() && c.EstaEnMano && c.NombreSpot == nombreBusqueda)
				{
					ocupado = true;
					break;
				}
			}

			if (!ocupado)
			{
				spotDestino = contenedorMano.GetNodeOrNull<Marker2D>(nombreBusqueda);
				nombreSpotLibre = nombreBusqueda;
				break; 
			}
		}

		if (spotDestino == null) return; 

		Carta nuevaCartaNode = (Carta)escenaCartaBase.Instantiate();
		nuevaCartaNode.NombreSpot = nombreSpotLibre; 
		contenedorMano.AddChild(nuevaCartaNode);

		Callable.From(() => {
			if (nuevaCartaNode != null && spotDestino != null)
			{
				Vector2 ajusteCentro = new Vector2(nuevaCartaNode.Size.X / 2, nuevaCartaNode.Size.Y / 2);
				nuevaCartaNode.GlobalPosition = spotDestino.GlobalPosition - ajusteCentro;
				nuevaCartaNode.Rotation = spotDestino.GlobalRotation;
				
				// La carta del centro (Spot2) se queda visualmente al fondo
				if (nombreSpotLibre == "Spot2") { nuevaCartaNode.ZIndex = 0; } 
				else { nuevaCartaNode.ZIndex = 1; }

				nuevaCartaNode.GuardarEstadoOriginal();
			}
		}).CallDeferred();

		int indiceAleatorio = random.Next(imagenesCartas.Length);
		
		string rutaImagen = imagenesCartas[indiceAleatorio];
		if (FileAccess.FileExists(rutaImagen))
		{
			TextureRect display = nuevaCartaNode.GetNodeOrNull<TextureRect>("foto");
			if (display != null) display.Texture = GD.Load<Texture2D>(rutaImagen);
		}

		string rutaTropa = escenasTropas[indiceAleatorio];
		if (FileAccess.FileExists(rutaTropa))
		{
			nuevaCartaNode.EscenaTropa = GD.Load<PackedScene>(rutaTropa);
		}

		nuevaCartaNode.GuiInput += (ev) => {
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				AnimarYAtacar(nuevaCartaNode);
			}
		};
	}

	private void AnimarYAtacar(Carta nodoCarta)
	{
		if (juegoTerminado) return;
		
		// SOLUCIÓN AL BLOQUEO DE CLICS:
		nodoCarta.EstaEnMano = false; 
		nodoCarta.MouseFilter = Control.MouseFilterEnum.Ignore; 

		Tween tween = CreateTween().SetParallel(true);
		Vector2 centro = GetViewportRect().Size / 2;

		tween.TweenProperty(nodoCarta, "global_position", centro - (nodoCarta.Size / 2), 0.4f);
		tween.TweenProperty(nodoCarta, "rotation", 0f, 0.4f); 
		tween.TweenProperty(nodoCarta, "modulate:a", 0.0f, 0.2f).SetDelay(0.2f);
		
		tween.Finished += () => {
			if (nodoCarta.EscenaTropa != null)
			{
				Node2D nuevaTropa = (Node2D)nodoCarta.EscenaTropa.Instantiate();
				AddChild(nuevaTropa);

				Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1");
				if (m1 != null) {
					// Dispersión en la zona roja
					int distAleaX = random.Next(100, 300);
					int distAleaY = random.Next(-130, 130);
					nuevaTropa.GlobalPosition = new Vector2(m1.GlobalPosition.X + distAleaX, m1.GlobalPosition.Y + distAleaY);
				}
			}
			nodoCarta.QueueFree();
			// Pequeña espera para asegurar que el asiento se libere en el siguiente frame
			GetTree().CreateTimer(0.1f).Timeout += () => { if (!juegoTerminado) CrearNuevaCarta(); };
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
