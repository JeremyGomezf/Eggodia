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

	public static string Base =>
		USAR_BACKEND_LOCAL ? $"http://localhost:{PUERTO_LOCAL}" : SERVIDOR_PROD;

	public static string Usuarios  => $"{Base}/api/usuarios";
	public static string Cartas    => $"{Base}/api/cartas";
	public static string Ranking   => $"{Base}/api/usuarios/ranking";
	public static string Resultado => $"{Base}/api/usuarios/resultado";
}
