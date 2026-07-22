using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── IA ADAPTATIVA ─────────────────────────────────────────────────────
	// Ajusta la dificultad según el rendimiento del jugador
	private void AjustarDificultad()
	{
		// Si el jugador tiene mucha vida y el rival poca → subir dificultad
		float pctJugador = (float)vidaJugador / vidaMaxJugador;
		float pctRival   = (float)vidaRival   / vidaMaxJugador;

		if (pctJugador > 0.7f && pctRival < 0.4f && _dificultadIA < 2)
			_dificultadIA++;
		else if (pctJugador < 0.3f && pctRival > 0.6f && _dificultadIA > 0)
			_dificultadIA--;
	}

	private async void EjecutarTurnoIA()
	{
		if (juegoTerminado || esTurnoJugador) return;

		// Fase de apertura: la IA llena sus 3 carriles sin atacar
		if (_faseApertura)
		{
			await ToSignal(GetTree().CreateTimer(0.9f), "timeout");
			string[] spotsRival = { "ModRival1", "ModRival2", "ModRival3" };
			foreach (string nombre in spotsRival)
			{
				if (juegoTerminado) return;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaIA());
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(0.65f), "timeout");
			}
			_faseApertura = false;
			if (!juegoTerminado) CambiarTurno();
			return;
		}

		AjustarDificultad();

		float delay = _dificultadIA == 0 ? 1.8f : _dificultadIA == 1 ? 1.2f : 0.85f;
		await ToSignal(GetTree().CreateTimer(delay * 0.4f), "timeout");
		if (juegoTerminado) return;

		// Evaluar estado del campo
		int tropasEnCampo = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (IsInstanceValid(n as Node2D)) tropasEnCampo++;

		float ratioHP      = (float)vidaRival / Mathf.Max(1, vidaMaxJugador);
		bool atacarPrimero = tropasEnCampo >= 2 && ratioHP > 0.35f;
		string[] puntos    = { "ModRival1", "ModRival2", "ModRival3" };

		if (atacarPrimero)
		{
			// Con tropas en campo: atacar primero (gasta energía)
			var bots = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);
			if (_dificultadIA == 2)
				bots.Sort((a, b) => Gi(b, "puntosAtaque").CompareTo(Gi(a, "puntosAtaque")));

			foreach (Node2D tropa in bots)
			{
				if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
				if (EstaBlockeada(tropa)) continue;
				Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
				if (!HabilidadUsada(tropa) && _dificultadIA >= 1 && random.Next(3) == 0)
					tropa.Call("EjecutarAccion", "usar_habilidad");
				else if (DebeDefender(tropa, objetivo))
					tropa.Call("EjecutarAccion", "preparar_defensa");
				else
					ProcesarCombateFrontal(tropa, "tropas_jugador");
				if (_dificultadIA == 2 && random.Next(5) == 0) IAUsarHechizo();
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}
			// Reforzar zonas vacías GRATIS (invocar no gasta energía)
			int maxNuevos = _dificultadIA + 1;
			int reforzadas = 0;
			foreach (string nombre in puntos)
			{
				if (reforzadas >= maxNuevos || juegoTerminado) break;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaIA());
				reforzadas++;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay * 0.7f), "timeout");
				if (juegoTerminado) return;
			}
		}
		else
		{
			// Sin tropas suficientes: invocar primero GRATIS, luego atacar (gasta energía)
			int aInvocar = _dificultadIA == 0 ? 1 : _dificultadIA == 1 ? 2 : 3;
			int invocadas = 0;
			foreach (string nombre in puntos)
			{
				if (invocadas >= aInvocar || juegoTerminado) break;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaIA());
				invocadas++;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}

			// Atacar con todas las tropas (incluyendo las recién invocadas)
			var bots = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);
			if (_dificultadIA == 2)
				bots.Sort((a, b) => Gi(b, "puntosAtaque").CompareTo(Gi(a, "puntosAtaque")));

			foreach (Node2D tropa in bots)
			{
				if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
				if (EstaBlockeada(tropa)) continue;
				Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
				if (!HabilidadUsada(tropa) && _dificultadIA >= 1 && random.Next(3) == 0)
					tropa.Call("EjecutarAccion", "usar_habilidad");
				else if (DebeDefender(tropa, objetivo))
					tropa.Call("EjecutarAccion", "preparar_defensa");
				else
					ProcesarCombateFrontal(tropa, "tropas_jugador");
				if (_dificultadIA == 2 && random.Next(5) == 0) IAUsarHechizo();
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}
		}

		if (!esTurnoJugador && !juegoTerminado) CambiarTurno();
	}

	private PackedScene ElegirTropaIA()
	{
		if (_dificultadIA == 2) return GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length / 2, escenasTropas.Length)]);
		if (_dificultadIA == 0) return GD.Load<PackedScene>(escenasTropas[random.Next(0, escenasTropas.Length / 2)]);
		return GD.Load<PackedScene>(escenasTropas[random.Next(escenasTropas.Length)]);
	}

	private bool DebeDefender(Node2D t, Node2D obj)
	{
		int vida = Gi(t,"vidaActual"), esc = Gi(t,"escudoActual");
		// Difícil defiende más inteligentemente
		float umbralVida = _dificultadIA == 2 ? 0.4f : 0.25f;
		if (obj != null) return ((float)vida / Mathf.Max(Gi(t,"vidaMaxima"),1) < umbralVida && esc > 50);
		return vida < 100 && esc > 0;
	}
}
