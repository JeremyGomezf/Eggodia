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
	// Nota: los huevos "exclusivos" (Jeremi, Ecotec, Dorado, Gonza, Carlos) NO tienen entrada
	// aquí a propósito — nunca deben aparecer en Tienda ni ser equipables/sorteables por el bot.
	public static readonly string[] SKIN_ESCENAS = {
		"res://escenas/personajes/reyhuevo1.tscn",
		"res://escenas/personajes/capitanhuevo1.tscn",
		"res://escenas/personajes/dinohuevo1.tscn",
		"res://escenas/personajes/majestadhuevo1.tscn",
		"res://escenas/personajes/paperdinohuevo1.tscn",
		"res://escenas/personajes/coronelhuevo1.tscn",
		"res://escenas/personajes/huevorosa1.tscn",
		"res://escenas/personajes/majestadhuevo2_1.tscn",
	};
	public static readonly int[] SKIN_PRECIOS = { 0, 250, 250, 250, 350, 400, 400, 450 };
	public static readonly string[] SKIN_NOMBRES = {
		"Rey Huevo", "Capitán Huevo", "Dino Huevo", "Majestad Huevo", "Paper Dino Huevo",
		"Coronel Huevo", "Huevo Rosa", "Majestad Huevo II"
	};
	// Renders unificados (todos 600x661, mismo tamaño real) — Paper Dino Huevo ya tiene su propio
	// render estático igual que los demás, no hace falta ningún caso especial animado.
	public static readonly string[] SKIN_IMAGENES = {
		"res://imagenes/RendersTropa/Huevo render/ReyHuevo_Render.png",
		"res://imagenes/RendersTropa/Huevo render/CapitanHuevo_Render.png",
		"res://imagenes/RendersTropa/Huevo render/DinoHuevo_Render.png",
		"res://imagenes/RendersTropa/Huevo render/MajestadHuevo_Render.png",
		"res://imagenes/RendersTropa/Huevo render/PaperDinoHuevo_Render.png",
		"res://imagenes/RendersTropa/Huevo render/CoronalHuevo_Render.png",
		"res://imagenes/RendersTropa/Huevo render/HuevoRosa_Render.png",
		"res://imagenes/RendersTropa/Huevo render/MajestadHuevo2_Render.png",
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

	public static string SkinExclusivaActiva
	{
		get => LeerStringEn(SEC_SKINS, "exclusiva_activa", "");
		set => EscribirStringEn(SEC_SKINS, "exclusiva_activa", value ?? "");
	}

	// Mapeo textura exclusiva (la que usan MenuPrincipal/Tienda para mostrarla) → escena de
	// personaje (la que necesita Campo1 para mostrarla EN BATALLA, arriba del trono). Antes solo
	// existía la textura — en partida siempre se caía a Rey Huevo aunque tuvieras una exclusiva
	// equipada, porque nada sabía a qué escena correspondía.
	public static readonly (string textura, string escena)[] SKINS_EXCLUSIVAS_ESCENAS = {
		("res://imagenes/RendersTropa/Huevo render/HuevoDorado_Render.png", "res://escenas/personajes/huevodorado1.tscn"),
		("res://imagenes/RendersTropa/Huevo render/HuevoEcotec_Render.png", "res://escenas/personajes/huevoecotec1.tscn"),
		("res://imagenes/RendersTropa/Huevo render/JeremyHuevo_Render.png", "res://escenas/personajes/huevojeremy1.tscn"),
		("res://imagenes/RendersTropa/Huevo render/CarlosHuevo_Render.png", "res://escenas/personajes/huevocarlos1.tscn"),
		("res://imagenes/RendersTropa/Huevo render/GonzaHuevo_Render.png",  "res://escenas/personajes/huevogonzalo1.tscn"),
	};

	// ── SKIN PARA EL RIVAL EN LÍNEA ──────────────────────────────────────────
	// Por la red viaja un solo int con la skin equipada. Antes ese int solo podía apuntar a
	// SKIN_ESCENAS (las estándar), así que si tenías una skin EXCLUSIVA equipada el rival te veía
	// con un huevo común. Ahora el índice cubre los dos arreglos: 0..N-1 son las estándar y de N
	// en adelante las exclusivas (N + posición en SKINS_EXCLUSIVAS_ESCENAS).
	// Compatibilidad: un cliente viejo manda 0..N-1 y se lee igual que siempre; si recibe un
	// índice extendido que no entiende, cae a una skin estándar en vez de romperse.
	public static int IndiceSkinParaOnline()
	{
		string exclusiva = SkinExclusivaActiva;
		if (!string.IsNullOrEmpty(exclusiva))
		{
			for (int i = 0; i < SKINS_EXCLUSIVAS_ESCENAS.Length; i++)
				if (SKINS_EXCLUSIVAS_ESCENAS[i].textura == exclusiva)
					return SKIN_ESCENAS.Length + i;
		}
		return SkinActivaIdx;
	}

	/// <summary>Ruta de escena de la skin que representa un índice recibido por la red (ver
	/// IndiceSkinParaOnline). Devuelve una estándar si el índice no corresponde a ninguna exclusiva.</summary>
	public static string EscenaSkinDesdeIndiceOnline(int idx)
	{
		int exclusivaIdx = idx - SKIN_ESCENAS.Length;
		if (exclusivaIdx >= 0 && exclusivaIdx < SKINS_EXCLUSIVAS_ESCENAS.Length)
			return SKINS_EXCLUSIVAS_ESCENAS[exclusivaIdx].escena;

		return SKIN_ESCENAS[Mathf.Clamp(idx, 0, SKIN_ESCENAS.Length - 1)];
	}

	public static bool TieneSkinExclusiva(string ruta)
	{
		if (string.IsNullOrEmpty(ruta)) return false;
		return LeerBoolEn(SEC_SKINS, $"owned_exclusiva_{ruta.GetHashCode()}", false);
	}

	public static void DesbloquearSkinExclusiva(string ruta)
	{
		if (string.IsNullOrEmpty(ruta)) return;
		EscribirBoolEn(SEC_SKINS, $"owned_exclusiva_{ruta.GetHashCode()}", true);
	}

	// ── SKINS DE TRONO ────────────────────────────────────────────────────────
	public static readonly string[] TRONO_TEXTURAS = {
		"res://imagenes/Tronos/TronoReal.png",    // idx 0 = default, gratis
		"res://imagenes/Tronos/TronoPapel.png",
		"res://imagenes/Tronos/TronoPiedra.png",
		"res://imagenes/Tronos/TronoRocoso.png",
		"res://imagenes/Tronos/BombaTrono.png",
		"res://imagenes/Tronos/TronoAjedrez.png",
		"res://imagenes/Tronos/TronoCofre.png",
		"res://imagenes/Tronos/TronoDado.png",
	};
	public static readonly int[] TRONO_PRECIOS = { 0, 200, 200, 200, 250, 250, 300, 300 };
	public static readonly string[] TRONO_NOMBRES = {
		"Trono Real", "Trono de Papel", "Trono de Piedra", "Trono Rocoso",
		"Trono Bomba", "Trono Ajedrez", "Trono Cofre", "Trono Dado"
	};

	private const string SEC_TRONOS = "tronos";

	public static int TronoActivoIdx
	{
		get => LeerIntEn(SEC_TRONOS, "activo", 0);
		set => EscribirIntEn(SEC_TRONOS, "activo", Mathf.Clamp(value, 0, TRONO_TEXTURAS.Length - 1));
	}

	public static string RutaTronoActiva => TRONO_TEXTURAS[TronoActivoIdx];

	public static bool TieneTrono(int idx)
	{
		if (idx == 0) return true;
		return LeerBoolEn(SEC_TRONOS, $"owned_{idx}", false);
	}

	public static void DesbloquearTrono(int idx)
	{
		EscribirBoolEn(SEC_TRONOS, $"owned_{idx}", true);
	}

	// ── COMPRAS DE CARTAS Y HECHIZOS (TIENDA) ──────────────────────────────────
	private const string SEC_TIENDA_ITEMS = "tienda_items";

	public static readonly string[] TIENDA_TROPA_IDS = {
		"kabar", "soldadocartoon", "campero", "granadero", "tiburon", "calamar", "tanque"
	};
	public static readonly string[] TIENDA_TROPA_NOMBRES = {
		"Ka-Bar", "Soldado Cartoon", "Campero", "Granadero", "Tiburón", "Calamar Gigante", "Tanque"
	};
	public static readonly int[] TIENDA_TROPA_PRECIOS = {
		500, 500, 500, 1000, 1000, 1500, 1500
	};
	public static readonly string[] TIENDA_TROPA_ICONOS = {
		"res://imagenes/iconos/KabarCA_icon.png",
		"res://imagenes/iconos/SoldadoCA_icon.png",
		"res://imagenes/iconos/CanperoCA_icon.png",
		"res://imagenes/iconos/GranaderoCA_icon.png",
		"res://imagenes/iconos/Tiburon_icon.png",
		"res://imagenes/iconos/Calamar_icon.png",
		"res://imagenes/iconos/TanqueCA_icon.png"
	};

	public static readonly string[] TIENDA_HECHIZO_IDS = {
		"escudo", "desprotegido", "encebollado", "debil"
	};
	public static readonly string[] TIENDA_HECHIZO_NOMBRES = {
		"Escudo", "Desprotegido", "Encebollado", "Débil"
	};
	public static readonly int[] TIENDA_HECHIZO_PRECIOS = {
		300, 300, 400, 300
	};
	public static readonly string[] TIENDA_HECHIZO_ICONOS = {
		"res://imagenes/HechizosPng/Escudo_hechizo.png",
		"res://imagenes/HechizosPng/Desprotegido_hechizo.png",
		"res://imagenes/HechizosPng/Encebo_hechizo.png",
		"res://imagenes/HechizosPng/Debil_hechizo.png"
	};

	public static string NormalizarIdItem(string raw)
	{
		if (string.IsNullOrEmpty(raw)) return "";
		return raw.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace("á", "a").Replace("ó", "o");
	}

	public static bool EsTropaDeTienda(string nombreOId)
	{
		string norm = NormalizarIdItem(nombreOId);
		foreach (var tid in TIENDA_TROPA_IDS)
		{
			if (norm.Contains(tid) || tid.Contains(norm)) return true;
		}
		return false;
	}

	public static bool EsHechizoDeTienda(string nombreOId)
	{
		string norm = NormalizarIdItem(nombreOId);
		foreach (var hid in TIENDA_HECHIZO_IDS)
		{
			if (norm.Contains(hid) || hid.Contains(norm)) return true;
		}
		return false;
	}

	public static bool TieneTropaDesbloqueada(string nombreOId)
	{
		string norm = NormalizarIdItem(nombreOId);
		string matchId = null;
		foreach (var tid in TIENDA_TROPA_IDS)
		{
			if (norm.Contains(tid) || tid.Contains(norm)) { matchId = tid; break; }
		}
		if (matchId == null) return true;
		return LeerBoolEn(SEC_TIENDA_ITEMS, $"tropa_{matchId}", false);
	}

	public static void DesbloquearTropa(string nombreOId)
	{
		string norm = NormalizarIdItem(nombreOId);
		string matchId = norm;
		foreach (var tid in TIENDA_TROPA_IDS)
		{
			if (norm.Contains(tid) || tid.Contains(norm)) { matchId = tid; break; }
		}
		EscribirBoolEn(SEC_TIENDA_ITEMS, $"tropa_{matchId}", true);
	}

	public static bool TieneHechizoDesbloqueado(string nombreOId)
	{
		string norm = NormalizarIdItem(nombreOId);
		string matchId = null;
		foreach (var hid in TIENDA_HECHIZO_IDS)
		{
			if (norm.Contains(hid) || hid.Contains(norm)) { matchId = hid; break; }
		}
		if (matchId == null) return true;
		return LeerBoolEn(SEC_TIENDA_ITEMS, $"hechizo_{matchId}", false);
	}

	public static void DesbloquearHechizo(string nombreOId)
	{
		string norm = NormalizarIdItem(nombreOId);
		string matchId = norm;
		foreach (var hid in TIENDA_HECHIZO_IDS)
		{
			if (norm.Contains(hid) || hid.Contains(norm)) { matchId = hid; break; }
		}
		EscribirBoolEn(SEC_TIENDA_ITEMS, $"hechizo_{matchId}", true);
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

	private static void BorrarSeccion(string sec)
	{
		var cfg = new ConfigFile();
		cfg.Load(RUTA);
		if (cfg.HasSection(sec)) cfg.EraseSection(sec);
		cfg.Save(RUTA);
	}

	/// <summary>Borra TODO lo que es propiedad de la CUENTA (skins, tronos, ítems de tienda y caché de
	/// códigos) del dispositivo. Se llama al CERRAR SESIÓN para que la siguiente cuenta empiece limpia y
	/// no herede nada de la anterior. Las monedas se resetean aparte (Economia). Al iniciar sesión se
	/// vuelve a cargar el inventario real desde el servidor.</summary>
	public static void LimpiarDatosDeCuenta()
	{
		BorrarSeccion(SEC_SKINS);
		BorrarSeccion(SEC_TRONOS);
		BorrarSeccion(SEC_TIENDA_ITEMS);
		BorrarSeccion(SEC_CODIGOS);
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
