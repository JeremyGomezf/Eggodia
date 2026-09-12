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

	private bool ValidarHechizo() => esTurnoJugador && movimientosRestantes > 0 && !juegoTerminado && !_faseApertura;

	// Aplica el efecto del hechizo pi (0=curación,2=veneno,3=bloqueo,4=encebollado) sobre
	// objetivo, validando que sea del bando correcto. "Robar Carta" (pi=1) no pasa por aquí:
	// tiene su propio flujo en Campo1.RoboCarta.cs porque no aplica un efecto directo sino que
	// abre la mini-pantalla de robo. Devuelve false si el objetivo no era válido (para que quien
	// llama pueda hacer que la carta regrese a la mano en vez de gastarse).
	private bool AplicarHechizoADestino(int pi, Node2D objetivo)
	{
		bool aliados = pi == 0 || pi == 4;
		string grupoEsperado = aliados ? "tropas_jugador" : "tropas_rival";
		if (!IsInstanceValid(objetivo) || !objetivo.IsInGroup(grupoEsperado)) return false;

		switch (pi)
		{
			case 2:
				objetivo.SetMeta("envenenado",   true);
				objetivo.SetMeta("danoVeneno",   50);
				objetivo.SetMeta("turnosVeneno", 3);
				objetivo.Modulate = new Color(0.6f,1f,0.4f);
				MarcarHechizoUsado(2);
				ActualizarIconosEstado(objetivo);
				MostrarAviso($"¡Envenenaste a {NombreCorto(objetivo)} del rival!", new Color(0.6f,1f,0.4f));
				break;
			case 3:
				objetivo.SetMeta("bloqueado",     true);
				objetivo.SetMeta("turnosBloqueo", 2);
				objetivo.Modulate = new Color(0.4f,0.6f,1.4f);
				MarcarHechizoUsado(3);
				ActualizarIconosEstado(objetivo);
				MostrarAviso($"¡Bloqueaste a {NombreCorto(objetivo)} del rival!", new Color(0.4f,0.7f,1f));
				break;
			case 0:
				AplicarCuracion(objetivo);
				MarcarHechizoUsado(0);
				MostrarAviso($"¡{NombreCorto(objetivo)} fue curado!", new Color(0.4f,1f,0.55f));
				break;
			case 4:
				AplicarEncebollado(objetivo);
				MarcarHechizoUsado(4);
				MostrarAviso($"¡{NombreCorto(objetivo)} recibió Encebollado!", new Color(1f,0.75f,0.25f));
				break;
			default:
				return false;
		}
		return true;
	}

	// ── INPUT ─────────────────────────────────────────────────────────────
	// Los hechizos ya no se activan por toque (ver Campo1.HUD.cs: ahora se arrastran igual que
	// las cartas de tropa). Aquí solo queda el input de Enroque y Sacrificio, que siguen siendo
	// por clic en el tablero.
	public override void _Input(InputEvent @event)
	{
		if (juegoTerminado || !esTurnoJugador) return;

		if (_torreEnroque != null && @event is InputEventMouseButton mbE && mbE.Pressed && mbE.ButtonIndex == MouseButton.Left)
		{
			if (IntentarClicEnroque(GetGlobalMousePosition())) return;
		}

		if (modoSacrificioActivo && @event is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			VerificarSacrificioEnCampo(GetGlobalMousePosition());
	}
}
