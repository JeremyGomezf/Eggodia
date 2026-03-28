using Godot;
using System;

public partial class Campo1 : Node2D
{
	private int vidaJugador = 2000;
	private int vidaRival = 2000;
	private int tiempoRestante = 180;
	private bool esTurnoJugador = true;
	private bool juegoTerminado = false;

	private Label labelVida1;
	private Label labelVida2;
	private Label labelTiempo;

	public override void _Ready()
	{
		labelVida1 = GetNodeOrNull<Label>("Vida1");
		labelVida2 = GetNodeOrNull<Label>("Vida2");
		labelTiempo = GetNodeOrNull<Label>("Tiempo");
		ActualizarInterfaz();
	}

	private void ActualizarInterfaz()
	{
		if (juegoTerminado)
		{
			labelVida1.Text = vidaJugador <= 0 ? "DERROTA" : "¡GANASTE!";
			labelVida2.Text = vidaRival <= 0 ? "DERROTA" : "¡GANASTE!";
		}
		else
		{
			labelVida1.Text = $"VIDA: {vidaJugador}";
			labelVida2.Text = $"VIDA: {vidaRival}";
			
			// Un pequeño truco: resaltamos con un mensaje quién tiene el turno
			GD.Print(esTurnoJugador ? "--> Turno de Jeremy" : "--> Turno del Rival");
		}
	}

	// --- FUNCIÓN PARA EL NUEVO BOTÓN ---
	private void _on_pasar_turno_pressed()
	{
		if (juegoTerminado) return;

		// Simplemente cambiamos el interruptor y actualizamos
		esTurnoJugador = !esTurnoJugador;
		ActualizarInterfaz();
		GD.Print("Turno cedido voluntariamente.");
	}

	private void _on_sacrificar_pressed()
	{
		if (juegoTerminado) return;

		if (esTurnoJugador)
		{
			vidaJugador -= 200;
			if (vidaJugador < 0) vidaJugador = 0;
		}
		else
		{
			vidaRival -= 200;
			if (vidaRival < 0) vidaRival = 0;
		}

		if (vidaJugador <= 0 || vidaRival <= 0)
		{
			juegoTerminado = true;
		}

		ActualizarInterfaz();

		if (!juegoTerminado)
		{
			esTurnoJugador = !esTurnoJugador;
		}
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
	}
}
