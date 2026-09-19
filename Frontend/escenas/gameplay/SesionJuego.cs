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

	// Mínimo de ardides equipados para poder jugar y para que Campo1 use tu selección real (por
	// debajo de esto, Campo1 arma su propio pool aleatorio de los 8 hechizos disponibles). Se pidió
	// "4 o 5" — 4 es el piso; equipar 5 o los 6 completos también sirve.
	public const int MIN_ARDIDES_JUGAR = 4;

	// ── MAZO SELECCIONADO ─────────────────────────────────────────────────
	// Lista de rutas de escenas (.tscn) que el jugador armó en el constructor
	public List<string> MazoSeleccionado { get; set; } = new();
	public List<string> ImagenesMazo     { get; set; } = new();
	public bool TieneMazo => MazoSeleccionado != null && MazoSeleccionado.Count >= 8;

	// ── ARDIDES SELECCIONADOS (opcional) ──────────────────────────────────
	// Ids estables (CartaData.IdHechizo) de los ardides elegidos en MenuConstructor (hasta 6). Si
	// el jugador equipó menos de MIN_ARDIDES_JUGAR, queda sin efecto y Campo1 arma un pool
	// aleatorio de los 8 hechizos disponibles.
	public List<string> ArdidesSeleccionados { get; set; } = new();
	public bool TieneArdides => ArdidesSeleccionados != null && ArdidesSeleccionados.Count >= MIN_ARDIDES_JUGAR;

	// ── RESULTADO ÚLTIMA PARTIDA ──────────────────────────────────────────
	public string UltimoResultado  { get; set; } = "";
	public int    DañoUltimaPartida { get; set; } = 0;
	public int    RachaActual       { get; set; } = 0;

	// El mazo también es del PERFIL (invitado o cuenta): cada uno arma y guarda el suyo.
	private string RutaMazoGuardado => Preferencias.RutaMazoDe(UsuarioId);

	// Ciclo de vida móvil: true mientras la app está en segundo plano.
	private bool _appEnSegundoPlano = false;

	public override void _Ready()
	{
		Instance = this;

		// Una sola vez: pasa los datos del formato viejo (todo mezclado) al perfil que corresponde.
		// Va ANTES que nada, porque todo lo demás ya lee desde los perfiles.
		Preferencias.MigrarAPerfilesSiHaceFalta();

		// Login persistente: SOLO una cuenta registrada queda recordada (así PanelLogin la manda
		// directo al menú). El invitado nunca se recuerda → cada vez que abre la app ve el login.
		int idGuardado = Preferencias.SesionUsuarioId;
		if (idGuardado > 0)
		{
			UsuarioId     = idGuardado;
			NombreJugador = Preferencias.SesionNombre;
		}

		// El mazo se lee DESPUÉS de restaurar la sesión: es el del perfil de esa cuenta.
		CargarMazoDeDisco();

		if (idGuardado > 0)
		{
			// Traer el saldo de monedas de la cuenta desde el servidor (diferido: Economia se autocarga
			// después que SesionJuego en el orden de AutoLoad). Así el saldo que el admin ajustó aparece
			// al reabrir la app aunque no se vuelva a iniciar sesión.
			CallDeferred(nameof(SincronizarMonedasCuenta));
		}
	}

	private void SincronizarMonedasCuenta()
	{
		// Carga TODO el inventario de la cuenta (monedas + skins + tronos + ítems + equipado) desde el
		// servidor, limpiando antes lo local. Así al reabrir la app se ve solo lo de ESTA cuenta.
		if (UsuarioId > 0) Economia.Instancia()?.CargarInventarioCuenta(UsuarioId);
	}

	/// <summary>
	/// Ciclo de vida de la app (móvil): si el jugador sale de la aplicación (cambia de app, apaga la
	/// pantalla o bloquea el celular) el juego pasa a segundo plano; al volver, se reinicia desde el
	/// menú con estado limpio — como cualquier juego móvil. La sesión (login) NO se cierra: sigue
	/// logueado. Cualquier partida en línea a medias se abandona (el rival gana por desconexión).
	/// </summary>
	public override void _Notification(int que)
	{
		if (que == NotificationApplicationPaused)
		{
			_appEnSegundoPlano = true;
		}
		else if (que == NotificationApplicationResumed && _appEnSegundoPlano)
		{
			_appEnSegundoPlano = false;
			ContextoOnline.Limpiar(); // abandona cualquier emparejamiento/partida en línea en curso
			GetTree()?.ChangeSceneToFile("res://escenas/menu/menu_principal.tscn");
		}
	}

	/// <summary>Cambia el PERFIL activo (login, "jugar como invitado" o cerrar sesión). Cada perfil
	/// tiene sus propios archivos, así que alcanza con apuntar al nuevo y recargar lo que está en
	/// memoria (mazo y monedas) — nada del perfil anterior se borra ni se mezcla.</summary>
	public void ActivarPerfil(int usuarioId, string nombre)
	{
		UsuarioId     = usuarioId;
		NombreJugador = string.IsNullOrEmpty(nombre) ? "Jugador" : nombre;
		MazoSeleccionado.Clear();
		ImagenesMazo.Clear();
		ArdidesSeleccionados.Clear();
		CargarMazoDeDisco();
		Economia.Instancia()?.RecargarPerfil(usuarioId);
	}

	/// <summary>Ruta del archivo del perfil activo. Para scripts GDScript (SummonTerminal), que no
	/// pueden leer la clase estática Preferencias.</summary>
	public string RutaPerfilActivo() => Preferencias.RutaPerfil;

	public void CerrarSesion()
	{
		Preferencias.CerrarSesionGuardada(); // el logout también se recuerda
		// Pasa al perfil de invitado. Lo de la cuenta queda intacto en SU archivo (y en el servidor)
		// para cuando vuelva a entrar; la próxima cuenta que entre usa el suyo propio.
		ActivarPerfil(-1, "Jugador");
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
			using var file = FileAccess.Open(RutaMazoGuardado, FileAccess.ModeFlags.Write);
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
			if (FileAccess.FileExists(RutaMazoGuardado))
			{
				using var file = FileAccess.Open(RutaMazoGuardado, FileAccess.ModeFlags.Read);
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
