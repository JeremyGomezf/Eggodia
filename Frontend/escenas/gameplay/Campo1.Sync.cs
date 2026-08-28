using Godot;
using System.Threading.Tasks;

public partial class Campo1 : Node2D
{
	// ── BLOQUEO GLOBAL DEL TABLERO ───────────────────────────────────────
	// Cuenta cuántos efectos/animaciones críticos siguen en curso (muros del Gólem,
	// trayectoria de un proyectil, Enroque, transformación del Peón, etc.). Se acumula
	// porque pueden solaparse; el tablero solo se considera libre cuando llega a 0.
	private int _bloqueosTableroActivos = 0;

	/// <summary>True mientras el tablero está resolviendo una animación/efecto crítico. La IA
	/// debe esperar a que esto sea false antes de emitir cualquier ataque o habilidad — misma
	/// regla para ambos bandos, es un estado del tablero, no de un jugador en particular.</summary>
	public bool HayAnimacionEnCurso => _bloqueosTableroActivos > 0;
	public bool EstadoTableroBloqueado => HayAnimacionEnCurso;

	public void IniciarBloqueoTablero() => _bloqueosTableroActivos++;

	public void FinalizarBloqueoTablero()
	{
		if (_bloqueosTableroActivos > 0) _bloqueosTableroActivos--;
	}

	/// <summary>Espera en bucle (sin congelar el árbol) hasta que el tablero quede 100% libre
	/// de animaciones/efectos pendientes. La IA debe llamarlo antes de cada acción.</summary>
	private async Task EsperarTableroLibre()
	{
		while (HayAnimacionEnCurso && !juegoTerminado)
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
	}
}
