using Godot;

public partial class MaguinPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 200; escudoActual = escudoMaximo = 220; puntosAtaque = 250; }
		base._Ready();
	}
}
