using Godot;

public partial class EncebolladoPrime : TropaBase
{
	public override string Tipo => Tipos.FUEGO;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 500; escudoActual = escudoMaximo = 550; puntosAtaque = 450; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada) return;

		string grupoEnemigo = IsInGroup("tropas_jugador") ? "tropas_rival" : "tropas_jugador";

		Tween tw = CreateTween();
		tw.TweenProperty(this, "modulate", new Color(1.8f, 1.2f, 0.1f), 0.2f);
		tw.TweenProperty(this, "modulate", Colors.White, 0.5f);
		Tween sc = CreateTween();
		sc.TweenProperty(this, "scale", Scale * 1.2f, 0.15f);
		sc.TweenProperty(this, "scale", Scale, 0.2f);

		foreach (Node n in GetTree().GetNodesInGroup(grupoEnemigo))
		{
			if (!(n is Node2D e) || !IsInstanceValid(e)) continue;
			e.Call("RecibirDaño", 150);
			var campo = GetTree().Root.FindChild("Campo1", true, false);
			if (campo != null) campo.Call("AplicarVenenoMeta", e, 30, 2);
		}
		habilidadUsada = true;
	}
}
