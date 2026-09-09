using System.Text;

/// <summary>
/// Tipo de combate de una tropa (rol dentro del mazo).
/// </summary>
public enum TipoTropa { Tactico, Asesino, Coloso, Desconocido }

/// <summary>
/// Serie / facción temática de una tropa.
/// </summary>
public enum SerieTropa { Ajedrez, Toon, Medieval, Pacifico, Papeleo, Desconocido }

/// <summary>
/// Fuente ÚNICA de verdad para clasificar cada carta por Tipo (Táctico/Asesino/Coloso),
/// Serie (Ajedrez/Toon/Medieval/Pacífico/Papeleo) y su imagen grande de batalla (CartasPng).
///
/// Se usa tanto en el constructor de mazo (MenuConstructor) como en la batalla (Campo1),
/// clasificando por la ruta de escena de la tropa o por su nombre — lo que esté disponible.
/// </summary>
public static class ClasificacionCartas
{
	public struct Info
	{
		public TipoTropa  Tipo;
		public SerieTropa Serie;
		public string     CartaPng; // imagen grande para la mano de batalla
	}

	// Cada entrada: (palabraClave normalizada, Tipo, Serie, imagen de batalla).
	// La palabra clave debe ser un substring único del nombre/ruta normalizado de esa tropa.
	private static readonly (string clave, TipoTropa tipo, SerieTropa serie, string png)[] TABLA =
	{
		// ── AJEDREZ ──
		("peon",           TipoTropa.Tactico, SerieTropa.Ajedrez,  "res://imagenes/CartasPng/Peon_Cart.png"),
		("torre",          TipoTropa.Coloso,  SerieTropa.Ajedrez,  "res://imagenes/CartasPng/Torre_Cart.png"),
		("arfil",          TipoTropa.Tactico, SerieTropa.Ajedrez,  "res://imagenes/CartasPng/Arfil_Cart.png"),
		("caballo",        TipoTropa.Tactico, SerieTropa.Ajedrez,  "res://imagenes/CartasPng/Caballo_Cart.png"),
		("dama",           TipoTropa.Asesino, SerieTropa.Ajedrez,  "res://imagenes/CartasPng/Dama_Cart.png"),
		// ── TOON ──
		("soldadocartoon", TipoTropa.Asesino, SerieTropa.Toon,     "res://imagenes/CartasPng/SoldCartoon_Cart.png"),
		("tanque",         TipoTropa.Coloso,  SerieTropa.Toon,     "res://imagenes/CartasPng/Tanque_Cart.png"),
		("granadero",      TipoTropa.Asesino, SerieTropa.Toon,     "res://imagenes/CartasPng/Granadero_Cart.png"),
		("kabar",          TipoTropa.Asesino, SerieTropa.Toon,     "res://imagenes/CartasPng/Kabar_Cart.png"),
		("campero",        TipoTropa.Tactico, SerieTropa.Toon,     "res://imagenes/CartasPng/Campero_Cart.png"),
		// ── MEDIEVAL ──
		("soldadoreal",    TipoTropa.Tactico, SerieTropa.Medieval, "res://imagenes/CartasPng/SoldReal_Cart.png"),
		("maguin",         TipoTropa.Tactico, SerieTropa.Medieval, "res://imagenes/CartasPng/Maguin_Cart.png"),
		("dragon",         TipoTropa.Asesino, SerieTropa.Medieval, "res://imagenes/CartasPng/DragonFlama_Cart.png"),
		("golem",          TipoTropa.Coloso,  SerieTropa.Medieval, "res://imagenes/CartasPng/GolemPedregal_Cart.png"),
		// ── PACIFICO ──
		("tiburon",        TipoTropa.Asesino, SerieTropa.Pacifico, "res://imagenes/CartasPng/Tiburon_Cart.png"),
		("calamar",        TipoTropa.Coloso,  SerieTropa.Pacifico, "res://imagenes/CartasPng/Calamar_Cart.png"),
		// ── PAPELEO ── (Paper-Rex: escena Paper_Rex.tscn o TRex_prime.tscn, ambas contienen "rex")
		("rex",            TipoTropa.Coloso,  SerieTropa.Papeleo,  "res://imagenes/CartasPng/PapeRex_Cart.png"),
	};

	/// <summary>Normaliza: minúsculas y solo caracteres a-z 0-9 (sin espacios, guiones, tildes).</summary>
	public static string Normalizar(string s)
	{
		if (string.IsNullOrEmpty(s)) return "";
		var sb = new StringBuilder(s.Length);
		foreach (char c0 in s.ToLowerInvariant())
		{
			char c = c0 switch { 'á' => 'a', 'é' => 'e', 'í' => 'i', 'ó' => 'o', 'ú' => 'u', 'ñ' => 'n', _ => c0 };
			if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
		}
		return sb.ToString();
	}

	/// <summary>Clasifica una carta por su ruta de escena o su nombre.
	/// Se puede pasar ambas (ruta preferida) para más robustez.</summary>
	public static Info Clasificar(string rutaEscena, string nombre = "")
	{
		string norm = Normalizar(rutaEscena) + "|" + Normalizar(nombre);
		foreach (var e in TABLA)
		{
			if (norm.Contains(e.clave))
				return new Info { Tipo = e.tipo, Serie = e.serie, CartaPng = e.png };
		}
		return new Info { Tipo = TipoTropa.Desconocido, Serie = SerieTropa.Desconocido, CartaPng = "" };
	}

	public static TipoTropa  TipoDe(string ruta, string nombre = "")  => Clasificar(ruta, nombre).Tipo;
	public static SerieTropa SerieDe(string ruta, string nombre = "") => Clasificar(ruta, nombre).Serie;

	/// <summary>Imagen grande de batalla (CartasPng) para la ruta de escena de una tropa.</summary>
	public static string ImagenBatalla(string rutaEscena, string nombre = "") => Clasificar(rutaEscena, nombre).CartaPng;

	// ── ETIQUETAS PARA BÚSQUEDA ──
	public static string NombreTipo(TipoTropa t) => t switch
	{
		TipoTropa.Tactico => "tactico",
		TipoTropa.Asesino => "asesino",
		TipoTropa.Coloso  => "coloso",
		_                 => ""
	};

	public static string NombreSerie(SerieTropa s) => s switch
	{
		SerieTropa.Ajedrez  => "ajedrez",
		SerieTropa.Toon     => "toon",
		SerieTropa.Medieval => "medieval",
		SerieTropa.Pacifico => "pacifico",
		SerieTropa.Papeleo  => "papeleo",
		_                   => ""
	};

	/// <summary>True si la carta coincide con la búsqueda: por nombre, por tipo
	/// (tactico/asesino/coloso) o por serie (ajedrez/toon/medieval/pacifico/papeleo).</summary>
	public static bool CoincideBusqueda(string rutaEscena, string nombre, string query)
	{
		if (string.IsNullOrEmpty(query)) return true;
		string q = Normalizar(query);
		if (q.Length == 0) return true;

		// Coincidencia por nombre visible
		if (Normalizar(nombre).Contains(q)) return true;

		var info = Clasificar(rutaEscena, nombre);
		// Coincidencia por tipo (acepta singular y plural: tactico/tacticos, etc.)
		string tipo = NombreTipo(info.Tipo);
		if (tipo.Length > 0 && (tipo.Contains(q) || q.Contains(tipo))) return true;
		// Coincidencia por serie
		string serie = NombreSerie(info.Serie);
		if (serie.Length > 0 && (serie.Contains(q) || q.Contains(serie))) return true;

		return false;
	}
}
