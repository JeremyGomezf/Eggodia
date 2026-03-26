using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // <--- ASEGÚRATE DE TENER ESTO

namespace KromaNexus.API.model
{
    public class Carta
    {
        [Key]
        public int Id { get; set; }

        public required string Nombre { get; set; }
        public string? Tipo { get; set; }
        public int Costo { get; set; }

        [Column("Ataque")] // <--- ESTO OBLIGA A BUSCAR "Ataque" EN SQL
        public int Ataque { get; set; }

        [Column("Defensa")] // <--- ESTO OBLIGA A BUSCAR "Defensa" EN SQL
        public int Defensa { get; set; }

        public string? Habilidad { get; set; }
    }
}