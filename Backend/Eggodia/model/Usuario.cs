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

        // Cosméticos EQUIPADOS por cuenta (se cargan al iniciar sesión). Antes eran locales del
        // dispositivo y "se traspasaban" entre cuentas en el mismo celular.
        public int    EquipSkinIdx       { get; set; } = 0;
        public string EquipSkinExclusiva { get; set; } = "";
        public int    EquipTronoIdx      { get; set; } = 0;

        // Experiencia (de ella sale el NIVEL) y mazo armado, guardados en la CUENTA: antes vivían solo
        // en el celular y se perdían al reinstalar o al entrar desde otro teléfono.
        public int    Experiencia { get; set; } = 0;
        public string MazoJson    { get; set; } = "";

        // Estadísticas para el ranking
        public int Victorias  { get; set; } = 0;
        public int Derrotas   { get; set; } = 0;
        public int Empates    { get; set; } = 0;
        public int DañoTotal  { get; set; } = 0;

        // Favoritos del perfil público (los ve quien te toque en el ranking): la tropa (ruta de su
        // escena) y el ardid (nombre) que más usa. Los calcula el juego tras 3 partidas y los manda
        // con cada fin de partida; vacíos hasta entonces.
        public string TropaFavorita { get; set; } = "";
        public string ArdidFavorito { get; set; } = "";

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
        public int    EquipSkinIdx       { get; set; }
        public string EquipSkinExclusiva { get; set; } = "";
        public int    EquipTronoIdx      { get; set; }
        public int    Experiencia { get; set; }
        public int    Victorias { get; set; }
        public int    Derrotas  { get; set; }
        public int    Empates   { get; set; }
        public int    DañoTotal { get; set; }
        // Sesión nueva: solo viene al iniciar sesión o registrarse (ver Sesiones). null en el resto.
        public string? Token    { get; set; }
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
