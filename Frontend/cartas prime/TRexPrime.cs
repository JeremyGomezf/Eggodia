using Godot;

public partial class TRexPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 400; escudoActual = escudoMaximo = 350; puntosAtaque = 400; }
		base._Ready();
	}
}
