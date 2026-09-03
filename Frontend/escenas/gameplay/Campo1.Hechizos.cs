using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── HECHIZOS ──────────────────────────────────────────────────────────
	private void AplicarEncebollado(Node2D objetivo)
	{
		int ata = 0, esc = 0, escMax = 0;
		try { ata    = (int)objetivo.Get("puntosAtaque"); } catch { }
		try { esc    = (int)objetivo.Get("escudoActual"); } catch { }
		try { escMax = (int)objetivo.Get("escudoMaximo"); } catch { }
		try { objetivo.Set("puntosAtaque", ata + 100); }                 catch { }
		try { objetivo.Set("escudoActual", esc + 100); }                 catch { }
		try { objetivo.Set("escudoMaximo", Mathf.Max(escMax, esc + 100)); } catch { }

		MostrarDañoFlotante(objetivo.GlobalPosition, 100, true);
		Tween tw = objetivo.CreateTween();
		tw.TweenProperty(objetivo, "modulate", new Color(1.6f, 1.3f, 0.2f), 0.2f);
		tw.TweenProperty(objetivo, "modulate", Colors.White, 0.5f);
	}

	private void AplicarCuracion(Node2D objetivo)
	{
		int vida = 0, vidaMax = 0;
		try { vida    = (int)objetivo.Get("vidaActual"); } catch { }
		try { vidaMax = (int)objetivo.Get("vidaMaxima"); } catch { }
		int curado = Mathf.Min(200, vidaMax - vida);
		try { objetivo.Set("vidaActual", vida + curado); } catch { }
		MostrarDañoFlotante(objetivo.GlobalPosition, curado, true);
		Tween tw = objetivo.CreateTween();
		tw.TweenProperty(objetivo, "modulate", new Color(0.3f,1.6f,0.5f), 0.2f);
		tw.TweenProperty(objetivo, "modulate", Colors.White, 0.5f);
	}

	private void UsarRobo()
	{
		if (!ValidarHechizo() || usadoRobo) return;
		foreach (string s in new[]{"Spot1","Spot2","Spot3"})
		{
			bool ok = false;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion()) ok = true;
			if (!ok) { CrearNuevaCartaEnSpot(s); break; }
		}
		usadoRobo = true;
		RegistrarGastoMovimiento();
	}

	private static bool HechizoApuntaAliados(string hechizo) => hechizo == "curacion" || hechizo == "encebollado";

	private void IniciarSeleccion(string hechizo, int slotIdx = -1)
	{
		if (!ValidarHechizo()) return;
		if (hechizo == "veneno"      && usadoVeneno)      return;
		if (hechizo == "bloqueo"     && usadoBloqueo)     return;
		if (hechizo == "curacion"    && usadoCuracion)    return;
		if (hechizo == "encebollado" && usadoEncebollado) return;
		_modoSeleccionObjetivo = true;
		_hechizoPendiente      = hechizo;
		_slotPendiente         = slotIdx;
		if (_lblInstruccion != null)
		{
			_lblInstruccion.Text = HechizoApuntaAliados(hechizo) ? "Toca una tropa\naliada" : "Toca una tropa\nenemiga";
			_lblInstruccion.Visible = true;
		}
	}

	private void AplicarHechizoEnObjetivo(Node2D objetivo)
	{
		string grupoEsperado = HechizoApuntaAliados(_hechizoPendiente) ? "tropas_jugador" : "tropas_rival";
		if (!IsInstanceValid(objetivo) || !objetivo.IsInGroup(grupoEsperado)) return;

		switch (_hechizoPendiente)
		{
			case "veneno":
				objetivo.SetMeta("envenenado",   true);
				objetivo.SetMeta("dañoVeneno",   50);
				objetivo.SetMeta("turnosVeneno", 3);
				objetivo.Modulate = new Color(0.6f,1f,0.4f);
				usadoVeneno = true;
				ActualizarIconosEstado(objetivo);
				break;
			case "bloqueo":
				objetivo.SetMeta("bloqueado",     true);
				objetivo.SetMeta("turnosBloqueo", 2);
				objetivo.Modulate = new Color(0.4f,0.6f,1.4f);
				usadoBloqueo = true;
				ActualizarIconosEstado(objetivo);
				break;
			case "curacion":
				AplicarCuracion(objetivo);
				usadoCuracion = true;
				break;
			case "encebollado":
				AplicarEncebollado(objetivo);
				usadoEncebollado = true;
				break;
		}

		_modoSeleccionObjetivo = false;
		_hechizoPendiente      = "";
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		AutoReemplazarHechizo(_slotPendiente);
		_slotPendiente = -1;
		ActualizarVisualesHechizos();
		RegistrarGastoMovimiento();
	}

	private bool ValidarHechizo() => esTurnoJugador && movimientosRestantes > 0 && !juegoTerminado && !_faseApertura;

	// ── INPUT ─────────────────────────────────────────────────────────────
	public override void _Input(InputEvent @event)
	{

		if (juegoTerminado || !esTurnoJugador) return;

		if (_torreEnroque != null && @event is InputEventMouseButton mbE && mbE.Pressed && mbE.ButtonIndex == MouseButton.Left)
		{
			if (IntentarClicEnroque(GetGlobalMousePosition())) return;
		}

		if (_modoSeleccionObjetivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mouse = GetGlobalMousePosition();
			string grupo = HechizoApuntaAliados(_hechizoPendiente) ? "tropas_jugador" : "tropas_rival";
			foreach (Node n in GetTree().GetNodesInGroup(grupo))
				if (n is Node2D t && IsInstanceValid(t) && t.GlobalPosition.DistanceTo(mouse) < 90f)
				{ AplicarHechizoEnObjetivo(t); return; }
		}

		if (modoSacrificioActivo && @event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			VerificarSacrificioEnCampo(GetGlobalMousePosition());
	}
}
