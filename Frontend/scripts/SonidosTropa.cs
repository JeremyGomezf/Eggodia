using Godot;
using System.Collections.Generic;

/// <summary>
/// Sonidos de cada personaje: ataque, daño, defensa, habilidad, muerte y diálogo.
///
/// Los audios viven en res://efectos/sonidos/tropas/&lt;carpeta&gt;/&lt;evento&gt;.ogg (convertidos a OGG Vorbis
/// desde los originales del Drive "Sonido de personajes eggodia"). Cada tropa busca su carpeta por
/// el nombre de su CLASE (en combate) o por el nombre de su ESCENA (en el Constructor de mazo).
///
/// Reglas:
///   · Si una tropa no tiene sonido de HABILIDAD, usa el de ATAQUE.
///   · Si falta cualquier otro audio (o la tropa no tiene carpeta), simplemente no suena nada.
///   · Las 5 piezas de ajedrez comparten ataque/daño/defensa/muerte y tienen diálogo propio.
///
/// Los reproductores se cuelgan de la escena actual (no de la tropa), así un sonido de muerte
/// sigue sonando aunque la tropa ya se haya liberado.
/// </summary>
public static class SonidosTropa
{
	private const string RUTA_BASE  = "res://efectos/sonidos/tropas/";
	private const float  VOLUMEN_DB = -3f;

	// ── AJUSTES DE VOZ POR TROPA/EVENTO ───────────────────────────────────────────────────────
	// Correcciones puntuales sobre VOLUMEN_DB cuando un audio viene más fuerte que el resto.
	private static readonly Dictionary<string, float> AJUSTE_VOLUMEN = new()
	{
		{ "MachiPrime/" + DANO, -7f }, // el quejido del Machi venía bastante más alto que los demás

		// El "ataque" del Granadero es en realidad el lanzamiento + la explosión de la granada
		// (4,5 s): reventaba por encima de todo lo demás. HABILIDAD reusa ese mismo audio.
		{ "GranaderoCartoonPrime/" + ATAQUE,    -8f },
		{ "GranaderoCartoonPrime/" + HABILIDAD, -8f },

		// El Ka-Bar se grabó mucho más bajo que el resto y en batalla casi no se oía (incluido el
		// sonido con el que aparece el fantasma, que usa muerte.ogg).
		{ "KaBarCartoonPrime/" + ATAQUE,    +6f },
		{ "KaBarCartoonPrime/" + DANO,      +6f },
		{ "KaBarCartoonPrime/" + DEFENSA,   +6f },
		{ "KaBarCartoonPrime/" + HABILIDAD, +6f },
		{ "KaBarCartoonPrime/" + MUERTE,    +6f },

		// Diálogos del Constructor de mazo: estos personajes se grabaron más bajo que el resto y
		// casi no se escuchaban al seleccionarlos.
		{ "SoldadoRealPrime/"      + DIALOGO, +5f },
		{ "MachiPrime/"            + DIALOGO, +5f },
		{ "PeonPrime/"             + DIALOGO, +5f },
		{ "DamaPrime/"             + DIALOGO, +5f },
		{ "CamperoCartoonPrime/"   + DIALOGO, +5f },
		{ "KaBarCartoonPrime/"     + DIALOGO, +5f },
	};

	/// <summary>Eventos que hoy NO se reproducen. Las líneas de muerte duran ~3 s y el personaje ya
	/// desapareció mucho antes, así que la voz quedaba sonando sola y descolgada de la animación.
	/// Se apagan por ahora; para volver a activarlas basta con vaciar este conjunto.</summary>
	private static readonly HashSet<string> EVENTOS_SILENCIADOS = new() { MUERTE };

	public const string ATAQUE    = "ataque";
	public const string DANO      = "dano";
	public const string DEFENSA   = "defensa";
	public const string HABILIDAD = "habilidad";
	public const string MUERTE    = "muerte";
	public const string DIALOGO   = "dialogo";

	// Nombre de la CLASE de la tropa → carpeta de sonidos.
	private static readonly Dictionary<string, string> CARPETA_POR_CLASE = new()
	{
		{ "MachiPrime",            "machi" },
		{ "MaguinPrime",           "maguin" },
		{ "SoldadoRealPrime",      "soldado_real" },
		{ "CamperoCartoonPrime",   "campero" },
		{ "GranaderoCartoonPrime", "granadero" },
		{ "KaBarCartoonPrime",     "kabar" },
		{ "SoldadoCartoonPrime",   "soldado_cartoon" },
		{ "TanqueCartoonPrime",    "tanque" },
		{ "ArfilPrime",            "ajedrez" },
		{ "CaballoPrime",          "ajedrez" },
		{ "DamaPrime",             "ajedrez" },
		{ "PeonPrime",             "ajedrez" },
		{ "TorrePrime",            "ajedrez" },
	};

	// Nombre de archivo de la ESCENA (Constructor de mazo) → nombre de la clase, para reusar el mapa de arriba.
	private static readonly Dictionary<string, string> CLASE_POR_ESCENA = new()
	{
		{ "machi_prime",              "MachiPrime" },
		{ "maguin_prime",             "MaguinPrime" },
		{ "soldadoreal_prime",        "SoldadoRealPrime" },
		{ "campero_cartoon_prime",    "CamperoCartoonPrime" },
		{ "granadero_cartoon_prime",  "GranaderoCartoonPrime" },
		{ "ka-bar_cartoon_prime",     "KaBarCartoonPrime" },
		{ "soldado_cartoon_prime",    "SoldadoCartoonPrime" },
		{ "tanque_cartoon_prime",     "TanqueCartoonPrime" },
		{ "arfil_prime",              "ArfilPrime" },
		{ "caballo_prime",            "CaballoPrime" },
		{ "dama_prime",               "DamaPrime" },
		{ "peon_prime",               "PeonPrime" },
		{ "torre_prime",              "TorrePrime" },
	};

