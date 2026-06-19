using Godot;

public partial class SoldadoRealPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 250; escudoActual = escudoMaximo = 300; puntosAtaque = 150; }
		base._Ready();
	}
}
