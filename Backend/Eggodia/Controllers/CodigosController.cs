using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;
using Eggodia.API.model;

namespace Eggodia.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CodigosController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CodigosController(AppDbContext db)
        {
            _db = db;
        }

        // POST: api/codigos/canjear
        [HttpPost("canjear")]
        public async Task<IActionResult> Canjear([FromBody] CanjearCodigoRequest req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Codigo))
                {
                    return BadRequest(new { success = false, message = "Debes ingresar un código válido." });
                }

                if (req.UserId <= 0)
                {
                    return BadRequest(new { success = false, message = "Debes iniciar sesión con una cuenta para canjear códigos." });
                }

                var usuario = await _db.Usuarios.FindAsync(req.UserId);
                if (usuario == null)
                {
                    return NotFound(new { success = false, message = "Usuario no encontrado." });
                }

                string codigoLimpio = req.Codigo.Trim().ToLowerInvariant();

                var promo = await _db.PromoCodes.FirstOrDefaultAsync(p => p.Codigo.ToLower() == codigoLimpio);
                if (promo == null)
                {
                    return BadRequest(new { success = false, message = "Código promocional no válido o inexistente." });
                }

                if (promo.ExpiraUtc.HasValue && DateTime.UtcNow > promo.ExpiraUtc.Value)
                {
                    return Conflict(new { success = false, message = "Este código ya expiró." });
                }

                if (promo.UsosActuales >= promo.MaxUsos)
                {
                    return Conflict(new { success = false, message = "Este código ya ha sido canjeado." });
                }

                // Un código compartido (muchos usos) se puede canjear UNA vez por cuenta.
                bool yaLoCanjeo = await _db.Database
                    .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM promo_canjes WHERE CodigoId = {promo.Id} AND UserId = {req.UserId}")
                    .SingleAsync() > 0;
                if (yaLoCanjeo)
                {
                    return Conflict(new { success = false, message = "Ya canjeaste este código." });
                }

                // Si es una skin, verificar si el usuario ya la posee
                if (promo.TipoRecompensa == "skin")
                {
                    bool yaTieneSkin = await _db.UserSkins.AnyAsync(us => us.UserId == req.UserId && us.SkinRuta == promo.ValorRecompensa);
                    if (yaTieneSkin)
                    {
                        return Conflict(new { success = false, message = "Ya tienes este skin de huevo desbloqueado en tu cuenta." });
                    }

                    _db.UserSkins.Add(new UserSkin
                    {
                        UserId = req.UserId,
                        SkinRuta = promo.ValorRecompensa,
                        Nombre = promo.NombreRecompensa,
                        FechaDesbloqueo = DateTime.UtcNow
                    });
                }

                // Monedas: las suma el SERVIDOR a la cuenta (antes solo el celular las sumaba y mandaba su
                // saldo total). La respuesta trae el saldo nuevo para que el cliente lo adopte.
                if (promo.TipoRecompensa == "monedas" && int.TryParse(promo.ValorRecompensa, out var monedasCodigo) && monedasCodigo > 0)
                    usuario.Monedas += monedasCodigo;

                // Marcar código como usado
                promo.UsosActuales++;
                promo.UsadoPorUsuarioId = req.UserId;
                promo.FechaCanje = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT OR IGNORE INTO promo_canjes (CodigoId, UserId, FechaUtc)
                    VALUES ({promo.Id}, {req.UserId}, {DateTime.UtcNow.ToString("o")});");

                return Ok(new
                {
                    success = true,
                    tipo = promo.TipoRecompensa,
                    valor = promo.ValorRecompensa,
                    nombre = promo.NombreRecompensa,
                    monedas = usuario.Monedas, // saldo YA actualizado de la cuenta
                    mensaje = promo.TipoRecompensa == "skin"
                        ? $"¡Felicidades! Has desbloqueado el skin: {promo.NombreRecompensa}"
                        : $"¡Código canjeado con éxito! Has recibido: {promo.NombreRecompensa}"
                });
            }
            catch (Exception ex)
            {
                // Antes: una excepción sin manejar acá (p. ej. "no such table" si promo_codes/user_skins
                // faltaban en una BD vieja) devolvía un 500 crudo sin cuerpo JSON, y el cliente lo
                // traducía en un mensaje genérico de "error de conexión" que escondía la causa real.
                Console.Error.WriteLine($"[Codigos/Canjear] Error inesperado: {ex}");
                return StatusCode(500, new { success = false, message = "Error interno del servidor al canjear el código. Intenta de nuevo en unos minutos." });
            }
        }

        // GET: api/codigos/usuario/{userId}/skins
        // Devuelve las skins de huevo desbloqueadas para el usuario (incluye skins automáticas de dev)
        [HttpGet("usuario/{userId}/skins")]
        public async Task<IActionResult> GetSkinsUsuario(int userId)
        {
            if (userId <= 0) return Ok(new List<UserSkin>());

            var usuario = await _db.Usuarios.FindAsync(userId);
            if (usuario == null) return NotFound(new { message = "Usuario no encontrado" });

            // Verificar si corresponde asignar skin exclusiva fija por cuenta
            await AsegurarSkinsDesarrollador(usuario);

            var skins = await _db.UserSkins
                .Where(us => us.UserId == userId)
                .OrderBy(us => us.FechaDesbloqueo)
                .ToListAsync();

            return Ok(skins);
        }

        private async Task AsegurarSkinsDesarrollador(Usuario usuario)
        {
            // Skins de desarrollador ligadas a la CUENTA EXACTA (por Id), NO al texto del nombre.
            // Antes se usaba nombre.Contains("jeremy"/"gonza"/"carlos"...) y cualquier nombre PARECIDO
            // (p. ej. "ggonzalo", "pruebadev") recibía la skin dev. Ahora solo la reciben las cuentas
            // reales de los desarrolladores por su Id fijo.
            string? skinExclusiva = null;
            string? nombreSkin = null;
            if (CuentasDev.Skins.TryGetValue(usuario.Id, out var dev))
            {
                skinExclusiva = dev.ruta;
                nombreSkin = dev.nombre;
            }

            if (skinExclusiva != null)
            {
                bool existe = await _db.UserSkins.AnyAsync(us => us.UserId == usuario.Id && us.SkinRuta == skinExclusiva);
                if (!existe)
                {
                    _db.UserSkins.Add(new UserSkin
                    {
                        UserId = usuario.Id,
                        SkinRuta = skinExclusiva,
                        Nombre = nombreSkin ?? "Skin Exclusiva",
                        FechaDesbloqueo = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                }
            }
        }
    }
}
