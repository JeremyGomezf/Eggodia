using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Eggodia.API.model
{
    /// <summary>
    /// Relación entre un usuario y las cartas desbloqueadas en su ejército/inventario en SQLite.
    /// </summary>
    public class UserCard
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string CardId { get; set; } = "";

        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Objeto de transferencia para canjear una carta física mediante su código.
    /// Acepta UserId tanto en formato numérico (1) como string ("1").
    /// </summary>
    public class ClaimCardDto
    {
        [Required]
        public object? UserId { get; set; }

        [Required]
        public string Code { get; set; } = "";

        /// <summary>
        /// Parsea de forma segura el UserId independientemente de si viene como int o string en el JSON.
        /// </summary>
        public int GetParsedUserId()
        {
            if (UserId is null) return 0;

            if (UserId is JsonElement elem)
            {
                if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out int n)) return n;
                if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), out int s)) return s;
            }

            if (int.TryParse(UserId.ToString(), out int parsed)) return parsed;
            return 0;
        }
    }
}
