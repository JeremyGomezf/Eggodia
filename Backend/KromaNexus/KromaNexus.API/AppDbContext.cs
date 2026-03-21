using Microsoft.EntityFrameworkCore;

namespace KromaNexus.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Aquí definimos la tabla de Cartas. 
        // Si aún no tienen el modelo "Carta", lo crearemos en el paso 2.
        public DbSet<Carta> Cartas { get; set; }
    }

    public class Carta
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int PuntosAtaque { get; set; }
        public int PuntosDefensa { get; set; }
        public string Tipo { get; set; } = "";
    }
}