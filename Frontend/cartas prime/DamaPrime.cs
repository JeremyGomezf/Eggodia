using Godot;
using System.Collections.Generic;

public partial class DamaPrime : TropaBase
{
	public override string Tipo => Tipos.SOMBRA;

	private bool _inspiracionActiva;
	private int  _turnosInspiracion;
	private List<(Node2D tropa, int ataqueOrig)> _inspirados = new();

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 350; escudoActual = escudoMaximo = 300; puntosAtaque = 350; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoAliado = IsInGroup("tropas_jugador") ? "tropas_jugador" : "tropas_rival";

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(1.5f, 1.2f, 0.5f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.4f);

		_inspirados.Clear();
		foreach (Node n in GetTree().GetNodesInGroup(grupoAliado))
		{
			if (!(n is Node2D a) || !IsInstanceValid(a) || a == this) continue;
			int atk = 0;
			try { atk = (int)a.Get("puntosAtaque"); } catch { continue; }
			_inspirados.Add((a, atk));
			try { a.Set("puntosAtaque", atk + 100); } catch { }
			Tween ta = a.CreateTween();
			ta.TweenProperty(a, "modulate", new Color(1.4f, 1.3f, 0.3f), 0.2f);
			ta.TweenProperty(a, "modulate", Colors.White, 0.5f);
		}
		_inspiracionActiva  = true;
		_turnosInspiracion  = 2;
		habilidadUsada      = true;
	}

	public override void TickHabilidad()
	{
		if (!_inspiracionActiva) return;
		_turnosInspiracion--;
		if (_turnosInspiracion > 0) return;
		foreach (var (tropa, ataqueOrig) in _inspirados)
		{
			if (!IsInstanceValid(tropa)) continue;
			try { tropa.Set("puntosAtaque", ataqueOrig); } catch { }
		}
		_inspirados.Clear();
		_inspiracionActiva = false;
	}
}
