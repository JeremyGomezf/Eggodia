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
		if (tiempoTurnoActual <= 0) CambiarTurno();
		ActualizarInterfaz();
	}

	private void CambiarTurno()
	{
		_turnosJugados++;

		// Castigo por carril propio vacío al terminar el turno (antes de pasar al otro bando).
		if (!_faseApertura) AplicarCastigoCarrilesVacios(esTurnoJugador);

		esTurnoJugador    = !esTurnoJugador;
		tiempoTurnoActual = 28;

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
			foreach (Node n in GetTree().GetNodesInGroup("tropas_jugador"))
			{
				if (n.HasMethod("SetActivo")) n.Call("SetActivo", true);
				if (n.HasMethod("AvanzarTurnoTropa")) n.Call("AvanzarTurnoTropa");
			}
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

	private void AnunciarTurno()
	{
		if (juegoTerminado) return;
		int turnoNum  = _turnosJugados / 2 + 1;
		string texto = _faseApertura
			? (esTurnoJugador ? "APERTURA — Coloca tus 3 tropas" : "APERTURA — La CPU prepara su formación")
			: (esTurnoJugador ? $"TU TURNO · Turno {turnoNum}" : $"TURNO CPU · Turno {turnoNum}");
		Color color = esTurnoJugador ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.4f);

		var panel = new PanelContainer();
		panel.ZIndex = 150;
		panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		panel.OffsetTop   = 155;
		panel.OffsetLeft  = -260;
		panel.OffsetRight = 260;

		var sb = new StyleBoxFlat();
		sb.BgColor = new Color(0.05f, 0.07f, 0.11f, 0.88f);
		sb.BorderWidthLeft = sb.BorderWidthRight = 4;
		sb.BorderColor = color;
		sb.ContentMarginLeft = sb.ContentMarginRight = 24;
		sb.ContentMarginTop  = sb.ContentMarginBottom = 10;
		sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
		sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 4;
		panel.AddThemeStyleboxOverride("panel", sb);

		var lbl = new Label();
		lbl.Text = texto;
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", 26);
		lbl.HorizontalAlignment = HorizontalAlignment.Center;
		panel.AddChild(lbl);
		AddChild(panel);

		panel.PivotOffset = new Vector2(260, 26);
		panel.Scale = new Vector2(0.9f, 0.9f);
		panel.Modulate = new Color(1, 1, 1, 0);
		Tween tw = CreateTween().SetParallel(true);
		tw.TweenProperty(panel, "modulate:a", 1.0f, 0.18f);
		tw.TweenProperty(panel, "scale", Vector2.One, 0.28f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.Chain().TweenInterval(0.9f);
		tw.Chain().TweenProperty(panel, "modulate:a", 0.0f, 0.35f);
		tw.Finished += () => { if (IsInstanceValid(panel)) panel.QueueFree(); };
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
		int daño   = t.HasMeta("dañoVeneno")  ? (int)t.GetMeta("dañoVeneno")  : 30;
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
