using Godot;
using System;

public partial class Campo1 : Node2D
{
	private int vidaJugador = 2000;
	private int vidaRival = 2000;
	private int tiempoRestante = 180;
	private bool esTurnoJugador = true;
	private bool juegoTerminado = false;

	private Label labelVida1, labelVida2, labelTiempo, labelMensajes;
	private Button botonSacrificar, botonPasarTurno, botonAtacar;

	[Export] private PackedScene escenaTronoRef = GD.Load<PackedScene>("res://escenas/gameplay/tronocampo.tscn");
	[Export] private PackedScene escenaReyHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/reyhuevo1.tscn");
	[Export] private PackedScene escenaDinoHuevoRef = GD.Load<PackedScene>("res://escenas/personajes/dinohuevo1.tscn");

	private tronocampo tronoJugador, tronoRival;

	public override void _Ready()
	{
		labelVida1 = GetNodeOrNull<Label>("Vida1");
		labelVida2 = GetNodeOrNull<Label>("Vida2");
		labelTiempo = GetNodeOrNull<Label>("Tiempo");
		labelMensajes = GetNodeOrNull<Label>("LabelMensajes"); 
		botonSacrificar = GetNodeOrNull<Button>("Sacrificar");
		botonPasarTurno = GetNodeOrNull<Button>("Pasar_Turno");

		CrearEscenaDeBatalla();
		ActualizarInterfaz();
	}

	private void MostrarMensaje(string mensaje)
	{
		if (labelMensajes != null)
		{
			labelMensajes.Text = mensaje;
			// Pequeño temporizador para borrar el mensaje después de 2 segundos
			GetTree().CreateTimer(2.0).Timeout += () => labelMensajes.Text = "";
		}
	}

	// --- BOTONES Y SEÑALES (Ahora son PUBLIC para que Godot las vea) ---

	public void _on_barajar_pressed()
	{
		if (juegoTerminado) return;
		MostrarMensaje("¡Mazo Barajado!");
		GD.Print("Barajando cartas...");
	}

	public void _on_atacar_pressed()
	{
		if (juegoTerminado || !esTurnoJugador) return;
		
		vidaRival -= 500;
		MostrarMensaje("¡Ataque Crítico! -500 HP");
		
		if (vidaRival <= 0) MatarHuevo(tronoRival, "Enemigo");
		ActualizarInterfaz();
	}

	public void _on_sacrificar_pressed()
	{
		if (juegoTerminado) return;
		vidaJugador -= 200;
		MostrarMensaje("¡Sacrificio! Perdiste 200 HP");
		
		if (vidaJugador <= 0) MatarHuevo(tronoJugador, "Jugador");
		ActualizarInterfaz();
	}

	public void _on_pasar_turno_pressed()
	{
		if (juegoTerminado) return;
		
		esTurnoJugador = !esTurnoJugador; // Cambia el turno al lado contrario
		string turnoDe = esTurnoJugador ? "Tu turno" : "Turno del Rival";
		MostrarMensaje($"¡Cambio de fase! {turnoDe}");
	}

	public void _on_timer_timeout()
	{
		if (tiempoRestante > 0 && !juegoTerminado) 
		{
			tiempoRestante--;
			int minutos = tiempoRestante / 60;
			int segundos = tiempoRestante % 60;
			if (labelTiempo != null) labelTiempo.Text = $"{minutos}:{segundos:D2}";
		}
		else if (tiempoRestante <= 0 && !juegoTerminado)
		{
			FinalizarPorTiempo();
		}
	}

	// --- LÓGICA DE BATALLA Y FIN DE JUEGO ---

	private void MatarHuevo(tronocampo trono, string quien)
	{
		juegoTerminado = true;
		if (trono != null) trono.EfectoMuerteMinecraft();
		MostrarMensaje($"¡{quien} ha muerto!");
	}

	private void CrearEscenaDeBatalla()
	{
		Marker2D m1 = GetNodeOrNull<Marker2D>("SpawnTrono1");
		Marker2D m2 = GetNodeOrNull<Marker2D>("SpawnTrono2");

		if (m1 != null) {
			tronoJugador = (tronocampo)escenaTronoRef.Instantiate();
			AddChild(tronoJugador);
			tronoJugador.GlobalPosition = m1.GlobalPosition;
			tronoJugador.CargarHuevo(escenaReyHuevoRef, false);
		}
		if (m2 != null) {
			tronoRival = (tronocampo)escenaTronoRef.Instantiate();
			AddChild(tronoRival);
			tronoRival.GlobalPosition = m2.GlobalPosition;
			tronoRival.CargarHuevo(escenaDinoHuevoRef, true);
		}
	}

	private void ActualizarInterfaz()
	{
		if (labelVida1 != null) labelVida1.Text = $"vida: {vidaJugador}";
		if (labelVida2 != null) labelVida2.Text = $"vida: {vidaRival}";
	}

	private void FinalizarPorTiempo()
	{
		juegoTerminado = true;
		string resultadoBatalla = "";

		if (vidaJugador > vidaRival) 
			resultadoBatalla = "¡EL JUGADOR GANA!";
		else if (vidaRival > vidaJugador) 
			resultadoBatalla = "¡EL RIVAL GANA!";
		else 
			resultadoBatalla = "¡EMPATE ÉPICO!";

		MostrarPantallaFinal(resultadoBatalla);
	}

	private void MostrarPantallaFinal(string textoResultado)
	{
		// 1. Capa nueva por encima de todo
		CanvasLayer capaFinal = new CanvasLayer();
		AddChild(capaFinal);

		// 2. Fondo oscuro
		ColorRect fondoOscuro = new ColorRect();
		fondoOscuro.Color = new Color(0, 0, 0, 0.75f); 
		fondoOscuro.SetAnchorsPreset(Control.LayoutPreset.FullRect); 
		capaFinal.AddChild(fondoOscuro);

		// 3. Letrero gigante
		Label letreroGigante = new Label();
		letreroGigante.Text = "¡JUEGO FINALIZADO!\n\n" + textoResultado;
		letreroGigante.SetAnchorsPreset(Control.LayoutPreset.FullRect); 
		letreroGigante.HorizontalAlignment = HorizontalAlignment.Center; 
		letreroGigante.VerticalAlignment = VerticalAlignment.Center; 

		// 4. Decoración del texto
		LabelSettings decoracion = new LabelSettings();
		decoracion.FontSize = 64; 
		decoracion.FontColor = new Color(1f, 0.84f, 0f); // Dorado
		decoracion.OutlineSize = 15; 
		decoracion.OutlineColor = new Color(0, 0, 0); // Borde negro
		decoracion.ShadowSize = 10;
		decoracion.ShadowColor = new Color(0, 0, 0, 0.8f); 
		decoracion.ShadowOffset = new Vector2(6, 6); 

		letreroGigante.LabelSettings = decoracion;
		capaFinal.AddChild(letreroGigante);
	}
}
