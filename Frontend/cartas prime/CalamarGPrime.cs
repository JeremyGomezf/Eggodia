using Godot;
using System.Collections.Generic;

/// <summary>Calamar Gigante — habilidad: bloquea las 2 tropas enemigas más cercanas por 1 turno.</summary>
public partial class CalamarGPrime : TropaBase
{
	public override string Tipo => Tipos.SOMBRA;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 400; escudoActual = escudoMaximo = 380; puntosAtaque = 370; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		var candidatos = new List<(Node2D n, float d)>();

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
			if (n is Node2D e && IsInstanceValid(e))
			{
				float d = GlobalPosition.DistanceTo(e.GlobalPosition);
				if (d <= 500f) candidatos.Add((e, d));
			}

		candidatos.Sort((a, b) => a.d.CompareTo(b.d));

		int count = 0;
		foreach (var (e, _) in candidatos)
		{
			if (count >= 2) break;
			e.SetMeta("bloqueado",     true);
			e.SetMeta("turnosBloqueo", 1);
			Tween tw = e.CreateTween();
			tw.TweenProperty(e, "modulate", new Color(0.2f, 0.1f, 0.35f, 0.9f), 0.2f);
			Vector2 orig = e.Position;
			Tween sh = e.CreateTween();
			sh.TweenProperty(e, "position", orig + new Vector2(6, 0),  0.05f);
			sh.TweenProperty(e, "position", orig - new Vector2(6, 0),  0.05f);
			sh.TweenProperty(e, "position", orig,                      0.05f);
			count++;
		}

		Tween self = CreateTween();
		self.TweenProperty(this, "scale", Scale * 1.2f, 0.2f);
		self.TweenProperty(this, "scale", Scale,        0.2f);

		habilidadUsada = true;
	}
}
