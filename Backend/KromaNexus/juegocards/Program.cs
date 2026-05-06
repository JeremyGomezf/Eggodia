using Microsoft.EntityFrameworkCore;
using KromaNexus.API.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Servicios básicos
builder.Services.AddControllers();

// 2. Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Configurar CORS
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// --- PASO 1 CONECTAR LA BASE DE DATOS (ESTO ES LO QUE FALTABA) ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(10, 4, 32))));
// -----------------------------------------------------------------

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

app.Run();