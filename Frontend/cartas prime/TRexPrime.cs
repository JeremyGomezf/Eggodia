using Godot;
using System.Collections.Generic;

public partial class TRexPrime : TropaBase
{
	private bool _rugidoActivo;
	private int  _turnosRugido;
	private List<(Node2D tropa, int ataqueOrig)> _afectados = new();

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 400; escudoActual = escudoMaximo = 350; puntosAtaque = 400; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";

		Tween tw = CreateTween();
		tw.TweenProperty(this, "scale", Scale * 1.3f, 0.15f).SetTrans(Tween.TransitionType.Back);
		tw.TweenProperty(this, "scale", Scale, 0.2f);

		_afectados.Clear();
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e)) continue;
			int atk = 0;
			try { atk = (int)e.Get("puntosAtaque"); } catch { continue; }
			_afectados.Add((e, atk));
			try { e.Set("puntosAtaque", (int)(atk * 0.7f)); } catch { }
			Tween te = e.CreateTween();
			te.TweenProperty(e, "modulate", new Color(1f, 0.5f, 0.5f), 0.2f);
		}
		_rugidoActivo  = true;
		_turnosRugido  = 2;
		habilidadUsada = true;
	}

	public override void TickHabilidad()
	{
		if (!_rugidoActivo) return;
		_turnosRugido--;
		if (_turnosRugido > 0) return;
		foreach (var (tropa, ataqueOrig) in _afectados)
		{
			if (!IsInstanceValid(tropa)) continue;
			try { tropa.Set("puntosAtaque", ataqueOrig); } catch { }
			tropa.Modulate = Colors.White;
		}
		_afectados.Clear();
		_rugidoActivo = false;
	}
}
