using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KromaNexus.API.Data; // Asegúrate de que este sea el nombre de tu carpeta Data

[ApiController]
[Route("api/[controller]")]
public class CartasController : ControllerBase
{
    private readonly AppDbContext _context;

    public CartasController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/Cartas
    [HttpGet]
    public async Task<IActionResult> GetCartas()
    {
        var lista = await _context.Cartas.ToListAsync();
        return Ok(lista);
    }
}