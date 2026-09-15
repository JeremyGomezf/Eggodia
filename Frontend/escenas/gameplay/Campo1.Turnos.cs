using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── RELOJ ─────────────────────────────────────────────────────────────
	private void OnTickReloj()
	{
		if (juegoTerminado) return;
		// En línea: mientras espero el turno del rival no corre el reloj (el turno no se me pasa solo).
		if (EsOnline && !esTurnoJugador) return;
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

		// Cooldown de hechizos: 5 turnos "en general" (cuentan los del jugador y los del rival) —
		// se descuenta en cada cambio de turno, no solo en el propio.
		for (int i = 0; i < _cooldownHechizo.Length; i++) if (_cooldownHechizo[i] > 0) _cooldownHechizo[i]--;
		ActualizarEstadoCartaRobada(); // libera "Robar Carta" en cuanto se juegue la carta del Spot4

		// Cooldown del botón de Ardid (2 rondas = 4 cambios de turno).
		if (_cooldownBtnArdid > 0)
		{
			_cooldownBtnArdid--;
			if (_cooldownBtnArdid == 0 && _btnCambiarHechizo != null)
			{
				_btnCambiarHechizo.Disabled = false;
				_btnCambiarHechizo.Modulate = Colors.White;
			}
		}

		if (modoSacrificioActivo) CancelarSacrificio();
		if (menuAcciones != null) menuAcciones.Visible = false;

		ProcesarStatusEfectos();

		// Libera carriles cuyo "Ocupado" quedó sin tropa (evita carriles fantasma).
		LimpiarCarrilesFantasma();

		if (esTurnoJugador)
		{
			// Avanzar cooldowns de reaparición antes de robar la mano del turno.
			AvanzarCooldownsJugador();

			// La reposición de la mano del CPU pasa acá, a los 2s de EMPEZAR MI turno (no el suyo) —
			// así cuando le vuelva a tocar al CPU ya tiene la mano lista, sin gastar ni un segundo de
			// SU propio turno esperando. Nunca durante la fase de apertura (ahí ya se arma la mano
			// inicial aparte, en InicializarManoVisualCPU).
			if (!_faseApertura)
				GetTree().CreateTimer(2.0).Timeout += () => { if (!juegoTerminado) RellenarManoVisualCPUSiFalta(); };

			// Si algún slot de hechizo quedó vacío (nada elegible cuando se gastó), reintentar
			// ahora que los cooldowns recién bajaron.
			for (int i = 0; i < 2; i++) if (_tarjetasHechizoCarta[i] == null) AutoReemplazarHechizo(i);

			// Asistente táctico: SOLO avisa cuando la habilidad de una tropa PROPIA acaba de
			// desbloquearse (transición bloqueada→lista). Sin alertas de vida baja ni de tropas
			// rivales — alcance reducido a pedido explícito del usuario.
			var avisosAsistente = new List<(string texto, Color color)>();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			{
				if (n.HasMethod("SetActivo")) n.Call("SetActivo", true);
				if (!(n is Node2D tropa) || !IsInstanceValid(tropa)) continue;

				bool bloqueadaAntes = false;
				try { bloqueadaAntes = (bool)tropa.Call("HabilidadBloqueada"); } catch { }
				// La ronda de invocación inicial (armar el campo) no cuenta — se saltea una sola vez.
				if (!_primerAvanceJugadorPendiente && tropa.HasMethod("AvanzarTurnoTropa")) tropa.Call("AvanzarTurnoTropa");

				bool tieneHabilidad = false;
				try { tieneHabilidad = (bool)tropa.Call("TieneHabilidadEspecial"); } catch { }
				if (tieneHabilidad && bloqueadaAntes)
				{
					bool sigueBloqueada = true;
					try { sigueBloqueada = (bool)tropa.Call("HabilidadBloqueada"); } catch { }
					if (!sigueBloqueada)
						avisosAsistente.Add(($"¡{NombreCorto(tropa)} ya tiene su habilidad lista!", new Color(0.5f, 0.85f, 1f)));
				}
			}
			_primerAvanceJugadorPendiente = false; // ya se consumió (o no aplicaba) — de acá en más cuenta normal
			if (avisosAsistente.Count > 0) MostrarAvisosAsistente(avisosAsistente);
		}
		else
		{
			// Programar aparición de coloso del CPU cada 3 turnos.
			int turnoNum = _turnosJugados / 2 + 1;
			_cpuColosoPendiente = (turnoNum % 3 == 0);
			// La reposición de MI mano pasa acá, a los 2s de empezar el turno DEL RIVAL — así cuando
			// vuelva a ser mi turno la mano ya está lista y no pierdo nada de mis 30s pensando la
			// estrategia esperando que aparezca una carta. Nunca durante la fase de apertura.
			if (!_faseApertura)
				GetTree().CreateTimer(2.0).Timeout += () => { if (!juegoTerminado) CompletarManoAlInicio(); };
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
				TickFuerza(tropa);
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

	// Fuerza: +100 de daño plano durante 2 turnos alternados (cuenta jugador y rival, igual que
	// Bloqueo/Veneno) — al expirar, revierte el bono de puntosAtaque y el tinte naranja.
	private void TickFuerza(Node2D t)
	{
		if (!t.HasMeta("fuerzaActiva")) return;
		bool activo; try { activo = (bool)t.GetMeta("fuerzaActiva"); } catch { return; }
		if (!activo) return;
		int turnos = t.HasMeta("turnosFuerza") ? (int)t.GetMeta("turnosFuerza") : 1;
		turnos--;
		if (turnos <= 0)
		{
			t.SetMeta("fuerzaActiva", false);
			int ata = 0; try { ata = (int)t.Get("puntosAtaque"); } catch { }
			try { t.Set("puntosAtaque", Mathf.Max(0, ata - 100)); } catch { }
			t.Modulate = Colors.White;
		}
		else t.SetMeta("turnosFuerza", turnos);
	}
}
