using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── HECHIZOS ──────────────────────────────────────────────────────────
	private void UsarEncebollado()
	{
		if (!ValidarHechizo() || usadoEncebollado) return;
		if (tropaSeleccionada == null || !IsInstanceValid(tropaSeleccionada) || !tropaSeleccionada.IsInGroup("tropas_jugador"))
		{ GD.Print("Selecciona una tropa tuya primero"); return; }

		int ata = 0, esc = 0, escMax = 0;
		try { ata    = (int)tropaSeleccionada.Get("puntosAtaque"); } catch { }
		try { esc    = (int)tropaSeleccionada.Get("escudoActual"); } catch { }
		try { escMax = (int)tropaSeleccionada.Get("escudoMaximo"); } catch { }
		try { tropaSeleccionada.Set("puntosAtaque", ata + 100); }                  catch { }
		try { tropaSeleccionada.Set("escudoActual", esc + 100); }                  catch { }
		try { tropaSeleccionada.Set("escudoMaximo", Mathf.Max(escMax,esc+100)); }  catch { }

		MostrarDañoFlotante(tropaSeleccionada.GlobalPosition, 100, true);
		Tween tw = tropaSeleccionada.CreateTween();
		tw.TweenProperty(tropaSeleccionada,"modulate", new Color(1.6f,1.3f,0.2f), 0.2f);
		tw.TweenProperty(tropaSeleccionada,"modulate", Colors.White, 0.5f);

		usadoEncebollado = true;
		RegistrarGastoMovimiento();
	}

	private void UsarCuracion()
	{
		if (!ValidarHechizo() || usadoCuracion) return;
		if (tropaSeleccionada != null && IsInstanceValid(tropaSeleccionada) && tropaSeleccionada.IsInGroup("tropas_jugador"))
		{
			int vida = 0, vidaMax = 0;
			try { vida    = (int)tropaSeleccionada.Get("vidaActual"); } catch { }
			try { vidaMax = (int)tropaSeleccionada.Get("vidaMaxima"); } catch { }
			int curado = Mathf.Min(200, vidaMax - vida);
			try { tropaSeleccionada.Set("vidaActual", vida + curado); } catch { }
			MostrarDañoFlotante(tropaSeleccionada.GlobalPosition, curado, true);
			Tween tw = tropaSeleccionada.CreateTween();
			tw.TweenProperty(tropaSeleccionada,"modulate", new Color(0.3f,1.6f,0.5f), 0.2f);
			tw.TweenProperty(tropaSeleccionada,"modulate", Colors.White, 0.5f);
		}
		else
		{
			int curado = Mathf.Min(200, vidaMaxJugador - vidaJugador);
			vidaJugador += curado;
			MostrarDañoFlotante(new Vector2(200, 300), curado, true);
		}
		usadoCuracion = true;
		RegistrarGastoMovimiento();
		ActualizarInterfaz();
	}

	private void UsarRobo()
	{
		if (!ValidarHechizo() || usadoRobo) return;
		foreach (string s in new[]{"Spot1","Spot2","Spot3","Spot4"})
		{
			bool ok = false;
			foreach (Node n in contenedorMano.GetChildren())
				if (n is Carta c && c.NombreSpot == s && !c.IsQueuedForDeletion()) ok = true;
			if (!ok) { CrearNuevaCartaEnSpot(s); break; }
		}
		usadoRobo = true;
		RegistrarGastoMovimiento();
	}

	private void IniciarSeleccion(string hechizo)
	{
		if (!ValidarHechizo()) return;
		if (hechizo == "veneno"  && usadoVeneno)  return;
		if (hechizo == "bloqueo" && usadoBloqueo) return;
		_modoSeleccionObjetivo = true;
		_hechizoPendiente      = hechizo;
		if (_lblInstruccion != null) _lblInstruccion.Visible = true;
	}

	private void AplicarHechizoEnObjetivo(Node2D objetivo)
	{
		if (!IsInstanceValid(objetivo) || !objetivo.IsInGroup("tropas_rival")) return;
		if (_hechizoPendiente == "veneno")
		{
			objetivo.SetMeta("envenenado",   true);
			objetivo.SetMeta("dañoVeneno",   50);
			objetivo.SetMeta("turnosVeneno", 3);
			objetivo.Modulate = new Color(0.6f,1f,0.4f);
			usadoVeneno = true;
		}
		else if (_hechizoPendiente == "bloqueo")
		{
			objetivo.SetMeta("bloqueado",     true);
			objetivo.SetMeta("turnosBloqueo", 2);
			objetivo.Modulate = new Color(0.4f,0.6f,1.4f);
			usadoBloqueo = true;
		}
		ActualizarIconosEstado(objetivo);
		_modoSeleccionObjetivo = false;
		_hechizoPendiente      = "";
		if (_lblInstruccion != null) _lblInstruccion.Visible = false;
		RegistrarGastoMovimiento();
	}

	private bool ValidarHechizo() => esTurnoJugador && movimientosRestantes > 0 && !juegoTerminado && !_faseApertura;

	// ── INPUT ─────────────────────────────────────────────────────────────
	public override void _Input(InputEvent @event)
	{

		if (juegoTerminado || !esTurnoJugador) return;

		if (_modoSeleccionObjetivo && @event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			Vector2 mouse = GetGlobalMousePosition();
			foreach (Node n in GetTree().GetNodesInGroup("tropas_rival"))
				if (n is Node2D t && IsInstanceValid(t) && t.GlobalPosition.DistanceTo(mouse) < 90f)
				{ AplicarHechizoEnObjetivo(t); return; }
		}

		if (modoSacrificioActivo && @event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			VerificarSacrificioEnCampo(GetGlobalMousePosition());
	}
}
