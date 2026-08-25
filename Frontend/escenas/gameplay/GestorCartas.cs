using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// GestorCartas — conecta el juego con el backend KromaNexus.
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
			new() { Id=1,  Nombre="Dragón",          Era=3, VidaMaxima=350, EscudoMaximo=250, PuntosAtaque=280, RutaImagen="res://imagenes/CartasPng/DragonCart.png",    RutaEscena="res://cartas prime/Dragon_prime.tscn" },
			new() { Id=2,  Nombre="Golem",            Era=3, VidaMaxima=450, EscudoMaximo=500, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/GolemCart.png",     RutaEscena="res://cartas prime/Golem_prime.tscn" },
			new() { Id=3,  Nombre="Maguín",           Era=3, VidaMaxima=200, EscudoMaximo=220, PuntosAtaque=250, RutaImagen="res://imagenes/CartasPng/MaguinCart.png",    RutaEscena="res://cartas prime/Maguin_prime.tscn" },
			new() { Id=4,  Nombre="Soldado Real",     Era=2, VidaMaxima=250, EscudoMaximo=300, PuntosAtaque=150, RutaImagen="res://imagenes/CartasPng/SoldRealCart.png",  RutaEscena="res://cartas prime/SoldadoReal_prime.tscn" },
			new() { Id=5,  Nombre="Torre",            Era=2, VidaMaxima=500, EscudoMaximo=450, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/TorreCart.png",     RutaEscena="res://cartas prime/Torre_prime.tscn" },
			new() { Id=6,  Nombre="Peón",             Era=2, VidaMaxima=150, EscudoMaximo=150, PuntosAtaque=100, RutaImagen="res://imagenes/CartasPng/PeonCart.png",      RutaEscena="res://cartas prime/Peon_prime.tscn" },
			new() { Id=8,  Nombre="Caballo",          Era=2, VidaMaxima=230, EscudoMaximo=250, PuntosAtaque=200, RutaImagen="res://imagenes/CartasPng/CaballoCart.png",   RutaEscena="res://cartas prime/Caballo_prime.tscn" },
			new() { Id=9,  Nombre="Dama",             Era=2, VidaMaxima=350, EscudoMaximo=300, PuntosAtaque=350, RutaImagen="res://imagenes/CartasPng/DamaCart.png",      RutaEscena="res://cartas prime/Dama_prime.tscn" },
			new() { Id=10, Nombre="T-Rex",            Era=1, VidaMaxima=400, EscudoMaximo=350, PuntosAtaque=400, RutaImagen="res://imagenes/CartasPng/TReXCart.png",      RutaEscena="res://cartas prime/TRex_prime.tscn" },
			new() { Id=11, Nombre="Tiburón",          Era=1, VidaMaxima=300, EscudoMaximo=200, PuntosAtaque=230, RutaImagen="res://imagenes/CartasPng/TiburonCart.png",   RutaEscena="res://cartas prime/Tiburon_prime.tscn" },
			new() { Id=12, Nombre="Calamar Gigante",  Era=1, VidaMaxima=400, EscudoMaximo=380, PuntosAtaque=370, RutaImagen="res://imagenes/CartasPng/CalamarGCart.png",  RutaEscena="res://cartas prime/CalamarG_prime.tscn" },
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
}
