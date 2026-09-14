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
	public static int    RivalSkinIdx  = 0;   // índice en Preferencias.SKIN_ESCENAS del huevo real del rival
	public static int    RivalTronoIdx = 0;   // índice en Preferencias.TRONO_TEXTURAS del trono real del rival

	public static bool SoyPrimero => Asiento == "A";

	public static void Limpiar()
	{
		Activo = false;
		MatchId = "";
		JugadorId = "";
		Asiento = "A";
		Semilla = "";
		RivalNombre = "Rival";
		RivalSkinIdx = 0;
		RivalTronoIdx = 0;
	}
}
