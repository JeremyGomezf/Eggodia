using System.ComponentModel.DataAnnotations;

namespace Eggodia.API.model
{
    /// <summary>
    /// Ítem del inventario de un usuario, comprado en la Tienda (propiedad POR CUENTA en el servidor,
    /// no en el dispositivo). Cubre todo lo comprable: skins de huevo base, tronos, tropas y hechizos.
    /// Las skins EXCLUSIVAS (dev/promo) siguen en UserSkins; el inventario las junta al devolverlas.
    /// </summary>
    public class UserItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string Tipo { get; set; } = "";   // "skin" | "trono" | "tropa" | "hechizo"

        [Required]
        public string ItemId { get; set; } = "";  // idx (skin/trono) o id/nombre (tropa/hechizo)

        public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    }

    public class ComprarRequest
    {
        public string Tipo { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int Costo { get; set; } = 0;
    }

    public class EquiparRequest
    {
        public int?    SkinIdx        { get; set; }
        public string? SkinExclusiva  { get; set; }
        public int?    TronoIdx       { get; set; }
    }
}
