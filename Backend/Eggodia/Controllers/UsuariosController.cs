using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;
using Eggodia.API.model;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsuariosController(AppDbContext db) { _db = db; }

    // POST: api/usuarios/registro
    [HttpPost("registro")]
    public async Task<IActionResult> Registro([FromBody] RegistroRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Verificar email único
        bool existe = await _db.Usuarios.AnyAsync(u => u.Email == req.Email);
        if (existe) return Conflict(new { mensaje = "El email ya está registrado." });

        var usuario = new Usuario
        {
            Nombre   = req.Nombre,
            Email    = req.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(req.Password) // hash seguro (nunca texto plano)
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // 11 personajes base con los que inicia todo jugador al registrarse
        var personajesBase = new[]
        {
            "peon",
            "torre",
            "arfil",
            "caballo",
            "dama",
            "soldado_real",
            "maguin",
            "machi",
            "dragon",
            "golem",
            "paper_rex"
        };

        // Exclusión estricta de las 7 cartas físicas exclusivas
        var cartasExclusivasExcluidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tiburon", "calamar_gigante", "soldado_cartoon", "campero", "granadero", "tanque", "kabar"
        };

        var mazoInicial = personajesBase
            .Where(cardId => !cartasExclusivasExcluidas.Contains(cardId))
            .Select(cardId => new UserCard
            {
                UserId = usuario.Id,
                CardId = cardId,
                AcquiredAt = DateTime.UtcNow
            });

        _db.UserCards.AddRange(mazoInicial);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, ToDto(usuario));
    }

    // POST: api/usuarios/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (usuario == null)
            return Unauthorized(new { mensaje = "Email o contraseña incorrectos." });

        bool ok;
        if (EsHashBCrypt(usuario.Password))
        {
            ok = BCrypt.Net.BCrypt.Verify(req.Password, usuario.Password);
        }
        else
        {
            // Cuenta antigua con contraseña en texto plano: comparar y migrar a hash al vuelo.
            ok = usuario.Password == req.Password;
            if (ok)
            {
                usuario.Password = BCrypt.Net.BCrypt.HashPassword(req.Password);
                await _db.SaveChangesAsync();
            }
        }

        if (!ok)
            return Unauthorized(new { mensaje = "Email o contraseña incorrectos." });

        return Ok(ToDto(usuario));
    }

    // Detecta si un valor ya es un hash BCrypt ($2a$/$2b$/$2y$...).
    private static bool EsHashBCrypt(string valor) =>
        !string.IsNullOrEmpty(valor) && valor.StartsWith("$2") && valor.Length >= 55;

    // GET: api/usuarios/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUsuario(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound();
        return Ok(ToDto(u));
    }

    // POST: api/usuarios/resultado
    // Llamado al terminar una partida para actualizar stats
    [HttpPost("resultado")]
    public async Task<IActionResult> GuardarResultado([FromBody] ResultadoPartidaRequest req)
    {
        var u = await _db.Usuarios.FindAsync(req.UsuarioId);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });

        switch (req.Resultado.ToLower())
        {
            case "victoria": u.Victorias++;  break;
            case "derrota":  u.Derrotas++;   break;
            case "empate":   u.Empates++;    break;
        }
        u.DañoTotal += req.DañoHecho;

        await _db.SaveChangesAsync();
        return Ok(ToDto(u));
    }

    // GET: api/usuarios/ranking
    [HttpGet("ranking")]
    public async Task<IActionResult> GetRanking()
    {
        var top = await _db.Usuarios
            .OrderByDescending(u => u.Victorias)
            .ThenByDescending(u => u.DañoTotal)
            .Take(20)
            .Select(u => new {
                u.Id, u.Nombre,
                u.Victorias, u.Derrotas, u.Empates,
                u.DañoTotal,
                WinRate = u.Victorias + u.Derrotas + u.Empates > 0
                    ? (int)((float)u.Victorias / (u.Victorias + u.Derrotas + u.Empates) * 100)
                    : 0
            })
            .ToListAsync();

        return Ok(top);
    }

    // POST: api/usuarios/{id}/monedas   { monedas }
    // El cliente sincroniza su saldo (tras ganar/gastar). El servidor guarda el valor absoluto.
    public class MonedasSyncRequest { public int Monedas { get; set; } }

    [HttpPost("{id}/monedas")]
    public async Task<IActionResult> SincronizarMonedas(int id, [FromBody] MonedasSyncRequest req)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });
        u.Monedas = Math.Max(0, req?.Monedas ?? 0);
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, u.Monedas });
    }

    private static UsuarioDto ToDto(Usuario u) => new()
    {
        Id        = u.Id,
        Nombre    = u.Nombre,
        Email     = u.Email,
        Monedas   = u.Monedas,
        Victorias = u.Victorias,
        Derrotas  = u.Derrotas,
        Empates   = u.Empates,
        DañoTotal = u.DañoTotal
    };
}
