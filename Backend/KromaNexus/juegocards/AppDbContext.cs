using Microsoft.EntityFrameworkCore;
using KromaNexus.API.model; // Asegúrate que esta ruta sea correcta

namespace KromaNexus.API.Data 
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Carta> Cartas { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Carta>(entity =>
            {
                // Forzamos el nombre de la tabla
                entity.ToTable("cartas"); 

                // Forzamos el nombre de las columnas exactas
                entity.Property(c => c.Ataque).HasColumnName("Ataque");
                entity.Property(c => c.Defensa).HasColumnName("Defensa");
            });
        }
    }
}