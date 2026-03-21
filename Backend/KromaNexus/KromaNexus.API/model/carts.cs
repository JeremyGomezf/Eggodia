using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Importante añadir esto

namespace KromaNexus.API.model
{
    public class Carta
    {
        [Key]
        public int Id { get; set; }

        public required string Nombre { get; set; }
        public string? Tipo { get; set; }
        public int Costo { get; set; }

        [Column("Ataque")] // Esto obliga a buscar "Ataque" en SQL
        public int Ataque { get; set; } 

        [Column("Defensa")] // Esto obliga a buscar "Defensa" en SQL
        public int Defensa { get; set; }

        public string? Habilidad { get; set; }
    }
}