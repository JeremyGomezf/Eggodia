using Godot;

public partial class TiburonPrime : TropaBase
{
	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 300; escudoActual = escudoMaximo = 200; puntosAtaque = 230; }
		base._Ready();
	}
}
