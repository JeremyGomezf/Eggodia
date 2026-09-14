using System.ComponentModel.DataAnnotations;

namespace Eggodia.API.model
{
    public class Usuario
    {
        [Key] public int Id { get; set; }
        [Required] public string Nombre   { get; set; } = "";
        [Required] public string Email    { get; set; } = "";
        [Required] public string Password { get; set; } = ""; // hash en producción

        // Monedas del jugador (saldo por CUENTA, guardado en el servidor). El cliente lo adopta al
        // iniciar sesión y lo sincroniza al ganar/gastar; el admin puede darlas/quitarlas.
        public int Monedas { get; set; } = 0;

        // Estadísticas para el ranking
        public int Victorias  { get; set; } = 0;
        public int Derrotas   { get; set; } = 0;
        public int Empates    { get; set; } = 0;
        public int DañoTotal  { get; set; } = 0;

        // Fecha de registro
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }

    // DTO para login (no enviamos password en respuesta)
    public class UsuarioDto
    {
        public int    Id       { get; set; }
        public string Nombre   { get; set; } = "";
        public string Email    { get; set; } = "";
        public int    Monedas   { get; set; }
        public int    Victorias { get; set; }
        public int    Derrotas  { get; set; }
        public int    Empates   { get; set; }
        public int    DañoTotal { get; set; }
    }

    public class LoginRequest
    {
        [Required] public string Email    { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    public class RegistroRequest
    {
        [Required] public string Nombre   { get; set; } = "";
        [Required] public string Email    { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    // Para actualizar stats después de una partida
    public class ResultadoPartidaRequest
    {
        public int    UsuarioId { get; set; }
        public string Resultado { get; set; } = ""; // "victoria", "derrota", "empate"
        public int    DañoHecho { get; set; }
    }
}
