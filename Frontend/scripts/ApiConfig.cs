using Godot;

/// <summary>
/// Configuración central de la URL del backend Eggodia.
/// En PC usa localhost; en móvil (Android/iOS) usa la IP de la PC en la red Wi-Fi
/// — el celular DEBE estar en la MISMA red Wi-Fi que la PC, y el backend debe
/// escuchar en la LAN (dotnet run --urls http://0.0.0.0:5289).
/// </summary>
public static class ApiConfig
{
	// ⚠️ IP de tu PC en la red local (Wi-Fi). Cámbiala si tu red cambia.
	private const string HOST_LAN = "192.168.1.18";
	private const int    PUERTO   = 5289;

	public static string Base =>
		OS.HasFeature("mobile")
			? $"http://{HOST_LAN}:{PUERTO}"
			: $"http://localhost:{PUERTO}";

	public static string Usuarios  => $"{Base}/api/usuarios";
	public static string Cartas    => $"{Base}/api/cartas";
	public static string Ranking   => $"{Base}/api/usuarios/ranking";
	public static string Resultado => $"{Base}/api/usuarios/resultado";
}
