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

        // Normalizar entradas (evita duplicados por espacios o mayúsculas).
        string nombre     = (req.Nombre ?? "").Trim();
        string email      = (req.Email  ?? "").Trim();
        string nombreLower = nombre.ToLowerInvariant();
        string emailLower  = email.ToLowerInvariant();

        if (nombre.Length < 3)
            return Conflict(new { mensaje = "El nombre debe tener al menos 3 caracteres." });

        // Nombres reservados (staff / impersonación): nadie los puede registrar.
        var reservados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "dev", "admin", "administrador", "moderador", "mod", "staff", "eggodia", "jeremy_dev", "gonza_dev", "soporte", "sistema" };
        if (reservados.Contains(nombreLower))
            return Conflict(new { mensaje = "Ese nombre está reservado. Elige otro." });

        // Email único (insensible a mayúsculas).
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == emailLower))
            return Conflict(new { mensaje = "El email ya está registrado." });

        // Nombre de usuario ÚNICO (insensible a mayúsculas): evita nombres repetidos o "similares"
        // por capitalización (p. ej. "Jeremy_dev" vs "jeremy_dev").
        if (await _db.Usuarios.AnyAsync(u => u.Nombre.ToLower() == nombreLower))
            return Conflict(new { mensaje = "Ese nombre de usuario ya está en uso. Elige otro." });

        var usuario = new Usuario
        {
            Nombre   = nombre,
            Email    = email,
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

    // ── INVENTARIO POR CUENTA (server-side; ver UserItem) ─────────────────────
    // GET api/usuarios/{id}/inventario  → todo lo que el jugador posee, para cargar al iniciar sesión.
    [HttpGet("{id}/inventario")]
    public async Task<IActionResult> Inventario(int id)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { mensaje = "Usuario no encontrado." });

        var items = await _db.UserItems.Where(x => x.UserId == id)
            .Select(x => new { tipo = x.Tipo, itemId = x.ItemId }).ToListAsync();
        var exclusivas = await _db.UserSkins.Where(s => s.UserId == id)
            .Select(s => s.SkinRuta).ToListAsync();

        return Ok(new
        {
            monedas = u.Monedas,
            equipSkinIdx = u.EquipSkinIdx,
            equipSkinExclusiva = u.EquipSkinExclusiva,
            equipTronoIdx = u.EquipTronoIdx,
            items,
            skinsExclusivas = exclusivas
        });
    }

    // POST api/usuarios/{id}/comprar { tipo, itemId, costo }
    // Compra SERVER-AUTORITATIVA: el servidor descuenta las monedas y registra el ítem en la cuenta.
    // Idempotente: si ya lo tiene, no cobra de nuevo. Evita trampas de cliente (monedas/ítems locales).
    [HttpPost("{id}/comprar")]
    public async Task<IActionResult> Comprar(int id, [FromBody] ComprarRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Tipo) || string.IsNullOrWhiteSpace(req.ItemId))
            return BadRequest(new { ok = false, mensaje = "Datos de compra incompletos." });

        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { ok = false, mensaje = "Usuario no encontrado." });

        bool yaTiene = await _db.UserItems.AnyAsync(x => x.UserId == id && x.Tipo == req.Tipo && x.ItemId == req.ItemId);
        if (yaTiene) return Ok(new { ok = true, yaTenia = true, monedas = u.Monedas });

        int costo = Math.Max(0, req.Costo);
        if (u.Monedas < costo)
            return BadRequest(new { ok = false, mensaje = "Monedas insuficientes.", monedas = u.Monedas });

        u.Monedas -= costo;
        _db.UserItems.Add(new UserItem { UserId = id, Tipo = req.Tipo, ItemId = req.ItemId });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, monedas = u.Monedas });
    }

    // POST api/usuarios/{id}/equipar { skinIdx?, skinExclusiva?, tronoIdx? }
    // Guarda el cosmético equipado en la CUENTA (solo se actualizan los campos enviados).
    [HttpPost("{id}/equipar")]
    public async Task<IActionResult> Equipar(int id, [FromBody] EquiparRequest req)
    {
        var u = await _db.Usuarios.FindAsync(id);
        if (u == null) return NotFound(new { ok = false, mensaje = "Usuario no encontrado." });
        if (req == null) return BadRequest(new { ok = false });

        if (req.SkinIdx.HasValue)      u.EquipSkinIdx = req.SkinIdx.Value;
        if (req.SkinExclusiva != null) u.EquipSkinExclusiva = req.SkinExclusiva;
        if (req.TronoIdx.HasValue)     u.EquipTronoIdx = req.TronoIdx.Value;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, u.EquipSkinIdx, u.EquipSkinExclusiva, u.EquipTronoIdx });
    }

    private static UsuarioDto ToDto(Usuario u) => new()
    {
        Id        = u.Id,
        Nombre    = u.Nombre,
        Email     = u.Email,
        Monedas   = u.Monedas,
        EquipSkinIdx       = u.EquipSkinIdx,
        EquipSkinExclusiva = u.EquipSkinExclusiva,
        EquipTronoIdx      = u.EquipTronoIdx,
        Victorias = u.Victorias,
        Derrotas  = u.Derrotas,
        Empates   = u.Empates,
        DañoTotal = u.DañoTotal
    };
}
