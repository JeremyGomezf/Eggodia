using Godot;

/// <summary>Interfaz de BtnOnline. Ahora abre el emparejamiento 1v1 real (Fase 1): entra a la cola
/// del backend y espera a que otro jugador entre. Toda la lógica y la UI viven en
/// MatchmakingOnline.cs (nodo autocontenido con su propia CanvasLayer).</summary>
public partial class MenuPrincipal : Control
{
	private MatchmakingOnline _matchmaking;

	private void MostrarPantallaOnline()
	{
		if (_matchmaking != null && IsInstanceValid(_matchmaking)) _matchmaking.QueueFree();
		_matchmaking = new MatchmakingOnline();
		AddChild(_matchmaking);
	}
}
