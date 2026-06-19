using Godot;

public partial class EncebolladoPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 500; escudoActual = escudoMaximo = 550; puntosAtaque = 450; }
		base._Ready();
	}
}
