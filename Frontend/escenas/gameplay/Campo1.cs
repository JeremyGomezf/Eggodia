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
		// Si el juego terminó, mostramos quién ganó y quién perdió
		if (juegoTerminado)
		{
			if (vidaJugador <= 0)
			{
				labelVida1.Text = "DERROTA";
				labelVida2.Text = "¡GANASTE!";
			}
			else if (vidaRival <= 0)
			{
				labelVida1.Text = "¡GANASTE!";
				labelVida2.Text = "DERROTA";
			}
		}
		else
		{
			// Si el juego sigue, mostramos la vida normal
			if (labelVida1 != null) labelVida1.Text = $"VIDA: {vidaJugador}";
			if (labelVida2 != null) labelVida2.Text = $"VIDA: {vidaRival}";
		}
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

		// Revisamos si tras este golpe alguien llegó a 0
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

	private void _on_barajar_pressed()
	{
		if (!juegoTerminado) GD.Print("Jeremy está barajando...");
	}
}
