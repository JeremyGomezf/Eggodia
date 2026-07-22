using Godot;

public partial class MaguinPrime : TropaBase
{
	public override string Tipo => Tipos.AGUA;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 200; escudoActual = escudoMaximo = 220; puntosAtaque = 250; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;
		if (!HasMeta("carril")) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		string miCarril = ((string)GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();

		Node2D objetivo = null;
		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (n is Node2D e && IsInstanceValid(e) && e.HasMeta("carril"))
			{
				string c = ((string)e.GetMeta("carril")).ToLower().Replace("modrival", "").Replace("mod", "").Trim();
				if (c == miCarril) { objetivo = e; break; }
			}
		}

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(0.5f, 0.8f, 2f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.4f);

		if (objetivo != null)
		{
			objetivo.Call("RecibirDaño", 100);
			objetivo.SetMeta("bloqueado", true);
			objetivo.SetMeta("turnosBloqueo", 1);
			Tween te = objetivo.CreateTween();
			te.TweenProperty(objetivo, "modulate", new Color(0.4f, 0.7f, 1.5f), 0.2f);
			Vector2 orig = objetivo.Position;
			Tween sh = objetivo.CreateTween();
			sh.TweenProperty(objetivo, "position", orig + new Vector2(5, 0), 0.04f);
			sh.TweenProperty(objetivo, "position", orig - new Vector2(5, 0), 0.04f);
			sh.TweenProperty(objetivo, "position", orig, 0.04f);
		}

		habilidadUsada = true;
	}
}
