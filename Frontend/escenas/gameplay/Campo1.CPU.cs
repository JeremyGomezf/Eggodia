using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ── CPU ADAPTATIVA ────────────────────────────────────────────────────
	// Ajusta la dificultad según el rendimiento del jugador
	private void AjustarDificultad()
	{
		float pctJugador = (float)vidaJugador / vidaMaxJugador;
		float pctRival   = (float)vidaRival   / vidaMaxJugador;

		if (pctJugador > 0.7f && pctRival < 0.4f && _dificultadCPU < 2)
			_dificultadCPU++;
		else if (pctJugador < 0.3f && pctRival > 0.6f && _dificultadCPU > 0)
			_dificultadCPU--;
	}

	private async void EjecutarTurnoCPU()
	{
		if (juegoTerminado || esTurnoJugador) return;

		// En partida en línea no hay CPU: el turno del rival lo maneja el jugador remoto.
		if (EsOnline) { EsperarRivalOnline(); return; }

		// La intro cinemática bloquea la partida: el bot espera a que termine.
		while (IntroEnCurso && !juegoTerminado) await ToSignal(GetTree().CreateTimer(0.2f), "timeout");
		if (juegoTerminado) return;

		// Fase de apertura: la CPU llena sus 3 carriles sin atacar
		if (_faseApertura)
		{
			await ToSignal(GetTree().CreateTimer(0.9f), "timeout");
			string[] spotsRival = { "ModRival1", "ModRival2", "ModRival3" };
			foreach (string nombre in spotsRival)
			{
				if (juegoTerminado) return;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaCPU());
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(0.65f), "timeout");
			}
			_faseApertura = false;
			if (!juegoTerminado) CambiarTurno();
			return;
		}

		AjustarDificultad();

		// Turno propio de cada tropa rival: se adelanta antes de cualquier invocación/ataque
		// de este turno, para que una tropa recién invocada hoy no se cuente a sí misma.
		// Sin aviso al jugador aquí — el asistente táctico solo notifica sobre tropas PROPIAS.
		// La ronda de invocación inicial (armar el campo) no cuenta — se saltea una sola vez, igual
		// que del lado jugador (ver Campo1.Turnos.cs).
		if (!_primerAvanceRivalPendiente)
		{
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			{
				if (!(n is Node2D tropaRival) || !IsInstanceValid(tropaRival)) continue;
				if (tropaRival.HasMethod("AvanzarTurnoTropa")) tropaRival.Call("AvanzarTurnoTropa");
			}
		}
		_primerAvanceRivalPendiente = false;

		// Cadencia orgánica entre acciones de la IA (0.5s-1.0s): más lenta en fácil, más ágil
		// en difícil, pero siempre dentro del rango legible para el jugador.
		float delay = _dificultadCPU == 0 ? 1.0f : _dificultadCPU == 1 ? 0.75f : 0.5f;
		await EsperarTableroLibre();
		await ToSignal(GetTree().CreateTimer(delay * 0.4f), "timeout");
		if (juegoTerminado) return;

		// Ardid Nuclear: si le conviene, la tira al empezar su turno. El contador de 10s no lo frena:
		// sigue jugando con la energía que le queda (antes de cada acción ya espera al tablero libre,
		// así que no ataca mientras la bomba cae o la pantalla está blanca).
		CPUIntentarNuclear();

		int tropasEnCampo = 0;
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (IsInstanceValid(n as Node2D)) tropasEnCampo++;

		float ratioHP      = (float)vidaRival / Mathf.Max(1, vidaMaxJugador);
		bool atacarPrimero = tropasEnCampo >= 2 && ratioHP > 0.35f;
		string[] puntos    = { "ModRival1", "ModRival2", "ModRival3" };

		if (atacarPrimero)
		{
			var bots = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);
			if (_dificultadCPU == 2)
				bots.Sort((a, b) =>
				{
					int p = PrioridadObjetivo(b).CompareTo(PrioridadObjetivo(a));
					return p != 0 ? p : Gi(b, "puntosAtaque").CompareTo(Gi(a, "puntosAtaque"));
				});

			foreach (Node2D tropa in bots)
			{
				if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
				if (EstaBlockeada(tropa)) continue;
				// Sincronización: no emitir la siguiente acción hasta que el tablero esté
				// 100% libre de animaciones/efectos pendientes (muros, proyectiles, Enroque...).
				await EsperarTableroLibre();
				if (juegoTerminado || !IsInstanceValid(tropa)) return;
				Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
				// Si el carril del jugador está vacío no puede atacar (misma regla que el jugador).
				bool puedeAtacar = objetivo != null;
				bool intentaHabilidad = !HabilidadUsada(tropa) && !HabilidadBloqueadaTurno(tropa) && _dificultadCPU >= 1 && random.Next(3) == 0;
				if (intentaHabilidad && tropa is TorrePrime torreIA)
				{
					if (!await IntentarEnroqueIA(torreIA) && puedeAtacar) ProcesarCombateFrontal(tropa, "tropas_jugador");
				}
				else if (intentaHabilidad)
					tropa.Call("EjecutarAccion", "usar_habilidad");
				else if (DebeDefender(tropa, objetivo))
					tropa.Call("EjecutarAccion", "preparar_defensa");
				else if (puedeAtacar)
					ProcesarCombateFrontal(tropa, "tropas_jugador");
				else
					continue; // nada que hacer con esta tropa: no gasta energía
				if (_dificultadCPU == 2 && random.Next(5) == 0) CPUUsarHechizo();
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}
			int maxNuevos = _dificultadCPU + 1;
			int reforzadas = 0;
			foreach (string nombre in puntos)
			{
				if (reforzadas >= maxNuevos || juegoTerminado) break;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaCPU());
				reforzadas++;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay * 0.7f), "timeout");
				if (juegoTerminado) return;
			}
		}
		else
		{
			int aInvocar = _dificultadCPU == 0 ? 1 : _dificultadCPU == 1 ? 2 : 3;
			int invocadas = 0;
			foreach (string nombre in puntos)
			{
				if (invocadas >= aInvocar || juegoTerminado) break;
				Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
				if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
				InvocacionRival(zona, ElegirTropaCPU());
				invocadas++;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}

			var bots = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D n2 && IsInstanceValid(n2)) bots.Add(n2);
			if (_dificultadCPU == 2)
				bots.Sort((a, b) =>
				{
					int p = PrioridadObjetivo(b).CompareTo(PrioridadObjetivo(a));
					return p != 0 ? p : Gi(b, "puntosAtaque").CompareTo(Gi(a, "puntosAtaque"));
				});

			foreach (Node2D tropa in bots)
			{
				if (movimientosRestantes <= 0 || juegoTerminado || !IsInstanceValid(tropa)) break;
				if (EstaBlockeada(tropa)) continue;
				// Sincronización: no emitir la siguiente acción hasta que el tablero esté
				// 100% libre de animaciones/efectos pendientes (muros, proyectiles, Enroque...).
				await EsperarTableroLibre();
				if (juegoTerminado || !IsInstanceValid(tropa)) return;
				Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
				// Si el carril del jugador está vacío no puede atacar (misma regla que el jugador).
				bool puedeAtacar = objetivo != null;
				bool intentaHabilidad = !HabilidadUsada(tropa) && !HabilidadBloqueadaTurno(tropa) && _dificultadCPU >= 1 && random.Next(3) == 0;
				if (intentaHabilidad && tropa is TorrePrime torreIA)
				{
					if (!await IntentarEnroqueIA(torreIA) && puedeAtacar) ProcesarCombateFrontal(tropa, "tropas_jugador");
				}
				else if (intentaHabilidad)
					tropa.Call("EjecutarAccion", "usar_habilidad");
				else if (DebeDefender(tropa, objetivo))
					tropa.Call("EjecutarAccion", "preparar_defensa");
				else if (puedeAtacar)
					ProcesarCombateFrontal(tropa, "tropas_jugador");
				else
					continue; // nada que hacer con esta tropa: no gasta energía
				if (_dificultadCPU == 2 && random.Next(5) == 0) CPUUsarHechizo();
				movimientosRestantes--;
				ActualizarInterfaz();
				await ToSignal(GetTree().CreateTimer(delay), "timeout");
				if (juegoTerminado) return;
			}
		}

		// Prioridad 1: nunca terminar el turno con un carril propio vacío (invocación gratis,
		// evita el castigo de -150 HP al Huevo rival).
		foreach (string nombre in puntos)
		{
			if (juegoTerminado) break;
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona == null || zona.GetNodeOrNull("Ocupado") != null) continue;
			InvocacionRival(zona, ElegirTropaCPU());
			ActualizarInterfaz();
			await ToSignal(GetTree().CreateTimer(delay * 0.5f), "timeout");
			if (juegoTerminado) return;
		}

		// Última pasada: ninguna tropa se queda parada sin hacer nada (sobre todo las que acaba de
		// invocar). Mientras le quede energía, cada una ataca, usa su habilidad o se cubre.
		await AccionesPendientesCPU(delay);

		if (!esTurnoJugador && !juegoTerminado) CambiarTurno();
	}

	/// <summary>Recorre las tropas del rival que todavía no hicieron nada este turno y les hace tomar la
	/// mejor decisión disponible: habilidad si la tiene lista, ataque si hay a quién, o cubrirse si le
	/// queda escudo. Antes las tropas recién invocadas (o las que no podían atacar) quedaban quietas.</summary>
	private async Task AccionesPendientesCPU(float delay)
	{
		var pendientes = new List<TropaBase>();
		foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
			if (n is TropaBase t && IsInstanceValid(t) && !t.EstaMuerta && !t.YaActuo && !EstaBlockeada(t))
				pendientes.Add(t);

		foreach (TropaBase tropa in pendientes)
		{
			if (movimientosRestantes <= 0 || juegoTerminado) return;
			if (!IsInstanceValid(tropa) || tropa.EstaMuerta || tropa.YaActuo) continue;
			await EsperarTableroLibre();
			if (!IsInstanceValid(tropa) || juegoTerminado) return;

			Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
			bool habilidadLista = !HabilidadUsada(tropa) && !HabilidadBloqueadaTurno(tropa);

			if (habilidadLista && tropa is TorrePrime torre)
			{
				if (!await IntentarEnroqueIA(torre) && objetivo != null) ProcesarCombateFrontal(tropa, "tropas_jugador");
				else if (objetivo == null) continue;
			}
			else if (habilidadLista)                      tropa.EjecutarAccion("usar_habilidad");
			else if (objetivo != null)                    ProcesarCombateFrontal(tropa, "tropas_jugador");
			else if (Gi(tropa, "escudoActual") > 0)       tropa.EjecutarAccion("preparar_defensa");
			else continue; // no hay nada útil que hacer con esta: no gasta energía

			movimientosRestantes--;
			ActualizarInterfaz();
			await ToSignal(GetTree().CreateTimer(delay * 0.8f), "timeout");
		}
	}

	/// <summary>Prioridad 3 de la IA: prefiere atacar cuando su objetivo de carril es un
	/// coloso enemigo (Paper-Rex/Tanque), para forzar su castigo masivo al Huevo si muere.</summary>
	private int PrioridadObjetivo(Node2D tropa)
	{
		Node2D objetivo = BuscarObjetivoEnCarril(tropa, "tropas_jugador");
		return (objetivo is TRexPrime || objetivo is TanqueCartoonPrime) ? 1 : 0;
	}

	// El CPU roba de su propio mazo (3 tácticos, 3 asesinos) e invoca un coloso
	// en sus turnos de coloso (cada 3 turnos), igual que el jugador.
	private PackedScene ElegirTropaCPU() => ElegirTropaCPUDeck();

	private bool DebeDefender(Node2D t, Node2D obj)
	{
		int vida = Gi(t,"vidaActual"), esc = Gi(t,"escudoActual");
		float umbralVida = _dificultadCPU == 2 ? 0.4f : 0.25f;
		if (obj != null) return ((float)vida / Mathf.Max(Gi(t,"vidaMaxima"),1) < umbralVida && esc > 50);
		return vida < 100 && esc > 0;
	}
}
