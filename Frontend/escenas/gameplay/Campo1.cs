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
		labelMensajes = GetNodeOrNull<Label>("LabelMensajes"); // Asegúrate de crearlo
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

	public void _on_barajar_pressed()
	{
		if (juegoTerminado) return;
		MostrarMensaje("¡Mazo Barajado!");
		GD.Print("Barajando cartas...");
	}

	private void _on_atacar_pressed()
	{
		if (juegoTerminado || !esTurnoJugador) return;
		
		vidaRival -= 500;
		MostrarMensaje("¡Ataque Crítico! -500 HP");
		
		if (vidaRival <= 0) MatarHuevo(tronoRival, "Enemigo");
		ActualizarInterfaz();
	}

	private void _on_sacrificar_pressed()
	{
		if (juegoTerminado) return;
		vidaJugador -= 200;
		MostrarMensaje("¡Sacrificio! Perdiste 200 HP");
		
		if (vidaJugador <= 0) MatarHuevo(tronoJugador, "Jugador");
		ActualizarInterfaz();
	}

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

	private void _on_timer_timeout()
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

	private void FinalizarPorTiempo()
	{
		juegoTerminado = true;
		if (vidaJugador > vidaRival) MostrarMensaje("¡TIEMPO AGOTADO! GANA JUGADOR");
		else if (vidaRival > vidaJugador) MostrarMensaje("¡TIEMPO AGOTADO! GANA RIVAL");
		else MostrarMensaje("¡EMPATE POR TIEMPO!");
	}
}
