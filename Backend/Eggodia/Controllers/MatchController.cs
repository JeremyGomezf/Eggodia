using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Emparejamiento 1v1 por sondeo (Fase 1 del multijugador).
///
/// POST /api/match/cola          { jugadorId, nombre }  → entra a la cola. Devuelve la partida
///                                                          (estado "esperando" o "emparejado").
/// GET  /api/match/{id}?jugadorId=..                     → estado actual (para sondear cada ~1.5s).
/// POST /api/match/{id}/cancelar { jugadorId }           → sale de la cola si aún esperaba.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MatchController : ControllerBase
{
    private readonly GestorPartidas _gestor;

    public MatchController(GestorPartidas gestor)
    {
        _gestor = gestor;
    }

    public class ColaRequest
    {
        public string JugadorId { get; set; } = "";
        public string Nombre { get; set; } = "";
    }

    [HttpPost("cola")]
    public IActionResult Cola([FromBody] ColaRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.JugadorId))
            return BadRequest(new { mensaje = "jugadorId requerido" });

        // El multijugador es solo para cuentas registradas. El cliente usa el prefijo "u" para
        // usuarios logueados (u{UsuarioId}) y "g" para invitados; aquí se rechaza a los invitados.
        if (!req.JugadorId.StartsWith("u"))
            return StatusCode(403, new { mensaje = "Solo las cuentas registradas pueden jugar en línea." });

        var p = _gestor.EntrarACola(req.JugadorId, string.IsNullOrWhiteSpace(req.Nombre) ? "Jugador" : req.Nombre);
        return Ok(Serializar(p, req.JugadorId));
    }

    [HttpGet("{id}")]
    public IActionResult Estado(string id, [FromQuery] string jugadorId = "")
    {
        var p = _gestor.Obtener(id);
        if (p == null) return NotFound(new { mensaje = "partida no existe" });
        return Ok(Serializar(p, jugadorId));
    }

    [HttpPost("{id}/cancelar")]
    public IActionResult Cancelar(string id, [FromBody] ColaRequest req)
    {
        _gestor.Cancelar(id, req?.JugadorId ?? "");
        return Ok(new { ok = true });
    }

    private static object Serializar(GestorPartidas.Partida p, string jugadorId)
    {
        string asiento = p.JugadorAId == jugadorId ? "A" : (p.JugadorBId == jugadorId ? "B" : "");
        string rival = asiento == "A" ? (p.JugadorBNombre ?? "") : p.JugadorANombre;
        return new
        {
            matchId = p.Id,
            estado  = p.Estado,     // esperando | emparejado
            asiento,                // A (primero en entrar) | B
            semilla = p.Semilla,    // RNG compartido para la Fase 2
            jugadorA = p.JugadorANombre,
            jugadorB = p.JugadorBNombre ?? "",
            rival
        };
    }
}
