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
    public bool TieneMazo => MazoSeleccionado.Count > 0;

    // ── RESULTADO ÚLTIMA PARTIDA ──────────────────────────────────────────
    public string UltimoResultado  { get; set; } = "";
    public int    DañoUltimaPartida { get; set; } = 0;

    public override void _Ready()
    {
        Instance = this;
    }

    public void CerrarSesion()
    {
        UsuarioId      = -1;
        NombreJugador  = "Jugador";
        MazoSeleccionado.Clear();
        ImagenesMazo.Clear();
    }

    /// <summary>Guardar mazo desde el constructor antes de ir a la batalla.</summary>
    public void GuardarMazo(List<string> escenas, List<string> imagenes)
    {
        MazoSeleccionado = new List<string>(escenas);
        ImagenesMazo     = new List<string>(imagenes);
        GD.Print($"[SesionJuego] Mazo guardado: {MazoSeleccionado.Count} cartas.");
    }
}
