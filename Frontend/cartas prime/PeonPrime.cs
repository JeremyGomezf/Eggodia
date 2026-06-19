using Godot;

public partial class PeonPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 150; escudoActual = escudoMaximo = 150; puntosAtaque = 100; }
		base._Ready();
	}
}
