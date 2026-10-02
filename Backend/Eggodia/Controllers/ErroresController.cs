using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;

/// <summary>
/// Errores que el juego reporta SOLO (antes solo se veían conectando el celular por cable y leyendo el
/// logcat). El cliente agrupa y limita lo que manda (ver ReporteErrores.cs).
///
///   POST /api/errores                      { version, plataforma, usuarioId, errores: [{mensaje, detalle}] }
///   GET  /api/errores?clave=XXXX&limite=N  → los últimos N (por defecto 200), para revisar. Misma clave
///                                            que el panel de administración (Admin:Clave).
///
/// Se guardan los últimos MAX_GUARDADOS; los más viejos se borran solos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ErroresController : ControllerBase
{
    private const int MAX_GUARDADOS = 2000;
    private const int MAX_POR_ENVIO = 20;

    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public ErroresController(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public class ErrorCliente
    {
        public string Mensaje { get; set; } = "";
        public string Detalle { get; set; } = "";
    }

    public class ReporteRequest
    {
        public string Version    { get; set; } = "";
        public string Plataforma { get; set; } = "";
        public int    UsuarioId  { get; set; }
        public List<ErrorCliente> Errores { get; set; } = new();
    }

    [HttpPost]
    public async Task<IActionResult> Reportar([FromBody] ReporteRequest req)
    {
        if (req?.Errores == null || req.Errores.Count == 0) return Ok(new { ok = true, guardados = 0 });

        string ahora = DateTime.UtcNow.ToString("o");
        string version = Cortar(req.Version, 20);
        string plataforma = Cortar(req.Plataforma, 40);
        int guardados = 0;
        foreach (var e in req.Errores.Take(MAX_POR_ENVIO))
        {
            if (string.IsNullOrWhiteSpace(e?.Mensaje)) continue;
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO errores_cliente (FechaUtc, Version, Plataforma, UsuarioId, Mensaje, Detalle)
                VALUES ({ahora}, {version}, {plataforma}, {req.UsuarioId}, {Cortar(e.Mensaje, 500)}, {Cortar(e.Detalle, 4000)});");
            guardados++;
        }

        // Tope: se conservan solo los últimos MAX_GUARDADOS.
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM errores_cliente WHERE Id <= (SELECT MAX(Id) FROM errores_cliente) - {MAX_GUARDADOS};");

        return Ok(new { ok = true, guardados });
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string clave = "", [FromQuery] int limite = 200)
    {
        string real = _cfg["Admin:Clave"] ?? "";
        if (string.IsNullOrEmpty(real) || clave != real) return Unauthorized(new { mensaje = "Clave inválida." });

        limite = Math.Clamp(limite, 1, MAX_GUARDADOS);
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, FechaUtc, Version, Plataforma, UsuarioId, Mensaje, Detalle FROM errores_cliente ORDER BY Id DESC LIMIT $limite;";
            var p = cmd.CreateParameter(); p.ParameterName = "$limite"; p.Value = limite; cmd.Parameters.Add(p);
            var lista = new List<object>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                lista.Add(new
                {
                    id = r.GetInt64(0), fechaUtc = r.GetString(1), version = r.GetString(2),
                    plataforma = r.GetString(3), usuarioId = r.GetInt64(4), mensaje = r.GetString(5), detalle = r.GetString(6)
                });
            return Ok(lista);
        }
        finally { await conn.CloseAsync(); }
    }

    private static string Cortar(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
}
