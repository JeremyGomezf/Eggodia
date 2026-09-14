using Godot;
using System.Collections.Generic;

/// <summary>
/// SESIÓN DEL JUEGO — AutoLoad Singleton
/// ───────────────────────────────────────
/// Guarda el usuario logueado y el mazo seleccionado.
/// Persiste entre escenas (menú → batalla → resultados).
///
/// AGREGAR EN GODOT:
/// Proyecto → Configuración → AutoLoad
/// Ruta: res://escenas/gameplay/SesionJuego.cs
/// Nombre: SesionJuego
/// </summary>
using System;
using System.Text.Json;

public partial class SesionJuego : Node
{
	public static SesionJuego Instance { get; private set; }

	// ── USUARIO LOGUEADO ──────────────────────────────────────────────────
	public int    UsuarioId   { get; set; } = -1;
	public string NombreJugador { get; set; } = "Jugador";
	public bool   EstaLogueado  => UsuarioId > 0;

	// ── MAZO SELECCIONADO ─────────────────────────────────────────────────
	// Lista de rutas de escenas (.tscn) que el jugador armó en el constructor
	public List<string> MazoSeleccionado { get; set; } = new();
	public List<string> ImagenesMazo     { get; set; } = new();
	public bool TieneMazo => MazoSeleccionado != null && MazoSeleccionado.Count >= 8;

	// ── ARDIDES SELECCIONADOS (opcional) ──────────────────────────────────
	// Ids estables (CartaData.IdHechizo) de los 6 ardides elegidos en MenuConstructor. Si el
	// jugador nunca los eligió (o eligió menos de 6), queda vacío y Campo1 arma un pool aleatorio
	// de los 8 hechizos disponibles, igual que hacía antes con 5.
	public List<string> ArdidesSeleccionados { get; set; } = new();
	public bool TieneArdides => ArdidesSeleccionados != null && ArdidesSeleccionados.Count == 6;

	// ── RESULTADO ÚLTIMA PARTIDA ──────────────────────────────────────────
	public string UltimoResultado  { get; set; } = "";
	public int    DañoUltimaPartida { get; set; } = 0;
	public int    RachaActual       { get; set; } = 0;

	private const string RUTA_MAZO_GUARDADO = "user://mazo_guardado.json";

	public override void _Ready()
	{
		Instance = this;
		CargarMazoDeDisco();

		// Login persistente: si había una sesión guardada, se restaura (así el jugador sigue
		// logueado entre reinicios y PanelLogin lo manda directo al menú).
		int idGuardado = Preferencias.SesionUsuarioId;
		if (idGuardado > 0)
		{
			UsuarioId     = idGuardado;
			NombreJugador = Preferencias.SesionNombre;
		}
	}

	public void CerrarSesion()
	{
		UsuarioId      = -1;
		NombreJugador  = "Jugador";
		MazoSeleccionado.Clear();
		ImagenesMazo.Clear();
		ArdidesSeleccionados.Clear();
		Preferencias.CerrarSesionGuardada(); // el logout también se recuerda
	}

	/// <summary>Guardar mazo desde el constructor antes de ir a la batalla o volver al menú.</summary>
	public void GuardarMazo(List<string> escenas, List<string> imagenes)
	{
		MazoSeleccionado = new List<string>(escenas);
		ImagenesMazo     = new List<string>(imagenes);
		GuardarMazoEnDisco();
		GD.Print($"[SesionJuego] Mazo guardado en memoria y disco: {MazoSeleccionado.Count} cartas.");
	}

	/// <summary>Guardar la selección de 6 ardides (opcional) desde MenuConstructor.</summary>
	public void GuardarMazoArdid(List<string> idsHechizo)
	{
		ArdidesSeleccionados = new List<string>(idsHechizo);
		GuardarMazoEnDisco();
		GD.Print($"[SesionJuego] Ardides guardados en memoria y disco: {ArdidesSeleccionados.Count} elegidos.");
	}

	private void GuardarMazoEnDisco()
	{
		try
		{
			using var file = FileAccess.Open(RUTA_MAZO_GUARDADO, FileAccess.ModeFlags.Write);
			if (file != null)
			{
				var data = new MazoPersistenteData
				{
					Escenas = MazoSeleccionado,
					Imagenes = ImagenesMazo,
					Ardides = ArdidesSeleccionados
				};
				string json = JsonSerializer.Serialize(data);
				file.StoreString(json);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[SesionJuego] Error al guardar mazo en disco: {ex.Message}");
		}
	}

	private void CargarMazoDeDisco()
	{
		try
		{
			if (FileAccess.FileExists(RUTA_MAZO_GUARDADO))
			{
				using var file = FileAccess.Open(RUTA_MAZO_GUARDADO, FileAccess.ModeFlags.Read);
				if (file != null)
				{
					string json = file.GetAsText();
					var data = JsonSerializer.Deserialize<MazoPersistenteData>(json);
					if (data != null && data.Escenas != null && data.Escenas.Count >= 8)
					{
						MazoSeleccionado = data.Escenas;
						ImagenesMazo = data.Imagenes ?? new List<string>();
						ArdidesSeleccionados = data.Ardides ?? new List<string>();
						GD.Print($"[SesionJuego] Mazo cargado desde disco: {MazoSeleccionado.Count} cartas, {ArdidesSeleccionados.Count} ardides.");
					}
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[SesionJuego] Error al cargar mazo desde disco: {ex.Message}");
		}
	}

	private class MazoPersistenteData
	{
		public List<string> Escenas { get; set; } = new();
		public List<string> Imagenes { get; set; } = new();
		public List<string> Ardides { get; set; } = new();
	}
}
