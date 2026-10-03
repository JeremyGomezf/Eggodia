using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Servicios básicos
builder.Services.AddControllers();

// Matchmaking 1v1 en memoria (Fase 1 multijugador)
builder.Services.AddSingleton<GestorPartidas>();

// Salas de WebSocket para el multijugador en tiempo real (push). Aditivo: convive con el sondeo REST.
builder.Services.AddSingleton<SalasWebSocket>();

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

// WebSockets para el multijugador en tiempo real (endpoint /ws/match).
app.UseWebSockets();

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

    // Cosméticos equipados por cuenta (skin/trono) — antes eran locales y se "traspasaban" entre
    // cuentas del mismo dispositivo. Cada ALTER se ignora si la columna ya existe.
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN EquipSkinIdx INTEGER NOT NULL DEFAULT 0;"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN EquipSkinExclusiva TEXT NOT NULL DEFAULT '';"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN EquipTronoIdx INTEGER NOT NULL DEFAULT 0;"); } catch { }

    // Nivel y mazo por CUENTA (antes solo en el celular: se perdían al reinstalar o cambiar de teléfono).
    // La experiencia solo se gana al vencer (+150 por victoria), así que la de las cuentas que ya
    // existían se reconstruye desde sus victorias. Ese UPDATE corre UNA sola vez: solo cuando el ALTER
    // acaba de crear la columna (si ya existía, el ALTER falla y se salta).
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN Experiencia INTEGER NOT NULL DEFAULT 0;");
        db.Database.ExecuteSqlRaw("UPDATE Usuarios SET Experiencia = Victorias * 150;");
    }
    catch { /* la columna ya existía */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN MazoJson TEXT NOT NULL DEFAULT '';"); } catch { }
    // Regalo de bienvenida para las cuentas que YA existían: las nuevas nacen con 500 monedas (ver
    // UsuariosController.MONEDAS_INICIALES), así que a las de antes se les suman 500 UNA sola vez. La
    // columna es solo la marca de "ya se dio": si el ALTER falla es porque ya existía y no se repite.
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN BonoInicialDado INTEGER NOT NULL DEFAULT 1;");
        db.Database.ExecuteSqlRaw("UPDATE Usuarios SET Monedas = Monedas + " + UsuariosController.MONEDAS_INICIALES + ";");
    }
    catch { /* la columna ya existía: el bono ya se dio */ }
    // Códigos con vencimiento (ver PromoCode.ExpiraUtc).
    try { db.Database.ExecuteSqlRaw("ALTER TABLE promo_codes ADD COLUMN ExpiraUtc TEXT NULL;"); } catch { }
    // Favoritos del perfil público (ver Usuario.TropaFavorita).
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN TropaFavorita TEXT NOT NULL DEFAULT '';"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Usuarios ADD COLUMN ArdidFavorito TEXT NOT NULL DEFAULT '';"); } catch { }

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

        // user_items: inventario de cosméticos comprados por cuenta (skins/tronos/tropas/hechizos).
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""user_items"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_user_items"" PRIMARY KEY AUTOINCREMENT,
                ""UserId"" INTEGER NOT NULL,
                ""Tipo"" TEXT NOT NULL,
                ""ItemId"" TEXT NOT NULL,
                ""FechaUtc"" TEXT NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_user_items_UserId_Tipo_ItemId"" ON ""user_items"" (""UserId"", ""Tipo"", ""ItemId"");");

        // partidas_jugadas: una fila por partida YA premiada (monedas/XP/estadísticas). El índice único
        // (UserId, PartidaId) hace idempotente el premio: si el cliente reintenta tras un corte de red,
        // la segunda vez no suma de nuevo. Sirve además de historial.
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""partidas_jugadas"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_partidas_jugadas"" PRIMARY KEY AUTOINCREMENT,
                ""UserId"" INTEGER NOT NULL,
                ""PartidaId"" TEXT NOT NULL,
                ""Modo"" TEXT NOT NULL,
                ""Resultado"" TEXT NOT NULL,
                ""Monedas"" INTEGER NOT NULL,
                ""Xp"" INTEGER NOT NULL,
                ""FechaUtc"" TEXT NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_partidas_jugadas_UserId_PartidaId"" ON ""partidas_jugadas"" (""UserId"", ""PartidaId"");");

        // promo_canjes: quién canjeó qué código. Con un código compartido por muchos (MaxUsos alto), es lo
        // que impide que la MISMA cuenta lo canjee dos veces (antes solo se impedía en códigos de skin, y
        // de rebote, porque ya la tenía; uno de monedas se podía canjear una y otra vez).
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""promo_canjes"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_promo_canjes"" PRIMARY KEY AUTOINCREMENT,
                ""CodigoId"" INTEGER NOT NULL,
                ""UserId"" INTEGER NOT NULL,
                ""FechaUtc"" TEXT NOT NULL
            );");
        db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_promo_canjes_CodigoId_UserId"" ON ""promo_canjes"" (""CodigoId"", ""UserId"");");

        // errores_cliente: errores que el juego reporta solo (antes solo se veían conectando el celular
        // por cable). Se guardan los últimos ~2000 (ver ErroresController).
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""errores_cliente"" (
                ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_errores_cliente"" PRIMARY KEY AUTOINCREMENT,
                ""FechaUtc"" TEXT NOT NULL,
                ""Version"" TEXT NOT NULL,
                ""Plataforma"" TEXT NOT NULL,
                ""UsuarioId"" INTEGER NOT NULL,
                ""Mensaje"" TEXT NOT NULL,
                ""Detalle"" TEXT NOT NULL
            );");
    }
    catch (Exception ex) { Console.Error.WriteLine($"[DB] No se pudieron asegurar las tablas auxiliares: {ex.Message}"); }

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