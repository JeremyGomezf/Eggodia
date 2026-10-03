using Godot;

/// <summary>
/// PREFERENCIAS — Helper estático de ajustes persistentes.
///
/// Se guardan en DOS lugares separados a propósito:
///   • DISPOSITIVO (user://preferencias.cfg): solo la sesión recordada y ajustes del aparato
///     (tutorial visto). No pertenece a ningún jugador.
///   • PERFIL: todo lo que PERTENECE al jugador (skins, tronos, ítems, códigos, progreso, uso de
///     cartas y monedas). El invitado y cada cuenta registrada tienen su PROPIO archivo, así nunca
///     se mezclan: entrar a una cuenta no borra lo del invitado, ni al revés.
/// </summary>
public static class Preferencias
{
	private const string RUTA_DISPOSITIVO = "user://preferencias.cfg";
	private const string SEC_DISPOSITIVO  = "dispositivo";
	private const string SECCION = "progreso";
	private const string SEC_SKINS = "skins";

	// ── PERFILES (invitado / cuenta) ──────────────────────────────────────────
	public static string RutaPerfilDe(int usuarioId) =>
		usuarioId > 0 ? $"user://perfil_cuenta_{usuarioId}.cfg" : "user://perfil_invitado.cfg";

	public static string RutaMazoDe(int usuarioId) =>
		usuarioId > 0 ? $"user://mazo_cuenta_{usuarioId}.json" : "user://mazo_invitado.json";

	/// <summary>Perfil ACTIVO: el de la cuenta logueada o el del invitado. Mientras SesionJuego
	/// todavía no existe (arranque), se usa la sesión recordada en disco.</summary>
	public static string RutaPerfil => RutaPerfilDe(SesionJuego.Instance?.UsuarioId ?? SesionUsuarioId);

	private static string RutaDe(string sec) =>
		sec == SEC_SESION || sec == SEC_DISPOSITIVO ? RUTA_DISPOSITIVO : RutaPerfil;

	/// <summary>Migración ÚNICA al sistema de perfiles. Antes todo vivía mezclado en preferencias.cfg
	/// (+ monedas en economia.cfg + mazo en mazo_guardado.json) y era de quien estuviera logueado en
	/// ese momento. La primera vez que corre esta versión, eso se mueve al perfil de la sesión
	/// recordada (cuenta o invitado), así nadie pierde su progreso. Después no vuelve a hacer nada.</summary>
	public static void MigrarAPerfilesSiHaceFalta()
	{
		var disp = new ConfigFile();
		disp.Load(RUTA_DISPOSITIVO); // si no existe (instalación nueva) queda vacío y solo se marca
		if ((bool)disp.GetValue(SEC_DISPOSITIVO, "perfiles_migrados", false)) return;

		int id = (int)disp.GetValue(SEC_SESION, "usuario_id", -1);
		string rutaPerfil = RutaPerfilDe(id);
		var perfil = new ConfigFile();
		perfil.Load(rutaPerfil);

		foreach (string sec in disp.GetSections())
		{
			if (sec == SEC_SESION || sec == SEC_DISPOSITIVO) continue;
			foreach (string clave in disp.GetSectionKeys(sec))
			{
				Variant valor = disp.GetValue(sec, clave);
				if (sec == SECCION && clave == "tutorial_visto")
					disp.SetValue(SEC_DISPOSITIVO, clave, valor); // el tutorial es del aparato
				else if (!perfil.HasSectionKey(sec, clave))
					perfil.SetValue(sec, clave, valor);
			}
			disp.EraseSection(sec);
		}

		// Monedas: vivían en su propio archivo; ahora son una sección más del perfil ("jugador",
		// la misma que usa Economia).
		const string RUTA_ECONOMIA_VIEJA = "user://economia.cfg";
		var eco = new ConfigFile();
		if (eco.Load(RUTA_ECONOMIA_VIEJA) == Error.Ok)
		{
			if (!perfil.HasSectionKey("jugador", "monedas"))
				perfil.SetValue("jugador", "monedas", eco.GetValue("jugador", "monedas", 0));
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(RUTA_ECONOMIA_VIEJA));
		}

