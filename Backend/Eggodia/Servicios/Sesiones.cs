using System.Security.Cryptography;
using System.Text;
using Eggodia.API.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Sesiones de las cuentas. Al iniciar sesión (o registrarse) el servidor entrega un código secreto
/// (token) que el juego guarda y manda en cada pedido ("Authorization: Bearer ..."). Así el servidor
/// sabe QUIÉN pide: antes bastaba con poner el número de cualquier cuenta en la dirección para tocar
/// sus monedas, su mazo o sus compras.
///
/// En la base se guarda solo el HASH del token (tabla "sesiones"): aunque alguien viera la base, no
/// podría usar las sesiones. Cada cuenta puede tener varias abiertas (varios celulares); se guardan
/// las más recientes y las que llevan mucho sin usarse se borran solas.
/// </summary>
public static class Sesiones
{
    private const int MAX_SESIONES_POR_CUENTA = 10;
    private const int DIAS_SIN_USO_PARA_VENCER = 180;

    public static string Hash(string token)
    {
        byte[] h = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(h);
    }

    /// <summary>Abre una sesión nueva para la cuenta y devuelve el token (solo esta vez se ve).</summary>
    public static async Task<string> Crear(AppDbContext db, int usuarioId)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string ahora = DateTime.UtcNow.ToString("o");
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sesiones (UsuarioId, TokenHash, CreadaUtc, UltimoUsoUtc)
            VALUES ({usuarioId}, {Hash(token)}, {ahora}, {ahora});");
        // Solo las más recientes: abrir sesión una y otra vez no llena la tabla.
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM sesiones WHERE UsuarioId = {usuarioId} AND Id NOT IN (
                SELECT Id FROM sesiones WHERE UsuarioId = {usuarioId} ORDER BY Id DESC LIMIT {MAX_SESIONES_POR_CUENTA});");
        return token;
    }

    /// <summary>Token que mandó el juego en este pedido, o null si no mandó ninguno.</summary>
    public static string? TokenDe(HttpContext http)
    {
        string? h = http.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(h) || !h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        string t = h["Bearer ".Length..].Trim();
        return t.Length is >= 20 and <= 200 ? t : null;
    }

    /// <summary>Cuenta dueña del token, o null si no existe (vencido, cerrado o inventado).</summary>
    public static async Task<int?> UsuarioDe(AppDbContext db, string token)
    {
        string hash = Hash(token);
        var filas = await db.Database
            .SqlQuery<FilaSesion>($"SELECT Id, UsuarioId, UltimoUsoUtc FROM sesiones WHERE TokenHash = {hash} LIMIT 1")
            .ToListAsync();
        if (filas.Count == 0) return null;
        var s = filas[0];
        // Marca de uso, a lo sumo una vez por hora (para vencer las que nadie usa).
        if (!DateTime.TryParse(s.UltimoUsoUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var uso)
            || DateTime.UtcNow - uso > TimeSpan.FromHours(1))
        {
            string ahora = DateTime.UtcNow.ToString("o");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sesiones SET UltimoUsoUtc = {ahora} WHERE Id = {s.Id};");
        }
        return s.UsuarioId;
    }

    /// <summary>Cierra todas las sesiones de una cuenta (p. ej. al cambiarle la contraseña o borrarla).</summary>
    public static Task CerrarTodas(AppDbContext db, int usuarioId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM sesiones WHERE UsuarioId = {usuarioId};");

    /// <summary>Borra las sesiones que llevan mucho sin usarse. Se llama al arrancar el servidor.</summary>
    public static void BorrarVencidas(AppDbContext db)
    {
        string limite = DateTime.UtcNow.AddDays(-DIAS_SIN_USO_PARA_VENCER).ToString("o");
        db.Database.ExecuteSqlInterpolated($"DELETE FROM sesiones WHERE UltimoUsoUtc < {limite};");
    }

    public class FilaSesion
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string UltimoUsoUtc { get; set; } = "";
    }
}
