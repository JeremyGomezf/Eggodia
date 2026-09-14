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
}

app.Run();