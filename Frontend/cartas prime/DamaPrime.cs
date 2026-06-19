using Godot;

public partial class DamaPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 350; escudoActual = escudoMaximo = 300; puntosAtaque = 350; }
		base._Ready();
	}
}
