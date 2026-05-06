using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using KromaNexus.API.Data;

/// <summary>
/// Le dice a Entity Framework cómo crear el DbContext en tiempo de diseño
/// (cuando corres dotnet ef database update) sin necesitar la conexión real.
/// Coloca este archivo en la raíz del proyecto backend.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Cadena de conexión hardcodeada solo para migraciones
        optionsBuilder.UseMySql(
            "Server=127.0.0.1;Port=3306;Database=cardsdatabase;User=root;Password=;",
            new MariaDbServerVersion(new Version(10, 4, 32))
        );

        return new AppDbContext(optionsBuilder.Options);
    }
}
