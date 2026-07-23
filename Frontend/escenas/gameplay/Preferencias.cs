using Godot;

/// <summary>
/// PREFERENCIAS — Helper estático de ajustes persistentes (user://preferencias.cfg).
/// Guarda flags de progreso que sobreviven al cierre del juego, como si el jugador
/// ya vio el tutorial. No necesita ser AutoLoad: lee/escribe en disco bajo demanda.
/// </summary>
public static class Preferencias
{
	private const string RUTA    = "user://preferencias.cfg";
	private const string SECCION = "progreso";

	/// <summary>True si el jugador ya vio la pantalla "Cómo Jugar" al menos una vez.</summary>
	public static bool TutorialVisto
	{
		get => LeerBool("tutorial_visto", false);
		set => EscribirBool("tutorial_visto", value);
	}

	// ── Internos ─────────────────────────────────────────────────────────
	private static bool LeerBool(string clave, bool porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA) != Error.Ok) return porDefecto;
		return (bool)cfg.GetValue(SECCION, clave, porDefecto);
	}

	private static void EscribirBool(string clave, bool valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RUTA);   // conserva otras claves si existen
		cfg.SetValue(SECCION, clave, valor);
		cfg.Save(RUTA);
	}
}
