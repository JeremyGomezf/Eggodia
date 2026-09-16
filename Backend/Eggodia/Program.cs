using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Servicios básicos
builder.Services.AddControllers();

// Matchmaking 1v1 en memoria (Fase 1 multijugador)
builder.Services.AddSingleton<GestorPartidas>();

// 2. Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Configurar CORS
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// --- PASO 1 CONECTAR LA BASE DE DATOS ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));
// ----------------------------------------

var app = builder.Build();

// 4. Activar la interfaz visual de Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); 
}

// 5. Usar la política de CORS
app.UseCors("AllowAll");

app.UseAuthorization();
app.MapControllers();

// Inicializar la base de datos de SQLite de forma automática
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Migración ligera: EnsureCreated NO agrega columnas nuevas a una BD ya creada. Nos aseguramos de
    // que la columna Monedas exista (necesaria para el saldo por cuenta y el panel de administración).
    // SQLite lanza error si la columna ya existe → se ignora.
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN Monedas INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* la columna ya existía */ }

    // Migración ligera (igual que arriba): EnsureCreated tampoco agrega TABLAS nuevas a una BD ya
    // creada — promo_codes/user_skins se agregaron después del primer despliegue, así que en un
    // servidor con una cards.db previa a ese cambio esas tablas nunca existían y CUALQUIER canje de
    // código reventaba con un 500 sin razón real ("no such table"). Las creamos aquí si faltan.
    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""promo_codes"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_promo_codes"" PRIMARY KEY AUTOINCREMENT,
                ""Codigo"" TEXT NOT NULL,
                ""TipoRecompensa"" TEXT NOT NULL,
                ""ValorRecompensa"" TEXT NOT NULL,
                ""NombreRecompensa"" TEXT NOT NULL,
                ""UsadoPorUsuarioId"" INTEGER NULL,
                ""FechaCanje"" TEXT NULL,
                ""MaxUsos"" INTEGER NOT NULL,
                ""UsosActuales"" INTEGER NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_promo_codes_Codigo"" ON ""promo_codes"" (""Codigo"");");

        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""user_skins"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_user_skins"" PRIMARY KEY AUTOINCREMENT,
                ""UserId"" INTEGER NOT NULL,
                ""SkinRuta"" TEXT NOT NULL,
                ""Nombre"" TEXT NOT NULL,
                ""FechaDesbloqueo"" TEXT NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_user_skins_UserId_SkinRuta"" ON ""user_skins"" (""UserId"", ""SkinRuta"");");

        // user_cards: inventario de cartas por usuario (se siembra en el registro). Sin esta tabla,
        // el registro reventaba con 500 "no such table: user_cards" en una BD creada antes de agregarla.
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""user_cards"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_user_cards"" PRIMARY KEY AUTOINCREMENT,
                ""UserId"" INTEGER NOT NULL,
                ""CardId"" TEXT NOT NULL,
                ""AcquiredAt"" TEXT NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"CREATE INDEX IF NOT EXISTS ""IX_user_cards_UserId"" ON ""user_cards"" (""UserId"");");
    }
    catch (Exception ex) { Console.Error.WriteLine($"[DB] No se pudieron asegurar promo_codes/user_skins/user_cards: {ex.Message}"); }

    // Siembra idempotente de los 20 códigos vigentes (10 Huevo Dorado + 10 Huevo Ecotec). No usa
    // HasData porque HasData solo se aplica cuando EnsureCreated crea la BD desde cero — en un
    // servidor con una BD ya existente, sin esto los códigos nunca llegarían a insertarse. Se hace
    // sin especificar Id (autoincrement) y con INSERT OR IGNORE contra el único índice de Codigo,
    // así se puede llamar en cada arranque sin duplicar filas.
    try
    {
        foreach (var pc in AppDbContext.GenerarCodigosPromo())
        {
            db.Database.ExecuteSqlInterpolated($@"
                INSERT OR IGNORE INTO promo_codes (Codigo, TipoRecompensa, ValorRecompensa, NombreRecompensa, MaxUsos, UsosActuales)
                VALUES ({pc.Codigo}, {pc.TipoRecompensa}, {pc.ValorRecompensa}, {pc.NombreRecompensa}, {pc.MaxUsos}, 0);");
        }
    }
    catch (Exception ex) { Console.Error.WriteLine($"[DB] No se pudieron sembrar los codigos promo: {ex.Message}"); }

    // Corrección idempotente: el bug antiguo (nombre.Contains("gonza"/"dev"...)) otorgó skins de
    // desarrollador a cuentas que NO son dev (p. ej. "ggonzalo", "pruebadev"). Se quitan a cualquiera
    // que no sea una de las cuentas dev reales (Id 1, 2, 4). Seguro de correr en cada arranque.
    try
    {
        db.Database.ExecuteSqlRaw(@"
            DELETE FROM user_skins
            WHERE UserId NOT IN (1, 2, 4)
              AND SkinRuta IN (
                'res://imagenes/PersonajesPng/JeremiHuevo.png',
                'res://imagenes/PersonajesPng/GonzaHuevo.png',
                'res://imagenes/PersonajesPng/CarlosHuevo.png');");
    }
    catch (Exception ex) { Console.Error.WriteLine($"[DB] No se pudo limpiar skins dev mal otorgadas: {ex.Message}"); }
}

app.Run();