	// Las piezas de ajedrez comparten carpeta pero cada una tiene su propio diálogo.
	private static readonly Dictionary<string, string> DIALOGO_AJEDREZ = new()
	{
		{ "ArfilPrime",   "dialogo_alfil" },
		{ "CaballoPrime", "dialogo_caballo" },
		{ "DamaPrime",    "dialogo_dama" },
		{ "PeonPrime",    "dialogo_peon" },
		{ "TorrePrime",   "dialogo_torre" },
	};

	// Caché: ruta → stream (o null si no existe), para no consultar el disco en cada golpe.
	private static readonly Dictionary<string, AudioStream> _cache = new();

	private static AudioStream Cargar(string carpeta, string archivo)
	{
		string ruta = RUTA_BASE + carpeta + "/" + archivo + ".ogg";
		if (_cache.TryGetValue(ruta, out var s)) return s;
		s = ResourceLoader.Exists(ruta) ? GD.Load<AudioStream>(ruta) : null;
		_cache[ruta] = s;
		return s;
	}

	/// <summary>El audio de "evento" para la tropa de clase "clase" (null si no tiene).</summary>
	public static AudioStream Obtener(string clase, string evento)
	{
		if (string.IsNullOrEmpty(clase) || !CARPETA_POR_CLASE.TryGetValue(clase, out string carpeta)) return null;

		if (evento == DIALOGO && DIALOGO_AJEDREZ.TryGetValue(clase, out string dialogoPieza))
			return Cargar(carpeta, dialogoPieza);

		var stream = Cargar(carpeta, evento);
		// Sin sonido propio de habilidad → se usa el de ataque (pedido explícito).
		if (stream == null && evento == HABILIDAD) stream = Cargar(carpeta, ATAQUE);
		return stream;
	}

	/// <summary>Reproduce el sonido "evento" de la tropa "tropa". Nunca falla: si no hay audio, no suena.</summary>
	/// <param name="forzar">Ignora EVENTOS_SILENCIADOS. Lo usa el Ka-Bar, cuyo "muerte.ogg" no es una
	/// despedida sino el sonido de su transformación en fantasma, y se dispara en ese momento exacto.</param>
	public static void Reproducir(Node tropa, string evento, bool forzar = false)
	{
		if (tropa == null || !GodotObject.IsInstanceValid(tropa)) return;
		if (!forzar && EVENTOS_SILENCIADOS.Contains(evento)) return;

		string clase = tropa.GetType().Name;
		float extraDb = AJUSTE_VOLUMEN.TryGetValue(clase + "/" + evento, out float ajuste) ? ajuste : 0f;
		ReproducirStream(tropa, Obtener(clase, evento), extraDb: extraDb);
	}

	/// <summary>Diálogo de la tropa cuya escena es "rutaEscena" (lo usa el selector del Constructor de mazo).</summary>
	public static void ReproducirDialogoPorEscena(Node contexto, string rutaEscena)
	{
		if (string.IsNullOrEmpty(rutaEscena)) return;
		string archivo = rutaEscena.GetFile().GetBaseName().ToLowerInvariant();
		if (!CLASE_POR_ESCENA.TryGetValue(archivo, out string clase)) return;
		float extraDb = AJUSTE_VOLUMEN.TryGetValue(clase + "/" + DIALOGO, out float ajuste) ? ajuste : 0f;
		ReproducirStream(contexto, Obtener(clase, DIALOGO), esDialogo: true, extraDb: extraDb);
	}

	// Un solo diálogo a la vez: al elegir otra carta rápido, el anterior se corta en vez de encimarse.
	private static AudioStreamPlayer _dialogoActual;

	private static void ReproducirStream(Node contexto, AudioStream stream, bool esDialogo = false, float extraDb = 0f)
	{
		if (stream == null || contexto == null || !GodotObject.IsInstanceValid(contexto)) return;
		var tree = contexto.GetTree();
		if (tree == null) return;

		if (esDialogo && _dialogoActual != null && GodotObject.IsInstanceValid(_dialogoActual))
		{
			DuckingMusica.Soltar(); // el diálogo anterior se corta: devuelve su "retención" del ducking
			_dialogoActual.QueueFree();
		}

		var player = new AudioStreamPlayer
		{
			Stream      = stream,
			VolumeDb    = VOLUMEN_DB + extraDb,
			Bus         = GlobalAudioManager.BUS_EFECTOS, // se silencia con el botón EFECTOS de Ajustes
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		(tree.CurrentScene ?? tree.Root).AddChild(player);

		// El "ducking" (bajar la música mientras habla un personaje) se aplica SOLO a los diálogos
		// del Constructor de mazo. En batalla NO: ahí la música se queda a su volumen de ambiente y
		// no se mueve, para que no esté subiendo y bajando con cada golpe.
		if (esDialogo)
		{
			DuckingMusica.Tomar(player);
			player.Finished += () => { DuckingMusica.Soltar(); player.QueueFree(); };
		}
		else
		{
			player.Finished += player.QueueFree;
		}

		player.Play();
		if (esDialogo) _dialogoActual = player;
	}
}
