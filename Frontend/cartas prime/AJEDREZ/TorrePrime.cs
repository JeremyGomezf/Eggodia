using Godot;

/// <summary>Torre — habilidad: Enroque Táctico (intercambia carril con un aliado adyacente,
/// o se mueve sola si el carril de destino está vacío). Caso especial: disponible de inmediato,
/// en la misma ronda en que se invoca (única carta con umbral 0).</summary>
public partial class TorrePrime : TropaBase
{
	public override string Tipo => Tipos.METAL;
	protected override int TurnoDesbloqueoHabilidad => 0;

	public override void _Ready()
	{
		if (vidaMaxima == 0) { vidaActual = vidaMaxima = 500; escudoActual = escudoMaximo = 450; puntosAtaque = 330; }
		base._Ready();
	}

	protected override void UsarHabilidadPropia()
	{
		if (habilidadUsada || HabilidadBloqueada() || !HasMeta("carril")) return;

		var campo = GetTree().Root.FindChild("Campo1", true, false);
		if (campo == null || !campo.HasMethod("IniciarSeleccionEnroque")) return;

		campo.Call("IniciarSeleccionEnroque", this);
	}

	// Mientras el tablero espera que elijas el carril de destino, la habilidad sigue SIN gastarse: si
	// el turno se va, queda disponible para el próximo (y no se pierde el movimiento).
	public override bool SeleccionPendiente
	{
		get
		{
			var campo = GetTree()?.Root?.FindChild("Campo1", true, false);
			if (campo == null || !campo.HasMethod("EsperandoEnroqueDe")) return false;
			try { return (bool)campo.Call("EsperandoEnroqueDe", this); } catch { return false; }
		}
	}

	public override void CancelarSeleccionPendiente()
	{
		if (!SeleccionPendiente) return;
		var campo = GetTree()?.Root?.FindChild("Campo1", true, false);
		if (campo != null && campo.HasMethod("CancelarSeleccionEnroque")) campo.Call("CancelarSeleccionEnroque");
	}
}
