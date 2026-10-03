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
        public int SkinIdx { get; set; } = 0;
        public int TronoIdx { get; set; } = 0;
    }

    [HttpPost("cola")]
    [CuentaPropia("req.JugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult Cola([FromBody] ColaRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.JugadorId))
            return BadRequest(new { mensaje = "jugadorId requerido" });

        // El multijugador es solo para cuentas registradas. El cliente usa el prefijo "u" para
        // usuarios logueados (u{UsuarioId}) y "g" para invitados; aquí se rechaza a los invitados.
        if (!req.JugadorId.StartsWith("u"))
            return StatusCode(403, new { mensaje = "Solo las cuentas registradas pueden jugar en línea." });

        var p = _gestor.EntrarACola(req.JugadorId, string.IsNullOrWhiteSpace(req.Nombre) ? "Jugador" : req.Nombre, req.SkinIdx, req.TronoIdx);
        return Ok(Serializar(p, req.JugadorId));
    }

    [HttpGet("{id}")]
    [CuentaPropia("jugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult Estado(string id, [FromQuery] string jugadorId = "")
    {
        var p = _gestor.Obtener(id);
        if (p == null) return NotFound(new { mensaje = "partida no existe" });
        // Si lleva esperando lo suficiente y no apareció nadie real, rival bot (ver GestorPartidas).
        if (p.JugadorAId == jugadorId) _gestor.AsignarBotSiTocaEsperar(p);
        return Ok(Serializar(p, jugadorId));
    }

    [HttpPost("{id}/cancelar")]
    [CuentaPropia("req.JugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult Cancelar(string id, [FromBody] ColaRequest req)
    {
        _gestor.Cancelar(id, req?.JugadorId ?? "");
        return Ok(new { ok = true });
    }

    public class TurnoRequest
    {
        public string JugadorId { get; set; } = "";
        public int Turno { get; set; }
        public string Estado { get; set; } = ""; // snapshot del tablero (JSON serializado)
    }

    // El jugador activo sube el snapshot del tablero al terminar su turno.
    [HttpPost("{id}/turno")]
    [CuentaPropia("req.JugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult SubirTurno(string id, [FromBody] TurnoRequest req)
    {
        if (req == null) return BadRequest(new { mensaje = "cuerpo requerido" });
        if (!_gestor.GuardarTurno(id, req.Turno, req.Estado))
            return NotFound(new { mensaje = "partida no existe" });
        return Ok(new { ok = true, turno = req.Turno });
    }

    // El jugador en espera sondea el último snapshot. Si turno <= desde, no hay nada nuevo.
    [HttpGet("{id}/turno")]
    public IActionResult BajarTurno(string id, [FromQuery] int desde = 0)
    {
        var t = _gestor.ObtenerTurno(id);
        if (t == null) return NotFound(new { mensaje = "partida no existe" });
        bool hayNuevo = t.Value.turno > desde;
        return Ok(new { turno = t.Value.turno, estado = hayNuevo ? t.Value.estado : "" });
    }

    // Latido periódico: mantiene "vivo" al jugador y avisa si el rival se cayó (desconexión /
    // inactividad > 12s). Si el rival se cayó, el que late gana; si ambos, empate.
    [HttpPost("{id}/latido")]
    [CuentaPropia("req.JugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult Latido(string id, [FromBody] ColaRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.JugadorId)) return BadRequest(new { mensaje = "jugadorId requerido" });
        // 404 = la partida ya no existe (p. ej. el servidor se reinició): el cliente lo avisa y vuelve al
        // menú en vez de quedarse esperando para siempre.
        if (_gestor.Obtener(id) == null) return NotFound(new { mensaje = "partida no existe" });
        var e = _gestor.LatidoYEstado(id, req.JugadorId);
        return Ok(new { rivalCaido = e.RivalCaido, resultado = e.Resultado, motivo = e.Motivo, rivalSilencio = e.RivalSilencioSeg });
    }

    // El cliente reporta el ganador al terminar la partida (huevo a 0 o por tiempo). El primero en
    // reportar fija el resultado; ambos clientes lo leen y muestran lo mismo (evita "los dos ganan").
    // Motivo: "normal" (huevo a 0 o por tiempo) o "rendicion" (el que reporta se rinde: ganador = el otro).
    public class ResultadoRequest { public string JugadorId { get; set; } = ""; public string Ganador { get; set; } = ""; public string Motivo { get; set; } = ""; }

    [HttpPost("{id}/resultado")]
    [CuentaPropia("req.JugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult ReportarResultado(string id, [FromBody] ResultadoRequest req)
    {
        if (req == null) return BadRequest(new { mensaje = "cuerpo requerido" });
        if (_gestor.Obtener(id) == null) return NotFound(new { mensaje = "partida no existe" });
        var (resultado, motivo) = _gestor.ReportarResultado(id, req.JugadorId, req.Ganador, req.Motivo);
        return Ok(new { resultado, motivo });
    }

    // ── Acciones en vivo ──────────────────────────────────────────────────
    public class AccionRequest
    {
        public string JugadorId { get; set; } = "";
        public string Accion { get; set; } = ""; // JSON con {tipo, datos, snapshot}
    }

    // El jugador activo publica una acción (invocar/atacar/habilidad/hechizo/fin_turno).
    [HttpPost("{id}/accion")]
    [CuentaPropia("req.JugadorId")] // en línea solo juegas como tu propia cuenta
    public IActionResult PublicarAccion(string id, [FromBody] AccionRequest req)
    {
        if (req == null) return BadRequest(new { mensaje = "cuerpo requerido" });
        int idx = _gestor.AgregarAccion(id, req.Accion);
        if (idx < 0) return NotFound(new { mensaje = "partida no existe" });
        return Ok(new { ok = true, indice = idx });
    }

    // El rival sondea las acciones que aún no reprodujo (índice > desde).
    [HttpGet("{id}/acciones")]
    public IActionResult BajarAcciones(string id, [FromQuery] int desde = -1)
    {
        var r = _gestor.AccionesDesde(id, desde);
        if (r == null) return NotFound(new { mensaje = "partida no existe" });
        return Ok(new { total = r.Value.total, acciones = r.Value.nuevas });
    }

    private static object Serializar(GestorPartidas.Partida p, string jugadorId)
    {
        string asiento = p.JugadorAId == jugadorId ? "A" : (p.JugadorBId == jugadorId ? "B" : "");
        string rival = asiento == "A" ? (p.JugadorBNombre ?? "") : p.JugadorANombre;
        int rivalSkinIdx  = asiento == "A" ? p.SkinIdxB  : p.SkinIdxA;
        int rivalTronoIdx = asiento == "A" ? p.TronoIdxB : p.TronoIdxA;
        return new
        {
            matchId = p.Id,
            estado  = p.Estado,     // esperando | emparejado
            asiento,                // A (primero en entrar) | B
            semilla = p.Semilla,    // RNG compartido para la Fase 2
            jugadorA = p.JugadorANombre,
            jugadorB = p.JugadorBNombre ?? "",
            rival,
            rivalSkinIdx,
            rivalTronoIdx,
            // El cliente juega contra la CPU (el bot del VS BOT), pero lo muestra como un rival en línea común.
            rivalEsBot = p.EsBot
        };
    }
}
