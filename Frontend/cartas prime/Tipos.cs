using Godot;

public static class Tipos
{
	public const string FUEGO      = "fuego";
	public const string AGUA       = "agua";
	public const string NATURALEZA = "naturaleza";
	public const string METAL      = "metal";
	public const string SOMBRA     = "sombra";
	public const string NEUTRO     = "neutro";

	// Cadena de ventajas (5-way): fuego → naturaleza → metal → sombra → agua → fuego
	public static float Multiplicador(string atk, string def)
	{
		if (atk == def || atk == NEUTRO || def == NEUTRO) return 1.0f;

		bool ventaja =
			(atk == FUEGO      && def == NATURALEZA) ||
			(atk == NATURALEZA && def == METAL)      ||
			(atk == METAL      && def == SOMBRA)     ||
			(atk == SOMBRA     && def == AGUA)       ||
			(atk == AGUA       && def == FUEGO);

		bool desventaja =
			(def == FUEGO      && atk == NATURALEZA) ||
			(def == NATURALEZA && atk == METAL)      ||
			(def == METAL      && atk == SOMBRA)     ||
			(def == SOMBRA     && atk == AGUA)       ||
			(def == AGUA       && atk == FUEGO);

		if (ventaja)    return 1.25f;
		if (desventaja) return 0.80f;
		return 1.0f;
	}

	public static Color Color(string tipo) => tipo switch
	{
		FUEGO      => new Color(1.0f, 0.35f, 0.20f),
		AGUA       => new Color(0.25f, 0.55f, 1.0f),
		NATURALEZA => new Color(0.30f, 0.80f, 0.35f),
		METAL      => new Color(0.75f, 0.75f, 0.85f),
		SOMBRA     => new Color(0.55f, 0.30f, 0.75f),
		_          => new Color(0.6f, 0.6f, 0.6f),
	};

	public static string Etiqueta(string tipo) => tipo switch
	{
		FUEGO      => "Fuego",
		AGUA       => "Agua",
		NATURALEZA => "Naturaleza",
		METAL      => "Metal",
		SOMBRA     => "Sombra",
		_          => "Neutro",
	};

	public static string Inicial(string tipo) => tipo switch
	{
		FUEGO      => "F",
		AGUA       => "A",
		NATURALEZA => "N",
		METAL      => "M",
		SOMBRA     => "S",
		_          => "·",
	};
}
