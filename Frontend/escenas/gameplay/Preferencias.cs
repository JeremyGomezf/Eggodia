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

	// ── NIVEL / EXPERIENCIA ──────────────────────────────────────────────────
	// +150 XP por cada victoria. Umbral nivel 2 = 1000, nivel 3 = 1500, nivel 4 = 2000...
	// (1000 + 500 por cada nivel adicional desde el 2).
	public const int XP_POR_VICTORIA = 150;

	public static int ExperienciaTotal
	{
		get => LeerIntEn(SECCION, "experiencia_total", 0);
		set => EscribirIntEn(SECCION, "experiencia_total", Mathf.Max(0, value));
	}

	public static int UmbralParaNivel(int nivel) => nivel <= 1 ? 0 : 1000 + (nivel - 2) * 500;

	public static int Nivel
	{
		get
		{
			int xp = ExperienciaTotal;
			int nivel = 1;
			while (xp >= UmbralParaNivel(nivel + 1)) nivel++;
			return nivel;
		}
	}

	public static void AgregarExperiencia(int cantidad) => ExperienciaTotal += cantidad;

	// ── ESTADÍSTICAS (para el perfil del jugador) ────────────────────────────
	public static int PartidasGanadas
	{
		get => LeerIntEn(SECCION, "partidas_ganadas", 0);
		set => EscribirIntEn(SECCION, "partidas_ganadas", Mathf.Max(0, value));
	}

	// ── TROFEOS HUEVO (tabla de líderes) ─────────────────────────────────────
	// A propósito NO se suma al ganarle al bot — solo cuentan victorias ONLINE reales (5 por
	// victoria). Hoy no hay matchmaking online todavía, así que este valor queda en 0 hasta que
	// exista esa función; ver Campo1.FinPartida.cs / MenuPrincipal.Online.cs para el gancho.
	public static int TrofeosHuevo
	{
		get => LeerIntEn(SECCION, "trofeos_huevo", 0);
		private set => EscribirIntEn(SECCION, "trofeos_huevo", Mathf.Max(0, value));
	}
	public static void GanarTrofeos(int cantidad) => TrofeosHuevo += cantidad;

	public static int PartidasPerdidas
	{
		get => LeerIntEn(SECCION, "partidas_perdidas", 0);
		set => EscribirIntEn(SECCION, "partidas_perdidas", Mathf.Max(0, value));
	}

	private const string SEC_USO_CARTAS   = "uso_cartas";
	private const string SEC_USO_HECHIZOS = "uso_hechizos";

	/// <summary>Suma un uso de <paramref name="nombreCarta"/> (p. ej. GetType().Name de la
	/// tropa) al contador persistente — solo debe llamarse para invocaciones del JUGADOR.</summary>
	public static void RegistrarUsoCarta(string nombreCarta)
	{
		if (string.IsNullOrEmpty(nombreCarta)) return;
		int actual = LeerIntEn(SEC_USO_CARTAS, nombreCarta, 0);
		EscribirIntEn(SEC_USO_CARTAS, nombreCarta, actual + 1);
	}

	public static void RegistrarUsoHechizo(string nombreHechizo)
	{
		if (string.IsNullOrEmpty(nombreHechizo)) return;
		int actual = LeerIntEn(SEC_USO_HECHIZOS, nombreHechizo, 0);
		EscribirIntEn(SEC_USO_HECHIZOS, nombreHechizo, actual + 1);
	}

	/// <summary>Nombre de la clave con más usos dentro de la sección dada, o null si no hay
	/// ninguna registrada todavía.</summary>
	private static string MasUsadoEn(string seccion)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA) != Error.Ok || !cfg.HasSection(seccion)) return null;
		string mejor = null; int max = 0;
		foreach (string clave in cfg.GetSectionKeys(seccion))
		{
			int usos = (int)cfg.GetValue(seccion, clave, 0);
			if (usos > max) { max = usos; mejor = clave; }
		}
		return mejor;
	}

	public static string CartaMasUsada()   => MasUsadoEn(SEC_USO_CARTAS);
	public static string HechizoMasUsado() => MasUsadoEn(SEC_USO_HECHIZOS);

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
		"res://imagenes/PersonajesPng/PaperDinoHuevoIcon.tres",
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

	// ── CÓDIGOS DE CANJE ──────────────────────────────────────────────────────
	private const string SEC_CODIGOS = "codigos_canjeados";
	public static bool CodigoYaCanjeado(string codigo) => LeerBoolEn(SEC_CODIGOS, codigo.ToUpperInvariant(), false);
	public static void MarcarCodigoCanjeado(string codigo) => EscribirBoolEn(SEC_CODIGOS, codigo.ToUpperInvariant(), true);

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

	private static string LeerStringEn(string sec, string clave, string porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RUTA) != Error.Ok) return porDefecto;
		return (string)cfg.GetValue(sec, clave, porDefecto);
	}

	private static void EscribirStringEn(string sec, string clave, string valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RUTA);
		cfg.SetValue(sec, clave, valor);
		cfg.Save(RUTA);
	}

	// ── SESIÓN PERSISTENTE (login recordado entre reinicios) ──────────────────
	private const string SEC_SESION = "sesion";

	public static int SesionUsuarioId
	{
		get => LeerIntEn(SEC_SESION, "usuario_id", -1);
		set => EscribirIntEn(SEC_SESION, "usuario_id", value);
	}

	public static string SesionNombre
	{
		get => LeerStringEn(SEC_SESION, "nombre", "");
		set => EscribirStringEn(SEC_SESION, "nombre", valor: value);
	}

	public static void GuardarSesion(int usuarioId, string nombre)
	{
		SesionUsuarioId = usuarioId;
		SesionNombre = nombre ?? "";
	}

	public static void CerrarSesionGuardada()
	{
		SesionUsuarioId = -1;
		SesionNombre = "";
	}
}
