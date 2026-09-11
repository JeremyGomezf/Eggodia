using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── RELOJ ─────────────────────────────────────────────────────────────
	private void OnTickReloj()
	{
		if (juegoTerminado) return;
		tiempoTotalPartida--;
		if (tiempoTotalPartida <= 0) { DeterminarGanadorPorTiempo(); return; }
		tiempoTurnoActual--;
		if (tiempoTurnoActual <= 0 && !_turnoFinalizando) { _turnoFinalizando = true; CambiarTurno(); }
		ActualizarInterfaz();
	}

	private void CambiarTurno()
	{
		_turnoFinalizando = false;
		_turnosJugados++;

		// Castigo por carril propio vacío al terminar el turno (antes de pasar al otro bando).
		if (!_faseApertura) AplicarCastigoCarrilesVacios(esTurnoJugador);

		esTurnoJugador    = !esTurnoJugador;
		tiempoTurnoActual = DURACION_TURNO_SEG;

		// Energía fija: 3/3 en todos los turnos, sin escalado ni bonos.
		movimientosRestantes = ENERGIA_MAXIMA;

		usosBarajar    = 0;
		usosSacrificio = 0;
		// Cada turno del jugador empieza en fase de invocación
		faseInvocacion = esTurnoJugador ? !TodosSpotsOcupados() : false;
		_comboTurno    = 0;
		_hechizoUsadoEsteTurno = false;
		_modoSeleccionObjetivo = false;
		_hechizoPendiente      = "";
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		if (modoSacrificioActivo) CancelarSacrificio();
		if (menuAcciones != null) menuAcciones.Visible = false;

		ProcesarStatusEfectos();

		// Libera carriles cuyo "Ocupado" quedó sin tropa (evita carriles fantasma).
		LimpiarCarrilesFantasma();

		if (esTurnoJugador)
		{
			// Avanzar cooldowns de reaparición antes de robar la mano del turno.
			AvanzarCooldownsJugador();
			CompletarManoAlInicio();

			// Asistente táctico: detecta, en el mismo momento en que se avanza el turno de cada
			// tropa, si su habilidad acaba de desbloquearse (transición bloqueada→lista) y si su
			// salud cruzó el umbral de "baja" por primera vez.
			var avisosAsistente = new List<(string texto, Color color)>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			{
				if (n.HasMethod("SetActivo")) n.Call("SetActivo", true);
				if (!(n is Node2D tropa) || !IsInstanceValid(tropa)) continue;

				bool bloqueadaAntes = false;
				try { bloqueadaAntes = (bool)tropa.Call("HabilidadBloqueada"); } catch { }
				if (tropa.HasMethod("AvanzarTurnoTropa")) tropa.Call("AvanzarTurnoTropa");

				bool tieneHabilidad = false;
				try { tieneHabilidad = (bool)tropa.Call("TieneHabilidadEspecial"); } catch { }
				if (tieneHabilidad && bloqueadaAntes)
				{
					bool sigueBloqueada = true;
					try { sigueBloqueada = (bool)tropa.Call("HabilidadBloqueada"); } catch { }
					if (!sigueBloqueada)
						avisosAsistente.Add(($"¡{NombreCorto(tropa)} ya tiene su habilidad lista!", new Color(0.5f, 0.85f, 1f)));
				}

				int vida = Gi(tropa, "vidaActual"), vidaMax = Gi(tropa, "vidaMaxima");
				if (vidaMax > 0 && vida > 0 && (float)vida / vidaMax <= 0.25f && !tropa.HasMeta("alerta_salud_baja"))
				{
					tropa.SetMeta("alerta_salud_baja", true);
					avisosAsistente.Add(($"¡{NombreCorto(tropa)} tiene la salud baja!", new Color(1f, 0.45f, 0.4f)));
				}
			}
			if (avisosAsistente.Count > 0) MostrarAvisosAsistente(avisosAsistente);
		}
		else
		{
			// Programar aparición de coloso del CPU cada 3 turnos.
			int turnoNum = _turnosJugados / 2 + 1;
			_cpuColosoPendiente = (turnoNum % 3 == 0);
			EjecutarTurnoCPU();
		}

		AnunciarTurno();
		ActualizarInterfaz();
	}

	// TurnoPanel/HBox/"turno o avisos": toast animado que vive fijo en el HUD (no se crea
	// ni se destruye por turno). Cambia de texto/color y hace un rebote "gelatina" al cambiar
	// de turno; el contador de segundos se refresca aparte, cada tick, en ActualizarContadorTurno.
	private string _turnoBaseTexto = "";
	private void AnunciarTurno()
	{
		if (juegoTerminado || _lblTurnoAviso == null) return;

		_turnoBaseTexto = esTurnoJugador ? "TU TURNO" : "TURNO DEL RIVAL";
		Color color = esTurnoJugador ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.4f);
		_lblTurnoAviso.AddThemeColorOverride("font_color", color);
		ActualizarContadorTurno();

		// TurnoPanel = HBox.GetParent() (el TextureRect ya trasladado al CanvasLayer)
		if (_lblTurnoAviso.GetParent()?.GetParent() is not Control panel) return;
		panel.PivotOffset = panel.Size / 2f;
		panel.Scale = _turnoPanelEscalaBase * 1.4f; // impacto inicial ("crece rápido")
		Tween tw = panel.CreateTween();
		tw.TweenProperty(panel, "scale", _turnoPanelEscalaBase, 0.55f)
		  .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
	}

	// Solo actualiza el "(Ns)" del texto de turno — se llama cada segundo desde ActualizarInterfaz,
	// sin repetir la animación de gelatina (esa es exclusiva del cambio de turno real).
	private void ActualizarContadorTurno()
	{
		if (juegoTerminado || _lblTurnoAviso == null || string.IsNullOrEmpty(_turnoBaseTexto)) return;
		int segundos = Mathf.Max(0, tiempoTurnoActual);
		_lblTurnoAviso.Text = $"{_turnoBaseTexto} ({segundos}s)";
	}

	// Muestra los avisos del asistente táctico uno tras otro (MostrarAviso solo permite uno a
	// la vez): cada uno dura DURACION_TOAST antes de dar paso al siguiente.
	private async void MostrarAvisosAsistente(List<(string texto, Color color)> avisos)
	{
		foreach (var (texto, color) in avisos)
		{
			if (juegoTerminado) return;
			MostrarAviso(texto, color);
			await ToSignal(GetTree().CreateTimer(DURACION_TOAST + 0.5f), "timeout");
		}
	}

	// ── CASTIGO POR CARRIL VACÍO ────────────────────────────────────────────
	private const int CASTIGO_CARRIL_VACIO = 150;

	private void AplicarCastigoCarrilesVacios(bool deJugador)
	{
		string[] carriles = deJugador
			? new[] { "Mod1", "Mod2", "Mod3" }
			: new[] { "ModRival1", "ModRival2", "ModRival3" };

		bool hayCarrilVacio = false;
		foreach (string nombre in carriles)
		{
			Node2D zona = GetTree().Root.FindChild(nombre, true, false) as Node2D;
			if (zona == null || zona.GetNodeOrNull("Ocupado") == null) { hayCarrilVacio = true; break; }
		}
		if (!hayCarrilVacio) return;

		if (deJugador)
		{
			vidaJugador -= CASTIGO_CARRIL_VACIO; if (vidaJugador < 0) vidaJugador = 0;
			MostrarAviso($"Carril vacío: -{CASTIGO_CARRIL_VACIO} HP", Colors.OrangeRed);
		}
		else
		{
			vidaRival -= CASTIGO_CARRIL_VACIO; if (vidaRival < 0) vidaRival = 0;
		}
		CheckEstadoJuego();
	}

	// ── STATUS EFFECTS ────────────────────────────────────────────────────
	private void ProcesarStatusEfectos()
	{
		foreach (string grupo in new[] { "tropas_jugador", "tropas_rival" })
		{
			var lista = new List<Node2D>();
			foreach (Node n in GetTree().GetNodesInGroup(grupo))
				if (n is Node2D n2 && IsInstanceValid(n2)) lista.Add(n2);

			foreach (Node2D tropa in lista)
			{
				TickVeneno(tropa);
				TickBloqueo(tropa);
				if (tropa.HasMethod("TickHabilidad")) tropa.Call("TickHabilidad");
				if (tropa.HasMethod("TickTransformacion")) tropa.Call("TickTransformacion");
				if (Gi(tropa, "vidaActual") <= 0) EjecutarMuerteTropaSacrificada(tropa);
			}
		}
		CheckEstadoJuego();
	}

	private void TickVeneno(Node2D t)
	{
		if (!t.HasMeta("envenenado")) return;
		bool env; try { env = (bool)t.GetMeta("envenenado"); } catch { return; }
		if (!env) return;
		int daño   = t.HasMeta("danoVeneno")  ? (int)t.GetMeta("danoVeneno")  : 30;
		int turnos = t.HasMeta("turnosVeneno") ? (int)t.GetMeta("turnosVeneno"): 1;
		MostrarDañoFlotante(t.GlobalPosition, daño);
		if (t.HasMethod("RecibirDaño")) t.Call("RecibirDaño", daño);
		turnos--;
		if (turnos <= 0) { t.SetMeta("envenenado", false); t.Modulate = Colors.White; }
		else             t.SetMeta("turnosVeneno", turnos);
		ActualizarIconosEstado(t);
	}

	private void TickBloqueo(Node2D t)
	{
		if (!t.HasMeta("bloqueado")) return;
		bool bl; try { bl = (bool)t.GetMeta("bloqueado"); } catch { return; }
		if (!bl) return;
		int turnos = t.HasMeta("turnosBloqueo") ? (int)t.GetMeta("turnosBloqueo") : 1;
		turnos--;
		if (turnos <= 0) { t.SetMeta("bloqueado", false); t.Modulate = Colors.White; }
		else             t.SetMeta("turnosBloqueo", turnos);
		ActualizarIconosEstado(t);
	}
}
