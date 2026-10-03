using Godot;

/// <summary>
/// Configuración central de la URL del backend Eggodia.
///
/// Por defecto el juego usa el SERVIDOR DE PRODUCCIÓN en la nube
/// (https://enyooichat.cloud, backend .NET 8 detrás de Cloudflare Tunnel),
/// así funciona desde cualquier lugar y en cualquier celular sin estar en una
/// red Wi-Fi concreta.
///
/// Si estás DESARROLLANDO el backend en tu propia PC (dotnet run en localhost:5289),
/// pon USAR_BACKEND_LOCAL = true para que el juego apunte a tu servidor local.
/// </summary>
public static class ApiConfig
{
	// true SOLO para desarrollo local del backend en tu PC. Normal: false (usa producción).
	private const bool USAR_BACKEND_LOCAL = false;

	private const string SERVIDOR_PROD  = "https://enyooichat.cloud";
	private const int    PUERTO_LOCAL   = 5289;

	// Servidor de PRUEBAS sin tocar el código:
	//   • PC: variable de entorno EGGODIA_API (p. ej. http://localhost:5299).
	//   • Celular conectado por USB: archivo user://servidor_pruebas.txt con la URL. Solo se puede crear
	//     con adb en un APK de depuración (run-as) y se usa junto con `adb reverse tcp:5299 tcp:5299`.
	// En los celulares de los jugadores no existe ninguno de los dos → producción.
	private static readonly string _baseEntorno = LeerServidorDePruebas();

	private static string LeerServidorDePruebas()
	{
		string entorno = OS.GetEnvironment("EGGODIA_API");
		if (!string.IsNullOrEmpty(entorno)) return entorno;
		const string ARCHIVO = "user://servidor_pruebas.txt";
		return FileAccess.FileExists(ARCHIVO) ? FileAccess.GetFileAsString(ARCHIVO).Trim() : "";
	}

	public static string Base =>
		!string.IsNullOrEmpty(_baseEntorno) ? _baseEntorno.TrimEnd('/')
		: USAR_BACKEND_LOCAL ? $"http://localhost:{PUERTO_LOCAL}" : SERVIDOR_PROD;

	public static string Usuarios  => $"{Base}/api/usuarios";
	public static string Cartas    => $"{Base}/api/cartas";
	public static string Ranking   => $"{Base}/api/usuarios/ranking";
	public static string Resultado => $"{Base}/api/usuarios/resultado";
	public static string ReclamarCartaFisica => $"{Base}/api/cartas/claim-physical-card";
	public static string CodigosCanjear     => $"{Base}/api/codigos/canjear";
	public static string CodigosSkins(int userId) => $"{Base}/api/codigos/usuario/{userId}/skins";

	/// <summary>Cabeceras para pedirle algo al servidor: JSON y, si hay cuenta, su sesión. Sin la sesión
	/// el servidor no deja tocar nada de la cuenta (monedas, mazo, compras, partidas en línea).</summary>
	public static string[] Cabeceras(bool json = true)
	{
		var lista = new System.Collections.Generic.List<string>();
		if (json) lista.Add("Content-Type: application/json");
		string token = Preferencias.SesionToken;
		if (!string.IsNullOrEmpty(token) && SesionJuego.Instance != null && SesionJuego.Instance.EstaLogueado)
			lista.Add("Authorization: Bearer " + token);
		return lista.ToArray();
	}

	/// <summary>¿El servidor dijo que la sesión ya no vale (contraseña cambiada, sesión vencida o APK sin
	/// sesión cuando ya se exige)? En ese caso se cierra la sesión y se pide entrar de nuevo.</summary>
	public static bool SesionVencida(long codigo, byte[] cuerpo)
	{
		if (codigo != 401 || cuerpo == null || cuerpo.Length == 0) return false;
		if (!System.Text.Encoding.UTF8.GetString(cuerpo).Contains("\"sesionInvalida\":true")) return false;
		SesionJuego.Instance?.SesionVencida();
		return true;
	}

	// WebSocket del multijugador en tiempo real (http→ws, https→wss).
	public static string WsBase =>
		Base.StartsWith("https://") ? "wss://" + Base.Substring("https://".Length)
		                            : "ws://"  + Base.Substring("http://".Length);
	public static string WsMatch(string matchId, string jugadorId) =>
		$"{WsBase}/ws/match?matchId={matchId}&jugadorId={jugadorId}";
}
