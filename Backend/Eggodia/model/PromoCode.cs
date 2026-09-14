using System.ComponentModel.DataAnnotations;

namespace Eggodia.API.model
{
    /// <summary>
    /// Código promocional canjeable en el backend.
    /// Puede recompensar con skins de huevo ("skin") o monedas ("monedas").
    /// </summary>
    public class PromoCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Codigo { get; set; } = "";

        [Required]
        public string TipoRecompensa { get; set; } = "skin"; // "skin" | "monedas"

        [Required]
        public string ValorRecompensa { get; set; } = ""; // Ruta de skin o cantidad de monedas

        public string NombreRecompensa { get; set; } = "";

        public int? UsadoPorUsuarioId { get; set; }

        public DateTime? FechaCanje { get; set; }

        public int MaxUsos { get; set; } = 1;

        public int UsosActuales { get; set; } = 0;
    }

    /// <summary>
    /// Registro persistente de skins de huevo desbloqueadas por cada usuario en su cuenta.
    /// </summary>
    public class UserSkin
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string SkinRuta { get; set; } = "";

        public string Nombre { get; set; } = "";

        public DateTime FechaDesbloqueo { get; set; } = DateTime.UtcNow;
    }

    public class CanjearCodigoRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string Codigo { get; set; } = "";
    }
}