		const string RUTA_MAZO_VIEJA = "user://mazo_guardado.json";
		if (FileAccess.FileExists(RUTA_MAZO_VIEJA) && !FileAccess.FileExists(RutaMazoDe(id)))
			DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(RUTA_MAZO_VIEJA),
			                         ProjectSettings.GlobalizePath(RutaMazoDe(id)));

		perfil.Save(rutaPerfil);
		disp.SetValue(SEC_DISPOSITIVO, "perfiles_migrados", true);
		disp.Save(RUTA_DISPOSITIVO);
		GD.Print($"[Preferencias] Datos migrados al perfil {rutaPerfil}");
	}

	// ── TUTORIAL ──────────────────────────────────────────────────────────────
	// Es del DISPOSITIVO (no del perfil): quien ya lo vio en este aparato no lo vuelve a ver al
	// cambiar de cuenta.
	public static bool TutorialVisto
	{
		get => LeerBoolEn(SEC_DISPOSITIVO, "tutorial_visto", false);
		set => EscribirBoolEn(SEC_DISPOSITIVO, "tutorial_visto", value);
	}

	/// <summary>Última versión publicada que informó el servidor (y su enlace de descarga). Es del
	/// DISPOSITIVO. Sirve para bloquear igual en la pantalla de carga si hay una actualización pendiente
	/// aunque esta vez no haya internet o el servidor no responda (ver ChequeoActualizacion).</summary>
	public static string VersionUltimaConocida
	{
		get => LeerStringEn(SEC_DISPOSITIVO, "version_ultima_conocida", "");
		set => EscribirStringEn(SEC_DISPOSITIVO, "version_ultima_conocida", value ?? "");
	}

	public static string UrlDescargaConocida
	{
		get => LeerStringEn(SEC_DISPOSITIVO, "url_descarga_conocida", "");
		set => EscribirStringEn(SEC_DISPOSITIVO, "url_descarga_conocida", value ?? "");
	}

	/// <summary>Tutorial pendiente por instalación NUEVA: el menú lo abre solo la primera vez que se
	/// llega a él, entre como entre (cuenta nueva, cuenta existente o invitado). También es del
	/// DISPOSITIVO.</summary>
	public static bool TutorialPendiente
	{
		get => LeerBoolEn(SEC_DISPOSITIVO, "tutorial_pendiente", false);
		set => EscribirBoolEn(SEC_DISPOSITIVO, "tutorial_pendiente", value);
	}

	/// <summary>Cuántas veces se abrió solo el tutorial obligatorio sin llegar a ganarlo. Es la red de
	/// seguridad para que nadie quede atrapado si algo del tutorial fallara en su celular (ver
	/// MenuPrincipal._Ready): a la 3.ª vez se abre igual, pero ya se puede salir.</summary>
	public static int IntentosTutorial
	{
		get => LeerIntEn(SEC_DISPOSITIVO, "intentos_tutorial", 0);
		set => EscribirIntEn(SEC_DISPOSITIVO, "intentos_tutorial", value);
	}

	/// <summary>Guías de pantalla (ver GuiaPasos): cada una se muestra UNA vez por aparato, la primera
	/// vez que se abre esa pantalla (menú, tienda, mazo, ajustes, pausa contra el bot). Subir
	/// VERSION_GUIAS hace que todas vuelvan a mostrarse una vez (p. ej. tras rediseñarlas).</summary>
	private const string VERSION_GUIAS = "guia2_";

	public static bool GuiaVista(string clave) => LeerBoolEn(SEC_DISPOSITIVO, VERSION_GUIAS + clave, false);

	public static void MarcarGuiaVista(string clave) => EscribirBoolEn(SEC_DISPOSITIVO, VERSION_GUIAS + clave, true);

	/// <summary>Si es la primera vez que corre el juego en este aparato (no existe ni el archivo del
	/// dispositivo), deja el tutorial pendiente. Quien actualiza desde una versión anterior ya tiene
	/// ese archivo → no se le abre nada. Tiene que correr ANTES que cualquier cosa que escriba
	/// preferencias.cfg (ver SesionJuego._Ready).</summary>
	public static void MarcarTutorialSiInstalacionNueva()
	{
		if (FileAccess.FileExists(RUTA_DISPOSITIVO)) return;
		TutorialPendiente = true;
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

	public static int Nivel => NivelDeExperiencia(ExperienciaTotal);

	/// <summary>Nivel que corresponde a una experiencia dada (también sirve para el perfil de otro
	/// jugador, que llega del servidor solo con su experiencia).</summary>
	public static int NivelDeExperiencia(int xp)
	{
		int nivel = 1;
		while (xp >= UmbralParaNivel(nivel + 1)) nivel++;
		return nivel;
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

	/// <summary>Con cuenta, el progreso (experiencia → nivel, victorias, derrotas) es el del SERVIDOR:
	/// así se ve el mismo nivel en cualquier celular donde se inicie sesión. Se adopta al entrar, al
	/// volver al menú y tras cada partida.</summary>
	public static void AdoptarProgresoDeServidor(int experiencia, int victorias, int derrotas)
	{
		var cfg = new ConfigFile();
		cfg.Load(RutaPerfil);
		cfg.SetValue(SECCION, "experiencia_total", Mathf.Max(0, experiencia));
		cfg.SetValue(SECCION, "partidas_ganadas", Mathf.Max(0, victorias));
		cfg.SetValue(SECCION, "partidas_perdidas", Mathf.Max(0, derrotas));
		cfg.Save(RutaPerfil);
	}

	// ── PREMIOS PENDIENTES (partidas terminadas sin internet) ────────────────
	// Si al terminar una partida no hay conexión, el premio (monedas/XP) queda guardado acá y se vuelve
	// a mandar después. El servidor no lo cobra dos veces (cada partida tiene su id).
	private const string SEC_PENDIENTES = "premios_pendientes";

	public static System.Collections.Generic.List<string> PremiosPendientes()
	{
		var lista = new System.Collections.Generic.List<string>();
		var cfg = new ConfigFile();
		if (cfg.Load(RutaPerfil) != Error.Ok || !cfg.HasSection(SEC_PENDIENTES)) return lista;
		foreach (string clave in cfg.GetSectionKeys(SEC_PENDIENTES))
			lista.Add((string)cfg.GetValue(SEC_PENDIENTES, clave, ""));
		return lista;
	}

	public static void GuardarPremioPendiente(string partidaId, string cuerpoJson) =>
		EscribirStringEn(SEC_PENDIENTES, ClavePendiente(partidaId), cuerpoJson);

	public static void QuitarPremioPendiente(string partidaId)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RutaPerfil) != Error.Ok) return;
		string clave = ClavePendiente(partidaId);
		if (!cfg.HasSectionKey(SEC_PENDIENTES, clave)) return;
		cfg.EraseSectionKey(SEC_PENDIENTES, clave);
		cfg.Save(RutaPerfil);
	}

	// Claves de ConfigFile: solo letras/números/guion bajo.
	private static string ClavePendiente(string partidaId) =>
		"p_" + System.Text.RegularExpressions.Regex.Replace(partidaId ?? "", "[^A-Za-z0-9]", "_");

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
		if (cfg.Load(RutaDe(seccion)) != Error.Ok || !cfg.HasSection(seccion)) return null;
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

	// ── FAVORITOS DEL PERFIL (tropa y ardid) ─────────────────────────────────
	// Recién después de PARTIDAS_PARA_FAVORITOS partidas terminadas (ganes, pierdas o empates; no
	// cuentan el tutorial ni rendirse contra el bot) el juego "detecta" tus favoritos. Antes de eso
	// una sola partida decidía todo y el perfil mostraba cualquier cosa.
	//   · Tropa favorita: la que más puntos junta sumando lo que más INVOCAS, las veces que usas su
	//     HABILIDAD y el DAÑO que causó (el mejor resultado). Ver PuntosTropa.
	//   · Ardid favorito: el que más usas.
	public const int PARTIDAS_PARA_FAVORITOS = 3;
	private const string SEC_HAB_CARTAS  = "habilidad_cartas";
	private const string SEC_DAÑO_CARTAS = "dano_cartas";

	/// <summary>Partidas que ya cuentan para los favoritos. Quien ya jugaba antes de que existiera este
	/// contador (tiene usos guardados) arranca con los favoritos ya listos, en vez de volver a esperar.</summary>
	public static int PartidasParaFavoritos
	{
		get
		{
			int v = LeerIntEn(SECCION, "partidas_favoritos", -1);
			if (v >= 0) return v;
			return MasUsadoEn(SEC_USO_CARTAS) != null ? PARTIDAS_PARA_FAVORITOS : 0;
		}
	}

	public static bool FavoritosListos => PartidasParaFavoritos >= PARTIDAS_PARA_FAVORITOS;

	/// <summary>Una habilidad usada por una tropa del JUGADOR (clave = ruta de su escena).</summary>
	public static void RegistrarHabilidadCarta(string rutaEscena)
	{
		if (string.IsNullOrEmpty(rutaEscena)) return;
		EscribirIntEn(SEC_HAB_CARTAS, rutaEscena, LeerIntEn(SEC_HAB_CARTAS, rutaEscena, 0) + 1);
	}

	/// <summary>Cierra una partida para los favoritos: suma el daño que causó cada tropa propia y cuenta
	/// la partida. Se llama una vez por partida terminada.</summary>
	public static void RegistrarPartidaParaFavoritos(System.Collections.Generic.IDictionary<string, int> dañoPorTropa)
	{
		int previas = PartidasParaFavoritos; // incluye el arranque de quien ya jugaba antes
		var cfg = new ConfigFile();
		cfg.Load(RutaPerfil);
		foreach (var par in dañoPorTropa)
		{
			if (string.IsNullOrEmpty(par.Key) || par.Value <= 0) continue;
			cfg.SetValue(SEC_DAÑO_CARTAS, par.Key, (int)cfg.GetValue(SEC_DAÑO_CARTAS, par.Key, 0) + par.Value);
		}
		cfg.SetValue(SECCION, "partidas_favoritos", previas + 1);
		cfg.Save(RutaPerfil);
	}

	// Cada invocación vale 1, cada habilidad usada 2 y cada 300 de daño causado 1.
	private static float PuntosTropa(int invocaciones, int habilidades, int daño) =>
		invocaciones + habilidades * 2f + daño / 300f;

	/// <summary>Ruta de escena de la tropa favorita, o null si todavía no hay partidas suficientes.</summary>
	public static string TropaFavorita()
	{
		if (!FavoritosListos) return null;
		var cfg = new ConfigFile();
		if (cfg.Load(RutaPerfil) != Error.Ok) return null;
		var claves = new System.Collections.Generic.HashSet<string>();
		foreach (string sec in new[] { SEC_USO_CARTAS, SEC_HAB_CARTAS, SEC_DAÑO_CARTAS })
			if (cfg.HasSection(sec)) foreach (string k in cfg.GetSectionKeys(sec)) claves.Add(k);

		string mejor = null; float max = 0f;
		foreach (string k in claves)
		{
			float puntos = PuntosTropa((int)cfg.GetValue(SEC_USO_CARTAS, k, 0),
				(int)cfg.GetValue(SEC_HAB_CARTAS, k, 0), (int)cfg.GetValue(SEC_DAÑO_CARTAS, k, 0));
			if (puntos > max) { max = puntos; mejor = k; }
		}
		return mejor;
	}

	/// <summary>Nombre del ardid favorito, o null si todavía no hay partidas suficientes.</summary>
	public static string ArdidFavorito() => FavoritosListos ? HechizoMasUsado() : null;

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

	// Lo EQUIPADO (skin/exclusiva/trono) se sube a la cuenta en cada cambio: el servidor es quien lo
	// devuelve al abrir la app (AplicarInventario). Antes nunca se subía y por eso siempre volvía el
	// Rey Huevo al reiniciar.
	public static int SkinActivaIdx
	{
		get => LeerIntEn(SEC_SKINS, "activa", 0);
		set
		{
			EscribirIntEn(SEC_SKINS, "activa", Mathf.Clamp(value, 0, SKIN_ESCENAS.Length - 1));
			Economia.Instance?.SolicitarSubirEquipado();
		}
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
		set
		{
			EscribirStringEn(SEC_SKINS, "exclusiva_activa", value ?? "");
			Economia.Instance?.SolicitarSubirEquipado();
		}
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
		set
		{
			EscribirIntEn(SEC_TRONOS, "activo", Mathf.Clamp(value, 0, TRONO_TEXTURAS.Length - 1));
			Economia.Instance?.SolicitarSubirEquipado();
		}
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
		"escudo", "desprotegido", "encebollado", "debil", "nuclear"
	};
	public static readonly string[] TIENDA_HECHIZO_NOMBRES = {
		"Escudo", "Desprotegido", "Encebollado", "Débil", "Nuclear"
	};
	public static readonly int[] TIENDA_HECHIZO_PRECIOS = {
		300, 300, 400, 300, 600
	};
	public static readonly string[] TIENDA_HECHIZO_ICONOS = {
		"res://imagenes/HechizosPng/Escudo_hechizo.png",
		"res://imagenes/HechizosPng/Desprotegido_hechizo.png",
		"res://imagenes/HechizosPng/Encebo_hechizo.png",
		"res://imagenes/HechizosPng/Debil_hechizo.png",
		"res://imagenes/HechizosPng/Nuclear_hechizo.png"
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
	private static bool LeerBoolEn(string sec, string clave, bool porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RutaDe(sec)) != Error.Ok) return porDefecto;
		return (bool)cfg.GetValue(sec, clave, porDefecto);
	}

	private static void EscribirBoolEn(string sec, string clave, bool valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RutaDe(sec));
		cfg.SetValue(sec, clave, valor);
		cfg.Save(RutaDe(sec));
	}

	private static int LeerIntEn(string sec, string clave, int porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RutaDe(sec)) != Error.Ok) return porDefecto;
		return (int)cfg.GetValue(sec, clave, porDefecto);
	}

	private static void EscribirIntEn(string sec, string clave, int valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RutaDe(sec));
		cfg.SetValue(sec, clave, valor);
		cfg.Save(RutaDe(sec));
	}

	private static string LeerStringEn(string sec, string clave, string porDefecto)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(RutaDe(sec)) != Error.Ok) return porDefecto;
		return (string)cfg.GetValue(sec, clave, porDefecto);
	}

	private static void EscribirStringEn(string sec, string clave, string valor)
	{
		var cfg = new ConfigFile();
		cfg.Load(RutaDe(sec));
		cfg.SetValue(sec, clave, valor);
		cfg.Save(RutaDe(sec));
	}

	private static void BorrarSeccion(string sec)
	{
		var cfg = new ConfigFile();
		cfg.Load(RutaDe(sec));
		if (cfg.HasSection(sec)) cfg.EraseSection(sec);
		cfg.Save(RutaDe(sec));
	}

	/// <summary>Vacía skins, tronos, ítems de tienda y caché de códigos del PERFIL ACTIVO. Solo lo usa
	/// Economia.AplicarInventario, justo antes de volver a llenarlo con lo que el servidor dice que tiene
	/// la cuenta (el servidor es la verdad). Ya NO se llama al cerrar sesión: cada perfil tiene su propio
	/// archivo, así que no hace falta borrar nada para que otra cuenta no lo herede.</summary>
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

	/// <summary>Código secreto de la sesión que entregó el servidor al iniciar sesión. Se manda en cada
	/// pedido (ver ApiConfig.Cabeceras) para que el servidor sepa que es de verdad esta cuenta.</summary>
	public static string SesionToken
	{
		get => LeerStringEn(SEC_SESION, "token", "");
		set => EscribirStringEn(SEC_SESION, "token", valor: value ?? "");
	}

	public static void GuardarSesion(int usuarioId, string nombre, string token)
	{
		SesionUsuarioId = usuarioId;
		SesionNombre = nombre ?? "";
		SesionToken = token ?? "";
	}

	public static void CerrarSesionGuardada()
	{
		SesionUsuarioId = -1;
		SesionNombre = "";
		SesionToken = "";
	}
}
