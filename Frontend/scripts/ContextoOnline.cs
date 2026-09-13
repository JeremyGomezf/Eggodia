/// <summary>
/// Contexto de una partida en línea, compartido entre el emparejamiento (MatchmakingOnline) y la
/// escena de batalla (Campo1). Es estático para sobrevivir al cambio de escena. Se activa al
/// emparejar y se limpia al volver al menú o al empezar una partida local (VS BOT / Jugar).
/// </summary>
public static class ContextoOnline
{
	public static bool   Activo      = false;
	public static string MatchId     = "";
	public static string JugadorId   = "";   // id propio (u{UsuarioId})
	public static string Asiento     = "A";   // "A" empieza la partida, "B" espera el primer turno
	public static string Semilla     = "";    // RNG compartido (para sincronía en el Milestone B)
	public static string RivalNombre = "Rival";

	public static bool SoyPrimero => Asiento == "A";

	public static void Limpiar()
	{
		Activo = false;
		MatchId = "";
		JugadorId = "";
		Asiento = "A";
		Semilla = "";
		RivalNombre = "Rival";
	}
}
