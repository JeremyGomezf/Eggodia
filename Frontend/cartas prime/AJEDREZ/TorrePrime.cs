using Godot;

/// <summary>Torre — habilidad: Enroque Táctico (intercambia carril con un aliado adyacente,
/// o se mueve sola si el carril de destino está vacío). Disponible desde su turno 1.</summary>
public partial class TorrePrime : TropaBase
{
	public override string Tipo => Tipos.METAL;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 500; escudoActual = escudoMaximo = 450; puntosAtaque = 350; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || HabilidadBloqueada() || !HasMeta("carril")) return;

		var campo = GetTree().Root.FindChild("Campo1", true, false);
		if (campo == null || !campo.HasMethod("IniciarSeleccionEnroque")) return;

		campo.Call("IniciarSeleccionEnroque", this);
	}
}
