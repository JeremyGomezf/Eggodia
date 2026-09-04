using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eggodia.API.model
{
    /// <summary>
    /// Modelo de carta — contiene todos los stats que usa el juego Godot.
    /// Ruta backend: model/Carta.cs
    /// </summary>
    public class Carta
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = "";

        // Stats de combate (coinciden con las variables de los scripts de Godot)
        public int VidaMaxima    { get; set; }
        public int EscudoMaximo  { get; set; }
        public int PuntosAtaque  { get; set; }

        // Rutas para que Godot cargue la imagen y la escena de la tropa
        public string RutaImagen { get; set; } = "";
        public string RutaEscena { get; set; } = "";

        // Descripción de la habilidad especial (para mostrar en UI)
        public string? Habilidad { get; set; }

        // ── CLASIFICACIÓN DEL JUEGO ────────────────────────────────────────
        // Tipo de combate: "Tactico" | "Asesino" | "Coloso"
        public string Tipo  { get; set; } = "";
        // Serie / facción: "Ajedrez" | "Toon" | "Medieval" | "Pacifico" | "Papeleo"
        public string Serie { get; set; } = "";
    }
}