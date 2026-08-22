using Godot;

/// <summary>
/// PREFERENCIAS — Helper estático de ajustes persistentes (user://preferencias.cfg).
/// </summary>
public static class Preferencias
{
	private const string RUTA    = "user://preferencias.cfg";
	private const string SECCION = "progreso";
	private const string SEC_SKINS = "skins";

	// ── TUTORIAL ──────────────────────────────────────────────────────────────
	public static bool TutorialVisto
	{
		get => LeerBool("tutorial_visto", false);
		set => EscribirBool("tutorial_visto", value);
	}

	// ── SKINS DE HUEVO ────────────────────────────────────────────────────────
	public static readonly string[] SKIN_ESCENAS = {
		"res://escenas/personajes/reyhuevo1.tscn",
		"res://escenas/personajes/capitanhuevo1.tscn",
		"res://escenas/personajes/dinohuevo1.tscn",
		"res://escenas/personajes/majestadhuevo1.tscn",
		"res://escenas/personajes/paperdinohuevo1.tscn",
	};
	public static readonly int[] SKIN_PRECIOS = { 0, 250, 250, 250, 350 };
	public static readonly string[] SKIN_NOMBRES = {
		"Rey Huevo", "Capitán Huevo", "Dino Huevo", "Majestad Huevo", "Paper Dino Huevo"
	};
	public static readonly string[] SKIN_IMAGENES = {
		"res://imagenes/PersonajesPng/ReyHuevo.png",
		"res://imagenes/PersonajesPng/CapitanHuevo.png",
		"res://imagenes/PersonajesPng/DinoHuevo.png",
		"res://imagenes/PersonajesPng/MajestadHuevo.png",
		"res://imagenes/PersonajesPng/DinoHuevo.png",
	};

	public static int SkinActivaIdx
	{
		get => LeerIntEn(SEC_SKINS, "activa", 0);
		set => EscribirIntEn(SEC_SKINS, "activa", Mathf.Clamp(value, 0, SKIN_ESCENAS.Length - 1));
	}

	public static string RutaSkinActiva => SKIN_ESCENAS[SkinActivaIdx];

	public static bool TieneSkin(int idx)
	{
		if (idx == 0) return true;
		return LeerBoolEn(SEC_SKINS, $"owned_{idx}", false);
	}

	public static void DesbloquearSkin(int idx)
	{
		EscribirBoolEn(SEC_SKINS, $"owned_{idx}", true);
	}

	// ── Internos ──────────────────────────────────────────────────────────────
	private static bool LeerBool(string clave, bool porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA) != Error.Ok) return porDefecto;
		return (bool)cfg.GetValue(SECCION, clave, porDefecto);
	}

	private static void EscribirBool(string clave, bool valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RUTA);
		cfg.SetValue(SECCION, clave, valor);
		cfg.Save(RUTA);
	}

	private static bool LeerBoolEn(string sec, string clave, bool porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA) != Error.Ok) return porDefecto;
		return (bool)cfg.GetValue(sec, clave, porDefecto);
	}

	private static void EscribirBoolEn(string sec, string clave, bool valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RUTA);
		cfg.SetValue(sec, clave, valor);
		cfg.Save(RUTA);
	}

	private static int LeerIntEn(string sec, string clave, int porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA) != Error.Ok) return porDefecto;
		return (int)cfg.GetValue(sec, clave, porDefecto);
	}

	private static void EscribirIntEn(string sec, string clave, int valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RUTA);
		cfg.SetValue(sec, clave, valor);
		cfg.Save(RUTA);
	}
}
