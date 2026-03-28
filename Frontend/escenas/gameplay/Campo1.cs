using Godot;
using System;

public partial class Campo1 : Node2D
{
	private int vidaJugador = 2000;
	private int vidaRival = 2000;
	private int tiempoRestante = 180;
	private bool esTurnoJugador = true;
	private bool juegoTerminado = false;

	private Label labelVida1, labelVida2, labelTiempo;
	private Button botonSacrificar, botonPasarTurno;

	public override void _Ready()
	{
		// Referencias a los nodos
		labelVida1 = GetNodeOrNull<Label>("Vida1");
		labelVida2 = GetNodeOrNull<Label>("Vida2");
		labelTiempo = GetNodeOrNull<Label>("Tiempo");
		botonSacrificar = GetNodeOrNull<Button>("Sacrificar");
		botonPasarTurno = GetNodeOrNull<Button>("Pasar_Turno");

		ActualizarInterfaz();
	}

	private void ActualizarInterfaz()
	{
		if (juegoTerminado)
		{
			if (labelVida1 != null) labelVida1.Text = vidaJugador <= 0 ? "DERROTA" : "VICTORIA";
			if (labelVida2 != null) labelVida2.Text = vidaRival <= 0 ? "DERROTA" : "VICTORIA";
			
			// Desactivar botones al terminar
			if (botonSacrificar != null) botonSacrificar.Disabled = true;
			if (botonPasarTurno != null) botonPasarTurno.Disabled = true;
		}
		else
		{
			// Formato limpio: LP significa Life Points
			if (labelVida1 != null) labelVida1.Text = $"HP: {vidaJugador}";
			if (labelVida2 != null) labelVida2.Text = $"HP: {vidaRival}";
		}
	}

	private void _on_sacrificar_pressed()
	{
		if (juegoTerminado) return;

		// El jugador activo pierde vida (mecánica de sacrificio)
		if (esTurnoJugador) vidaJugador -= 200;
		else vidaRival -= 200;

		// Verificar si alguien perdió
		if (vidaJugador <= 0) { vidaJugador = 0; juegoTerminado = true; }
		if (vidaRival <= 0) { vidaRival = 0; juegoTerminado = true; }

		ActualizarInterfaz();
		
		// Cambio de turno automático tras la acción
		if (!juegoTerminado) esTurnoJugador = !esTurnoJugador;
	}

	private void _on_pasar_turno_pressed()
	{
		if (juegoTerminado) return;
		esTurnoJugador = !esTurnoJugador;
		ActualizarInterfaz();
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
		if (!juegoTerminado) GD.Print("Barajando mazo...");
	}
}
