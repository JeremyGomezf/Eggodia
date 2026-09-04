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
}