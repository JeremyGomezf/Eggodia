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

                if (promo.UsosActuales >= promo.MaxUsos)
                {
                    return Conflict(new { success = false, message = "Este código ya ha sido canjeado." });
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

                // Marcar código como usado
                promo.UsosActuales++;
                promo.UsadoPorUsuarioId = req.UserId;
                promo.FechaCanje = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    tipo = promo.TipoRecompensa,
                    valor = promo.ValorRecompensa,
                    nombre = promo.NombreRecompensa,
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
            string nombreLower = usuario.Nombre.ToLowerInvariant();
            string emailLower = usuario.Email.ToLowerInvariant();

            string? skinExclusiva = null;
            string? nombreSkin = null;

            if (nombreLower.Contains("jeremy") || emailLower.Contains("jeremy"))
            {
                skinExclusiva = "res://imagenes/PersonajesPng/JeremiHuevo.png";
                nombreSkin = "Jeremi Huevo";
            }
            else if (nombreLower.Contains("gonza") || emailLower.Contains("gonza") || usuario.Id == 2)
            {
                skinExclusiva = "res://imagenes/PersonajesPng/GonzaHuevo.png";
                nombreSkin = "Gonza Huevo";
            }
            else if (nombreLower.Contains("kankox") || nombreLower.Contains("carlos") || emailLower.Contains("carlos") || usuario.Id == 4)
            {
                skinExclusiva = "res://imagenes/PersonajesPng/CarlosHuevo.png";
                nombreSkin = "Carlos Huevo";
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
