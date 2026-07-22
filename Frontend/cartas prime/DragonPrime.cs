using Godot;

public partial class DragonPrime : TropaBase
{
	public override string Tipo => Tipos.FUEGO;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 350; escudoActual = escudoMaximo = 250; puntosAtaque = 280; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";
		int dañoAoE = (int)(puntosAtaque * 0.4f);

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(2f, 0.5f, 0.1f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.4f);
		Tween sc = CreateTween();
		sc.TweenProperty(this, "scale", Scale * 1.15f, 0.15f);
		sc.TweenProperty(this, "scale", Scale, 0.2f);

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (n is Node2D e && IsInstanceValid(e))
			{
				e.Call("RecibirDaño", dañoAoE);
				Tween tf = e.CreateTween();
				tf.TweenProperty(e, "modulate", new Color(2f, 0.3f, 0.0f), 0.15f);
				tf.TweenProperty(e, "modulate", Colors.White, 0.3f);
			}
		}
		habilidadUsada = true;
	}
}
