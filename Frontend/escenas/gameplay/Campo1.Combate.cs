using Godot;
using System;
using System.Collections.Generic;

public partial class Campo1 : Node2D
{
	// ── COMBATE CON NÚMEROS FLOTANTES ─────────────────────────────────────
	private void ProcesarCombateFrontal(Node2D atacante, string grupoEnemigo, bool ignorarMuro = false)
	{
		if (!IsInstanceValid(atacante) || EstaBlockeada(atacante)) return;
		int    daño = Gi(atacante, "puntosAtaque");
		Node2D obj  = BuscarObjetivoEnCarril(atacante, grupoEnemigo, ignorarMuro);
		bool   autogestionado = false;
		try { autogestionado = (bool)atacante.Call("AutogestionaDañoAtaque"); } catch { }

		float multEra  = 1.0f;
		float multTipo = 1.0f;
		if (obj != null && IsInstanceValid(obj))
		{
			multEra  = ObtenerMultiplicadorEra(ObtenerEraTropa(atacante), ObtenerEraTropa(obj));
			multTipo = Tipos.Multiplicador(ObtenerTipoTropa(atacante), ObtenerTipoTropa(obj));
			daño = (int)(daño * multEra * multTipo);
		}

		bool critico = random.Next(100) < 10;
		if (critico) daño = (int)(daño * 1.5f);

		atacante.Call("EjecutarAccion", "atacar");

		if (obj != null && IsInstanceValid(obj))
		{
			if (!autogestionado)
			{
				int vidaAntes = Gi(obj, "vidaActual");
				obj.Call("RecibirDaño", daño);
				int vidaDespues = Gi(obj, "vidaActual");
				int dañoReal = vidaAntes - vidaDespues;
				if (dañoReal > 0)
				{
					if (critico)
						MostrarDañoFlotanteCritico(obj.GlobalPosition, dañoReal);
					else
						MostrarDañoFlotante(obj.GlobalPosition, dañoReal);
				}
				MostrarVentajaEra(obj.GlobalPosition, multEra * multTipo);
				if (critico) ScreenShake(8f);

				if (grupoEnemigo == "tropas_rival") _dañoTotalJugador += dañoReal;
				else                                _dañoTotalRival   += dañoReal;

				if (dañoReal > 0)
				{
					string atk = NombreCorto(atacante), def = NombreCorto(obj);
					string extra = critico ? " (crítico)" : multEra * multTipo > 1f ? " (ventaja)" : "";
					bool jugadorAtaca = grupoEnemigo == "tropas_rival";
					RegistrarEvento($"{atk} → {def}: {dañoReal}{extra}",
						jugadorAtaca ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f));
				}
			}
		}
		else
		{
			if (grupoEnemigo == "tropas_rival")
			{
				vidaRival -= daño; if (vidaRival < 0) vidaRival = 0;
				if (critico) MostrarDañoFlotanteCritico(new Vector2(900, 200), daño);
				else         MostrarDañoFlotante(new Vector2(900, 200), daño);
				_dañoTotalJugador += daño;
				RegistrarEvento($"{NombreCorto(atacante)} golpea la base rival: {daño}", new Color(0.5f, 1f, 0.6f));
			}
			else
			{
				vidaJugador -= daño; if (vidaJugador < 0) vidaJugador = 0;
				if (critico) MostrarDañoFlotanteCritico(new Vector2(200, 200), daño);
				else         MostrarDañoFlotante(new Vector2(200, 200), daño);
				_dañoTotalRival += daño;
				RegistrarEvento($"{NombreCorto(atacante)} golpea tu base: {daño}", new Color(1f, 0.55f, 0.5f));
			}
			ScreenShake(critico ? 12f : 8f);
			CheckEstadoJuego();
		}
	}

	private Node2D BuscarObjetivoEnCarril(Node2D atacante, string grupo, bool ignorarMuro = false)
	{
		string carril = ((string)atacante.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","");

		if (!ignorarMuro)
		{
			string grupoMuro = grupo == "tropas_rival" ? "muros_rival" : "muros_jugador";
			foreach (Node n in GetTree().GetNodesInGroup(grupoMuro))
			{
				if (!(n is Node2D m) || !IsInstanceValid(m) || !m.HasMeta("carril")) continue;
				if (((string)m.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","") == carril) return m;
			}
		}

		Node2D mejor  = null; int min = int.MaxValue;
		foreach (Node n in GetTree().GetNodesInGroup(grupo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e) || !e.HasMeta("carril")) continue;
			if (((string)e.GetMeta("carril")).ToLower().Replace("modrival","").Replace("mod","") != carril) continue;
			int v = Gi(e,"vidaActual"); if (v < min) { min = v; mejor = e; }
		}
		return mejor;
	}
}
