using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// GestorCartas — conecta el juego con el backend Eggodia.
/// Carga las cartas desde la API al iniciar y notifica a Campo1.
///
/// CÓMO USAR:
/// 1. Agrega este script como AutoLoad en Proyecto → Ajustes del Proyecto → AutoLoad
///    Nombre: GestorCartas | Ruta: res://escenas/gameplay/HttpRequest.cs
/// 2. Campo1 llama GestorCartas.Instance.ObtenerCartas() para obtener la lista
/// </summary>
public partial class GestorCartas : Node
{
	// ── SINGLETON ─────────────────────────────────────────────────────────
	public static GestorCartas Instance { get; private set; }

	// ── CONFIG ────────────────────────────────────────────────────────────
	// Cambia esta URL cuando despliegues el backend en producción
	private const string URL_BASE = "http://localhost:5289/api/cartas";

	// ── ESTADO ────────────────────────────────────────────────────────────
	public bool CartasCargadas { get; private set; } = false;
	public List<DatoCarta> Cartas { get; private set; } = new();

	// Señal que Campo1 escucha
	[Signal] public delegate void OnCartasCargadasEventHandler();
	[Signal] public delegate void ErrorCargaEventHandler(string mensaje);

	private Godot.HttpRequest _http;

	// ══════════════════════════════════════════════════════════════════════
	public override void _Ready()
	{
		Instance = this;

		_http = new Godot.HttpRequest();
		AddChild(_http);
		_http.RequestCompleted += OnRequestCompleted;

		// Cargar cartas automáticamente al iniciar
		CargarCartasDesdeAPI();
	}

	// ── PETICIÓN ──────────────────────────────────────────────────────────
	public void CargarCartasDesdeAPI()
	{
		CartasCargadas = false;
		GD.Print($"[GestorCartas] Conectando a {URL_BASE}...");

		Error error = _http.Request(URL_BASE);
		if (error != Error.Ok)
		{
			GD.PrintErr($"[GestorCartas] Error al conectar: {error}");
			UsarCartasLocales(); // Fallback si no hay internet
		}
	}

	// ── RESPUESTA ─────────────────────────────────────────────────────────
	private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
	{
		if (result != (long)Godot.HttpRequest.Result.Success || responseCode != 200)
		{
			GD.PrintErr($"[GestorCartas] Error HTTP {responseCode}. Usando cartas locales.");
			UsarCartasLocales();
			return;
		}

		try
		{
			string json = System.Text.Encoding.UTF8.GetString(body);
			var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var lista    = JsonSerializer.Deserialize<List<DatoCarta>>(json, opciones);

			if (lista == null || lista.Count == 0)
			{
				GD.Print("[GestorCartas] API devolvió lista vacía. Usando cartas locales.");
				UsarCartasLocales();
				return;
			}

			Cartas = lista;
			CartasCargadas = true;
			GD.Print($"[GestorCartas] ✅ {Cartas.Count} cartas cargadas desde la API.");
			EmitSignal(SignalName.OnCartasCargadas);
		}
		catch (Exception e)
		{
			GD.PrintErr($"[GestorCartas] Error parseando JSON: {e.Message}");
			UsarCartasLocales();
		}
	}

