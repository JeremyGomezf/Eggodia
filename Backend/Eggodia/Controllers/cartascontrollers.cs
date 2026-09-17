using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Eggodia.API.Data;
using Eggodia.API.model;

/// <summary>
/// Endpoints de cartas.
/// GET  /api/cartas             → todas las cartas
/// GET  /api/cartas/{id}        → una carta por id
/// GET  /api/cartas/tipo/{t}    → cartas por tipo (Tactico | Asesino | Coloso)
/// POST /api/cartas             → crear carta nueva (admin)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CartasController : ControllerBase
{
    private readonly AppDbContext _context;

    public CartasController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/cartas
    [HttpGet]
    public async Task<IActionResult> GetCartas()
    {
        var lista = await _context.Cartas
            .OrderBy(c => c.Serie)
            .ThenBy(c => c.Id)
            .ToListAsync();
        return Ok(lista);
    }

    // GET: api/cartas/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCarta(int id)
    {
        var carta = await _context.Cartas.FindAsync(id);
        if (carta == null) return NotFound(new { mensaje = $"Carta {id} no existe." });
        return Ok(carta);
    }

    // GET: api/cartas/tipo/Coloso
    [HttpGet("tipo/{tipo}")]
    public async Task<IActionResult> GetCartasPorTipo(string tipo)
    {
        var lista = await _context.Cartas
            .Where(c => c.Tipo == tipo)
            .OrderBy(c => c.Id)
            .ToListAsync();
        return Ok(lista);
    }

    // POST: api/cartas
    [HttpPost]
    public async Task<IActionResult> CrearCarta([FromBody] Carta carta)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        _context.Cartas.Add(carta);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCarta), new { id = carta.Id }, carta);
    }

    // PUT: api/cartas/5
    [HttpPut("{id}")]
    public async Task<IActionResult> ActualizarCarta(int id, [FromBody] Carta carta)
    {
        if (id != carta.Id) return BadRequest();
        _context.Entry(carta).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    public record PhysicalCardData(
        string CardId,
        string Name,
        string Serie,
        string Tipo,
        int Hp,
        int Atk,
        int Def,
        string Habilidad,
        string Dialogue
    );

    // Catálogo con los 7 códigos reales de las tarjetas físicas (5 dígitos finales)
    private static readonly Dictionary<string, PhysicalCardData> PhysicalCardsDb = new()
    {
        { "09764", new PhysicalCardData("tiburon", "Tiburón", "PACÍFICO", "Asesino", 260, 320, 180, "Mordida feroz: siguiente ataque hace x2 daño", "¡Felicidades, me has encontrado! Mis mandíbulas están listas para destrozar a tus rivales.") },
        { "09750", new PhysicalCardData("calamar", "Calamar Gigante", "PACÍFICO", "Coloso", 400, 370, 380, "Depredador acuático", "Emerjo desde las profundidades abisales para unirme a tus tropas.") },
        { "09393", new PhysicalCardData("soldado_cartoon", "Soldado Cartoon", "TOON", "Asesino", 230, 220, 170, "Fuego automático: Dispara ráfagas rápidas de metralla", "¡Reportándome al deber! ¡Listos para abrir fuego constante!") },
        { "27058", new PhysicalCardData("campero", "Campero", "TOON", "Táctico", 160, 350, 110, "Camuflaje: Oculto en arbustos hasta disparar su rifle", "Blanco fijado en la mira. Nadie nos verá venir.") },
        { "26920", new PhysicalCardData("granadero", "Granadero", "TOON", "Asesino", 240, 300, 190, "Lanzamiento Explosivo: Lanza granadas que causan daño de área", "¡Fuego en el hoyo! Mis explosivos abrirán el camino hacia la victoria.") },
        { "09388", new PhysicalCardData("tanque", "Tanque", "TOON", "Coloso", 550, 420, 480, "Cañonazo Blindado: Disparo pesado que derriba estructuras", "¡Blindaje pesado desplegado! Aplanaremos cualquier defensa enemiga.") },
        { "27052", new PhysicalCardData("kabar", "Kabar", "TOON", "Asesino", 180, 270, 130, "Invisibilidad Espectral: Atraviesa enemigos y ataca por la espalda", "¿Sentiste ese frío en tu espalda?... Ahora lucho para ti.") }
    };

    // GET: api/cartas/usuario/{userId}
    // Devuelve todas las cartas desbloqueadas en el ejército del usuario
    [HttpGet("usuario/{userId}")]
    public async Task<IActionResult> GetCartasUsuario(int userId)
    {
        var cartas = await _context.UserCards
            .Where(uc => uc.UserId == userId)
            .OrderBy(uc => uc.AcquiredAt)
            .ToListAsync();
        return Ok(cartas);
    }

    // POST: api/cartas/claim-physical-card
    [HttpPost("claim-physical-card")]
    public async Task<IActionResult> ClaimPhysicalCard([FromBody] ClaimCardDto request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { success = false, message = "Código de tarjeta no válido." });
        }

        int userId = request.GetParsedUserId();
        if (userId <= 0)
        {
            return BadRequest(new { success = false, message = "Identificador de usuario no válido." });
        }

        var cleanCode = request.Code.Trim();

        // a) Validar si el código existe en la colección de las 7 cartas físicas
        if (!PhysicalCardsDb.TryGetValue(cleanCode, out var cardInfo))
        {
            return BadRequest(new { success = false, message = "Código de tarjeta no válido." });
        }

        // b) Verificar en SQLite (tabla UserCards) si el usuario ya posee esa carta
        var userCardExists = await _context.UserCards
            .AnyAsync(uc => uc.UserId == userId && uc.CardId == cardInfo.CardId);

        if (userCardExists)
        {
            return Conflict(new { success = false, message = "Ya posees esta carta en tu ejército." });
        }

        // c) Insertarla en la tabla del usuario con fecha de obtención y guardar cambios en SQLite
        _context.UserCards.Add(new UserCard
        {
            UserId = userId,
            CardId = cardInfo.CardId,
            AcquiredAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // d) Retornar 200 OK con el objeto JSON para que Godot reproduzca el diálogo y actualice el mazo
        return Ok(new
        {
            success = true,
            cardId = cardInfo.CardId,
            name = cardInfo.Name,
            dialogue = cardInfo.Dialogue,
            hp = cardInfo.Hp,
            atk = cardInfo.Atk,
            def = cardInfo.Def,
            serie = cardInfo.Serie,
            habilidad = cardInfo.Habilidad
        });
    }
}