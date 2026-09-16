using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── HECHIZOS ──────────────────────────────────────────────────────────
	private void AplicarEncebollado(Node2D objetivo)
	{
		int ata = 0, vida = 0, vidaMax = 0;
		try { ata     = (int)objetivo.Get("puntosAtaque"); } catch { }
		try { vida    = (int)objetivo.Get("vidaActual"); } catch { }
		try { vidaMax = (int)objetivo.Get("vidaMaxima"); } catch { }
		try { objetivo.Set("puntosAtaque", ata + 100); }                   catch { }
		try { objetivo.Set("vidaActual", vida + 100); }                    catch { }
		try { objetivo.Set("vidaMaxima", Mathf.Max(vidaMax, vida + 100)); } catch { }

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

	// Color de Bloqueo: oscuro/casi negro (antes azul) — el único estado cuyo tinte NO dura 2s
	// sino hasta que expira el bloqueo en sí (ver TickBloqueo, Campo1.Turnos.cs).
	private static readonly Color COLOR_BLOQUEO = new Color(0.12f, 0.12f, 0.15f);

	// Aplica el efecto del hechizo en el slot pi (índice dentro de _poolActivo) sobre objetivo,
	// validando que sea del bando correcto. "Robar Carta" no pasa por aquí: tiene su propio flujo
	// en Campo1.RoboCarta.cs porque no aplica un efecto directo sino que abre la mini-pantalla de
	// robo. Devuelve false si el objetivo no era válido (para que quien llama pueda hacer que la
	// carta regrese a la mano en vez de gastarse).
	private bool AplicarHechizoADestino(int pi, Node2D objetivo)
	{
		if (pi < 0 || pi >= _poolActivo.Length) return false;
		HechizoDef def = _poolActivo[pi];
		string grupoEsperado = def.Aliado ? "tropas_jugador" : "tropas_rival";
		if (!IsInstanceValid(objetivo) || !objetivo.IsInGroup(grupoEsperado)) return false;

		switch (def.Id)
		{
			case "veneno":
				objetivo.SetMeta("envenenado",   true);
				objetivo.SetMeta("danoVeneno",   50);
				objetivo.SetMeta("turnosVeneno", 3);
				objetivo.Modulate = new Color(0.6f,1f,0.4f);
				MarcarHechizoUsado(pi);
				ActualizarIconosEstado(objetivo);
				MostrarAviso($"¡Envenenaste a {NombreCorto(objetivo)} del rival!", new Color(0.6f,1f,0.4f));
				break;
			case "bloqueo":
				if (objetivo.HasMethod("AlSerBloqueado")) objetivo.Call("AlSerBloqueado");
				objetivo.SetMeta("bloqueado",     true);
				objetivo.SetMeta("turnosBloqueo", 2);
				objetivo.Modulate = COLOR_BLOQUEO;
				MarcarHechizoUsado(pi);
				ActualizarIconosEstado(objetivo);
				MostrarAviso($"¡Bloqueaste a {NombreCorto(objetivo)} del rival!", new Color(0.4f,0.7f,1f));
				break;
			case "curacion":
				AplicarCuracion(objetivo);
				MarcarHechizoUsado(pi);
				MostrarAviso($"¡{NombreCorto(objetivo)} fue curado!", new Color(0.4f,1f,0.55f));
				break;
			case "encebollado":
				AplicarEncebollado(objetivo);
				MarcarHechizoUsado(pi);
				MostrarAviso($"¡{NombreCorto(objetivo)} recibió Encebollado!", new Color(1f,0.75f,0.25f));
				break;
			case "desprotegido":
				AplicarDesprotegido(objetivo);
				MarcarHechizoUsado(pi);
				MostrarAviso($"¡Desprotegiste a {NombreCorto(objetivo)} del rival!", new Color(0.4f,0.8f,1f));
				break;
			case "escudo":
				AplicarEscudo(objetivo);
				MarcarHechizoUsado(pi);
				MostrarAviso($"¡{NombreCorto(objetivo)} recibió Escudo!", new Color(0.2f,0.4f,1f));
				break;
			case "fuerza":
				AplicarFuerza(objetivo);
				MarcarHechizoUsado(pi);
				MostrarAviso($"¡{NombreCorto(objetivo)} recibió Fuerza!", new Color(1f,0.55f,0.1f));
				break;
			default:
				return false;
		}
		return true;
	}

	// ── DESPROTEGIDO / ESCUDO / FUERZA ────────────────────────────────────
	// Igual que AplicarCuracion/AplicarEncebollado: solo tocan Get/Set/Modulate/Tween, nunca
	// EjecutarAccion, por lo que no interrumpen ninguna animación de defensa/cobertura en curso.
	private void AplicarDesprotegido(Node2D objetivo)
	{
		int esc = 0, escMax = 0;
		try { esc    = (int)objetivo.Get("escudoActual"); } catch { }
		try { escMax = (int)objetivo.Get("escudoMaximo"); } catch { }
		float mitadMax = escMax * 0.5f;
		float quitar = esc > mitadMax ? mitadMax : esc * 0.75f;
		int nuevo = Mathf.Max(0, esc - Mathf.RoundToInt(quitar));
		try { objetivo.Set("escudoActual", nuevo); } catch { }

		MostrarDañoFlotante(objetivo.GlobalPosition, esc - nuevo);
		Tween tw = objetivo.CreateTween();
		tw.TweenProperty(objetivo, "modulate", new Color(0.4f, 0.8f, 1f), 0.3f); // Celeste
		tw.TweenProperty(objetivo, "modulate", Colors.White, 1.7f); // 2s en total
	}

	private void AplicarEscudo(Node2D objetivo)
	{
		int esc = 0, escMax = 0;
		try { esc    = (int)objetivo.Get("escudoActual"); } catch { }
		try { escMax = (int)objetivo.Get("escudoMaximo"); } catch { }
		int nuevo = Mathf.Min(escMax, esc + Mathf.RoundToInt(escMax * 0.5f));
		try { objetivo.Set("escudoActual", nuevo); } catch { }

		MostrarDañoFlotante(objetivo.GlobalPosition, nuevo - esc, true);
		Tween tw = objetivo.CreateTween();
		tw.TweenProperty(objetivo, "modulate", new Color(0.2f, 0.4f, 1f), 0.3f); // Azul
		tw.TweenProperty(objetivo, "modulate", Colors.White, 1.7f);
	}

	private void AplicarFuerza(Node2D objetivo)
	{
		int ata = 0;
		try { ata = (int)objetivo.Get("puntosAtaque"); } catch { }
		try { objetivo.Set("puntosAtaque", ata + 100); } catch { }
		objetivo.SetMeta("fuerzaActiva", true);
		objetivo.SetMeta("turnosFuerza", 2);

		Tween tw = objetivo.CreateTween();
		tw.TweenProperty(objetivo, "modulate", new Color(1f, 0.55f, 0.1f), 0.3f); // Naranja
		tw.TweenProperty(objetivo, "modulate", Colors.White, 1.7f);
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