	// ── FALLBACK LOCAL ────────────────────────────────────────────────────
	// Si el backend no está disponible, usa las cartas hardcodeadas
	// (las mismas que tenía Campo1 antes, para que el juego siempre funcione)
	private void UsarCartasLocales()
	{
		GD.Print("[GestorCartas] Usando cartas locales (modo offline).");
		Cartas = new List<DatoCarta>
		{
			new() { Id=1,  Nombre="Peón",            Rol="Tactico", Serie="Ajedrez",  Era=2, VidaMaxima=150, EscudoMaximo=150, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/PeonCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Peon_prime.tscn" },
			new() { Id=2,  Nombre="Torre",           Rol="Coloso",  Serie="Ajedrez",  Era=2, VidaMaxima=500, EscudoMaximo=450, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TorreCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Torre_prime.tscn" },
			new() { Id=3,  Nombre="Arfil",           Rol="Tactico", Serie="Ajedrez",  Era=2, VidaMaxima=280, EscudoMaximo=270, PuntosAtaque=240, RutaImagen="res://imagenes/CartasPng/ArfilCart.png",     RutaEscena="res://cartas prime/AJEDREZ/Arfil_prime.tscn" },
			new() { Id=4,  Nombre="Caballo",         Rol="Tactico", Serie="Ajedrez",  Era=2, VidaMaxima=230, EscudoMaximo=250, PuntosAtaque=200, RutaImagen="res://imagenes/CartasPng/CaballoCart.png",   RutaEscena="res://cartas prime/AJEDREZ/Caballo_prime.tscn" },
			new() { Id=5,  Nombre="Dama",            Rol="Asesino", Serie="Ajedrez",  Era=2, VidaMaxima=350, EscudoMaximo=300, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/DamaCart.png",      RutaEscena="res://cartas prime/AJEDREZ/Dama_prime.tscn" },
			new() { Id=6,  Nombre="Soldado Cartoon", Rol="Asesino", Serie="Toon",     Era=3, VidaMaxima=220, EscudoMaximo=350, PuntosAtaque=50,  RutaImagen="res://imagenes/CartasPng/SoldCartoonCart.png",RutaEscena="res://cartas prime/TOONS/Soldado_cartoon_prime.tscn" },
			new() { Id=7,  Nombre="Tanque",          Rol="Coloso",  Serie="Toon",     Era=3, VidaMaxima=820, EscudoMaximo=0,   PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TanqueCart.png",    RutaEscena="res://cartas prime/TOONS/Tanque_cartoon_prime.tscn" },
			new() { Id=8,  Nombre="Granadero",       Rol="Asesino", Serie="Toon",     Era=3, VidaMaxima=50,  EscudoMaximo=600, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/GranaderoCart.png", RutaEscena="res://cartas prime/TOONS/Granadero_cartoon_prime.tscn" },
			new() { Id=9,  Nombre="Ka-Bar",          Rol="Asesino", Serie="Toon",     Era=3, VidaMaxima=250, EscudoMaximo=360, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/FantasmaCart.png",  RutaEscena="res://cartas prime/TOONS/Ka-Bar_cartoon_prime.tscn" },
			new() { Id=10, Nombre="Campero",         Rol="Tactico", Serie="Toon",     Era=3, VidaMaxima=200, EscudoMaximo=300, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/CamperoCart.png",   RutaEscena="res://cartas prime/TOONS/Campero_cartoon_prime.tscn" },
			new() { Id=11, Nombre="Soldado Real",    Rol="Tactico", Serie="Medieval", Era=2, VidaMaxima=250, EscudoMaximo=300, PuntosAtaque=150, RutaImagen="res://imagenes/CartasPng/SoldRealCart.png",  RutaEscena="res://cartas prime/MEDIEVAL/SoldadoReal_prime.tscn" },
			new() { Id=12, Nombre="Maguín",          Rol="Tactico", Serie="Medieval", Era=2, VidaMaxima=200, EscudoMaximo=220, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/MaguinCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Maguin_prime.tscn" },
			new() { Id=13, Nombre="Dragón",          Rol="Asesino", Serie="Medieval", Era=2, VidaMaxima=350, EscudoMaximo=250, PuntosAtaque=280, RutaImagen="res://imagenes/CartasPng/DragonCart.png",    RutaEscena="res://cartas prime/MEDIEVAL/Dragon_prime.tscn" },
			new() { Id=14, Nombre="Golem",           Rol="Coloso",  Serie="Medieval", Era=2, VidaMaxima=450, EscudoMaximo=500, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/GolemCart.png",     RutaEscena="res://cartas prime/MEDIEVAL/Golem_prime.tscn" },
			new() { Id=15, Nombre="Tiburón",         Rol="Asesino", Serie="Pacifico", Era=1, VidaMaxima=300, EscudoMaximo=200, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/TiburonCart.png",   RutaEscena="res://cartas prime/PACIFICO/Tiburon_prime.tscn" },
			new() { Id=16, Nombre="Calamar Gigante", Rol="Coloso",  Serie="Pacifico", Era=1, VidaMaxima=400, EscudoMaximo=380, PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/CalamarGCart.png",  RutaEscena="res://cartas prime/PACIFICO/CalamarG_prime.tscn" },
			new() { Id=17, Nombre="Paper-Rex",       Rol="Coloso",  Serie="Papeleo",  Era=1, VidaMaxima=850, EscudoMaximo=0,   PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/PaperReXCart.png",  RutaEscena="res://cartas prime/PAPEL/TRex_prime.tscn" },
		};
		CartasCargadas = true;
		EmitSignal(SignalName.OnCartasCargadas);
	}

	// ── API PÚBLICA ───────────────────────────────────────────────────────
	public List<DatoCarta> ObtenerCartas()        => Cartas;
	public List<DatoCarta> ObtenerCartasPorEra(int era) =>
		Cartas.FindAll(c => c.Era == era);

	public string[] ObtenerRutasImagenes() =>
		Cartas.ConvertAll(c => c.RutaImagen).ToArray();

	public string[] ObtenerRutasEscenas() =>
		Cartas.ConvertAll(c => c.RutaEscena).ToArray();
}

// ── DTO que coincide con el JSON del backend ──────────────────────────────
public class DatoCarta
{
	public int    Id           { get; set; }
	public string Nombre       { get; set; } = "";
	public int    Era          { get; set; }
	public string Tipo         { get; set; } = "Normal";
	public int    VidaMaxima   { get; set; }
	public int    EscudoMaximo { get; set; }
	public int    PuntosAtaque { get; set; }
	public string RutaImagen   { get; set; } = "";
	public string RutaEscena   { get; set; } = "";
	public string Habilidad    { get; set; } = "";
	public string Rol          { get; set; } = ""; // Tactico | Asesino | Coloso
	public string Serie        { get; set; } = ""; // Ajedrez | Toon | Medieval | Pacifico | Papeleo
}
