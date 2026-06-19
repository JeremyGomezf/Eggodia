using Godot;

public partial class DragonPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 350; escudoActual = escudoMaximo = 250; puntosAtaque = 280; }
		base._Ready();
	}
}
